using FarmHand.API.Services;
using FarmHand.Application.Common.Behaviors;
using FarmHand.Application.Common.Exceptions;
using FarmHand.Application.Common.Interfaces;
using FarmHand.Application.Common.Services;
using FarmHand.Application.Features.Farms;
using FarmHand.Application.Features.Payments;
using FarmHand.Domain.Interfaces;
using FarmHand.Infrastructure;
using FarmHand.Infrastructure.Persistence;
using FarmHand.Infrastructure.SeedData;
using FarmHand.Infrastructure.Services;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerUI;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

const long MaxRequestBytes = 25 * 1024 * 1024;
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = MaxRequestBytes);
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = MaxRequestBytes);

builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "FarmHand API",
        Version = "v1",
        Description = "AI-assisted livestock and poultry health API. Auth routes, /api/system/info and /health are anonymous; all other /api routes require a JWT bearer token.",
        Contact = new OpenApiContact { Name = "Team HackForge" }
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the raw JWT. Swagger UI adds the \"Bearer \" prefix automatically."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });

    options.MapType<IFormFile>(() => new OpenApiSchema { Type = "string", Format = "binary" });
    options.CustomSchemaIds(type => type.FullName?.Replace("+", "."));

    foreach (var xmlFile in new[] { "FarmHand.API.xml", "FarmHand.Application.xml" })
    {
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
            options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
    }
});

// Application layer (MediatR, validators, behaviors, read-model projectors)
builder.Services.AddMediatR(typeof(CreateFarmCommandHandler).Assembly);
builder.Services.AddValidatorsFromAssemblyContaining<CreateFarmCommandValidator>();
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
builder.Services.AddScoped<ObservationProjector>();
builder.Services.AddScoped<VetRequestProjector>();
builder.Services.AddScoped<PaymentSettlement>();

// Development databases get the demo scenario unless Demo:Enabled is explicitly false.
if (builder.Environment.IsDevelopment() && builder.Configuration["Demo:Enabled"] is null)
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Demo:Enabled"] = "true" });
var demoEnabled = builder.Configuration.GetValue("Demo:Enabled", false);

// Infrastructure
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment.IsDevelopment());

// Request-scoped identity and resource authorization
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IResourceAuthorizationService, ResourceAuthorizationService>();

// The API usually sits behind a reverse proxy or the web app's server-side proxy. Only trusted
// proxies may set X-Forwarded-For, which the rate limiter uses to identify clients.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 2;
    foreach (var proxy in (builder.Configuration["ReverseProxy:KnownProxies"] ?? string.Empty)
                 .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        if (IPAddress.TryParse(proxy, out var address))
            options.KnownProxies.Add(address);
    }
    foreach (var network in (builder.Configuration["ReverseProxy:KnownNetworks"] ?? string.Empty)
                 .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        var parts = network.Split('/');
        if (parts.Length == 2 && IPAddress.TryParse(parts[0], out var prefix) && int.TryParse(parts[1], out var length))
            options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(prefix, length));
    }
});

// CORS is closed unless origins are configured. The bundled web app calls the API from its own server,
// so browsers never need cross-origin access in the default deployment.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("configured", policy =>
    {
        if (allowedOrigins.Length > 0)
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().WithMethods("GET", "POST", "PUT", "DELETE").WithExposedHeaders("Retry-After");
    });
});

// JWT Authentication - fail fast if the key is missing or too weak
var jwtSection = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSection["SecretKey"];
if (string.IsNullOrWhiteSpace(secretKey)
    || Encoding.UTF8.GetByteCount(secretKey) < 32
    || secretKey.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException(
        "Jwt:SecretKey is required, must be at least 32 bytes, and must not be the example placeholder. Set it in appsettings or user secrets.");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"] ?? "FarmHand",
            ValidAudience = jwtSection["Audience"] ?? "FarmHandUsers",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        // Reject tokens whose version no longer matches the user's current token version
        // (after deactivation, password change, or sign-out everywhere).
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var userId = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var tokenVersionClaim = context.Principal?.FindFirst("token_version")?.Value;
                if (!Guid.TryParse(userId, out var parsedUserId) || !int.TryParse(tokenVersionClaim, out var tokenVersion))
                {
                    context.Fail("Invalid token.");
                    return;
                }

                var unitOfWork = context.HttpContext.RequestServices.GetRequiredService<IUnitOfWork>();
                var user = await unitOfWork.Users.GetByIdAsync(parsedUserId, context.HttpContext.RequestAborted);

                if (user is null || !user.IsActive || user.TokenVersion != tokenVersion)
                    context.Fail("Token revoked or user inactive.");
            }
        };
    });

builder.Services.AddAuthorization();

static string ClientKey(HttpContext context) =>
    context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

static string UserKey(HttpContext context) =>
    context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? $"ip:{ClientKey(context)}";

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, ct) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
        context.HttpContext.Response.ContentType = "application/problem+json";
        await context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too many requests. Please wait a moment and try again."
        }, ct);
    };

    // Brute-force protection for sign-in, registration and password reset (per client IP).
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        ClientKey(context),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));

    // AI, speech and upload endpoints cost money per call: limit per signed-in user.
    options.AddPolicy("ai", context => RateLimitPartition.GetSlidingWindowLimiter(
        UserKey(context),
        _ => new SlidingWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(10), SegmentsPerWindow = 5, QueueLimit = 0 }));

    options.AddPolicy("payments", context => RateLimitPartition.GetFixedWindowLimiter(
        UserKey(context),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context => RateLimitPartition.GetFixedWindowLimiter(
        UserKey(context),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 600, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

var app = builder.Build();

// Migrate/seed only when explicitly enabled for this environment.
var migrateOnStartup = app.Environment.IsDevelopment()
    || demoEnabled
    || app.Configuration.GetValue<bool>("Database:MigrateOnStartup");

if (migrateOnStartup)
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<FarmHandDbContext>();

    await AdoptBaselineMigrationAsync(dbContext);
    await dbContext.Database.MigrateAsync();

    if (demoEnabled)
    {
        var seeder = scope.ServiceProvider.GetRequiredService<IDemoDataSeeder>();
        await seeder.SeedAsync();
    }
}

app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "FarmHand API v1");
        options.DocExpansion(DocExpansion.List);
        options.DisplayRequestDuration();
        options.EnableDeepLinking();
        options.EnableFilter();
    });
}
else
{
    app.UseHsts();
}

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

        var status = exception switch
        {
            PublicException publicException => publicException.StatusCode,
            AiUnavailableException => StatusCodes.Status503ServiceUnavailable,
            ExternalServiceException => StatusCodes.Status503ServiceUnavailable,
            ValidationException => StatusCodes.Status400BadRequest,
            BadHttpRequestException badRequest => badRequest.StatusCode,
            KeyNotFoundException => StatusCodes.Status404NotFound,
            UnauthorizedAccessException => StatusCodes.Status403Forbidden,
            ArgumentException => StatusCodes.Status400BadRequest,
            InvalidOperationException => StatusCodes.Status409Conflict,
            DbUpdateConcurrencyException => StatusCodes.Status409Conflict,
            DbUpdateException => StatusCodes.Status409Conflict,
            HttpRequestException => StatusCodes.Status502BadGateway,
            OperationCanceledException when context.RequestAborted.IsCancellationRequested => 499,
            _ => StatusCodes.Status500InternalServerError
        };

        if (status >= 500)
            logger.LogError(exception, "Unhandled exception");
        else
            logger.LogWarning("Request failed with {StatusCode}: {ExceptionType}", status, exception?.GetType().Name);

        // Only explicitly public messages and validation errors are exposed.
        // Framework exception messages can contain paths, SQL, or other internals.
        var title = status switch
        {
            StatusCodes.Status400BadRequest => "The request is invalid.",
            StatusCodes.Status403Forbidden => "You do not have permission to perform this action.",
            StatusCodes.Status404NotFound => "The requested resource was not found.",
            StatusCodes.Status409Conflict => "The request conflicts with the current state.",
            StatusCodes.Status413PayloadTooLarge => "The upload is too large.",
            StatusCodes.Status502BadGateway => "An upstream service is temporarily unavailable.",
            StatusCodes.Status503ServiceUnavailable => "A required service is temporarily unavailable.",
            _ => "An error occurred while processing your request."
        };

        var detail = exception switch
        {
            PublicException p => p.Message,
            AiUnavailableException ai => ai.Message,
            ExternalServiceException ext => ext.Message,
            _ => null
        };

        var code = exception switch
        {
            PublicException { Code: { } c } => c,
            AiUnavailableException => "ai_unavailable",
            ExternalServiceException => "service_unavailable",
            _ => null
        };

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path,
            Type = $"https://farmhand.ai/problems/{status}"
        };

        problem.Extensions["traceId"] = context.TraceIdentifier;
        if (code is not null) problem.Extensions["code"] = code;

        if (exception is ValidationException validationException)
        {
            problem.Extensions["errors"] = validationException.Errors
                .GroupBy(e => string.IsNullOrEmpty(e.PropertyName) ? "request" : char.ToLowerInvariant(e.PropertyName[0]) + e.PropertyName[1..])
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
        }

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem);
    });
});

app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers.TryAdd("X-Content-Type-Options", "nosniff");
    headers.TryAdd("X-Frame-Options", "DENY");
    headers.TryAdd("Referrer-Policy", "no-referrer");
    headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
    headers.TryAdd("Cross-Origin-Resource-Policy", "same-site");
    if (!context.Request.Path.StartsWithSegments("/swagger"))
        headers.TryAdd("Content-Security-Policy", "default-src 'none'; frame-ancestors 'none'; base-uri 'none'");
    if (context.Request.Path.StartsWithSegments("/api"))
        headers.TryAdd("Cache-Control", "no-store");
    await next();
});

if (!app.Configuration.GetValue("Https:Disabled", false))
    app.UseHttpsRedirection();

app.UseCors("configured");
app.UseAuthentication();

// Accounts with an administrator-issued temporary password can only change it (enforced server-side).
var passwordChangeAllowList = new[] { "/api/auth/me", "/api/auth/change-password", "/api/auth/logout", "/api/auth/revoke", "/api/system/info" };
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true
        && context.User.HasClaim(JwtService.PasswordChangeClaim, "true")
        && !passwordChangeAllowList.Any(p => context.Request.Path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase)))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Password change required.",
            Detail = "Set a new password before continuing.",
            Extensions = { ["code"] = "password_change_required" }
        });
        return;
    }

    await next();
});

app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow })).DisableRateLimiting();
app.MapGet("/health/ready", async (FarmHandDbContext db, CancellationToken ct) =>
    await db.CanConnectAsync(ct)
        ? Results.Ok(new { status = "ready", database = "connected" })
        : Results.Json(new { status = "unavailable", database = "unreachable" }, statusCode: StatusCodes.Status503ServiceUnavailable))
    .DisableRateLimiting();

app.Run();

// The first migration in this repository is a re-baseline of an earlier schema. When a database that
// already has the tables (but no migration history) is pointed at this build, record the baseline as
// applied instead of trying to recreate tables. A brand-new database is left alone so MigrateAsync can
// create it from scratch.
static async Task AdoptBaselineMigrationAsync(FarmHandDbContext dbContext)
{
    const string baselineMigrationId = "20261021213428_LivestockRestructure";
    const string productVersion = "9.0.0";

    var creator = dbContext.Database.GetService<Microsoft.EntityFrameworkCore.Storage.IRelationalDatabaseCreator>();
    if (!await creator.ExistsAsync())
        return;

    await dbContext.Database.OpenConnectionAsync();
    try
    {
        var connection = dbContext.Database.GetDbConnection();

        await using (var checkCommand = connection.CreateCommand())
        {
            checkCommand.CommandText = """
                SELECT
                    to_regclass('public."Farms"') IS NOT NULL AS "HasFarms",
                    to_regclass('public."__EFMigrationsHistory"') IS NOT NULL AS "HasHistory"
                """;

            await using var reader = await checkCommand.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return;

            var hasFarms = reader.GetBoolean(0);
            var hasHistory = reader.GetBoolean(1);
            if (!hasFarms) return;

            await reader.CloseAsync();

            if (!hasHistory)
            {
                await dbContext.Database.ExecuteSqlRawAsync("""
                    CREATE TABLE "__EFMigrationsHistory" (
                        "MigrationId" character varying(150) NOT NULL,
                        "ProductVersion" character varying(32) NOT NULL,
                        CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
                    )
                    """);
            }

            await dbContext.Database.ExecuteSqlRawAsync($"""
                INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                SELECT '{baselineMigrationId}', '{productVersion}'
                WHERE NOT EXISTS (
                    SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '{baselineMigrationId}'
                )
                """);
        }
    }
    finally
    {
        await dbContext.Database.CloseConnectionAsync();
    }
}

public partial class Program;

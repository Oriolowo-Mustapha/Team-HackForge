using FarmHand.API.Services;
using FarmHand.Application.Common.Behaviors;
using FarmHand.Application.Common.Interfaces;
using FarmHand.Application.Common.Services;
using FarmHand.Application.Features.Farms;
using FarmHand.Infrastructure;
using FarmHand.Infrastructure.Persistence;
using FarmHand.Infrastructure.SeedData;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerUI;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "FarmHand API",
        Version = "v1",
        Description = "Voice-first poultry and livestock health API. Auth routes and /health are anonymous; all other /api routes require a JWT bearer token.",
        Contact = new OpenApiContact
        {
            Name = "Team HackForge"
        }
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
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    options.MapType<IFormFile>(() => new OpenApiSchema
    {
        Type = "string",
        Format = "binary"
    });

    foreach (var xmlFile in new[] { "FarmHand.API.xml", "FarmHand.Application.xml" })
    {
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
        {
            options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
        }
    }
});

// Application layer (MediatR, validators, behaviors)
builder.Services.AddMediatR(typeof(CreateFarmCommandHandler).Assembly);
builder.Services.AddValidatorsFromAssemblyContaining<CreateFarmCommandValidator>();
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));

// Infrastructure
builder.Services.AddInfrastructure(builder.Configuration);

// Request-scoped identity and resource authorization
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IResourceAuthorizationService, ResourceAuthorizationService>();

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
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"] ?? "FarmHand",
            ValidAudience = jwtSection["Audience"] ?? "FarmHandUsers",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

var app = builder.Build();

// Migrate/seed only when explicitly enabled for this environment.
var migrateOnStartup = app.Environment.IsDevelopment()
    || app.Configuration.GetValue<bool>("Database:MigrateOnStartup");

if (migrateOnStartup)
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<FarmHandDbContext>();

    await AdoptBaselineMigrationAsync(dbContext);
    await dbContext.Database.MigrateAsync();

    if (app.Environment.IsDevelopment())
    {
        var seeder = scope.ServiceProvider.GetRequiredService<IDemoDataSeeder>();
        await seeder.SeedAsync();
    }
}

// Configure the HTTP request pipeline.
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

app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

            var status = exception switch
            {
                ValidationException => StatusCodes.Status400BadRequest,
                KeyNotFoundException => StatusCodes.Status404NotFound,
                UnauthorizedAccessException => StatusCodes.Status403Forbidden,
                ArgumentOutOfRangeException => StatusCodes.Status400BadRequest,
                ArgumentException => StatusCodes.Status400BadRequest,
                InvalidOperationException => StatusCodes.Status409Conflict,
                DbUpdateException => StatusCodes.Status409Conflict,
                HttpRequestException => StatusCodes.Status502BadGateway,
                _ => StatusCodes.Status500InternalServerError
            };

            if (status >= 500)
                logger.LogError(exception, "Unhandled exception");
            else
                logger.LogWarning(exception, "Request failed with {StatusCode}", status);

            var problem = new ProblemDetails
            {
                Status = status,
                Title = status switch
                {
                    StatusCodes.Status400BadRequest => "The request is invalid.",
                    StatusCodes.Status403Forbidden => "You do not have permission to perform this action.",
                    StatusCodes.Status404NotFound => "The requested resource was not found.",
                    StatusCodes.Status409Conflict => "The request conflicts with the current state.",
                    StatusCodes.Status502BadGateway => "An upstream service failed.",
                    _ => "An error occurred while processing your request."
                },
                Detail = status == StatusCodes.Status500InternalServerError
                    ? "Please check the server logs for more information."
                    : exception?.Message,
                Type = $"https://farmhand.ai/problems/{status}"
            };

            if (exception is ValidationException validationException)
            {
                problem.Extensions["errors"] = validationException.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            }

            context.Response.StatusCode = status;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(problem);
        });
    });

app.Use(async (context, next) =>
{
    context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
    context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
    context.Response.Headers.TryAdd("Referrer-Policy", "no-referrer");
    await next();
});

app.UseHttpsRedirection();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.Run();

static async Task AdoptBaselineMigrationAsync(FarmHandDbContext dbContext)
{
    const string baselineMigrationId = "20261021213428_LivestockRestructure";
    const string productVersion = "9.0.0";

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

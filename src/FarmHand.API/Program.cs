using FarmHand.Application.Common.Behaviors;
using FarmHand.Application.Common.Interfaces;
using FarmHand.Application.Features.Farms;
using FarmHand.Application.Features.Incidents;
using FarmHand.Application.Features.Poultry;
using FarmHand.Application.Features.VetCoordination;
using FarmHand.Application.Features.Payments;
using FarmHand.Infrastructure;
using FarmHand.Infrastructure.Persistence;
using FarmHand.Infrastructure.SeedData;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Application layer (MediatR, validators, behaviors)
builder.Services.AddMediatR(typeof(CreateFarmCommandHandler).Assembly);
builder.Services.AddValidatorsFromAssemblyContaining<CreateFarmCommandValidator>();
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));

// Infrastructure
builder.Services.AddInfrastructure(builder.Configuration);

// JWT Authentication - fail fast if key is missing
var jwtSection = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSection["SecretKey"];
if (string.IsNullOrWhiteSpace(secretKey))
    throw new InvalidOperationException("Jwt:SecretKey is required. Set it in appsettings or user secrets.");

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

var app = builder.Build();

// Migrate database and seed demo data (Development only)
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<FarmHandDbContext>();
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
    app.UseSwaggerUI();
}

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new
        {
            type = "https://farmhand.ai/problems/server-error",
            title = "An error occurred while processing your request.",
            status = 500,
            detail = "Please check the server logs for more information."
        });
    });
});

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Auth endpoint - issue JWT token
app.MapPost("/api/auth/login", (LoginRequest req, IConfiguration config) =>
{
    var jwtSection = config.GetSection("Jwt");
    var key = jwtSection["SecretKey"] ?? throw new InvalidOperationException("Jwt:SecretKey missing");
    var issuer = jwtSection["Issuer"] ?? "FarmHand";
    var audience = jwtSection["Audience"] ?? "FarmHandUsers";
    var expiryMinutes = int.Parse(jwtSection["ExpirationMinutes"] ?? "10080");

    // In production, validate credentials against a user store
    var claims = new[]
    {
        new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, req.Email),
        new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Email, req.Email),
        new System.Security.Claims.Claim(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        new System.Security.Claims.Claim(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()),
        new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "Farmer")
    };

    var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
    var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
    var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
        issuer: issuer,
        audience: audience,
        claims: claims,
        expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
        signingCredentials: credentials);

    var tokenString = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
    return Results.Ok(new { token = tokenString, expiresAt = token.ValidTo });
});

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.Run();

public record LoginRequest(string Email, string Password);

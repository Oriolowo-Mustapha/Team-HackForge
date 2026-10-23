using FarmHand.Application.Common.Interfaces;
using FarmHand.Application.Features.Poultry;
using FarmHand.Domain.Interfaces;
using FarmHand.Infrastructure.AI.Clients;
using FarmHand.Infrastructure.AI.Services;
using FarmHand.Infrastructure.Email;
using FarmHand.Infrastructure.Payments;
using FarmHand.Infrastructure.Persistence;
using FarmHand.Infrastructure.Repositories;
using FarmHand.Infrastructure.SeedData;
using FarmHand.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace FarmHand.Infrastructure;

public static class ServiceRegistration
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isDevelopment = false)
    {
        // Database
        services.AddDbContext<FarmHandDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        // Unit of Work & Repositories
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IFarmRepository, FarmRepository>();
        services.AddScoped<ILivestockCategoryRepository, LivestockCategoryRepository>();
        services.AddScoped<ILivestockAnimalRepository, LivestockAnimalRepository>();
        services.AddScoped<IPoultryObservationRepository, PoultryObservationRepository>();
        services.AddScoped<IIncidentRepository, IncidentRepository>();
        services.AddScoped<IVetRequestRepository, VetRequestRepository>();
        services.AddScoped<ICostShareRepository, CostShareRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IAIDiagnosisRepository, AIDiagnosisRepository>();
        services.AddScoped<ITreatmentPlanRepository, TreatmentPlanRepository>();
        services.AddScoped<IFollowUpRepository, FollowUpRepository>();
        services.AddScoped<IPreventionPlanRepository, PreventionPlanRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IVetPartnerRepository, VetPartnerRepository>();
        services.AddScoped<IIncidentActionRepository, IncidentActionRepository>();
        services.AddScoped<IAuditEventRepository, AuditEventRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
        services.AddScoped<IPlatformStatsRepository, PlatformStatsRepository>();
        services.AddScoped<IPoultryRuleEngine, PoultryRuleEngine>();

        // AI providers: generous per-attempt timeouts (LLM calls are slow) and one retry on transient errors.
        services.AddHttpClient<IGeminiAiClient, GeminiAiClient>()
            .AddResilienceHandler("gemini", builder => ConfigureAiPipeline(builder));
        services.AddHttpClient<IGroqAiClient, GroqAiClient>()
            .AddResilienceHandler("groq", builder => ConfigureAiPipeline(builder));
        services.AddHttpClient<YarnGptService>(client => client.Timeout = TimeSpan.FromSeconds(75));
        services.AddScoped<IFarmAiService, FarmAiService>();
        services.AddScoped<IVisionAnalysisService, VisionAnalysisService>();
        services.AddScoped<ITranscriptionService, TranscriptionService>();
        services.AddScoped<IFarmHistoryService, FarmHistoryService>();

        // Payments: never retry unsafe (POST/PUT) calls to the provider; verification GETs may retry.
        services.AddHttpClient<IPaymentService, FlutterwavePaymentService>()
            .AddStandardResilienceHandler(options =>
            {
                options.Retry.ShouldHandle = args => ValueTask.FromResult(
                    IsSafeRequest(args.Outcome) && HttpClientResiliencePredicates.IsTransient(args.Outcome));
                options.Retry.MaxRetryAttempts = 2;
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(15);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(40);
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
            });

        // Images: Cloudinary when configured, otherwise local disk (development and demos).
        var storageProvider = configuration["Storage:Provider"] ?? "Auto";
        var useCloudinary = string.Equals(storageProvider, "Cloudinary", StringComparison.OrdinalIgnoreCase)
            || (string.Equals(storageProvider, "Auto", StringComparison.OrdinalIgnoreCase) && CloudinaryImageStorage.IsConfigured(configuration));
        services.AddSingleton<LocalImageStorage>();
        if (useCloudinary)
            services.AddHttpClient<IImageStorage, CloudinaryImageStorage>(client => client.Timeout = TimeSpan.FromSeconds(45));
        else
            services.AddSingleton<IImageStorage>(sp => sp.GetRequiredService<LocalImageStorage>());

        services.AddScoped<IJwtService, JwtService>();
        services.AddHttpClient<IEmailService, BrevoEmailService>(client =>
            client.Timeout = TimeSpan.FromSeconds(15));
        services.AddScoped<IAuditLogger, AuditLogger>();
        services.AddScoped<ISystemStatus, SystemStatus>();
        services.AddSingleton<IFieldEncryptor>(_ => AesGcmFieldEncryptor.FromConfiguration(
            configuration,
            allowDerivedKey: isDevelopment || configuration.GetValue("Demo:Enabled", false)));
        services.AddScoped<IDemoDataSeeder, DemoDataSeeder>();

        return services;
    }

    private static bool IsSafeRequest(Outcome<HttpResponseMessage> outcome) =>
        outcome.Result?.RequestMessage?.Method is { } method && (method == HttpMethod.Get || method == HttpMethod.Head);

    private static void ConfigureAiPipeline(Polly.ResiliencePipelineBuilder<HttpResponseMessage> builder)
    {
        builder.AddTimeout(TimeSpan.FromSeconds(90));
        builder.AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = 1,
            Delay = TimeSpan.FromSeconds(1),
            BackoffType = Polly.DelayBackoffType.Exponential
        });
        builder.AddTimeout(TimeSpan.FromSeconds(45));
    }
}

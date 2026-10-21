using FarmHand.Application.Common.Interfaces;
using FarmHand.Domain.Interfaces;
using FarmHand.Infrastructure.AI.Clients;
using FarmHand.Infrastructure.AI.Services;
using FarmHand.Infrastructure.Payments;
using FarmHand.Infrastructure.Persistence;
using FarmHand.Infrastructure.Repositories;
using FarmHand.Infrastructure.SeedData;
using FarmHand.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FarmHand.Infrastructure;

public static class ServiceRegistration
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
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
        services.AddScoped<ITreatmentPlanRepository, TreatmentPlanRepository>();
        services.AddScoped<IFollowUpRepository, FollowUpRepository>();
        services.AddScoped<IPreventionPlanRepository, PreventionPlanRepository>();

        // AI Clients
        services.AddHttpClient<IGeminiAiClient, GeminiAiClient>();
        services.AddHttpClient<IGroqAiClient, GroqAiClient>();
        services.AddScoped<IFarmAiService, FarmAiService>();

        // Services
        services.AddScoped<IPaymentService, NombaPaymentService>();
        services.AddScoped<IJwtService, JwtService>();
        services.AddHttpClient<YarnGptService>();
        services.AddScoped<YarnGptService>();
        services.AddHttpClient<CloudinaryService>();
        services.AddScoped<ICloudinaryService, CloudinaryService>();
        services.AddHttpClient<IGroqVisionService, GroqVisionService>();
        services.AddScoped<IGroqVisionService, GroqVisionService>();
        services.AddScoped<IVisionAnalysisService, VisionAnalysisService>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IVetPartnerRepository, VetPartnerRepository>();

        return services;
    }
}

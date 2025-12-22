using Crawlers.BusinessLogics.Services;
using Crawlers.BusinessLogics.Services.Interfaces;
using Crawlers.Src.Utility.Https;

namespace Crawlers.Extensions;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection RegisterServices(this IServiceCollection services, ConfigurationManager configuration)
    {
        // Utility
        services.AddScoped<IHttpClientService, HttpClientService>();

        // Service
        services.AddScoped<ICompaniesDataService, CompaniesDataService>();

        return services;
    }
}
using Crawlers.BusinessLogics.Services;
using Crawlers.BusinessLogics.Services.Interfaces;
using Crawlers.BusinessLogics.Services.TEEIA;
using Crawlers.BusinessLogics.Services.TPCA;
using Crawlers.BusinessLogics.Services.TPCIA;
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
        services.AddScoped<TpcaScraperService>();
        services.AddScoped<TeeiaScraperService>();
        services.AddScoped<TpciaScraperService>();

        return services;
    }
}
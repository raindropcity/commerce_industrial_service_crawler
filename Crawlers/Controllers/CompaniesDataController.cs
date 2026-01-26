using Crawlers.BusinessLogics.Models.Companies;
using Crawlers.BusinessLogics.Services;
using Crawlers.BusinessLogics.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Crawlers.Controllers;

[ApiController]
[Route("api/company-data")]
public class CompaniesDataController : ControllerBase
{
    private readonly ILogger<CompaniesDataController> _logger;

    public CompaniesDataController(ILogger<CompaniesDataController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 已知須查詢的公司名稱前提下，自動化向工商登記請求公司資料
    /// </summary>
    /// <param name="service">The service.</param>
    /// <param name="csvFile">The CSV file.</param>
    /// <returns></returns>
    [HttpPost]
    [Route("by-csv")]
    public async Task<FileStreamResult> GetByCsvFileAsync(
        [FromServices] ICompaniesDataService service,
        IFormFile csvFile)
    {
        return await service.GetByCsvFileAsync(csvFile);
    }

    [HttpGet]
    [Route("tpca/by-url")]
    public async Task<FileStreamResult> GetByUrlAsync(
        [FromServices] TpcaScraperService service)
    {
        return await service.ScrapeAsync();
    }
}

using Crawlers.BusinessLogics.Models.TPCA;
using Crawlers.BusinessLogics.Services.ASIP;
using Crawlers.BusinessLogics.Services.Interfaces;
using Crawlers.BusinessLogics.Services.TEEIA;
using Crawlers.BusinessLogics.Services.TPCA;
using Crawlers.BusinessLogics.Services.TPCIA;
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
    /// <returns>公司聯絡資料 CSV 檔</returns>
    [HttpPost]
    [Route("by-csv")]
    public async Task<FileStreamResult> GetByCsvFileAsync(
        [FromServices] ICompaniesDataService service,
        IFormFile csvFile)
    {
        return await service.GetByCsvFileAsync(csvFile);
    }

    /// <summary>
    /// 傳入 IndustryItemIds 清單，自動化抽取 TPCA 網站的公司聯絡資料
    /// </summary>
    /// <param name="service">The service.</param>
    /// <param name="request"></param>
    /// <returns>公司聯絡資料 CSV 檔</returns>
    [HttpPost]
    [Route("tpca/by-url")]
    public async Task<FileStreamResult> GetTpcaInfoByUrlAsync(
        [FromServices] TpcaScraperService service,
        [FromBody] TpcaRequest request)
    {
        return await service.ScrapeAsync(request);
    }

    /// <summary>
    /// 自動化至 TEEIA 網站抽取公司聯絡資料
    /// </summary>
    /// <param name="service">The service.</param>
    /// <returns>公司聯絡資料 CSV 檔</returns>
    [HttpGet]
    [Route("teeia")]
    public async Task<FileStreamResult> GetTeeiaInfoByHtml(
        [FromServices] TeeiaScraperService service)
    {
        return await service.ScrapeAsync();
    }

    /// <summary>
    /// 自動化至 TPCIA 網站抽取公司聯絡資料
    /// </summary>
    /// <param name="service">The service.</param>
    /// <returns>公司聯絡資料 CSV 檔</returns>
    [HttpGet]
    [Route("tpcia")]
    public async Task<FileStreamResult> GetTpciaInfoByHtml(
        [FromServices] TpciaScraperService service)
    {
        return await service.ScrapeAsync();
    }

    /// <summary>
    /// 自動化至 ASIP 網站抽取公司聯絡資料
    /// </summary>
    /// <param name="service">The service.</param>
    /// <returns>公司聯絡資料 CSV 檔</returns>
    [HttpGet]
    [Route("asip")]
    public async Task<FileStreamResult> GetAsipInfoByHtml(
        [FromServices] AsipScraperService service)
    {
        return await service.ScrapeAsync();
    }
}

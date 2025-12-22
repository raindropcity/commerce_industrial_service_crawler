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

    [HttpPost]
    [Route("by-csv")]
    public async Task<FileStreamResult> GetByCsvFileAsync(
        [FromServices] ICompaniesDataService service,
        IFormFile csvFile)
    {
        return await service.GetByCsvFileAsync(csvFile);
    }
}

using Crawlers.BusinessLogics.Models.Companies;
using Crawlers.BusinessLogics.Services.Interfaces;
using Crawlers.Src.Utility.Https;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace Crawlers.BusinessLogics.Services;

public class CompaniesDataService : ICompaniesDataService
{
    /// <summary>
    /// IHttpClientService
    /// </summary>
    private readonly IHttpClientService _httpClientService;

    /// <summary>
    /// The logger
    /// </summary>
    private ILogger<CompaniesDataService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompaniesDataService"/> class.
    /// </summary>
    /// <param name="httpClientService">The HTTP client service.</param>
    /// <param name="logger">The logger.</param>
    public CompaniesDataService(
        IHttpClientService httpClientService,
        ILogger<CompaniesDataService> logger)
    {
        _httpClientService = httpClientService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<FileStreamResult> GetByCsvFileAsync(IFormFile csvFile)
    {
        // 傳入參數為 csv 格式的檔案，有兩個欄位，分別為公司名稱及公司狀態

        // 1. 讀取檔案
        using var reader = new StreamReader(csvFile.OpenReadStream());
        var csvContent = await reader.ReadToEndAsync();
        var lines = csvContent.Split('\n').Skip(1); // Skip header

        // 2. 將檔案內容轉換為儲存公司名稱及公司狀態的 Entity 列表
        var request = new List<GetCompanyDataRequest>();
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var columns = line.Split(',');
            request.Add(new GetCompanyDataRequest
            {
                CompanyName = columns[0].Trim(),
                CompanyStatusDef = string.IsNullOrWhiteSpace(columns[1]) ? "01" : columns[1].Trim() // "01"代表核准設立
            });
        }

        // 3. 透過 IHttpClientService 取得公司資料
        var responseList = new List<GetCompanyDataResponse>();
        var requestCount = 0;
        foreach (var company in request)
        {
            requestCount++;
            _logger.LogInformation("序號：{RequestCount}, 公司名稱：{CompanyName}", requestCount, company.CompanyName);
            string requestUri = GetTargetRoute(company);
            var companyData = (await _httpClientService.GetAsync<List<GetCompanyDataResponse>>(requestUri, 300)) ?? new List<GetCompanyDataResponse> { new GetCompanyDataResponse { Company_Name = $"{company.CompanyName}(查無資料)" } };
            responseList.AddRange(companyData);
        }

        // 4. 將取得的公司資料寫入檔案，回傳 csv 檔案
        var responseCsvContent = new StringBuilder();
        responseCsvContent.AppendLine("統一編號,公司名稱,公司狀態,公司資本額,實收資本額,代表人,地址,核准設立日期,核准變更日期");

        foreach (var response in responseList)
        {
            //responseCsvContent.AppendLine(string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8}",
            //    string.IsNullOrWhiteSpace(response.Business_Accounting_NO) ? "查無資料" : response.Business_Accounting_NO,
            //    string.IsNullOrWhiteSpace(response.Company_Name),
            //    string.IsNullOrWhiteSpace(response.Company_Status_Desc),
            //    response.Capital_Stock_Amount,
            //    response.Paid_In_Capital_Amount,
            //    string.IsNullOrWhiteSpace(response.Responsible_Name),
            //    string.IsNullOrWhiteSpace(response.Company_Location),
            //    string.IsNullOrWhiteSpace(response.Company_Location),
            //    string.IsNullOrWhiteSpace(response.Company_Setup_Date),
            //    string.IsNullOrWhiteSpace(response.Change_Of_Approval_Data)));
            responseCsvContent.AppendLine($"{response.Business_Accounting_NO},{response.Company_Name},{response.Company_Status_Desc},{response.Capital_Stock_Amount},{response.Paid_In_Capital_Amount},{response.Responsible_Name},{response.Company_Location},{response.Company_Setup_Date},{response.Change_Of_Approval_Data}");
        }

        var localPath = Path.GetTempPath();
        var csvFileName = "CompanyData.csv";
        var csvFilePath = Path.Combine(localPath, csvFileName);
        await File.WriteAllTextAsync(csvFilePath, responseCsvContent.ToString());

        var memory = new MemoryStream();
        using (var stream = new FileStream(csvFilePath, FileMode.Open))
        {
            await stream.CopyToAsync(memory);
        }
        memory.Position = 0;

        // 5. 刪除臨時檔案
        File.Delete(csvFilePath);

        return new FileStreamResult(memory, "text/csv") { FileDownloadName = csvFileName };
    }

    /// <summary>
    /// 公司登記關鍵字查詢的路由(經濟部商業司)
    /// </summary>
    /// <param name="company">The company.</param>
    /// <returns>route</returns>
    /// <remarks>
    /// 請參考:https://data.gcis.nat.gov.tw/od/rule#F0E8FB8D-E2FD-472E-886C-91C673641F31
    /// 公司登記關鍵字查詢
    /// </remarks>
    private static string GetTargetRoute(GetCompanyDataRequest company)
    {
        return $"https://data.gcis.nat.gov.tw/od/data/api/6BBA2268-1367-4B42-9CCA-BC17499EBE8C?$format=json&$filter=Company_Name like {company.CompanyName} and Company_Status eq {company.CompanyStatusDef}&$skip=0&$top=50";
    }
}

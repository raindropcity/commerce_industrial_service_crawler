using Crawlers.BusinessLogics.Models.Companies;
using Crawlers.Src.Utility.Https;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace Crawlers.BusinessLogics.Services;

public class TpcaScraperService
{
    /// <summary>
    /// IHttpClientService
    /// </summary>
    private readonly IHttpClientService _httpClientService;

    /// <summary>
    /// The HTTP Client
    /// </summary>
    private readonly HttpClient _http = new HttpClient();

    /// <summary>
    /// The logger
    /// </summary>
    private ILogger<TpcaScraperService> _logger;

    public TpcaScraperService(
        IHttpClientService httpClientService,
        ILogger<TpcaScraperService> logger,
        HttpClient http)
    {
        _httpClientService = httpClientService;
        _logger = logger;

        //// 由於 TPCA 網站對於沒有 Header 的請求會直接回 500，因此這邊預設加入常見的 Header
        _http.DefaultRequestHeaders.Clear();

        _http.DefaultRequestHeaders.Add(
            "User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
            "AppleWebKit/537.36 (KHTML, like Gecko) " +
            "Chrome/120.0.0.0 Safari/537.36"
        );

        _http.DefaultRequestHeaders.Add(
            "Accept",
            "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8"
        );

        _http.DefaultRequestHeaders.Add(
            "Accept-Language",
            "zh-TW,zh;q=0.9,en-US;q=0.8,en;q=0.7"
        );

        _http.DefaultRequestHeaders.Add(
            "Accept-Encoding",
        "gzip, deflate"
        );

        _http.DefaultRequestHeaders.Add(
            "Connection",
            "keep-alive"
        );
    }

    public async Task<FileStreamResult> ScrapeAsync()
    {
        // 1. 以下 POST 請求，可以直接取得 memberIds
        var url = "https://www.tpca.org.tw/Industry/PagingMember";
        var request = new TpcaPostMemberIdRequest();
        var postMemberIdResponse = await _httpClientService.PostAsJsonAsync<TpcaPostMemberIdRequest, TpcaPostMemberIdResponse>(url, request, _http);


        var memberIds = postMemberIdResponse.Rows.Select(x => x.MemberID).ToList();

        _logger.LogInformation("Found {Count} memberIds", memberIds.Count);

        // 2. 逐一取得會員詳細資料頁面，整理出所需欄位
        var result = new List<TpcaMemberDto>();

        foreach (var id in memberIds)
        {
            var detailUrl = $"https://www.tpca.org.tw/Industry/Detail?memberid={id}&mid=819&itemid=1";

            var detailHtml = await _httpClientService.GetStringAsync(detailUrl, _http);
            var detailDoc = new HtmlDocument();
            detailDoc.LoadHtml(detailHtml);

            string GetValue(string label) =>
                detailDoc.DocumentNode
                    .SelectSingleNode($"//div[contains(@class,'member_deatil_th') and normalize-space(text())='{label}']" + "/following-sibling::div[contains(@class,'table-cell')]")
                    ?.InnerText
                    ?.Trim() ?? "";

            var companyName = detailDoc.DocumentNode.SelectSingleNode("//h2[contains(@class,'page_title')]")?.InnerText.Trim() ?? "";

            _logger.LogInformation("Scraping memberId: {MemberId}, CompanyName: {CompanyName}", id, companyName);

            result.Add(new TpcaMemberDto
            {
                CompanyName = companyName,
                Phone = GetValue("電話"),
                //Fax = GetValue("傳真"), // 網頁上有，但目前暫不需要
                Address = GetValue("地址"),
                //Website = GetValue("公司網址"), // 網頁上有，但目前暫不需要
                Contact = GetValue("業務窗口"),
                Email = GetValue("業務窗口Email")
            });

            await Task.Delay(100); // polite scraping
        }

        // 3. 處理寫入 CSV
        _logger.LogInformation("Generating CSV file...");
        var responseCsvContent = new StringBuilder();
        responseCsvContent.AppendLine("公司名稱,電話,地址,業務窗口,業務窗口Email");

        foreach (var item in result)
        {
            responseCsvContent.AppendLine($"{item.CompanyName},{item.Phone},{item.Address},{item.Contact},{item.Email}");
        }

        var localPath = Path.GetTempPath();
        var csvFileName = "TpcaData.csv";
        var csvFilePath = Path.Combine(localPath, csvFileName);
        await System.IO.File.WriteAllTextAsync(csvFilePath, responseCsvContent.ToString());

        var memory = new MemoryStream();
        using (var stream = new FileStream(csvFilePath, FileMode.Open))
        {
            await stream.CopyToAsync(memory);
        }
        memory.Position = 0;

        // 4. 刪除臨時檔案
        System.IO.File.Delete(csvFilePath);

        return new FileStreamResult(memory, "text/csv") { FileDownloadName = csvFileName };
    }
}
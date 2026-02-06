using Crawlers.BusinessLogics.Models.TEEIA;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text;
using static System.Net.WebRequestMethods;

namespace Crawlers.BusinessLogics.Services.TEEIA;

public class TeeiaScraperService
{
    const string BaseListUrl = "https://www.teeia.org.tw/tw/manufacture?theme=all&page={0}";

    /// <summary>
    /// The HTTP Client
    /// </summary>
    private readonly HttpClient _http = new HttpClient();

    /// <summary>
    /// The logger
    /// </summary>
    private ILogger<TeeiaScraperService> _logger;

    public TeeiaScraperService(
        ILogger<TeeiaScraperService> logger,
        HttpClient http)
    {
        _logger = logger;
        _http = http;
    }

    public async Task<FileStreamResult> ScrapeAsync()
    {
        // 1) 最大頁數
        _logger.LogInformation("1) 取得最大頁數...");
        var firstListHtml = await GetStringWithRetry(_http, string.Format(BaseListUrl, 1));
        var doc = LoadDoc(firstListHtml);
        var node = doc.DocumentNode.SelectSingleNode(
            "//div[contains(@class,'pagePagination')]"
        );
        var totalPageStr = node.GetAttributeValue("bk-total-page", "");

        if(!int.TryParse(totalPageStr, out int maxPage)) 
        {
            _logger.LogWarning($"無法解析最大頁數，預設為 1。原始值：{totalPageStr}");
            maxPage = 1;
        }

        _logger.LogInformation($"最大頁數 = {maxPage}");

        // 2) 逐頁收集公司資訊頁 URL
        _logger.LogInformation("2) 逐頁收集公司資訊頁 URL...");
        var companyUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int page = 1; page <= maxPage; page++)
        {
            var listUrl = string.Format(BaseListUrl, page);
            _logger.LogInformation($"  - Page {page}/{maxPage}: {listUrl}");

            var html = await GetStringWithRetry(_http, listUrl);
            foreach (var url in GetCompanyDetailUrls(html))
                companyUrls.Add(url);

            await Task.Delay(300); // 禮貌
        }

        _logger.LogInformation($"公司資訊頁 URL 數量 = {companyUrls.Count}");

        // 3) 逐公司抽資料
        _logger.LogInformation("3) 逐公司頁抽取欄位...");
        var results = new List<TeeiaMemberDto>();
        int i = 0;

        foreach (var url in companyUrls)
        {
            i++;
            _logger.LogInformation($"  [{i}/{companyUrls.Count}] {url}");

            try
            {
                var detailHtml = await GetStringWithRetry(_http, url);
                var row = ParseCompanyContact(detailHtml, url);
                results.Add(row);
            }
            catch (Exception ex)
            {
                results.Add(new TeeiaMemberDto
                {
                    SourceUrl = url,
                    CompanyNameZh = "",
                    Address = "",
                    ContactPerson = "",
                    Phone = "",
                    Email = "",
                    Error = ex.Message
                });
            }

            await Task.Delay(300);
        }

        // 處理寫入 CSV
        _logger.LogInformation("Generating CSV file...");
        var responseCsvContent = new StringBuilder();
        responseCsvContent.AppendLine("公司名稱,電話,地址,業務窗口,業務窗口Email");

        foreach (var item in results)
        {
            responseCsvContent.AppendLine($"{item.CompanyNameZh},{item.Phone},{item.Address},{item.ContactPerson},{item.Email}");
        }

        var localPath = Path.GetTempPath();
        var csvFileName = "TeeiaData.csv";
        var csvFilePath = Path.Combine(localPath, csvFileName);
        await System.IO.File.WriteAllTextAsync(csvFilePath, responseCsvContent.ToString());

        var memory = new MemoryStream();
        using (var stream = new FileStream(csvFilePath, FileMode.Open))
        {
            await stream.CopyToAsync(memory);
        }
        memory.Position = 0;

        // 刪除臨時檔案
        System.IO.File.Delete(csvFilePath);

        return new FileStreamResult(memory, "text/csv") { FileDownloadName = csvFileName };
    }

    private async Task<string> GetStringWithRetry(HttpClient http, string url, int maxRetry = 3)
    {
        Exception? last = null;

        for (int attempt = 1; attempt <= maxRetry; attempt++)
        {
            try
            {
                return await http.GetStringAsync(url);
            }
            catch (Exception ex)
            {
                last = ex;
                await Task.Delay(500 * attempt);
            }
        }

        throw new Exception($"GET 失敗（已重試 {maxRetry} 次）：{url}", last);
    }

    private int GetMaxPage(string listHtml)
    {
        var doc = LoadDoc(listHtml);

        // XPath：抓所有 class 含 page 與 bk-page 的 div，且有 data-page
        var nodes = doc.DocumentNode.SelectNodes(
            "//div[contains(@class,'page') and contains(@class,'bk-page') and @data-page]"
        );

        if (nodes == null || nodes.Count == 0)
            return 1;

        int max = 1;
        foreach (var n in nodes)
        {
            var s = n.GetAttributeValue("data-page", "");
            if (int.TryParse(s, out var p))
                max = Math.Max(max, p);
        }
        return max;
    }

    private HtmlDocument LoadDoc(string html)
    {
        var doc = new HtmlDocument
        {
            OptionFixNestedTags = true
        };
        doc.LoadHtml(html);
        return doc;
    }

    private IEnumerable<string> GetCompanyDetailUrls(string listHtml)
    {
        var doc = LoadDoc(listHtml);

        var nodes = doc.DocumentNode.SelectNodes(
            "//a[contains(@class,'pic-box')]"
        );

        if (nodes == null) yield break;

        foreach (var n in nodes)
        {
            var url = n.GetAttributeValue("href", "").Trim();
            if (!string.IsNullOrWhiteSpace(url))
                yield return url;
        }
    }

    private TeeiaMemberDto ParseCompanyContact(string detailHtml, string sourceUrl)
    {
        const string KeyCompanyName = "企業中文名稱";
        const string KeyAddress = "聯絡地址";
        const string KeyContact = "聯絡人";
        const string KeyPhone = "聯絡電話";
        const string KeyEmail = "電子郵件";

        var doc = LoadDoc(detailHtml);

        // 你的圖三：.row.cn 內有 p.title / p.detail
        var rowNodes = doc.DocumentNode.SelectNodes("//*[contains(@class,'row') and contains(@class,'cn')]");

        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (rowNodes != null)
        {
            foreach (var row in rowNodes)
            {
                var titleNode = row.SelectSingleNode(".//p[contains(@class,'title')]");
                var detailNode = row.SelectSingleNode(".//p[contains(@class,'detail')]");

                var title = CleanText(titleNode?.InnerText);
                if (string.IsNullOrWhiteSpace(title)) continue;

                var detail = CleanText(detailNode?.InnerText);
                dict[title] = detail;
            }
        }

        string Get(string key) => dict.TryGetValue(key, out var v) ? v : "";

        return new TeeiaMemberDto
        {
            SourceUrl = sourceUrl,
            CompanyNameZh = Get(KeyCompanyName),
            Address = Get(KeyAddress),
            ContactPerson = Get(KeyContact),
            Phone = Get(KeyPhone),
            Email = Get(KeyEmail),
            Error = ""
        };
    }

    private string CleanText(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";

        s = WebUtility.HtmlDecode(s);
        s = s.Replace("\u00A0", " "); // nbsp
        s = s.Trim();

        // 去掉首尾引號（" 企業 " 這種）
        s = s.Trim(' ', '"', '“', '”');

        // 把多空白壓成一格（避免換行/多空白）
        s = string.Join(" ", s.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries));
        return s;
    }
}
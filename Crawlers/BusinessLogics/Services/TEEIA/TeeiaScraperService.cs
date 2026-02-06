using Crawlers.BusinessLogics.Models.TEEIA;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using Crawlers.Src.Utility.Helpers;

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

        if (!int.TryParse(totalPageStr, out int maxPage))
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

            await Task.Delay(100); // 禮貌
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

            await Task.Delay(100);
        }

        // 4) 處理寫入 CSV
        _logger.LogInformation("Generating CSV file...");
        var lines = new List<string>
        {
            "公司名稱,電話,地址,業務窗口,業務窗口Email"
        };

        foreach (var item in results)
        {
            lines.Add($"{item.CompanyNameZh},{item.Phone},{item.Address},{item.ContactPerson},{item.Email}");
        }

        var csvFileName = "TeeiaData.csv";
        var csvFile = await CsvFileHelper.CreateFileStreamResultAsync(lines, csvFileName);

        _logger.LogInformation("Complete CSV file generation.");
        return csvFile;
    }

    /// <summary>
    /// 取得 HTML 結構字串，並在失敗時重試（最多重試 maxRetry 次，每次重試間隔逐漸增加）
    /// </summary>
    /// <param name="http">The HttpClient instance used to send the request.</param>
    /// <param name="url">The URL to request.</param>
    /// <param name="maxRetry">The maximum number of retry attempts.</param>
    /// <returns>A task representing the asynchronous operation, with the response body as a string.</returns>
    /// <exception cref="Exception">Thrown if all retry attempts fail.</exception>
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

    /// <summary>
    /// Parses the specified HTML string into an HtmlDocument with nested tag correction enabled.
    /// </summary>
    /// <param name="html">The HTML content to parse.</param>
    /// <returns>An HtmlDocument representing the parsed HTML.</returns>
    private HtmlDocument LoadDoc(string html)
    {
        var doc = new HtmlDocument
        {
            OptionFixNestedTags = true
        };
        doc.LoadHtml(html);
        return doc;
    }

    /// <summary>
    /// Extracts company detail URLs from the provided HTML containing a list of companies.
    /// </summary>
    /// <param name="listHtml">The HTML string representing the list of companies.</param>
    /// <returns>An enumerable collection of company detail URLs found in the HTML.</returns>
    private IEnumerable<string> GetCompanyDetailUrls(string listHtml)
    {
        var doc = LoadDoc(listHtml);

        //// 目標的 anchor 元素具有 class="pic-box"，因此使用 XPath "//a[contains(@class,'pic-box')]" 來選取這些元素
        var nodes = doc.DocumentNode.SelectNodes(
            "//a[contains(@class,'pic-box')]"
        );

        if (nodes == null) yield break;

        foreach (var n in nodes)
        {
            //// 抽取 anchor 元素的 href 屬性 value，以獲得目標 url
            var url = n.GetAttributeValue("href", "").Trim();
            if (!string.IsNullOrWhiteSpace(url))
                yield return url;
        }
    }

    /// <summary>
    /// Parses company contact information from the provided HTML and returns a TeeiaMemberDto with extracted details.
    /// </summary>
    /// <param name="detailHtml">The HTML string containing the company contact details.</param>
    /// <param name="sourceUrl">The source URL associated with the company contact information.</param>
    /// <returns>A TeeiaMemberDto populated with company name, address, contact person, phone, email, and source URL.</returns>
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

    /// <summary>
    /// Cleans and normalizes a string by decoding HTML entities, trimming whitespace and quotation marks, and
    /// collapsing multiple spaces into one.
    /// </summary>
    /// <param name="s">The input string to clean and normalize.</param>
    /// <returns>A cleaned and normalized string, or an empty string if the input is null or whitespace.</returns>
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
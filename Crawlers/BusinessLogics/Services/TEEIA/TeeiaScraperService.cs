using Crawlers.BusinessLogics.Models.TEEIA;
using Microsoft.AspNetCore.Mvc;
using Crawlers.Src.Utility.Helpers;

namespace Crawlers.BusinessLogics.Services.TEEIA;

public class TeeiaScraperService : HtmlScraperServiceBase
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

    /// <summary>
    /// inheritdoc
    /// </summary>
    public override async Task<FileStreamResult> ScrapeAsync()
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
}
using Crawlers.BusinessLogics.Models;
using Crawlers.BusinessLogics.Models.TPCIA;
using Crawlers.Src.Utility.Comparers;
using Crawlers.Src.Utility.Helpers;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;

namespace Crawlers.BusinessLogics.Services.TPCIA;
public class TpciaScraperService : HtmlScraperServiceBase
{
    private Dictionary<string, string> BaseListUrls = new Dictionary<string, string>
    {
        { "元件製造", "https://www.tpcia.org.tw/%E7%94%A2%E5%93%81%E8%B3%87%E8%A8%8A/%E5%85%83%E4%BB%B6%E8%A3%BD%E9%80%A0?page={0}" },
        { "材料廠", "https://www.tpcia.org.tw/%E7%94%A2%E5%93%81%E8%B3%87%E8%A8%8A/%E6%9D%90%E6%96%99%E5%BB%A0?page={0}" },
        { "設備廠", "https://www.tpcia.org.tw/%E7%94%A2%E5%93%81%E8%B3%87%E8%A8%8A/%E8%A8%AD%E5%82%99%E5%BB%A0?page={0}" },
        { "通路/代理/其他", "https://www.tpcia.org.tw/%E7%94%A2%E5%93%81%E8%B3%87%E8%A8%8A/%E9%80%9A%E8%B7%AF-%E4%BB%A3%E7%90%86-%E5%85%B6%E4%BB%96?page={0}" }
    };

    /// <summary>
    /// The HTTP Client
    /// </summary>
    private readonly HttpClient _http = new HttpClient();

    /// <summary>
    /// The logger
    /// </summary>
    private ILogger<TpciaScraperService> _logger;

    public TpciaScraperService(
        ILogger<TpciaScraperService> logger,
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
        var maxPage = 2; // 現況觀察此網站每個類別的公司列表都是 2 頁，先寫死，未來如果有變化再調整

        // 1) 逐頁收集公司資訊頁 URL
        _logger.LogInformation("1) 逐頁收集公司資訊頁 URL...");

        var companyUrls = new HashSet<UrlDto>(new UrlDtoUrlComparer());

        foreach (var item in BaseListUrls)
        {
            _logger.LogInformation($"- 類別: {item.Key}");

            var baseUrl = item.Value;

            for (int page = 1; page <= maxPage; page++)
            {
                var listUrl = string.Format(baseUrl, page);
                _logger.LogInformation($"  - Page {page}/{maxPage}: {listUrl}");

                var html = await GetStringWithRetry(_http, listUrl);
                foreach (var info in GetCompanyDetailUrls(html))
                {
                    var urlDto = new UrlDto
                    {
                        Category = item.Key,
                        Url = info.Url,
                        CompanyName = info.CompanyName
                    };

                    companyUrls.Add(urlDto);
                }

                await Task.Delay(100); // 禮貌
            }
        }

        _logger.LogInformation($"公司資訊頁 URL 數量 = {companyUrls.Count}");

        // 2) 逐公司抽資料
        _logger.LogInformation("2) 逐公司頁抽取欄位...");
        var results = new List<TpciaMemberDto>();
        int i = 0;

        foreach (var url in companyUrls)
        {
            i++;
            _logger.LogInformation($"  [{i}/{companyUrls.Count}] {url.Url}");

            try
            {
                var detailHtml = await GetStringWithRetry(_http, url.Url);
                var row = ParseCompanyContact(detailHtml, url);
                row.Category = url.Category;
                results.Add(row);
            }
            catch (Exception ex)
            {
                results.Add(new TpciaMemberDto
                {
                    SourceUrl = url.Url,
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

        // 3) 處理寫入 CSV
        _logger.LogInformation("Generating CSV file...");
        var lines = new List<string>
        {
            "公司名稱,電話,地址,業務窗口,業務窗口Email"
        };

        var groupedResults = results.GroupBy(x => x.Category);
        var category = string.Empty;

        foreach (var item in groupedResults)
        {
            if(category != item.Key)
            {
                category = item.Key;
                lines.Add(item.Key);
            }

            var members = item.ToList();
            foreach (var member in members)
                lines.Add($"{member.CompanyNameZh},{member.Phone},{member.Address},{member.ContactPerson},{member.Email}");
        }

        var csvFileName = "TpciaData.csv";
        var csvFile = await CsvFileHelper.CreateFileStreamResultAsync(lines, csvFileName);

        _logger.LogInformation("Complete CSV file generation.");
        return csvFile;
    }

    /// <summary>
    /// Extracts company detail URLs from the provided HTML containing a list of companies.
    /// </summary>
    /// <param name="listHtml">The HTML string representing the list of companies.</param>
    /// <returns>An enumerable collection of company detail URLs found in the HTML.</returns>
    private IEnumerable<(string Url, string CompanyName)> GetCompanyDetailUrls(string listHtml)
    {
        var doc = LoadDoc(listHtml);

        //// 目標的 anchor 元素會是 class="pro_in1" 的 div 的子元素
        var nodes = doc.DocumentNode.SelectNodes(
            "//div[contains(concat(' ', normalize-space(@class), ' '), ' pro_in1 ')]/a[@href]"
        );

        if (nodes == null) yield break;

        foreach (var n in nodes)
        {
            //// 抽取 anchor 元素的 href 屬性 value，以獲得目標 url
            var url = n.GetAttributeValue("href", "").Trim();
            var companyName = n.GetAttributeValue("title", "").Trim();
            if (!string.IsNullOrWhiteSpace(url))
                yield return (url, companyName);
        }
    }

    /// <summary>
    /// Parses company contact information from the provided HTML and returns a TeeiaMemberDto with extracted details.
    /// </summary>
    /// <param name="detailHtml">The HTML string containing the company contact details.</param>
    /// <param name="sourceUrl">The source URL associated with the company contact information.</param>
    /// <returns>A TeeiaMemberDto populated with company name, address, contact person, phone, email, and source URL.</returns>
    private TpciaMemberDto ParseCompanyContact(string detailHtml, UrlDto urlDto)
    {
        // 這個頁面的標題文字（跟你貼的 HTML 一致）
        const string KeyPhone = "電話";
        var keyContactWindow = "業務窗口";

        var doc = LoadDoc(detailHtml);

        // 1) 把 table 的每列轉成 dict: { 左欄標題 => 右欄內容 }
        //    這裡用比較泛用的：找出所有 tr，且該 tr 有兩個 td
        var trNodes = doc.DocumentNode.SelectNodes("//table//tr[td and count(td) >= 2]");

        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (trNodes != null)
        {
            foreach (var tr in trNodes)
            {
                var tds = tr.SelectNodes("./td");
                if (tds == null || tds.Count < 2) continue;

                var key = CleanText(tds[0].InnerText);   // 左欄：電話 / 業務窗口 / ...
                var val = CleanText(tds[1].InnerText);   // 右欄：03-xxxx / 姓名：... E-mail：...

                if (string.IsNullOrWhiteSpace(key)) continue;

                // 同一 key 若重複出現，保留最新或你要改成 append 也行
                dict[key] = val;
            }
        }

        string Get(string key) => dict.TryGetValue(key, out var v) ? v : "";

        // 2) 解析電話
        var phone = Get(KeyPhone);

        // 3) 解析業務窗口（姓名、E-mail）
        var bizText = Get(keyContactWindow);
        if (string.IsNullOrWhiteSpace(bizText))
        {
            keyContactWindow = "聯絡窗口";
            bizText = Get(keyContactWindow);
        }
        var contactName = ExtractAfterLabel(bizText, "姓名");
        var email = ExtractAfterLabel(bizText, "E-mail", "Email", "E-Mail");

        return new TpciaMemberDto
        {
            SourceUrl = urlDto.Url,
            CompanyNameZh = urlDto.CompanyName,
            Address = string.Empty,
            ContactPerson = contactName,
            Phone = phone,
            Email = email,

            Error = ""
        };
    }

    /// <summary>
    /// 從一段文字中擷取「標籤：值」的值，例如：
    /// "姓名：陳士鈞\nE-mail：phil.chen@tai.com.tw"
    /// </summary>
    private static string ExtractAfterLabel(string input, params string[] labels)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";

        foreach (var label in labels)
        {
            // 抓「標籤：值」，值先用最寬鬆方式抓一段（避免依賴換行）
            var pattern = @$"(?:^|\s){Regex.Escape(label)}\s*[:：]\s*(?<v>.+)";
            var m = Regex.Match(input, pattern, RegexOptions.IgnoreCase);
            if (!m.Success) continue;

            var v = m.Groups["v"].Value.Trim();

            // 如果後面還有其他欄位（常見：E-mail/Email），把它切掉
            // 例如： "陳士鈞 E-mail：phil..." -> "陳士鈞"
            v = Regex.Split(v, @"\s*(E-?mail|Email)\s*[:：]\s*", RegexOptions.IgnoreCase)[0].Trim();

            return v;
        }

        return string.Empty;
    }
}
using Crawlers.BusinessLogics.Models.ASIP;
using Crawlers.Src.Utility.Helpers;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;

namespace Crawlers.BusinessLogics.Services.ASIP;

public class AsipScraperService : HtmlScraperServiceBase
{
    const string BaseListUrl = "https://www.asip.org.tw/com_n.php?pageNum_reComList={0}";

    /// <summary>
    /// The HTTP Client
    /// </summary>
    private readonly HttpClient _http = new HttpClient();

    /// <summary>
    /// The logger
    /// </summary>
    private ILogger<AsipScraperService> _logger;

    public AsipScraperService(
        HttpClient http,
        ILogger<AsipScraperService> logger)
    {
        _http = http;
        _logger = logger;
    }

    public override async Task<FileStreamResult> ScrapeAsync()
    {
        // 1) 最大頁數
        var maxPage = 28; // 現況觀察此網站是 29 頁(從0開始算)，先寫死，未來如果有變化再調整
        _logger.LogInformation($"最大頁數 = {maxPage + 1}");

        // 2) 逐頁收集公司資訊頁 URL
        _logger.LogInformation("2) 逐頁收集公司資訊頁 URL...");
        var companyUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int page = 0; page <= maxPage; page++)
        {
            var listUrl = string.Format(BaseListUrl, page);
            _logger.LogInformation($"  - Page {page + 1}/{maxPage + 1}: {listUrl}");

            var html = await GetStringWithRetry(_http, listUrl);
            foreach (var url in GetCompanyDetailUrls(html))
                companyUrls.Add(url);

            await Task.Delay(100); // 禮貌
        }

        _logger.LogInformation($"公司資訊頁 URL 數量 = {companyUrls.Count}");

        // 3) 逐公司抽資料
        _logger.LogInformation("3) 逐公司頁抽取欄位...");
        var results = new List<AsipMemberDto>();
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
                results.Add(new AsipMemberDto
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
            "科學園區,產業類別,公司名稱,電話,地址,業務窗口,公共關係窗口,業務窗口Email(有可能是公共關係窗口的Email)"
        };

        foreach (var item in results)
        {
            lines.Add($"{item.SciencePark},{item.Category},{item.CompanyNameZh},{item.Phone},{item.Address},{item.ContactPerson},{item.PublicRelationship},{item.Email}");
        }

        var csvFileName = "AsipData.csv";
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

        // 找出有 onclick，且 onclick 內包含 outinfo.php 的 a
        var nodes = doc.DocumentNode.SelectNodes(
            "//a[@onclick and contains(@onclick,'outinfo.php')]"
        );

        if (nodes == null) yield break;

        // 抓 window.open('...') 的第一個參數（支援 ' 或 "）
        var re = new Regex(@"window\.open\(\s*(['""])(?<url>.*?)\1",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        foreach (var a in nodes)
        {
            var onclick = a.GetAttributeValue("onclick", "").Trim();
            if (string.IsNullOrWhiteSpace(onclick)) continue;

            var m = re.Match(onclick);
            if (!m.Success) continue;

            var url = m.Groups["url"].Value.Trim(); // 會抽取出以下格式路徑 com/outinfo.php?id=5
            if (!string.IsNullOrWhiteSpace(url))
                yield return new Uri(new Uri("https://www.asip.org.tw/"), url).ToString(); // 加上 Domain，變成絕對路徑
        }
    }

    /// <summary>
    /// Parses company contact information from the provided HTML and returns a TeeiaMemberDto with extracted details.
    /// </summary>
    /// <param name="detailHtml">The HTML string containing the company contact details.</param>
    /// <param name="sourceUrl">The source URL associated with the company contact information.</param>
    /// <returns>A TeeiaMemberDto populated with company name, address, contact person, phone, email, and source URL.</returns>
    private AsipMemberDto ParseCompanyContact(string detailHtml, string url)
    {
        // 這個頁面的標題文字（跟你貼的 HTML 一致）
        const string KeyCategory = "產業類別：";
        const string KeyCompanyName = "廠商名稱：";
        const string KeyAddress = "公司地址：";
        const string KeyEmail = "電子郵件：";
        const string KeyPhone = "公司電話：";
        const string KeyPublicRelationship = "公共關係：";
        const string KeyContactWindow = "業務連絡：";

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

        // 解析公司名稱
        var rawCompanyName = Get(KeyCompanyName);
        var companyName = ExtractAfterLabel(rawCompanyName, "中");

        // 解析業務窗口、公共關係窗口
        var rawContactWindow = Get(KeyContactWindow);
        var rawPublicRelationship = Get(KeyPublicRelationship);
        var contactWindow = ExtractAfterLabel(rawContactWindow, "中");
        var publicRelationship = ExtractAfterLabel(rawPublicRelationship, "中");

        // 解析公司地址
        var rawAddress = Get(KeyAddress);
        // 1) 取出文字（<br> 會變成換行或空白；我們統一成空白）
        var text = HtmlEntity.DeEntitize(rawAddress ?? string.Empty);
        text = Regex.Replace(text, @"\s+", " ").Trim();
        // 2) 抓出所有 Key：Value 配對（Value 抓到「下一個 Key：」之前）
        // key: 允許「地址(中)」這種帶括號的 key
        var pairRegex = new Regex(
            @"(?<k>[^\s：:]+(?:\([^)]+\))?)\s*[:：]\s*(?<v>.*?)(?=(?:\s+[^\s：:]+(?:\([^)]+\))?\s*[:：])|$)",
            RegexOptions.Compiled);

        foreach (Match m in pairRegex.Matches(text))
        {
            var key = m.Groups["k"].Value.Trim();
            var val = m.Groups["v"].Value.Trim();

            // 排除你不想要的欄位「郵遞區號」
            if (key.Equals("郵遞區號", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!string.IsNullOrWhiteSpace(key))
                dict[key] = val;
        }

        return new AsipMemberDto
        {
            SourceUrl = url,
            CompanyNameZh = companyName,
            Address = Get("地址(中)"),
            ContactPerson = contactWindow,
            Phone = Get(KeyPhone),
            Email = Get(KeyEmail),
            Category = Get(KeyCategory),
            PublicRelationship = publicRelationship,
            SciencePark = Get("科學園區"),
            Error = ""
        };
    }

    /// <summary>
    /// 從一段文字中擷取「標籤：值」的值，例如：
    /// "中：聯華生技股份有限公司"
    /// </summary>
    private static string ExtractAfterLabel(string input, params string[] labels)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";

        foreach (var label in labels)
        {
            // label 後面抓一整段
            var pattern = @$"(?:^|\s){Regex.Escape(label)}\s*[:：]\s*(?<v>.+)";
            var m = Regex.Match(input, pattern, RegexOptions.IgnoreCase);
            if (!m.Success) continue;

            var v = m.Groups["v"].Value.Trim();

            // 1) 先把後續其他欄位切掉（你原本的 Email 邏輯）
            v = Regex.Split(v, @"\s*(E-?mail|Email)\s*[:：]\s*", RegexOptions.IgnoreCase)[0].Trim();

            // 2) 如果還出現像「中：公司名稱XXX」這種多段冒號，取最後一段
            //    例如： "中：公司名稱XXX" -> "公司名稱XXX"
            //         "中: 公司名稱XXX" -> "公司名稱XXX"
            v = Regex.Replace(v, @"^.*?[:：]\s*", "").Trim();

            return v;
        }

        return string.Empty;
    }
}
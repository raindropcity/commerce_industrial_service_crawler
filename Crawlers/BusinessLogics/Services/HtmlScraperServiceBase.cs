using HtmlAgilityPack;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Crawlers.BusinessLogics.Services;

/// <summary>
/// 處理從 HTML 結構抓取資訊的基礎服務，提供共用的 HTTP 請求重試、HTML 解析和文字清理等功能
/// </summary>
public abstract class HtmlScraperServiceBase
{
    /// <summary>
    /// 客製化抽取資料邏輯，並統一以 FileStreamResult 回傳 CSV 檔
    /// </summary>
    /// <returns></returns>
    public abstract Task<FileStreamResult> ScrapeAsync();

    /// <summary>
    /// 取得 HTML 結構字串，並在失敗時重試（最多重試 maxRetry 次，每次重試間隔逐漸增加）
    /// </summary>
    /// <param name="http">The HttpClient instance used to send the request.</param>
    /// <param name="url">The URL to request.</param>
    /// <param name="maxRetry">The maximum number of retry attempts.</param>
    /// <returns>A task representing the asynchronous operation, with the response body as a string.</returns>
    /// <exception cref="Exception">Thrown if all retry attempts fail.</exception>
    protected async Task<string> GetStringWithRetry(HttpClient http, string url, int maxRetry = 3)
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
    protected HtmlDocument LoadDoc(string html)
    {
        var doc = new HtmlDocument
        {
            OptionFixNestedTags = true
        };
        doc.LoadHtml(html);
        return doc;
    }

    /// <summary>
    /// Cleans and normalizes a string by decoding HTML entities, trimming whitespace and quotation marks, and
    /// collapsing multiple spaces into one.
    /// </summary>
    /// <param name="s">The input string to clean and normalize.</param>
    /// <returns>A cleaned and normalized string, or an empty string if the input is null or whitespace.</returns>
    protected string CleanText(string? s)
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
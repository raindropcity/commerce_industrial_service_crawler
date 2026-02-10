namespace Crawlers.BusinessLogics.Models;

/// <summary>
/// 該會員公司網址 DTO
/// </summary>
public class UrlDto
{
    /// <summary>
    /// 該公司產業類別(選填)
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// 公司名稱(選填)
    /// </summary>
    public string CompanyName { get; set; } = string.Empty;

    /// <summary>
    /// 網址
    /// </summary>
    public string Url { get; set; }
}
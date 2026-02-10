namespace Crawlers.BusinessLogics.Models;

public class MemberInfoBase
{
    public string SourceUrl { get; set; } = string.Empty;
    public string CompanyNameZh { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string ContactPerson { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
}
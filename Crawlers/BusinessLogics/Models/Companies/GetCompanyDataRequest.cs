namespace Crawlers.BusinessLogics.Models.Companies;

public class GetCompanyDataRequest
{
    /// <summary>
    /// 公司名稱
    /// </summary>
    public string CompanyName { get; set; } = null!;

    /// <summary>
    /// 公司狀態代碼
    /// </summary>
    /// <remarks>詳見 https://data.gcis.nat.gov.tw/od/cmpStatusCodeData?type=xls</remarks>
    public string CompanyStatusDef { get; set; } = "01";
}
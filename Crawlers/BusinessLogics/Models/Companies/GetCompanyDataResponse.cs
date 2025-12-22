namespace Crawlers.BusinessLogics.Models.Companies;

public class GetCompanyDataResponse
{
    /// <summary>
    /// 統一編號
    /// </summary>
    public string Business_Accounting_NO { get; set; } = string.Empty;

    /// <summary>
    /// 公司名稱
    /// </summary>
    public string Company_Name { get; set; } = string.Empty;

    /// <summary>
    /// 公司狀態代碼
    /// </summary>
    public string Company_Status { get; set; } = string.Empty;

    /// <summary>
    /// 公司狀態描述
    /// </summary>
    public string Company_Status_Desc { get; set; } = string.Empty;

    /// <summary>
    /// 資本額
    /// </summary>
    public decimal? Capital_Stock_Amount { get; set; } = decimal.Zero;

    /// <summary>
    /// 實收資本額
    /// </summary>
    public decimal? Paid_In_Capital_Amount { get; set; } = decimal.Zero;

    /// <summary>
    /// 負責人姓名
    /// </summary>
    public string Responsible_Name { get; set; } = string.Empty;

    /// <summary>
    /// 登記機關代碼
    /// </summary>
    public string Register_Organization { get; set; } = string.Empty;

    /// <summary>
    /// 登記機關描述
    /// </summary>
    public string Register_Organization_Desc { get; set; } = string.Empty;

    /// <summary>
    /// 公司地址
    /// </summary>
    public string Company_Location { get; set; } = string.Empty;

    /// <summary>
    /// 公司設立日期
    /// </summary>
    public string Company_Setup_Date { get; set; } = string.Empty;

    /// <summary>
    /// 核准變更日期
    /// </summary>
    public string Change_Of_Approval_Data { get; set; } = string.Empty;
}

namespace Crawlers.BusinessLogics.Models.TPCA;

public class TpcaPostMemberIdRequest
{
    public string Sort { get; set; } = "Code";

    public int Limit { get; set; } = 1000;

    public int NowPage { get; set; } = 1;

    public int Industryitemid { get; set; } = 1;

    public int UseLang { get; set; } = 1;

    public int Offset { get; set; } = 0;

    /// <summary>
    /// 建立物件
    /// </summary>
    /// <param name="industryitemid">industryitemid</param>
    /// <returns>TpcaPostMemberIdRequest 實體</returns>
    public static TpcaPostMemberIdRequest Create(int industryitemid)
    {
        var instant = new TpcaPostMemberIdRequest();
        instant.Industryitemid = industryitemid;

        return instant;
    }
}
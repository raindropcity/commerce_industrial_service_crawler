namespace Crawlers.BusinessLogics.Models.Companies;

public class TpcaPostMemberIdRequest
{
    public string Sort { get; set; } = "Code";

    public int Limit { get; set; } = 1000;

    public int NowPage { get; set; } = 1;

    public int Industryitemid { get; set; } = 1;

    public int UseLang { get; set; } = 1;

    public int Offset { get; set; } = 0;
}
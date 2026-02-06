namespace Crawlers.BusinessLogics.Models.TPCA;

public class TpcaPostMemberIdResponse
{
    public int Total { get; set; }

    public List<RowItem> Rows { get; set; }
}

public class RowItem
{
    public int MemberID { get; set; }

    public string HtmlStr { get; set; }
}
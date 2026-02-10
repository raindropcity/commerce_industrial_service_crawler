using Crawlers.BusinessLogics.Models;

namespace Crawlers.Src.Utility.Comparers;

/// <summary>
/// 自訂 HashSet<UrlDto> 判斷是否重複的比較規則
/// </summary>
public sealed class UrlDtoUrlComparer : IEqualityComparer<UrlDto>
{
    public bool Equals(UrlDto? x, UrlDto? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x is null || y is null) return false;

        return StringComparer.OrdinalIgnoreCase.Equals(x.Url, y.Url);
    }

    public int GetHashCode(UrlDto obj)
    {
        // 重要：HashSet 會用 hash code 分桶，所以必須跟 Equals 的規則一致
        return StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Url ?? string.Empty);
    }
}
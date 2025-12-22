using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text.Unicode;

namespace Crawlers.Src.Utility.Extensions;
public static class JsonExtensions
{
    //// 設定反序列化為忽略大小寫
    private static readonly JsonSerializerOptions DeserializedJsonSerializerOptions = new() { PropertyNameCaseInsensitive = true };

    //// 因中文字元被轉成 UCN (Unicode Character Name，例如："\u9ED1\u6697\u57F7\u884C\u7DD2") 因此設定Encoder
    private static readonly JsonSerializerOptions SerializedJsonSerializerOptions = new() { Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) };

    private const string JsonPattern = @"^(\[|\{)(.|\n)*(\]|\})$";

    /// <summary>
    /// To the Json String.
    /// </summary>
    /// <param name="target">The target.</param>
    /// <returns>Json String</returns>
    public static string ToJson<T>(this T target)
    {
        return JsonSerializer.Serialize(target, SerializedJsonSerializerOptions);
    }

    /// <summary>
    /// To the typed object.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="s">The s.</param>
    /// <returns>TypedObject</returns>
    public static T ToTypedObject<T>(this string s)
    {
        if (Regex.IsMatch(s, JsonPattern, RegexOptions.Compiled) == false)
        {
            return default(T);
        }

        return JsonSerializer.Deserialize<T>(s, DeserializedJsonSerializerOptions);
    }

    /// <summary>
    /// string 是否為 Json 格式
    /// </summary>
    /// <param name="s">The s.</param>
    public static bool IsJson(this string s)
    {
        return s != null && Regex.IsMatch(s, JsonPattern, RegexOptions.Compiled);
    }
}

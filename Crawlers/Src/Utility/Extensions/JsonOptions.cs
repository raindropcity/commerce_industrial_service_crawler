using System.Text.Json.Serialization;
using System.Text.Json;

namespace Crawlers.Src.Utility.Extensions;

/// <summary>
/// JsonOptions
/// </summary>
public static class JsonOptions
{
    /// <summary>
    /// Web JsonSerializerOptions
    /// </summary>
    public static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// JsonOptions
    /// </summary>
    static JsonOptions()
    {
        Web.Converters.Add(new JsonStringEnumConverter());
    }
}
namespace Crawlers.Src.Utility.Https;

public interface IHttpClientService
{
    /// <summary>
    /// Send a Get request
    /// </summary>
    /// <typeparam name="T">回應泛型</typeparam>
    /// <param name="requestUri">請求網址</param>
    /// <param name="timeout">逾時(秒)</param>
    /// <returns>回應</returns>
    Task<T> GetAsync<T>(string requestUri, double timeout = 30) where T : class;
}

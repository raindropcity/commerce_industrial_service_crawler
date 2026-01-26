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

    /// <summary>
    /// http post
    /// </summary>
    /// <param name="requestUri">請求網址</param>
    /// <param name="requestBody">http body</param>
    /// <param name="timeout">逾時(秒)</param>
    /// <typeparam name="TRequest">請求T</typeparam>
    /// <typeparam name="TResponse">回應T</typeparam>
    /// <returns>Task</returns>
    Task<TResponse> PostAsJsonAsync<TRequest, TResponse>(string requestUri, TRequest requestBody, HttpClient client = null, double timeout = 30)
        where TRequest : class
        where TResponse : class;

    /// <summary>
    /// 讀取 HTML
    /// </summary>
    /// <param name="requestUri">The request URI.</param>
    /// <param name="timeout">The timeout.</param>
    /// <returns></returns>
    Task<string> GetStringAsync(string requestUri, HttpClient client = null, double timeout = 30);
}

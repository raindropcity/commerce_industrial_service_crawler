using Crawlers.Src.Utility.Extensions;

namespace Crawlers.Src.Utility.Https;

public class HttpClientService : IHttpClientService
{
    /// <summary>
    /// IHttpClientFactory
    /// </summary>
    private readonly IHttpClientFactory _clientFactory;

    /// <summary>
    /// ILogger<IHttpClientService>
    /// </summary>
    private readonly ILogger<IHttpClientService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpClientService"/> class.
    /// </summary>
    /// <param name="clientFactory">IHttpClientFactory</param>
    /// <param name="logger">ILogger</param>
    public HttpClientService(IHttpClientFactory clientFactory, ILogger<IHttpClientService> logger)
    {
        this._clientFactory = clientFactory;
        this._logger = logger;
    }

    /// <inheritdoc />
    public async Task<T> GetAsync<T>(string requestUri, double timeout = 30) where T : class
    {
        var httpClient = this.GetHttpClient(timeout);
        var httpResponseMessage = await httpClient.GetAsync(requestUri);

        var responseContent = await httpResponseMessage.Content.ReadAsStringAsync();
        if (httpResponseMessage.IsSuccessStatusCode)
        {
            var responseEntity = responseContent.ToTypedObject<T>();
            return responseEntity;
        }
        else
        {
            this._logger.LogError("Http StatusCode:{StatusCode}, ResponseContent: {ResponseContent}", httpResponseMessage.StatusCode, responseContent);
            return null;
        }
    }

    /// <summary>
    /// http post
    /// </summary>
    /// <param name="requestUri">請求網址</param>
    /// <param name="requestBody">http body</param>
    /// <param name="timeout">逾時(秒)</param>
    /// <typeparam name="TRequest">請求T</typeparam>
    /// <typeparam name="TResponse">回應T</typeparam>
    /// <returns>Task{TResponse}</returns>
    public async Task<TResponse> PostAsJsonAsync<TRequest, TResponse>(string requestUri, TRequest requestBody, HttpClient client = null, double timeout = 30)
        where TRequest : class
        where TResponse : class
    {
        var httpClient = client ?? this.GetHttpClient(timeout);
        var response = await httpClient.PostAsJsonAsync(requestUri, requestBody);

        if (response.IsSuccessStatusCode == false)
        {
            var message = await response.Content.ReadAsStringAsync();
            _logger.LogError("PostAsJsonAsyncV2 Error. Response Body: {Message}", message);
            response.EnsureSuccessStatusCode();
        }

        return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions.Web);
    }

    /// <inheritdoc />
    public async Task<string> GetStringAsync(string requestUri, HttpClient client = null, double timeout = 30)
    {
        var httpClient = client ?? this.GetHttpClient(timeout);
        var html = await httpClient.GetStringAsync(requestUri);

        return html;
    }

    /// <summary>
    /// 取得http client
    /// </summary>
    /// <param name="timeout">逾時(秒)</param>
    /// <returns>HttpClient</returns>
    private HttpClient GetHttpClient(double timeout = 30)
    {
        var httpClient = this._clientFactory.CreateClient();
        httpClient.Timeout = TimeSpan.FromSeconds(timeout);
        return httpClient;
    }
}

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

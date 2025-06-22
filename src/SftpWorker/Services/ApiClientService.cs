using Microsoft.Extensions.Options;
using SftpWorker.Configuration;
using SftpWorker.Models;
using System.Text;
using System.Text.Json;

namespace SftpWorker.Services
{
    public class ApiClientService : IApiClientService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ApiSettings _apiSettings;

        public ApiClientService(IHttpClientFactory httpClientFactory, IOptions<ApiSettings> apiSettings)
        {
            _httpClientFactory = httpClientFactory;
            _apiSettings = apiSettings.Value;
        }

        public async Task<ApiIngestResponse> PostRecordsAsync(ApiIngestRequest request, CancellationToken cancellationToken)
        {
            var client = _httpClientFactory.CreateClient();
            var url = $"{_apiSettings.BaseUrl}/{_apiSettings.IngestEndpoint}";
            var httpContent = new StringContent(
                JsonSerializer.Serialize(request),
                Encoding.UTF8,
                "application/json"
            );

            try
            {
                var response = await client.PostAsync(url, httpContent, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    // Optionally read and use response body
                    var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    return new ApiIngestResponse(true, responseBody);
                }
                else
                {
                    var errorMsg = await response.Content.ReadAsStringAsync(cancellationToken);
                    return new ApiIngestResponse(false, $"Error: {response.StatusCode} - {errorMsg}");
                }
            }
            catch (Exception ex)
            {
                return new ApiIngestResponse(false, $"Exception: {ex.Message}");
            }

        }
    }
}

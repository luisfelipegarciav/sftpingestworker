using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using SftpWorker.Configuration;
using SftpWorker.Models;
using SftpWorker.Services;

namespace SftpWorker.Tests.Services
{
    public class ApiClientServiceTests
    {
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;

        private readonly ApiSettings _apiSettings;
        private readonly ApiClientService _apiClientService;

        public ApiClientServiceTests()
        {
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _apiSettings = new ApiSettings
            {
                IngestEndpoint = "https://api.example.com/ingest",
                ApiKey = "test-api-key"
            };
            _apiClientService = new ApiClientService(_httpClientFactoryMock.Object, Options.Create(_apiSettings));
        }

        [Fact]
        public async Task PostRecordsAsync_ShouldReturnFailure_WhenApiCallIsFailure()
        {
            // Arrange
            var request = GenerateRequest();

            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Post),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ThrowsAsync(new HttpRequestException("API call failed"));

            var httpClient = new HttpClient(handlerMock.Object);

            _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

            // Act
            var response = await _apiClientService.PostRecordsAsync(request, CancellationToken.None);
            // Assert
            Assert.False(response.IsSuccessful);
            Assert.Contains("API call failed", response.Message);
        }

        [Fact]
        public async Task PostRecordsAsync_ShouldReturnFailure_WhenApiCallReturnsFailure()
        {
            // Arrange
            var request = GenerateRequest();

            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Post),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = System.Net.HttpStatusCode.BadRequest,
                    Content = new StringContent("Bad Request")
                });

            var httpClient = new HttpClient(handlerMock.Object);

            _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

            // Act
            var response = await _apiClientService.PostRecordsAsync(request, CancellationToken.None);
            // Assert
            Assert.False(response.IsSuccessful);
            Assert.Contains($"Error: {System.Net.HttpStatusCode.BadRequest}", response.Message);
        }

        [Fact]
        public async Task PostRecordsAsync_ShouldReturnSuccess_WhenApiCallReturnsSuccess()
        {
            // Arrange
            var request = GenerateRequest();

            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Post),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = System.Net.HttpStatusCode.OK,
                    Content = new StringContent("OK")
                });

            var httpClient = new HttpClient(handlerMock.Object);

            _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

            // Act
            var response = await _apiClientService.PostRecordsAsync(request, CancellationToken.None);
            // Assert
            Assert.True(response.IsSuccessful);
            Assert.Contains("OK", response.Message);
        }

        private static ApiIngestRequest GenerateRequest()
            => new ApiIngestRequest
            (
                [
                    new IncomingCsvRecord("123", "Alice", "Candy", "abc123", "Chocolate", "N")
                ],
                "test.csv"
            );
    }
}

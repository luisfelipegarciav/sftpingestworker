using SftpWorker.Models;

namespace SftpWorker.Services
{
    public interface IApiClientService
    {
        Task<ApiIngestResponse> PostRecordsAsync(ApiIngestRequest request, CancellationToken cancellationToken);
    }
}

using Microsoft.Extensions.Options;
using SftpWorker.Configuration;
using SftpWorker.Models;
using SftpWorker.Services;

namespace SftpWorker
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly ISftpService _sftpService;
        private readonly ICsvParserService _csvService;
        private readonly IApiClientService _apiService;
        private readonly WorkerSettings _workerSettings;

        public Worker(
            ILogger<Worker> logger,
            ISftpService sftpService,
            ICsvParserService csvService,
            IApiClientService apiService,
            IOptions<WorkerSettings> workerOptions
        )
        {
            _logger = logger;
            _sftpService = sftpService;
            _csvService = csvService;
            _apiService = apiService;
            _workerSettings = workerOptions.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Worker started at: {time}", DateTimeOffset.Now);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var files = await _sftpService.ListCsvFilesAsync(stoppingToken);
                    foreach (var file in files)
                    {
                        using var stream = await _sftpService.DownloadFileAsync(file, stoppingToken);
                        var records = _csvService.ParseCsv(stream).ToList();
                        var request = new ApiIngestRequest(records, file);
                        var response = await _apiService.PostRecordsAsync(request, stoppingToken);
                        if (response.IsSuccessful)
                        {
                            await _sftpService.DeleteFileAsync(file, stoppingToken);
                            _logger.LogInformation("Processed and deleted file {file}", file);
                        }
                        else
                        {
                            _logger.LogWarning("Failed to process file {file}", file);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled error in worker loop");
                }

                await Task.Delay(TimeSpan.FromSeconds(_workerSettings.IntervalSeconds), stoppingToken);
            }
        }
    }
}

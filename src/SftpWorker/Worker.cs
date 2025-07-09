using CsvHelper;
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
        private readonly string[] _folders = { WorkflowFolder.ToProcess.ToFolderName(), WorkflowFolder.InProgress.ToFolderName(), WorkflowFolder.Archive.ToFolderName() };

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

            try
            {
                await _sftpService.EnsureWorkflowFoldersExistAsync(_folders, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Failed to ensure workflow folders exist. Shutting down worker.");
                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessFilesIterationAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled error in worker loop iteration");
                }
                await Task.Delay(TimeSpan.FromSeconds(_workerSettings.IntervalSeconds), stoppingToken);
            }
        }

        public virtual async Task ProcessFilesIterationAsync(CancellationToken stoppingToken)
        {
            var correlationId = Guid.NewGuid().ToString();

            using (_logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
            {
                try
                {
                    var files = await _sftpService.GetFilesToProcess(stoppingToken);
                    if (!files.Any())
                    {
                        _logger.LogInformation("No files to process at {time}", DateTimeOffset.Now);
                        await Task.Delay(TimeSpan.FromSeconds(_workerSettings.IntervalSeconds), stoppingToken);
                        return;
                    }
                    var renamedFiles = await _sftpService.MoveFileAsync(WorkflowFolder.ToProcess.ToFolderName(), WorkflowFolder.InProgress.ToFolderName(), files, stoppingToken, correlationId);

                    foreach (var failedOnMove in renamedFiles.FailedFiles)
                    {
                        _logger.LogError("Failed to move file from '{OriginalFileName}' to '{RenamedFileName}' folder", failedOnMove.Key, WorkflowFolder.InProgress.ToFolderName());
                    }

                    foreach (var file in renamedFiles.MovedFiles)
                    {
                        _logger.LogInformation("Moved file from '{OriginalFileName}' to '{RenamedFileName}'", file.Key, file.Value);
                        var currentFileName = file.Value;
                        try
                        {
                            using var stream = await _sftpService.DownloadFileAsync(currentFileName, stoppingToken);
                            var records = _csvService.ParseCsv(stream).ToList();
                            if (!records.Any())
                            {
                                _logger.LogWarning("File {FileName} had no records after parsing", currentFileName);
                                await _sftpService.MoveFileAsync(WorkflowFolder.InProgress.ToFolderName(), WorkflowFolder.Archive.ToFolderName(), [currentFileName], stoppingToken);
                                continue;
                            }
                            var request = new ApiIngestRequest(records, currentFileName);
                            var response = await _apiService.PostRecordsAsync(request, stoppingToken);
                            if (response.IsSuccessful)
                            {
                                _logger.LogInformation("Processed file {FileName} and sent {Count} records", currentFileName, records.Count);
                            }
                            else
                            {
                                _logger.LogWarning("Failed to process file {FileName}. Response: {@Response}", currentFileName, response);
                            }
                            await _sftpService.MoveFileAsync(WorkflowFolder.InProgress.ToFolderName(), WorkflowFolder.Archive.ToFolderName(), [currentFileName], stoppingToken);
                        }
                        catch (FileNotFoundException fnfEx)
                        {
                            _logger.LogWarning(fnfEx, "File {FileName} was not found during download—possible race condition or external removal.", currentFileName);
                        }
                        catch (CsvHelperException csvException)
                        {
                            _logger.LogWarning(csvException, "Unable to parse csv file {FileName}.", currentFileName);
                        }
                        catch (Exception currentFileException)
                        {
                            _logger.LogError(currentFileException, "Failed to process file {FileName} in current iteration", currentFileName);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled error in worker loop");
                }
            }
        }
    }
}

using CsvHelper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SftpWorker.Configuration;
using SftpWorker.Models;
using SftpWorker.Services;

namespace SftpWorker.Tests
{
    public class WorkerTests
    {

        private readonly Mock<ILogger<Worker>> _loggerMock;
        private readonly Mock<ISftpService> _sftpServiceMock;
        private readonly Mock<ICsvParserService> _csvServiceMock;
        private readonly Mock<IApiClientService> _apiServiceMock;
        private readonly WorkerSettings _workerSettings;
        private readonly Worker _worker;

        public WorkerTests()
        {
            _loggerMock = new Mock<ILogger<Worker>>();
            _sftpServiceMock = new Mock<ISftpService>();
            _csvServiceMock = new Mock<ICsvParserService>();
            _apiServiceMock = new Mock<IApiClientService>();
            _workerSettings = new WorkerSettings
            {
                IntervalSeconds = 10,
            };
            _worker = new Worker(
                _loggerMock.Object,
                _sftpServiceMock.Object,
                _csvServiceMock.Object,
                _apiServiceMock.Object,
                Options.Create(_workerSettings)
            );
        }

        [Fact]
        public async Task Worker_Handle_UnExpectedException()
        {
            // Arrange
            var stoppingToken = new CancellationToken();

            _sftpServiceMock
                .Setup(s => s.GetFilesToProcess(stoppingToken))
                .ThrowsAsync(new Exception("Unhandled error in worker loop"));

            // Act
            await _worker.ProcessFilesIterationAsync(stoppingToken);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Unhandled error in worker loop")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once
            );
        }

        [Fact]
        public async Task Worker_Handle_NoFilesToProcess()
        {
            // Arrange
            var stoppingToken = new CancellationToken();

            _sftpServiceMock
                .Setup(s => s.GetFilesToProcess(stoppingToken))
                .ReturnsAsync(Enumerable.Empty<string>());

            // Act
            await _worker.ProcessFilesIterationAsync(stoppingToken);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("No files to process at")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once
            );
        }

        [Fact]
        public async Task Worker_Handle_LogFailedFilesToMove()
        {
            // Arrange
            var stoppingToken = new CancellationToken();

            var files = new List<string> { "file1.txt" };
            var failedFiles = new Dictionary<string, Exception> { { "file1.txt", new Exception("Not valid file") } };

            _sftpServiceMock
                .Setup(s => s.GetFilesToProcess(stoppingToken))
                .ReturnsAsync(files);

            _sftpServiceMock
                .Setup(s => s.MoveFileAsync(It.IsAny<string>(), It.IsAny<string>(), files, stoppingToken, It.IsAny<string>()))
                .ReturnsAsync(new MoveFilesResult
                {
                    FailedFiles = failedFiles
                });

            // Act
            await _worker.ProcessFilesIterationAsync(stoppingToken);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"Failed to move file from '{failedFiles.First().Key}' to")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once
            );
        }

        [Fact]
        public async Task Worker_Handle_FileNotFoundException()
        {
            // Arrange
            var stoppingToken = new CancellationToken();

            var files = new List<string> { "file1.csv" };
            var movedFiles = new Dictionary<string, string> { { "file1.csv", "file1__abc.csv" } };

            _sftpServiceMock
                .Setup(s => s.GetFilesToProcess(stoppingToken))
                .ReturnsAsync(files);

            _sftpServiceMock
                .Setup(s => s.MoveFileAsync(It.IsAny<string>(), It.IsAny<string>(), files, stoppingToken, It.IsAny<string>()))
                .ReturnsAsync(new MoveFilesResult
                {
                    MovedFiles = movedFiles
                });

            _sftpServiceMock
                .Setup(s => s.DownloadFileAsync(It.IsAny<string>(), stoppingToken))
                .ThrowsAsync(new FileNotFoundException("File not found during download"));

            // Act
            await _worker.ProcessFilesIterationAsync(stoppingToken);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"Moved file from '{movedFiles.First().Key}' to")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once
            );

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"File {movedFiles.First().Value} was not found during download—possible race condition or external removal.")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once
            );
        }

        [Fact]
        public async Task Worker_Handle_UnableToParseCSV()
        {
            // Arrange
            var stoppingToken = new CancellationToken();

            var files = new List<string> { "file1.csv" };
            var movedFiles = new Dictionary<string, string> { { "file1.csv", "file1__abc.csv" } };

            _sftpServiceMock
                .Setup(s => s.GetFilesToProcess(stoppingToken))
                .ReturnsAsync(files);

            _sftpServiceMock
                .Setup(s => s.MoveFileAsync(It.IsAny<string>(), It.IsAny<string>(), files, stoppingToken, It.IsAny<string>()))
                .ReturnsAsync(new MoveFilesResult
                {
                    MovedFiles = movedFiles
                });

            using var ms = new MemoryStream();

            _sftpServiceMock
                .Setup(s => s.DownloadFileAsync(It.IsAny<string>(), stoppingToken))
                .ReturnsAsync(ms);

            _csvServiceMock
                .Setup(s => s.ParseCsv(ms))
                .Throws(new HeaderValidationException(null, It.IsAny<InvalidHeader[]>()));

            // Act
            await _worker.ProcessFilesIterationAsync(stoppingToken);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"Moved file from '{movedFiles.First().Key}' to")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once
            );

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"Unable to parse csv file {movedFiles.First().Value}.")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once
            );
        }

        [Fact]
        public async Task Worker_Handle_UnableToProcessEmptyFile()
        {
            // Arrange
            var stoppingToken = new CancellationToken();

            var files = new List<string> { "file1.csv" };
            var movedFiles = new Dictionary<string, string> { { "file1.csv", "file1__abc.csv" } };

            _sftpServiceMock
                .Setup(s => s.GetFilesToProcess(stoppingToken))
                .ReturnsAsync(files);

            _sftpServiceMock
                .Setup(s => s.MoveFileAsync(It.IsAny<string>(), It.IsAny<string>(), files, stoppingToken, It.IsAny<string>()))
                .ReturnsAsync(new MoveFilesResult
                {
                    MovedFiles = movedFiles
                });

            using var ms = new MemoryStream();

            _sftpServiceMock
                .Setup(s => s.DownloadFileAsync(It.IsAny<string>(), stoppingToken))
                .ReturnsAsync(ms);

            _csvServiceMock
                .Setup(s => s.ParseCsv(ms))
                .Returns(Enumerable.Empty<IncomingCsvRecord>());

            // Act
            await _worker.ProcessFilesIterationAsync(stoppingToken);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"Moved file from '{movedFiles.First().Key}' to")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once
            );

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"File {movedFiles.First().Value} had no records after parsing")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once
            );
        }

        [Fact]
        public async Task Worker_Handle_UnableToProcessFile()
        {
            // Arrange
            var stoppingToken = new CancellationToken();

            var files = new List<string> { "file1.csv" };
            var movedFiles = new Dictionary<string, string> { { "file1.csv", "file1__abc.csv" } };

            _sftpServiceMock
                .Setup(s => s.GetFilesToProcess(stoppingToken))
                .ReturnsAsync(files);

            _sftpServiceMock
                .Setup(s => s.MoveFileAsync(It.IsAny<string>(), It.IsAny<string>(), files, stoppingToken, It.IsAny<string>()))
                .ReturnsAsync(new MoveFilesResult
                {
                    MovedFiles = movedFiles
                });

            using var ms = new MemoryStream();

            _sftpServiceMock
                .Setup(s => s.DownloadFileAsync(It.IsAny<string>(), stoppingToken))
                .ReturnsAsync(ms);

            _csvServiceMock
                .Setup(s => s.ParseCsv(ms))
                .Returns(new List<IncomingCsvRecord>
                {
                    new IncomingCsvRecord("123", "Alice", "Candy", "abc123", "Chocolate", "N")
                });

            _apiServiceMock
                .Setup(s => s.PostRecordsAsync(
                    It.IsAny<ApiIngestRequest>(),
                    stoppingToken))
                .ThrowsAsync(new Exception("API call failed"));

            // Act
            await _worker.ProcessFilesIterationAsync(stoppingToken);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"Moved file from '{movedFiles.First().Key}' to")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once
            );

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"Failed to process file {movedFiles.First().Value} in current iteration")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once
            );
        }

        [Fact]
        public async Task Worker_Handle_FailureApiProcess()
        {
            // Arrange
            var stoppingToken = new CancellationToken();

            var files = new List<string> { "file1.csv" };
            var movedFiles = new Dictionary<string, string> { { "file1.csv", "file1__abc.csv" } };

            _sftpServiceMock
                .Setup(s => s.GetFilesToProcess(stoppingToken))
                .ReturnsAsync(files);

            _sftpServiceMock
                .Setup(s => s.MoveFileAsync(It.IsAny<string>(), It.IsAny<string>(), files, stoppingToken, It.IsAny<string>()))
                .ReturnsAsync(new MoveFilesResult
                {
                    MovedFiles = movedFiles
                });

            using var ms = new MemoryStream();

            _sftpServiceMock
                .Setup(s => s.DownloadFileAsync(It.IsAny<string>(), stoppingToken))
                .ReturnsAsync(ms);

            _csvServiceMock
                .Setup(s => s.ParseCsv(ms))
                .Returns(new List<IncomingCsvRecord>
                {
                    new IncomingCsvRecord("123", "Alice", "Candy", "abc123", "Chocolate", "N")
                });

            _apiServiceMock
                .Setup(s => s.PostRecordsAsync(
                    It.IsAny<ApiIngestRequest>(),
                    stoppingToken))
                .ReturnsAsync(new ApiIngestResponse
                (
                    false,
                    "Error: Bad Request"
                ));

            _sftpServiceMock
                .Setup(s => s.MoveFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string[]>(), stoppingToken, null))
                .ReturnsAsync(new MoveFilesResult
                {
                    MovedFiles = movedFiles
                });

            // Act
            await _worker.ProcessFilesIterationAsync(stoppingToken);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"Moved file from '{movedFiles.First().Key}' to")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once
            );

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"Failed to process file {movedFiles.First().Value}. Response:")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once
            );
        }

        [Fact]
        public async Task Worker_Handle_Success()
        {
            // Arrange
            var stoppingToken = new CancellationToken();

            var files = new List<string> { "file1.csv" };
            var movedFiles = new Dictionary<string, string> { { "file1.csv", "file1__abc.csv" } };

            _sftpServiceMock
                .Setup(s => s.GetFilesToProcess(stoppingToken))
                .ReturnsAsync(files);

            _sftpServiceMock
                .Setup(s => s.MoveFileAsync(It.IsAny<string>(), It.IsAny<string>(), files, stoppingToken, It.IsAny<string>()))
                .ReturnsAsync(new MoveFilesResult
                {
                    MovedFiles = movedFiles
                });

            using var ms = new MemoryStream();

            _sftpServiceMock
                .Setup(s => s.DownloadFileAsync(It.IsAny<string>(), stoppingToken))
                .ReturnsAsync(ms);

            _csvServiceMock
                .Setup(s => s.ParseCsv(ms))
                .Returns(new List<IncomingCsvRecord>
                {
                    new IncomingCsvRecord("123", "Alice", "Candy", "abc123", "Chocolate", "N")
                });

            _apiServiceMock
                .Setup(s => s.PostRecordsAsync(
                    It.IsAny<ApiIngestRequest>(),
                    stoppingToken))
                .ReturnsAsync(new ApiIngestResponse
                (
                    true,
                    "OK"
                ));

            _sftpServiceMock
                .Setup(s => s.MoveFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string[]>(), stoppingToken, null))
                .ReturnsAsync(new MoveFilesResult
                {
                    MovedFiles = movedFiles
                });

            // Act
            await _worker.ProcessFilesIterationAsync(stoppingToken);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"Moved file from '{movedFiles.First().Key}' to")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once
            );

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"Processed file {movedFiles.First().Value} and sent")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once
            );
        }
    }
}
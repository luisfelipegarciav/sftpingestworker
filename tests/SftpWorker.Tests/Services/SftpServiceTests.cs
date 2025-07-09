using Microsoft.Extensions.Options;
using Moq;
using Renci.SshNet.Sftp;
using SftpWorker.Configuration;
using SftpWorker.Services;
using ISftpClient = SftpWorker.Services.ISftpClient;

namespace SftpWorker.Tests.Services
{
    public class SftpServiceTests
    {
        public readonly Mock<ISftpClientFactory> _sftpClientFactoryMock;
        public readonly Mock<ISftpClient> _sftpClientMock;
        public readonly SftpSettings _sftpSettings;
        public readonly SftpService _sftpService;

        public SftpServiceTests()
        {
            _sftpSettings = new SftpSettings
            {
                FilePattern = "csv",
                RemoteFolder = "/remote/path",
            };

            _sftpClientFactoryMock = new Mock<ISftpClientFactory>();
            _sftpClientMock = new Mock<ISftpClient>();

            _sftpClientFactoryMock
                .Setup(x => x.Create())
                .Returns(_sftpClientMock.Object);

            _sftpService = new SftpService(_sftpClientFactoryMock.Object, Options.Create(_sftpSettings));
        }

        [Fact]
        public async Task GetFilesToProcess_ReturnsOnlyFilesMatchingPattern()
        {
            // Arrange
            var files = new[]
            {
                CreateSftpFile("file1.csv", false),
                CreateSftpFile("file2.txt", false),
                CreateSftpFile("file3.csv", false),
                CreateSftpFile("directory1", true)
            };

            _sftpClientMock
                .Setup(x => x.ConnectAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _sftpClientMock
                .Setup(x => x.ListDirectoryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(GetAsyncEnumerable(files));

            _sftpClientMock.Setup(s => s.IsConnected).Returns(true);
            _sftpClientMock.Setup(s => s.Disconnect());

            // Act
            var result = (await _sftpService.GetFilesToProcess(CancellationToken.None)).ToList();
            // Assert
            Assert.Contains("file1.csv", result);
            Assert.DoesNotContain("file2.txt", result);
            Assert.Contains("file3.csv", result);
            Assert.DoesNotContain("direcroty1", result);
            Assert.Equal(2, result.Count());
        }

        [Fact]
        public async Task GetFilesToProcess_ReturnsEmptyWhenNoFilesFound()
        {
            // Arrange
            var files = new[]
            {
                CreateSftpFile("file2.txt", false),
                CreateSftpFile("directory1", true)
            };

            _sftpClientMock
                .Setup(x => x.ConnectAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _sftpClientMock
                .Setup(x => x.ListDirectoryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(GetAsyncEnumerable(files));

            _sftpClientMock.Setup(s => s.IsConnected).Returns(true);
            _sftpClientMock.Setup(s => s.Disconnect());

            // Act
            var result = (await _sftpService.GetFilesToProcess(CancellationToken.None)).ToList();
            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetFilesToProcess_ShouldThrow_WhenSftpConnectionFails()
        {
            // Arrange
            _sftpClientMock
                .Setup(x => x.ConnectAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Connection failed"));

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => _sftpService.GetFilesToProcess(CancellationToken.None));
        }

        [Fact]
        public async Task DownloadFileAsync_ShouldThrow_WhenFilenameIsEmpty()
        {
            // Arrange
            string fileName = string.Empty;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _sftpService.DownloadFileAsync(fileName, CancellationToken.None));
        }

        [Fact]
        public async Task DownloadFileAsync_ShouldThrow_WhenFileNotFound()
        {
            // Arrange
            string fileName = "test.csv";

            _sftpClientMock
                .Setup(x => x.ConnectAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _sftpClientMock
                .Setup(x => x.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Act & Assert
            await Assert.ThrowsAsync<FileNotFoundException>(() => _sftpService.DownloadFileAsync(fileName, CancellationToken.None));
        }

        [Fact]
        public async Task DownloadFileAsync_ShouldReturnStream()
        {
            // Arrange
            string fileName = "test.csv";

            _sftpClientMock
                .Setup(x => x.ConnectAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _sftpClientMock
                .Setup(x => x.IsConnected)
                .Returns(true);

            _sftpClientMock
                .Setup(x => x.Disconnect());

            _sftpClientMock
                .Setup(x => x.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            using var memoryStream = new MemoryStream();

            _sftpClientMock
                .Setup(x => x.DownloadAsync(It.IsAny<string>(), memoryStream))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _sftpService.DownloadFileAsync(fileName, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public async Task DownloadFileAsync_ShouldCleanUpConnection_EvenOnException()
        {
            // Arrange
            string fileName = "test.csv";

            _sftpClientMock
                .Setup(x => x.ConnectAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _sftpClientMock
                .Setup(x => x.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            _sftpClientMock
                .Setup(x => x.DownloadAsync(It.IsAny<string>(), It.IsAny<MemoryStream>()))
                .ThrowsAsync(new IOException("Download failed"));

            // Act & Assert
            await Assert.ThrowsAsync<IOException>(() => _sftpService.DownloadFileAsync(fileName, CancellationToken.None));

            // Verify Clean up
            _sftpClientMock.Verify(s => s.Dispose(), Times.Once);
        }

        [Fact]
        public async Task EnsureWorkflowFoldersExistAsync_ShouldCreateMissingFoldersOnly()
        {
            // Arrange
            var workflowFolders = new Dictionary<string, bool>()
            {
                { "folder1", true },
                { "folder2", false },
                { "folder3", false },
            };

            _sftpClientMock
                .Setup(x => x.ConnectAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _sftpClientMock
                .Setup(x => x.IsConnected)
                .Returns(true);

            _sftpClientMock
                .Setup(x => x.Disconnect());

            foreach (var item in workflowFolders)
            {
                _sftpClientMock
                   .Setup(x => x.ExistsAsync($"{_sftpSettings.RemoteFolder}/{item.Key}", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(item.Value);
            }

            // Act
            await _sftpService.EnsureWorkflowFoldersExistAsync(workflowFolders.Keys, CancellationToken.None);

            // Assert
            foreach (var folder in workflowFolders)
            {
                var remoteFolder = $"{_sftpSettings.RemoteFolder}/{folder.Key}";
                if (folder.Value)
                {
                    _sftpClientMock.Verify(s => s.CreateDirectoryAsync(remoteFolder, It.IsAny<CancellationToken>()), Times.Never);
                }
                else
                {
                    _sftpClientMock.Verify(s => s.CreateDirectoryAsync(remoteFolder, It.IsAny<CancellationToken>()), Times.Once);
                }
            }
        }

        [Fact]
        public async Task EnsureWorkflowFoldersExistAsync_ShouldNotCreateExistingFolders()
        {
            // Arrange
            var workflowFolders = new Dictionary<string, bool>()
            {
                { "folder1", true },
                { "folder2", true },
            };

            _sftpClientMock
                .Setup(x => x.ConnectAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _sftpClientMock
                .Setup(x => x.IsConnected)
                .Returns(true);

            _sftpClientMock
                .Setup(x => x.Disconnect());

            foreach (var item in workflowFolders)
            {
                _sftpClientMock
                   .Setup(x => x.ExistsAsync($"{_sftpSettings.RemoteFolder}/{item.Key}", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(item.Value);
            }

            // Act
            await _sftpService.EnsureWorkflowFoldersExistAsync(workflowFolders.Keys, CancellationToken.None);

            // Assert
            _sftpClientMock.Verify(s => s.CreateDirectoryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task EnsureWorkflowFoldersExistAsync_ShouldRespectCancellationToken()
        {
            // Arrange
            var folders = new List<string> { "folder1", "folder2" };
            var cts = new CancellationTokenSource();

            _sftpClientMock
                .Setup(x => x.ConnectAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _sftpClientMock
                .Setup(x => x.IsConnected)
                .Returns(true);

            _sftpClientMock
                .Setup(x => x.Disconnect());

            _sftpClientMock
                .Setup(x => x.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => {
                    cts.Cancel();
                    return false;
                });

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                _sftpService.EnsureWorkflowFoldersExistAsync(folders, cts.Token));
        }

        [Fact]
        public async Task MoveFileAsync_ShouldThrow_MissingSource()
        {
            // Arrange
            var sourceFolder = string.Empty;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _sftpService.MoveFileAsync(sourceFolder, "destination", new List<string> { "file1.csv" }, CancellationToken.None));
        }

        [Fact]
        public async Task MoveFileAsync_ShouldThrow_MissingDestination()
        {
            // Arrange
            var destFolder = string.Empty;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _sftpService.MoveFileAsync("sourceFolder", destFolder, new List<string> { "file1.csv" }, CancellationToken.None));
        }

        [Fact]
        public async Task MoveFileAsync_ShouldThrow_MissingFiles()
        {
            // Arrange
            var files = new List<string>();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _sftpService.MoveFileAsync("sourceFolder", "destFolder", files, CancellationToken.None));
        }

        [Fact]
        public async Task MoveFileAsync_ShouldMoveFilesNoCollisions()
        {
            // Arrange
            var files = new List<string>
            {
                "file.csv",
                "file2.csv",
            };

            var correlationId = Guid.NewGuid().ToString();

            _sftpClientMock
                .Setup(x => x.ConnectAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _sftpClientMock
                .Setup(x => x.IsConnected)
                .Returns(true);

            _sftpClientMock
                .Setup(x => x.Disconnect());

            _sftpClientMock
                .Setup(x => x.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            _sftpClientMock
                .Setup(x => x.RenameFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()));

            // Act
            var result = await _sftpService.MoveFileAsync("sourceFolder", "destinationFolder", files, CancellationToken.None, correlationId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.MovedFiles.Count);
            Assert.Collection(
                result.MovedFiles,
                item => Assert.Equal("file.csv", item.Key),
                item => Assert.Equal("file2.csv", item.Key)
            );

            _sftpClientMock
                .Verify(x => x.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), times: Times.Exactly(2));

            _sftpClientMock
                .Verify(x => x.RenameFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), times: Times.Exactly(2));
        }

        [Fact]
        public async Task MoveFileAsync_ShouldReportAsFailedIfDestinationExists()
        {
            // Arrange
            var files = new List<string>
            {
                "file.csv",
            };

            var correlationId = Guid.NewGuid().ToString();

            _sftpClientMock
                .Setup(x => x.ConnectAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _sftpClientMock
                .Setup(x => x.IsConnected)
                .Returns(true);

            _sftpClientMock
                .Setup(x => x.Disconnect());

            _sftpClientMock
                .Setup(x => x.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _sftpService.MoveFileAsync("sourceFolder", "destinationFolder", files, CancellationToken.None, correlationId);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result.MovedFiles);
            Assert.Single(result.FailedFiles);
            Assert.Contains($"Destination file already exists:", result.FailedFiles.First().Value.Message);

            _sftpClientMock
                .Verify(x => x.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), times: Times.Exactly(1));

            _sftpClientMock
                .Verify(x => x.RenameFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), times: Times.Never);
        }

        [Fact]
        public async Task MoveFileAsync_ShouldReportAsFailedIfSourceDoesNotExist()
        {
            // Arrange
            var files = new List<string>
            {
                "file.csv",
            };

            var correlationId = Guid.NewGuid().ToString();

            _sftpClientMock
                .Setup(x => x.ConnectAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _sftpClientMock
                .Setup(x => x.IsConnected)
                .Returns(true);

            _sftpClientMock
                .Setup(x => x.Disconnect());

            _sftpClientMock
                .Setup(x => x.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            _sftpClientMock
                .Setup(x => x.RenameFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new IOException("Source not found"));

            // Act
            var result = await _sftpService.MoveFileAsync("sourceFolder", "destinationFolder", files, CancellationToken.None, correlationId);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result.MovedFiles);
            Assert.Single(result.FailedFiles);
            Assert.Contains($"Source not found", result.FailedFiles.First().Value.Message);

            _sftpClientMock
                .Verify(x => x.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), times: Times.Exactly(1));

            _sftpClientMock
                .Verify(x => x.RenameFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), times: Times.Once);
        }

        private static ISftpFile CreateSftpFile(string name, bool isDirectory)
        {
            var mock = new Mock<ISftpFile>();
            mock.Setup(f => f.Name).Returns(name);
            mock.Setup(f => f.IsDirectory).Returns(isDirectory);
            return mock.Object;
        }

        private static async IAsyncEnumerable<ISftpFile> GetAsyncEnumerable(IEnumerable<ISftpFile> files)
        {
            foreach (var file in files)
            {
                yield return file;
                await Task.Yield();
            }
        }
    }
}

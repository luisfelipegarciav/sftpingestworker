using Microsoft.Extensions.Options;
using SftpWorker.Configuration;
using SftpWorker.Models;

namespace SftpWorker.Services
{
    public class SftpService : ISftpService
    {
        private readonly ISftpClientFactory _sftpClientFactory;
        private readonly SftpSettings _sftpSettings;

        public SftpService(ISftpClientFactory sftpClientFactory, IOptions<SftpSettings> sftpSettings)
        {
            _sftpClientFactory = sftpClientFactory;
            _sftpSettings = sftpSettings.Value;
        }

        public async Task<IEnumerable<string>> GetFilesToProcess(CancellationToken cancellationToken)
        {
            using var sftp = _sftpClientFactory.Create();
            try
            {
                await sftp.ConnectAsync(cancellationToken);
                var remoteFolder = $"{_sftpSettings.RemoteFolder}/{WorkflowFolder.ToProcess.ToFolderName()}";
                var result = new List<string>();
                await foreach (var file in sftp.ListDirectoryAsync(remoteFolder, cancellationToken))
                {
                    if (!file.IsDirectory && file.Name.EndsWith(_sftpSettings.FilePattern))
                        result.Add(file.Name);
                }
                return result;
            }
            finally
            {
                if (sftp.IsConnected)
                {
                    sftp.Disconnect();
                }
            }
        }

        public async Task<Stream> DownloadFileAsync(string fileName, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentNullException(nameof(fileName));

            using var sftp = _sftpClientFactory.Create();
            try
            {
                await sftp.ConnectAsync(cancellationToken);
                var remoteFolder = $"{_sftpSettings.RemoteFolder}/{WorkflowFolder.InProgress.ToFolderName()}";
                var remoteFileFullPath = $"{remoteFolder}/{fileName}";
                var fileExists = await sftp.ExistsAsync(remoteFileFullPath, cancellationToken);
                if (!fileExists)
                {
                    throw new FileNotFoundException($"File '{fileName}' not found in remote folder '{_sftpSettings.RemoteFolder}'.");
                }
                var ms = new MemoryStream();
                await sftp.DownloadAsync(remoteFileFullPath, ms);
                ms.Position = 0;
                return ms;
            }
            finally
            {
                if (sftp.IsConnected)
                {
                    sftp.Disconnect();
                }
            }
        }

        public async Task EnsureWorkflowFoldersExistAsync(IEnumerable<string> workflowFolders, CancellationToken cancellationToken)
        {
            using var sftp = _sftpClientFactory.Create();
            try
            {
                await sftp.ConnectAsync(cancellationToken);
                foreach (var folder in workflowFolders)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var remoteFolder = folder.StartsWith("/") ? folder : $"{_sftpSettings.RemoteFolder}/{folder}";
                    if (!await sftp.ExistsAsync(remoteFolder, cancellationToken))
                    {
                        await sftp.CreateDirectoryAsync(remoteFolder, cancellationToken);
                    }
                }
            }
            finally
            {
                if (sftp.IsConnected)
                {
                    sftp.Disconnect();
                }
            }
        }

        public async Task<MoveFilesResult> MoveFileAsync(string sourceFolder, string destinationFolder, IEnumerable<string> fileNames, CancellationToken cancellationToken, string? suffix = null)
        {
            if (string.IsNullOrWhiteSpace(sourceFolder))
                throw new ArgumentNullException(nameof(sourceFolder));

            if (string.IsNullOrWhiteSpace(destinationFolder))
                throw new ArgumentNullException(nameof(destinationFolder));

            if (fileNames == null || !fileNames.Any())
                throw new ArgumentNullException(nameof(fileNames));

            using var sftp = _sftpClientFactory.Create();
            try
            {
                await sftp.ConnectAsync(cancellationToken);
                var result = new MoveFilesResult();
                foreach (var file in fileNames)
                {
                    try
                    {
                        var fileName = string.IsNullOrWhiteSpace(suffix) ? file : $"{Path.GetFileNameWithoutExtension(file)}__{suffix}{Path.GetExtension(file)}";
                        var sourcePath = $"{_sftpSettings.RemoteFolder}/{sourceFolder}/{file}";
                        var destPath = $"{_sftpSettings.RemoteFolder}/{destinationFolder}/{fileName}";

                        if (await sftp.ExistsAsync(destPath, cancellationToken))
                        {
                            result.FailedFiles.Add(file, new IOException($"Destination file already exists: {destPath}"));
                            continue;
                        }

                        await sftp.RenameFileAsync(
                            $"{_sftpSettings.RemoteFolder}/{sourceFolder}/{file}",
                            $"{_sftpSettings.RemoteFolder}/{destinationFolder}/{fileName}",
                            cancellationToken
                        );
                        result.MovedFiles.Add(file, fileName);
                    }
                    catch (Exception ex)
                    {
                        result.FailedFiles.Add(file, ex);
                    }
                }
                return result;
            }
            finally
            {
                if (sftp.IsConnected)
                {
                    sftp.Disconnect();
                }
            }
        }
    }
}

using Renci.SshNet;
using Renci.SshNet.Async;
using Renci.SshNet.Sftp;

namespace SftpWorker.Services
{
    public class SshNetSftpClientWrapper : ISftpClient
    {
        private readonly SftpClient _client;

        public SshNetSftpClientWrapper(SftpClient client)
        {
            _client = client;
        }

        public Task ConnectAsync(CancellationToken cancellationToken) => _client.ConnectAsync(cancellationToken);

        public IAsyncEnumerable<ISftpFile> ListDirectoryAsync(string path, CancellationToken cancellationToken)
            => _client.ListDirectoryAsync(path, cancellationToken);

        public bool IsConnected => _client.IsConnected;

        public void Disconnect() => _client.Disconnect();

        public void Dispose() => _client.Dispose();

        public Task<bool> ExistsAsync(string path, CancellationToken cancellationToken)
            => _client.ExistsAsync(path, cancellationToken);

        public Task DownloadAsync(string path, Stream output)
            => _client.DownloadAsync(path, output);

        public Task CreateDirectoryAsync(string path, CancellationToken cancellationToken)
            => _client.CreateDirectoryAsync(path, cancellationToken);

        public Task RenameFileAsync(string sourcePath, string destPath, CancellationToken cancellationToken)
            => _client.RenameFileAsync(sourcePath, destPath, cancellationToken);
    }
}

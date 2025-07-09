using Renci.SshNet.Sftp;

namespace SftpWorker.Services
{
    public interface ISftpClient : IDisposable
    {
        Task ConnectAsync(CancellationToken cancellationToken);
        IAsyncEnumerable<ISftpFile> ListDirectoryAsync(string path, CancellationToken cancellationToken);
        bool IsConnected { get; }
        void Disconnect();
        Task<bool> ExistsAsync(string path, CancellationToken cancellationToken);
        Task DownloadAsync(string path, Stream output);
        Task CreateDirectoryAsync(string path, CancellationToken cancellationToken);
        Task RenameFileAsync(string sourcePath, string destPath, CancellationToken cancellationToken);
    }
}

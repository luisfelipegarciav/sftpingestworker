namespace SftpWorker.Services
{
    public interface ISftpService
    {
        Task<IEnumerable<string>> ListCsvFilesAsync(CancellationToken cancellationToken);
        Task<Stream> DownloadFileAsync(string fileName, CancellationToken cancellationToken);
        Task DeleteFileAsync(string fileName, CancellationToken cancellationToken);
        Task MoveFileAsync(string fileName, string destination, CancellationToken cancellationToken);
    }
}

using SftpWorker.Models;

namespace SftpWorker.Services
{
    public interface ISftpService
    {
        Task<IEnumerable<string>> GetFilesToProcess(CancellationToken cancellationToken);
        Task<Stream> DownloadFileAsync(string fileName, CancellationToken cancellationToken);
        Task EnsureWorkflowFoldersExistAsync(IEnumerable<string> workflowFolders, CancellationToken cancellationToken);
        Task<MoveFilesResult> MoveFileAsync(string sourceFolder, string destinationFolder, IEnumerable<string> fileNames, CancellationToken cancellationToken, string? preffix = null);
    }
}

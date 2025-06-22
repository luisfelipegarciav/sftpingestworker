using Microsoft.Extensions.Options;
using Renci.SshNet;
using Renci.SshNet.Sftp;
using SftpWorker.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime;
using System.Text;
using System.Threading.Tasks;

namespace SftpWorker.Services
{
    public class SftpService : ISftpService
    {
        //private readonly ILogger<SftpService> _logger;
        private readonly SftpSettings _sftpSettings;

        public SftpService(IOptions<SftpSettings> sftpSettings)
        {
            _sftpSettings = sftpSettings.Value;
        }

        public async Task<IEnumerable<string>> ListCsvFilesAsync(CancellationToken cancellationToken)
        {
            using var sftp = new SftpClient(
                _sftpSettings.Host,
                _sftpSettings.Port,
                _sftpSettings.Username,
                _sftpSettings.Password
            );
            sftp.Connect();
            var result = new List<string>();
            await foreach (var file in sftp.ListDirectoryAsync(_sftpSettings.RemoteFolder, cancellationToken))
            {
                if (!file.IsDirectory && file.Name.EndsWith(_sftpSettings.FilePattern))
                    result.Add(file.Name);
            }
            sftp.Disconnect();
            return result;
        }

        public Task<Stream> DownloadFileAsync(string fileName, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task DeleteFileAsync(string fileName, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task MoveFileAsync(string fileName, string destination, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}

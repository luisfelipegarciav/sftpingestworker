using Renci.SshNet;
using SftpWorker.Configuration;

namespace SftpWorker.Services
{
    public class SftpClientFactory : ISftpClientFactory
    {
        private readonly SftpSettings _settings;

        public SftpClientFactory(SftpSettings settings)
        {
            _settings = settings;
        }

        public ISftpClient Create()
        {
            var sftpClient = new SftpClient(
                _settings.Host,
                _settings.Port,
                _settings.Username,
                _settings.Password
            );
            return new SshNetSftpClientWrapper(sftpClient);
        }
    }
}

namespace SftpWorker.Configuration
{
    public class SftpSettings
    {
        public string Host { get; set; } = "";
        public int Port { get; set; } = 22;
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string RemoteFolder { get; set; } = "";
        public string FilePattern { get; set; } = ".csv";
    }
}

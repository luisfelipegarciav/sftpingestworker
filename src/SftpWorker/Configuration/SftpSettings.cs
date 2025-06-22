namespace SftpWorker.Configuration
{
    public record SftpSettings
    (
        string Host,
        int Port = 22,
        string Username = "",
        string Password = "",
        string RemoteFolder = "",
        string FilePattern = ".csv"
    );
}

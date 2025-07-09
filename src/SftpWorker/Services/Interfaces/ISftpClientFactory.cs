namespace SftpWorker.Services
{
    public interface ISftpClientFactory
    {
        ISftpClient Create();
    }
}

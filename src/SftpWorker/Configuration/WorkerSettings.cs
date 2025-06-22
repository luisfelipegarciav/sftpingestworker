namespace SftpWorker.Configuration
{
    public record WorkerSettings
    (
        //string WorkerName = "SFTP Worker",
        //int MaxConcurrentDownloads = 5,
        //int MaxRetries = 3,
        //TimeSpan RetryDelay = default,
        int IntervalSeconds = 300
    );
}

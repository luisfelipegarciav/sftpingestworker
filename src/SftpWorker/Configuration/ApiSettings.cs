namespace SftpWorker.Configuration
{
    public record ApiSettings
    (
        string BaseUrl,
        string IngestEndpoint = "/api/ingest",
        string? ApiKey = null
    );
}

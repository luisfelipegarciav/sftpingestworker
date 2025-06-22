namespace SftpWorker.Models
{
    public record ApiIngestRequest
    (
        List<IncomingCsvRecord> Records,
        string FileName
    );

    public record ApiIngestResponse
    (
        bool IsSuccessful,
        string Message = ""
    );
}

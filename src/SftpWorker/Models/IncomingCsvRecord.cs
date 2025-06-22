namespace SftpWorker.Models
{
    public record IncomingCsvRecord
    (
        string CustomerId,
        string CustomerName,
        string Category,
        string Sku,
        string Description,
        string Deleted = "N"
    );
}

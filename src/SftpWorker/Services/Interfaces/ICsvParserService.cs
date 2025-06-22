using SftpWorker.Models;

namespace SftpWorker.Services
{
    public interface ICsvParserService
    {
        IEnumerable<IncomingCsvRecord> ParseCsv(Stream csvStream);
    }
}

using SftpWorker.Models;
using System.Globalization;

namespace SftpWorker.Services
{
    public class CsvParserService : ICsvParserService
    {
        public IEnumerable<IncomingCsvRecord> ParseCsv(Stream csvStream)
        {
            using var reader = new StreamReader(csvStream);
            using var csv = new CsvHelper.CsvReader(reader, CultureInfo.InvariantCulture);
            return csv.GetRecords<IncomingCsvRecord>().ToList();
        }
    }
}

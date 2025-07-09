using SftpWorker.Models;
using SftpWorker.Services;
using System.Reflection.Metadata;
using System.Text;

namespace SftpWorker.Tests.Services
{
    public class CsvParserServiceTests
    {
        private readonly CsvParserService _csvParserService;
        public CsvParserServiceTests()
        {
            _csvParserService = new CsvParserService();
        }

        [Fact]
        public void Should_Parse_Valid_Csv_To_Records()
        {
            // Arrange  
            var customerId = "123";
            var customerName = "Alice";
            var category = "Candy";
            var sku = "abc123";
            var description = "Chocolate";
            var deleted = "N";
            var sb = new StringBuilder();
            sb.AppendLine("CustomerId,CustomerName,Category,Sku,Description,Deleted");
            sb.AppendLine($"{customerId},{customerName},{category},{sku},{description},{deleted}");
            var csv = sb.ToString();
            var bytes = Encoding.UTF8.GetBytes(csv);
            using var stream = new MemoryStream(bytes);

            // Act  
            var records = _csvParserService.ParseCsv(stream);

            // Assert  
            Assert.NotEmpty(records);
            Assert.Equivalent(records, new List<IncomingCsvRecord>
               {
                   new(customerId, customerName, category, sku, description, deleted)
               });
        }

        [Fact]
        public void Should_return_empty_or_throw_on_malformed_CSV()
        {
            // Arrange
            var customerName = "Alice";
            var category = "Candy";
            var sku = "abc123";
            var description = "Chocolate";
            var deleted = "N";
            var sb = new StringBuilder();
            sb.AppendLine("CustomerName,Category,Sku,Description,Deleted");
            sb.AppendLine($"{customerName},{category},{sku},{description},{deleted}");
            var csv = sb.ToString();
            var bytes = Encoding.UTF8.GetBytes(csv);
            using var stream = new MemoryStream(bytes);

            // Act & Assert
            Assert.Throws<CsvHelper.HeaderValidationException>(() => _csvParserService.ParseCsv(stream));
        }

        [Fact]
        public void Should_handle_empty_files_gracefully()
        {
            // Arrange
            var sb = new StringBuilder();
            sb.AppendLine("CustomerId,CustomerName,Category,Sku,Description,Deleted");
            var csv = sb.ToString();
            var bytes = Encoding.UTF8.GetBytes(csv);
            using var stream = new MemoryStream(bytes);

            // Act  
            var records = _csvParserService.ParseCsv(stream);

            // Assert  
            Assert.Empty(records);
        }
    }
}

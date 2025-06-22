using SftpWorker.Configuration;
using SftpWorker.Services;

namespace SftpWorker
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, config) =>
                {
                    // Load configuration from appsettings.json and environment variables
                    config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                          .AddEnvironmentVariables();
                })
                .ConfigureServices((context, services) =>
                {
                    services.Configure<SftpSettings>(context.Configuration.GetSection("SftpSettings"));
                    services.Configure<ApiSettings>(context.Configuration.GetSection("ApiSettings"));
                    services.Configure<WorkerSettings>(context.Configuration.GetSection("WorkerSettings"));

                    services.AddHttpClient();
                    services.AddSingleton<ISftpService, SftpService>();
                    services.AddSingleton<ICsvParserService, CsvParserService>();
                    services.AddSingleton<IApiClientService, ApiClientService>();
                    services.AddHostedService<Worker>();
                })
                .Build();

            await host.RunAsync();
        }
    }
}
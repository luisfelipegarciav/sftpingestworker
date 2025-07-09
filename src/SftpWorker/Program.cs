using Microsoft.Extensions.Options;
using Serilog;
using SftpWorker.Configuration;
using SftpWorker.Services;

namespace SftpWorker
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Console() // Console output, also JSON possible
                .WriteTo.File(
                    path: "logs/log-.json",
                    rollingInterval: RollingInterval.Day,
                    formatter: new Serilog.Formatting.Json.JsonFormatter())
                .CreateLogger();

            try
            {
                Log.Information("Starting up");
                //CreateHostBuilder(args).Build().Run();

                var host = Host.CreateDefaultBuilder(args)
                    .UseSerilog()
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
                        services.AddSingleton<SftpSettings>(sp => sp.GetRequiredService<IOptions<SftpSettings>>().Value);
                        services.AddSingleton<ISftpClientFactory, SftpClientFactory>();
                        services.AddTransient<ISftpService, SftpService>();
                        services.AddSingleton<ICsvParserService, CsvParserService>();
                        services.AddSingleton<IApiClientService, ApiClientService>();
                        services.AddHostedService<Worker>();
                    })
                    .Build();

                await host.RunAsync();
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}
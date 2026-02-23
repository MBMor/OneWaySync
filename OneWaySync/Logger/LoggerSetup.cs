using Microsoft.Extensions.Logging;
using Serilog;

namespace OneWaySync.Logger
{
    public static class LoggerSetup
    {
        public static ILoggerFactory CreateLoggerFactory(string logFilePath)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console(outputTemplate:
                    "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(logFilePath!, rollingInterval: RollingInterval.Day)
                .CreateLogger();

            return LoggerFactory.Create(builder =>
            {              
                builder.ClearProviders();  
                builder.AddSerilog();
            });
        }
     }
}

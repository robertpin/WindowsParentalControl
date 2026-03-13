using Serilog;

namespace ParentalControl.Core.Logging;

public static class LoggingConfig
{
    private const string LogDirectory = @"C:\ProgramData\ParentalControl\logs";

    public static ILogger CreateLogger(string componentName)
    {
        Directory.CreateDirectory(LogDirectory);

        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                path: Path.Combine(LogDirectory, $"{componentName}-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }
}

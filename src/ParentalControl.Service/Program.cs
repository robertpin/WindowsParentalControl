using Microsoft.Extensions.Hosting.WindowsServices;
using ParentalControl.Core.Data;
using ParentalControl.Core.Logging;
using ParentalControl.Service;

DatabaseManager.Initialize();
var logger = LoggingConfig.CreateLogger("Service");
logger.Information("Parental Control Service starting");

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton(logger);
builder.Services.AddSingleton<SessionTracker>();
builder.Services.AddHostedService<UsageMonitorWorker>();
builder.Services.AddSingleton<IHostLifetime>(sp =>
{
    var env = sp.GetRequiredService<IHostEnvironment>();
    var appLifetime = sp.GetRequiredService<IHostApplicationLifetime>();
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    var hostOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<HostOptions>>();
    var serviceOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<WindowsServiceLifetimeOptions>>();
    var sessionTracker = sp.GetRequiredService<SessionTracker>();
    var serilogLogger = sp.GetRequiredService<Serilog.ILogger>();

    return new ParentalControlServiceLifetime(
        env, appLifetime, loggerFactory, hostOptions, serviceOptions, sessionTracker, serilogLogger);
});

var host = builder.Build();

var sessionTracker = host.Services.GetRequiredService<SessionTracker>();
sessionTracker.Initialize();

host.Run();

using System.ServiceProcess;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Options;

namespace ParentalControl.Service;

public sealed class ParentalControlServiceLifetime : WindowsServiceLifetime
{
    private readonly SessionTracker _sessionTracker;
    private readonly Serilog.ILogger _logger;

    public ParentalControlServiceLifetime(
        IHostEnvironment environment,
        IHostApplicationLifetime applicationLifetime,
        ILoggerFactory loggerFactory,
        IOptions<HostOptions> optionsAccessor,
        IOptions<WindowsServiceLifetimeOptions> windowsServiceOptions,
        SessionTracker sessionTracker,
        Serilog.ILogger logger)
        : base(environment, applicationLifetime, loggerFactory, optionsAccessor, windowsServiceOptions)
    {
        CanHandleSessionChangeEvent = true;
        CanHandlePowerEvent = true;
        _sessionTracker = sessionTracker;
        _logger = logger;
    }

    protected override void OnSessionChange(SessionChangeDescription changeDescription)
    {
        base.OnSessionChange(changeDescription);

        try
        {
            switch (changeDescription.Reason)
            {
                case SessionChangeReason.SessionLogon:
                    _sessionTracker.OnUserLogon(changeDescription.SessionId);
                    break;
                case SessionChangeReason.SessionLogoff:
                    _sessionTracker.OnUserLogoff(changeDescription.SessionId);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error handling session change: {Reason}, SessionId: {SessionId}",
                changeDescription.Reason, changeDescription.SessionId);
        }
    }

    protected override bool OnPowerEvent(PowerBroadcastStatus powerStatus)
    {
        try
        {
            switch (powerStatus)
            {
                case PowerBroadcastStatus.Suspend:
                    _sessionTracker.OnSleep();
                    break;
                case PowerBroadcastStatus.ResumeSuspend:
                case PowerBroadcastStatus.ResumeAutomatic:
                    _sessionTracker.OnWake();
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error handling power event: {PowerStatus}", powerStatus);
        }

        return base.OnPowerEvent(powerStatus);
    }
}

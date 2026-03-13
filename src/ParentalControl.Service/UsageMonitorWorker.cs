using ParentalControl.Core.Data;
using ParentalControl.Core.Models;
using ParentalControl.Core.Platform;
namespace ParentalControl.Service;

public sealed class UsageMonitorWorker : BackgroundService
{
    private readonly SessionTracker _sessionTracker;
    private readonly Serilog.ILogger _logger;
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(60);
    private DateOnly _lastCleanupDate = DateOnly.MinValue;

    public UsageMonitorWorker(SessionTracker sessionTracker, Serilog.ILogger logger)
    {
        _sessionTracker = sessionTracker;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.Information("Usage monitor worker started");

        try
        {
            ProcessTick();
            RunCleanupIfNeeded();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error during initial usage monitor tick");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TickInterval, stoppingToken);

            if (!_sessionTracker.IsAwake)
                continue;

            try
            {
                ProcessTick();
                RunCleanupIfNeeded();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error during usage monitor tick");
            }
        }
    }

    private void ProcessTick()
    {
        _sessionTracker.TickAllSessions();

        var today = DateOnly.FromDateTime(DateTime.Now);
        var now = TimeOnly.FromDateTime(DateTime.Now);

        foreach (var (sessionId, session) in _sessionTracker.ActiveSessions)
        {
            var user = UserRepository.GetBySid(session.UserSid);
            if (user is null || !user.IsRestricted) continue;

            var limit = LimitRepository.GetByUserId(user.Id);
            if (limit is null) continue;

            var usage = UsageRepository.GetUsage(user.Id, today);
            if (usage is not null && usage.MinutesUsed >= limit.DailyMinutes)
            {
                _logger.Information("Daily limit reached for {Username} ({MinutesUsed}/{DailyMinutes} min)",
                    session.Username, usage.MinutesUsed, limit.DailyMinutes);
                EventRepository.LogEvent(session.UserSid, EventType.LIMIT_REACHED,
                    $"Used {usage.MinutesUsed} of {limit.DailyMinutes} minutes");
                SessionManager.ForceLogoff(sessionId);
                EventRepository.LogEvent(session.UserSid, EventType.FORCED_LOGOUT, "Daily limit reached");
                _sessionTracker.RemoveSession(sessionId);
                continue;
            }

            if (now < limit.ScheduleStart || now >= limit.ScheduleEnd)
            {
                _logger.Information("Outside allowed schedule for {Username} (allowed {Start}-{End})",
                    session.Username, limit.ScheduleStart, limit.ScheduleEnd);
                SessionManager.ForceLogoff(sessionId);
                EventRepository.LogEvent(session.UserSid, EventType.FORCED_LOGOUT, "Outside allowed schedule");
                _sessionTracker.RemoveSession(sessionId);
            }
        }
    }

    private void RunCleanupIfNeeded()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        if (today <= _lastCleanupDate) return;

        _lastCleanupDate = today;

        var eventsCutoff = DateTime.Now.AddDays(-DatabaseManager.RetentionDays);
        var usageCutoff = today.AddDays(-DatabaseManager.RetentionDays);

        var eventsDeleted = EventRepository.DeleteOlderThan(eventsCutoff);
        var usageDeleted = UsageRepository.DeleteOlderThan(usageCutoff);

        if (eventsDeleted > 0 || usageDeleted > 0)
        {
            _logger.Information("Database cleanup: deleted {EventsDeleted} events and {UsageDeleted} usage records older than {Days} days",
                eventsDeleted, usageDeleted, DatabaseManager.RetentionDays);
        }
    }
}

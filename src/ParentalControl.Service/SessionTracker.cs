using System.Collections.Concurrent;
using ParentalControl.Core.Data;
using ParentalControl.Core.Models;
using ParentalControl.Core.Platform;
namespace ParentalControl.Service;

public sealed record ActiveSession(int SessionId, string UserSid, string Username, DateTime LastTick);

public sealed class SessionTracker
{
    private readonly ConcurrentDictionary<int, ActiveSession> _activeSessions = new();
    private volatile bool _isAwake = true;
    private readonly Serilog.ILogger _logger;

    public SessionTracker(Serilog.ILogger logger)
    {
        _logger = logger;
    }

    public bool IsAwake => _isAwake;

    public IReadOnlyDictionary<int, ActiveSession> ActiveSessions => _activeSessions;

    public void Initialize()
    {
        var sessions = SessionManager.GetActiveSessions();
        foreach (var (sessionId, username, sid) in sessions)
        {
            if (sid is null) continue;

            var user = UserRepository.GetBySid(sid);
            if (user is null)
            {
                user = UserRepository.Upsert(sid, username, false);
            }

            _activeSessions.TryAdd(sessionId, new ActiveSession(sessionId, sid, username, DateTime.Now));
            _logger.Information("Recovered existing session: {Username} (SID: {Sid}) on session {SessionId}",
                username, sid, sessionId);
        }
    }

    public void OnUserLogon(int sessionId)
    {
        var username = SessionManager.GetSessionUsername(sessionId);
        var sid = SessionManager.GetSessionUserSid(sessionId);

        if (username is null || sid is null)
        {
            _logger.Warning("Could not resolve user for session {SessionId}", sessionId);
            return;
        }

        var user = UserRepository.GetBySid(sid);
        if (user is null)
        {
            user = UserRepository.Upsert(sid, username, false);
        }

        if (!user.IsRestricted)
        {
            _activeSessions.TryAdd(sessionId, new ActiveSession(sessionId, sid, username, DateTime.Now));
            EventRepository.LogEvent(sid, EventType.LOGIN);
            _logger.Information("User logged in (unrestricted): {Username}", username);
            return;
        }

        var limit = LimitRepository.GetByUserId(user.Id);
        UsageRecord? usage = null;
        if (limit is not null)
        {
            var now = TimeOnly.FromDateTime(DateTime.Now);
            if (now < limit.ScheduleStart || now > limit.ScheduleEnd)
            {
                _logger.Information("Login denied (outside schedule): {Username}", username);
                EventRepository.LogEvent(sid, EventType.LOGIN_DENIED, "Outside allowed schedule");
                SessionManager.ForceLogoff(sessionId);
                return;
            }

            var today = DateOnly.FromDateTime(DateTime.Now);
            usage = UsageRepository.GetUsage(user.Id, today);
            if (usage is not null && usage.MinutesUsed >= limit.DailyMinutes)
            {
                _logger.Information("Login denied (limit reached): {Username}", username);
                EventRepository.LogEvent(sid, EventType.LOGIN_DENIED, "Daily limit already reached");
                SessionManager.ForceLogoff(sessionId);
                return;
            }
        }

        _activeSessions.TryAdd(sessionId, new ActiveSession(sessionId, sid, username, DateTime.Now));

        var loginDetail = "";
        if (limit is not null)
        {
            var remaining = limit.DailyMinutes - (usage?.MinutesUsed ?? 0);
            loginDetail = $"Remaining: {remaining / 60}h {remaining % 60}m";
        }
        EventRepository.LogEvent(sid, EventType.LOGIN, loginDetail);
        _logger.Information("User logged in (restricted): {Username}", username);
    }

    public void OnUserLogoff(int sessionId)
    {
        if (_activeSessions.TryRemove(sessionId, out var session))
        {
            FlushSessionTime(session);
            EventRepository.LogEvent(session.UserSid, EventType.LOGOUT);
            _logger.Information("User logged off: {Username}", session.Username);
        }
    }

    public void OnSleep()
    {
        _isAwake = false;
        _logger.Information("System entering sleep");

        var now = DateTime.Now;
        foreach (var (sessionId, session) in _activeSessions)
        {
            FlushSessionTime(session);
            _activeSessions[sessionId] = session with { LastTick = now };
            EventRepository.LogEvent(session.UserSid, EventType.SLEEP);
        }
    }

    public void OnWake()
    {
        _isAwake = true;
        _logger.Information("System resuming from sleep");

        var now = DateTime.Now;
        foreach (var (sessionId, session) in _activeSessions)
        {
            var sleepMinutes = (int)(now - session.LastTick).TotalMinutes;
            var detail = sleepMinutes > 1 ? $"After ~{sleepMinutes} min sleep" : "";
            _activeSessions[sessionId] = session with { LastTick = now };
            EventRepository.LogEvent(session.UserSid, EventType.WAKE, detail);
        }
    }

    public void RemoveSession(int sessionId)
    {
        _activeSessions.TryRemove(sessionId, out _);
    }

    public void TickAllSessions()
    {
        var now = DateTime.Now;
        foreach (var (sessionId, session) in _activeSessions)
        {
            var elapsed = (int)(now - session.LastTick).TotalMinutes;
            if (elapsed < 1) continue;

            var user = UserRepository.GetBySid(session.UserSid);
            if (user is null) continue;

            var today = DateOnly.FromDateTime(now);

            if (elapsed > 2)
            {
                _logger.Warning("Time gap detected ({Elapsed} min) for {Username} — possible undetected sleep/hibernate",
                    elapsed, session.Username);
                EventRepository.LogEvent(session.UserSid, EventType.SLEEP, $"Detected retroactively (~{elapsed} min gap)");
                EventRepository.LogEvent(session.UserSid, EventType.WAKE, $"Detected retroactively (~{elapsed} min gap)");
                UsageRepository.AddMinutes(user.Id, today, 1);
                _activeSessions[sessionId] = session with { LastTick = now };
                continue;
            }

            UsageRepository.AddMinutes(user.Id, today, elapsed);
            _activeSessions[sessionId] = session with { LastTick = now };
        }
    }

    private void FlushSessionTime(ActiveSession session)
    {
        var now = DateTime.Now;
        var elapsed = (int)(now - session.LastTick).TotalMinutes;
        if (elapsed < 1) return;
        if (elapsed > 2) elapsed = 1;

        var user = UserRepository.GetBySid(session.UserSid);
        if (user is null) return;

        var today = DateOnly.FromDateTime(now);
        UsageRepository.AddMinutes(user.Id, today, elapsed);
    }
}

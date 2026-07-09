using JetBrains.Annotations;
using Microsoft.Xna.Framework;
using FezEditor.Tools;

namespace FezEditor.Services;

[UsedImplicitly]
public class StatusService : IDisposable
{
    private readonly AppStorageService _storageService;

    private readonly List<StatusHint> _hints = new();

    private readonly Lock _activityLock = new();

    private StatusActivity? _currentActivity;

    private long _activityId;

    public StatusService(Game game)
    {
        _storageService = game.GetService<AppStorageService>();
    }

    public void ClearHints()
    {
        _hints.Clear();
    }

    public void AddHint(string binding, string label)
    {
        _hints.Add(new StatusHint(binding, label));
    }

    public StatusSnapshot GetSnapshot()
    {
        StatusActivity? activity;
        lock (_activityLock)
        {
            activity = _currentActivity;
        }

        var hatStatus = string.IsNullOrWhiteSpace(_storageService.HatLauncherPath)
            ? "Not configured"
            : File.Exists(_storageService.HatLauncherPath)
                ? "Available"
                : "Missing";

        var appStatus = new List<StatusHint>
        {
            new("HAT", hatStatus),
            new("", $"{FezEditor.Version} ({FezEditor.Commit})")
        };

        return new StatusSnapshot(_hints.ToArray(), appStatus, activity);
    }

    public StatusActivityHandle BeginActivity(string text, float? progress = null)
    {
        lock (_activityLock)
        {
            var id = ++_activityId;
            _currentActivity = new StatusActivity(text, NormalizeProgress(progress));
            return new StatusActivityHandle(this, id);
        }
    }

    internal void ReportActivity(long id, string text, float? progress)
    {
        lock (_activityLock)
        {
            if (id == _activityId && _currentActivity != null)
            {
                _currentActivity = new StatusActivity(text, NormalizeProgress(progress));
            }
        }
    }

    internal void EndActivity(long id)
    {
        lock (_activityLock)
        {
            if (id == _activityId)
            {
                _currentActivity = null;
            }
        }
    }

    private static float? NormalizeProgress(float? progress)
    {
        return progress.HasValue ? Math.Clamp(progress.Value, 0f, 1f) : null;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _hints.Clear();
        lock (_activityLock)
        {
            _currentActivity = null;
            _activityId++;
        }
    }
}

public sealed record StatusHint(string Binding, string Label);

public sealed record StatusSnapshot(
    IReadOnlyList<StatusHint> Left,
    IReadOnlyList<StatusHint> Right,
    StatusActivity? Activity);

public sealed record StatusActivity(string Text, float? Progress);

public sealed class StatusActivityHandle : IDisposable
{
    private StatusService? _service;

    private readonly long _id;

    internal StatusActivityHandle(StatusService service, long id)
    {
        _service = service;
        _id = id;
    }

    public void Report(string text, float? progress = null)
    {
        _service?.ReportActivity(_id, text, progress);
    }

    public void Dispose()
    {
        var service = Interlocked.Exchange(ref _service, null);
        service?.EndActivity(_id);
    }
}
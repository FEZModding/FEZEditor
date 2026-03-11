using JetBrains.Annotations;

namespace FezEditor.Services;

[UsedImplicitly]
public class StatusService : IDisposable
{
    public IReadOnlyList<(string Binding, string Label)> Hints => _hints;

    private readonly List<(string Binding, string Label)> _hints = new();

    public void ClearHints()
    {
        _hints.Clear();
    }

    public void AddHints(params IEnumerable<(string binding, string label)> hints)
    {
        _hints.AddRange(hints);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _hints.Clear();
    }
}
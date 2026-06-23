using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using FezEditor.Tools;

namespace FezEditor.Structure;

public class History : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        IncludeFields = true,
        WriteIndented = false,
        Converters = { new TrileEmplacementConverter() }
    };

    private static readonly Change EmptyChange = new(string.Empty, string.Empty);

    private const int MaxHistorySize = byte.MaxValue;

    private readonly LinkedList<UndoOperation> _undoStack = new();

    private readonly LinkedList<UndoOperation> _redoStack = new();

    private object _tracked = null!;

    private Type TrackedType
    {
        get
        {
            if (_tracked == null)
            {
                throw new InvalidOperationException("Cannot use history before tracking an object!");
            }

            return _tracked.GetType();
        }
    }

    public bool CanUndo => _undoStack.Count > 0;

    public bool CanRedo => _redoStack.Count > 0;

    public event Action<Change>? StateChanged;

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _undoStack.Clear();
        _redoStack.Clear();
    }

    public void Track(object target)
    {
        _tracked = target;
    }

    public IDisposable BeginScope(string name, object? tag = null)
    {
        // TODO: Remove tag param later, leave it for now for less refactoring
        return new Scope(this, name);
    }

    public void Undo()
    {
        if (!CanUndo)
        {
            return;
        }

        var after = _undoStack.Last!.Value;
        _undoStack.RemoveLast();

        var before = CaptureState(after.Name);
        _redoStack.AddLast(before);
        if (_redoStack.Count > MaxHistorySize)
        {
            _redoStack.RemoveFirst();
        }

        Restore(after);
        StateChanged?.Invoke(new Change(before.Json, after.Json));
    }

    public void Redo()
    {
        if (!CanRedo)
        {
            return;
        }

        var after = _redoStack.Last!.Value;
        _redoStack.RemoveLast();

        var before = CaptureState(after.Name);
        _undoStack.AddLast(before);
        if (_undoStack.Count > MaxHistorySize)
        {
            _undoStack.RemoveFirst();
        }

        Restore(after);
        StateChanged?.Invoke(new Change(before.Json, after.Json));
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        StateChanged?.Invoke(EmptyChange);
    }

    private UndoOperation CaptureState(string name)
    {
        var json = JsonSerializer.Serialize(_tracked, TrackedType, JsonOptions);
        return new UndoOperation(name, json);
    }

    private void Restore(UndoOperation op)
    {
        var restored = JsonSerializer.Deserialize(op.Json, TrackedType, JsonOptions)!;
        foreach (var property in TrackedType.GetProperties())
        {
            if (property is { CanRead: true, CanWrite: true } &&
                property.GetCustomAttribute<JsonIgnoreAttribute>() == null)
            {
                property.SetValue(_tracked, property.GetValue(restored));
            }
        }

        foreach (var field in TrackedType.GetFields())
        {
            if (!field.IsInitOnly &&
                field.GetCustomAttribute<JsonIgnoreAttribute>() == null)
            {
                field.SetValue(_tracked, field.GetValue(restored));
            }
        }
    }

    private void Push(UndoOperation before, UndoOperation after)
    {
        if (before.Json.Equals(after.Json))
        {
            return;
        }

        _undoStack.AddLast(before);
        if (_undoStack.Count > MaxHistorySize)
        {
            _undoStack.RemoveFirst();
        }

        _redoStack.Clear();
        StateChanged?.Invoke(new Change(before.Json, after.Json));
    }

    public sealed record Change(string BeforeJson, string AfterJson);

    private sealed class Scope : IDisposable
    {
        private readonly History _service;

        private readonly UndoOperation _before;

        private bool _disposed;

        internal Scope(History service, string name)
        {
            _service = service;
            _before = service.CaptureState(name);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            var after = _service.CaptureState(_before.Name);
            _service.Push(_before, after);
        }
    }

    private sealed record UndoOperation(string Name, string Json);
}
namespace FezEditor.Structure;

public readonly record struct InputHint(string Binding, string Label) : IComparable<InputHint>
{
    public int CompareTo(InputHint other)
    {
        var labelOrder = StringComparer.OrdinalIgnoreCase.Compare(Label, other.Label);
        return labelOrder != 0
            ? labelOrder
            : StringComparer.OrdinalIgnoreCase.Compare(Binding, other.Binding);
    }
}

public sealed class InputHints : IReadOnlyList<InputHint>
{
    public int Count => _items.Count;

    public InputHint this[int index] => _items[index];

    private readonly List<InputHint> _items = new();

    public void Add(string binding, string label)
    {
        var hint = new InputHint(binding, label);
        if (!_items.Contains(hint))
        {
            _items.Add(hint);
            _items.Sort();
        }
    }

    public void Clear()
    {
        _items.Clear();
    }

    public IEnumerator<InputHint> GetEnumerator()
    {
        return _items.GetEnumerator();
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
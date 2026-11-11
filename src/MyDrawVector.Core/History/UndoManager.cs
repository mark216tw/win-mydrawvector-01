namespace MyDrawVector.Core.History;

public sealed class UndoManager
{
    private readonly List<string> _undo = [];
    private readonly List<string> _redo = [];
    private string? _lastCoalesceKey;
    private DateTime _lastCoalesceAt;

    public int Limit { get; init; } = 100;

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public int UndoCount => _undo.Count;

    public int RedoCount => _redo.Count;

    public void Push(string state)
    {
        Add(state);
        _lastCoalesceKey = null;
    }

    public bool ShouldPushCoalesced(string key, TimeSpan window)
    {
        return !string.Equals(_lastCoalesceKey, key, StringComparison.Ordinal) ||
               DateTime.UtcNow - _lastCoalesceAt >= window;
    }

    public void PushCoalesced(string state, string key, TimeSpan window)
    {
        if (!ShouldPushCoalesced(key, window))
        {
            return;
        }

        Add(state);
        _lastCoalesceKey = key;
        _lastCoalesceAt = DateTime.UtcNow;
    }

    public string? Undo(string currentState)
    {
        if (_undo.Count == 0)
        {
            return null;
        }

        var state = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        _redo.Add(currentState);
        _lastCoalesceKey = null;
        return state;
    }

    public string? Redo(string currentState)
    {
        if (_redo.Count == 0)
        {
            return null;
        }

        var state = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        _undo.Add(currentState);
        _lastCoalesceKey = null;
        return state;
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
        _lastCoalesceKey = null;
    }

    private void Add(string state)
    {
        _undo.Add(state);
        if (_undo.Count > Limit)
        {
            _undo.RemoveAt(0);
        }

        _redo.Clear();
    }
}

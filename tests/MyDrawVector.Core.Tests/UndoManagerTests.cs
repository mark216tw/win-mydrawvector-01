using MyDrawVector.Core.History;
using Xunit;

namespace MyDrawVector.Core.Tests;

public sealed class UndoManagerTests
{
    [Fact]
    public void UndoRedo_RestoresStates()
    {
        var history = new UndoManager();
        history.Push("state-1");
        history.Push("state-2");

        Assert.True(history.CanUndo);
        Assert.Equal("state-2", history.Undo("current"));
        Assert.Equal("state-1", history.Undo("current"));
        Assert.False(history.CanUndo);
        Assert.True(history.CanRedo);
        Assert.Equal("current", history.Redo("state-1"));
    }

    [Fact]
    public void NewEdit_ClearsRedo()
    {
        var history = new UndoManager();
        history.Push("state-1");
        _ = history.Undo("current");
        Assert.True(history.CanRedo);

        history.Push("state-2");
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void Limit_DropsOldestState()
    {
        var history = new UndoManager { Limit = 2 };
        history.Push("1");
        history.Push("2");
        history.Push("3");

        Assert.Equal("3", history.Undo("current"));
        Assert.Equal("2", history.Undo("current"));
        Assert.False(history.CanUndo);
    }

    [Fact]
    public void PushCoalesced_MergesRapidChangesWithSameKey()
    {
        var history = new UndoManager();
        var window = TimeSpan.FromMinutes(1);

        history.PushCoalesced("state-1", "opacity", window);
        history.PushCoalesced("state-2", "opacity", window);
        history.PushCoalesced("state-3", "opacity", window);

        Assert.Equal(1, history.UndoCount);
        Assert.Equal("state-1", history.Undo("current"));
    }

    [Fact]
    public void ShouldPushCoalesced_MatchesPushBehavior()
    {
        var history = new UndoManager();
        var window = TimeSpan.FromMinutes(1);

        Assert.True(history.ShouldPushCoalesced("opacity", window));
        history.PushCoalesced("state-1", "opacity", window);
        Assert.False(history.ShouldPushCoalesced("opacity", window));
        Assert.True(history.ShouldPushCoalesced("rotation", window));
    }

    [Fact]
    public void PushCoalesced_KeepsDifferentKeysSeparate()
    {
        var history = new UndoManager();
        var window = TimeSpan.FromMinutes(1);

        history.PushCoalesced("state-1", "opacity", window);
        history.PushCoalesced("state-2", "rotation", window);

        Assert.Equal(2, history.UndoCount);
    }

    [Fact]
    public void PushCoalesced_StartsNewGroupAfterUndo()
    {
        var history = new UndoManager();
        var window = TimeSpan.FromMinutes(1);

        history.PushCoalesced("state-1", "opacity", window);
        _ = history.Undo("current");
        history.PushCoalesced("state-2", "opacity", window);

        Assert.Equal(1, history.UndoCount);
        Assert.Equal("state-2", history.Undo("current"));
    }
}

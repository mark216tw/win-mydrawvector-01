using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MyDrawVector.Core.Geometry;
using MyDrawVector.Core.History;
using MyDrawVector.Core.Models;
using MyDrawVector.Core.Serialization;

namespace MyDrawVector.ViewModels;

public enum AlignKind
{
    Left,
    CenterX,
    Right,
    Top,
    CenterY,
    Bottom,
}

public sealed partial class EditorViewModel : ObservableObject
{
    private static readonly TimeSpan CoalesceWindow = TimeSpan.FromMilliseconds(700);

    private Document _document = Document.CreateDefault();
    private string _statusText = "就緒";
    private bool _isDirty;
    private string? _currentFilePath;

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public bool IsDirty
    {
        get => _isDirty;
        set => SetProperty(ref _isDirty, value);
    }

    public int ChangeVersion { get; private set; }

    public string? CurrentFilePath
    {
        get => _currentFilePath;
        set => SetProperty(ref _currentFilePath, value);
    }

    public EditorViewModel()
    {
        SyncCurrentLayer();
        Selection.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(PrimarySelection));
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(SelectionSummary));
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        };
    }

    public string? CurrentLayerId { get; private set; }

    public Layer CurrentLayer =>
        _document.FindLayer(CurrentLayerId ?? string.Empty)
        ?? _document.Layers.OrderBy(l => l.Order).First();

    public int CurrentLayerObjectCount => _document.CountObjects(CurrentLayer.Id);

    public event EventHandler? DocumentChanged;

    public event EventHandler? SelectionChanged;

    public Document Document => _document;

    public UndoManager History { get; } = new();

    public ObservableCollection<SceneObject> Selection { get; } = [];

    public SceneObject? PrimarySelection => Selection.Count == 1 ? Selection[0] : null;

    public bool HasSelection => Selection.Count > 0;

    public string SelectionSummary
    {
        get
        {
            if (Selection.Count == 0)
            {
                return "未選取物件";
            }

            return Selection.Count == 1
                ? Selection[0].Name
                : $"已選取 {Selection.Count} 個物件";
        }
    }

    public void NewDocument()
    {
        _document = Document.CreateDefault();
        CurrentFilePath = null;
        CurrentLayerId = null;
        SyncCurrentLayer();
        IsDirty = false;
        History.Clear();
        Selection.Clear();
        StatusText = "新文件";
        OnPropertyChanged(nameof(Document));
        DocumentChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetDocument(Document document, string? path, bool resetHistory)
    {
        _document = document;
        _document.EnsureLayers();
        CurrentFilePath = path;
        CurrentLayerId = null;
        SyncCurrentLayer();
        IsDirty = false;
        if (resetHistory)
        {
            History.Clear();
        }

        Selection.Clear();
        OnPropertyChanged(nameof(Document));
        DocumentChanged?.Invoke(this, EventArgs.Empty);
    }

    public void PushUndo(string? coalesceKey = null)
    {
        if (coalesceKey is not null && !History.ShouldPushCoalesced(coalesceKey, CoalesceWindow))
        {
            return;
        }

        var state = DocumentSerializer.SerializeCompact(_document);
        if (coalesceKey is null)
        {
            History.Push(state);
        }
        else
        {
            History.PushCoalesced(state, coalesceKey, CoalesceWindow);
        }
    }

    public bool Undo()
    {
        var selectionSnapshot = CaptureSelectionSnapshot();
        var state = History.Undo(DocumentSerializer.SerializeCompact(_document));
        if (state is null)
        {
            return false;
        }

        _document = DocumentSerializer.Deserialize(state);
        SyncCurrentLayer();
        RestoreSelection(selectionSnapshot);
        ChangeVersion++;
        IsDirty = true;
        StatusText = "已復原";
        OnPropertyChanged(nameof(Document));
        DocumentChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool Redo()
    {
        var selectionSnapshot = CaptureSelectionSnapshot();
        var state = History.Redo(DocumentSerializer.SerializeCompact(_document));
        if (state is null)
        {
            return false;
        }

        _document = DocumentSerializer.Deserialize(state);
        SyncCurrentLayer();
        RestoreSelection(selectionSnapshot);
        ChangeVersion++;
        IsDirty = true;
        StatusText = "已重做";
        OnPropertyChanged(nameof(Document));
        DocumentChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private (List<string> ObjectIds, string? LayerId) CaptureSelectionSnapshot()
    {
        return (Selection.Select(item => item.Id).ToList(), CurrentLayerId);
    }

    private void RestoreSelection((List<string> ObjectIds, string? LayerId) snapshot)
    {
        if (!string.IsNullOrWhiteSpace(snapshot.LayerId) &&
            _document.FindLayer(snapshot.LayerId) is not null)
        {
            CurrentLayerId = snapshot.LayerId;
            OnPropertyChanged(nameof(CurrentLayer));
            OnPropertyChanged(nameof(CurrentLayerObjectCount));
        }

        Selection.Clear();
        foreach (var id in snapshot.ObjectIds)
        {
            if (_document.Objects.FirstOrDefault(item => item.Id == id) is { } restored)
            {
                Selection.Add(restored);
            }
        }
    }

    public void AddObject(SceneObject sceneObject, bool select = true)
    {
        if (string.IsNullOrWhiteSpace(sceneObject.Name))
        {
            sceneObject.Name = CreateDefaultName(sceneObject);
        }

        sceneObject.LayerId = CurrentLayer.Id;
        sceneObject.ZIndex = _document.NextZIndex(CurrentLayer.Id);
        _document.Objects.Add(sceneObject);
        if (sceneObject is ImageObject image && _document.FindAsset(image.AssetId) is null)
        {
            // The window adds the asset metadata before calling this method.
        }

        if (select)
        {
            Select(sceneObject, additive: false);
        }

        MarkDirty();
    }

    public void RemoveSelection()
    {
        if (Selection.Count == 0)
        {
            return;
        }

        foreach (var item in Selection.ToList())
        {
            _document.Objects.Remove(item);
        }

        Selection.Clear();
        _document.NormalizeZIndexes();
        MarkDirty();
    }

    public List<SceneObject> CloneSelection()
    {
        return Selection.Select(o => o.Clone()).ToList();
    }

    public void DuplicateSelection()
    {
        if (Selection.Count == 0)
        {
            return;
        }

        var clones = CloneSelection();
        foreach (var clone in clones)
        {
            clone.Id = Guid.NewGuid().ToString("N");
            clone.Name = CreateDefaultName(clone);
            clone.Transform.CenterX += 12;
            clone.Transform.CenterY += 12;
            clone.LayerId = CurrentLayer.Id;
            clone.ZIndex = _document.NextZIndex(CurrentLayer.Id);
            _document.Objects.Add(clone);
        }

        Selection.Clear();
        foreach (var clone in clones)
        {
            Selection.Add(clone);
        }

        MarkDirty();
    }

    public void PasteClones(IReadOnlyList<SceneObject> source)
    {
        if (source.Count == 0)
        {
            return;
        }

        var pasted = new List<SceneObject>();
        foreach (var item in source)
        {
            var clone = item.Clone();
            clone.Id = Guid.NewGuid().ToString("N");
            clone.Name = CreateDefaultName(clone);
            clone.Transform.CenterX += 16;
            clone.Transform.CenterY += 16;
            clone.LayerId = CurrentLayer.Id;
            clone.ZIndex = _document.NextZIndex(CurrentLayer.Id);
            _document.Objects.Add(clone);
            pasted.Add(clone);
        }

        Selection.Clear();
        foreach (var item in pasted)
        {
            Selection.Add(item);
        }

        MarkDirty();
    }

    public void BringForward()
    {
        foreach (var layer in SelectedLayers())
        {
            var ordered = _document.ObjectsInLayer(layer.Id).ToList();
            for (var i = ordered.Count - 2; i >= 0; i--)
            {
                if (Selection.Contains(ordered[i]) && !Selection.Contains(ordered[i + 1]))
                {
                    (ordered[i], ordered[i + 1]) = (ordered[i + 1], ordered[i]);
                }
            }

            ApplyLayerZOrder(ordered);
        }

        _document.NormalizeZIndexes();
        MarkDirty();
    }

    public void SendBackward()
    {
        foreach (var layer in SelectedLayers())
        {
            var ordered = _document.ObjectsInLayer(layer.Id).ToList();
            for (var i = 1; i < ordered.Count; i++)
            {
                if (Selection.Contains(ordered[i]) && !Selection.Contains(ordered[i - 1]))
                {
                    (ordered[i], ordered[i - 1]) = (ordered[i - 1], ordered[i]);
                }
            }

            ApplyLayerZOrder(ordered);
        }

        _document.NormalizeZIndexes();
        MarkDirty();
    }

    public void BringToFront()
    {
        foreach (var layer in SelectedLayers())
        {
            var ordered = _document.ObjectsInLayer(layer.Id)
                .Where(o => !Selection.Contains(o))
                .Concat(Selection.Where(o => o.LayerId == layer.Id))
                .ToList();
            ApplyLayerZOrder(ordered);
        }

        _document.NormalizeZIndexes();
        MarkDirty();
    }

    public void SendToBack()
    {
        foreach (var layer in SelectedLayers())
        {
            var ordered = Selection.Where(o => o.LayerId == layer.Id)
                .Concat(_document.ObjectsInLayer(layer.Id).Where(o => !Selection.Contains(o)))
                .ToList();
            ApplyLayerZOrder(ordered);
        }

        _document.NormalizeZIndexes();
        MarkDirty();
    }

    private IEnumerable<Layer> SelectedLayers()
    {
        return Selection
            .Select(o => o.LayerId)
            .Distinct()
            .Select(id => _document.FindLayer(id))
            .Where(l => l is not null)
            .Cast<Layer>();
    }

    private static void ApplyLayerZOrder(IReadOnlyList<SceneObject> ordered)
    {
        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i].ZIndex = i;
        }
    }

    public void SetCurrentLayer(Layer layer)
    {
        CurrentLayerId = layer.Id;
        OnPropertyChanged(nameof(CurrentLayer));
        OnPropertyChanged(nameof(CurrentLayerObjectCount));
    }

    public void SelectLayer(Layer layer)
    {
        SetCurrentLayer(layer);
        Selection.Clear();
        DocumentChanged?.Invoke(this, EventArgs.Empty);
    }

    public Layer AddLayer(string name)
    {
        var current = CurrentLayer;
        var order = current.Order + 1;
        foreach (var layer in _document.Layers.Where(l => l.Order >= order))
        {
            layer.Order++;
        }

        var newLayer = new Layer
        {
            Name = MakeUniqueLayerName(string.IsNullOrWhiteSpace(name) ? "圖層" : name.Trim()),
            Order = order,
        };
        _document.Layers.Add(newLayer);
        _document.NormalizeLayerOrders();
        CurrentLayerId = newLayer.Id;
        Selection.Clear();
        OnPropertyChanged(nameof(CurrentLayer));
        OnPropertyChanged(nameof(CurrentLayerObjectCount));
        MarkDirty();
        return newLayer;
    }

    public bool RenameLayer(Layer layer, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        layer.Name = MakeUniqueLayerName(name.Trim(), layer);
        MarkDirty();
        return true;
    }

    public bool DeleteLayer(Layer layer)
    {
        if (_document.Layers.Count <= 1)
        {
            return false;
        }

        var ordered = _document.Layers.OrderBy(l => l.Order).ToList();
        var index = ordered.IndexOf(layer);
        _document.Objects.RemoveAll(o => o.LayerId == layer.Id);
        _document.Layers.Remove(layer);
        _document.NormalizeLayerOrders();
        _document.NormalizeZIndexes();
        Selection.Clear();

        if (string.Equals(CurrentLayerId, layer.Id, StringComparison.Ordinal))
        {
            var remaining = _document.Layers.OrderBy(l => l.Order).ToList();
            CurrentLayerId = remaining[Math.Clamp(index, 0, remaining.Count - 1)].Id;
        }

        OnPropertyChanged(nameof(CurrentLayer));
        OnPropertyChanged(nameof(CurrentLayerObjectCount));
        MarkDirty();
        return true;
    }

    public void ToggleLayerVisibility(Layer layer)
    {
        layer.IsVisible = !layer.IsVisible;
        if (!layer.IsVisible)
        {
            Selection.Clear();
        }

        MarkDirty();
    }

    public void ToggleLayerLock(Layer layer)
    {
        layer.IsLocked = !layer.IsLocked;
        if (layer.IsLocked)
        {
            Selection.Clear();
        }

        MarkDirty();
    }

    public void MoveLayer(Layer layer, bool up)
    {
        var ordered = _document.Layers.OrderBy(l => l.Order).ToList();
        var index = ordered.IndexOf(layer);
        var target = up ? index + 1 : index - 1;
        if (target < 0 || target >= ordered.Count)
        {
            return;
        }

        (ordered[index], ordered[target]) = (ordered[target], ordered[index]);
        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i].Order = i;
        }

        MarkDirty();
    }

    private string MakeUniqueLayerName(string baseName, Layer? except = null)
    {
        var name = baseName;
        var index = 2;
        while (_document.Layers.Any(l => l != except && string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            name = $"{baseName} ({index++})";
        }

        return name;
    }

    private void SyncCurrentLayer()
    {
        if (string.IsNullOrWhiteSpace(CurrentLayerId) ||
            _document.FindLayer(CurrentLayerId) is null)
        {
            CurrentLayerId = _document.Layers.OrderBy(l => l.Order).First().Id;
        }

        OnPropertyChanged(nameof(CurrentLayer));
        OnPropertyChanged(nameof(CurrentLayerObjectCount));
    }

    public void AlignSelection(AlignKind kind)
    {
        if (Selection.Count == 0)
        {
            return;
        }

        var reference = Selection.Count == 1
            ? new RectD(0, 0, _document.Canvas.Width, _document.Canvas.Height)
            : GetSelectionBounds();

        foreach (var item in Selection)
        {
            var bounds = item.WorldBounds;
            switch (kind)
            {
                case AlignKind.Left:
                    item.Transform.CenterX += reference.X - bounds.X;
                    break;
                case AlignKind.CenterX:
                    item.Transform.CenterX += reference.CenterX - bounds.CenterX;
                    break;
                case AlignKind.Right:
                    item.Transform.CenterX += reference.Right - bounds.Right;
                    break;
                case AlignKind.Top:
                    item.Transform.CenterY += reference.Y - bounds.Y;
                    break;
                case AlignKind.CenterY:
                    item.Transform.CenterY += reference.CenterY - bounds.CenterY;
                    break;
                case AlignKind.Bottom:
                    item.Transform.CenterY += reference.Bottom - bounds.Bottom;
                    break;
            }
        }

        MarkDirty();
    }

    public RectD GetSelectionBounds()
    {
        if (Selection.Count == 0)
        {
            return new RectD(0, 0, 0, 0);
        }

        var bounds = Selection.Select(o => o.WorldBounds).ToList();
        var minX = bounds.Min(b => b.X);
        var minY = bounds.Min(b => b.Y);
        var maxX = bounds.Max(b => b.Right);
        var maxY = bounds.Max(b => b.Bottom);
        return new RectD(minX, minY, maxX - minX, maxY - minY);
    }

    public void Select(SceneObject? sceneObject, bool additive)
    {
        if (sceneObject is null)
        {
            if (!additive)
            {
                Selection.Clear();
            }

            return;
        }

        if (additive)
        {
            if (Selection.Contains(sceneObject))
            {
                Selection.Remove(sceneObject);
            }
            else
            {
                Selection.Add(sceneObject);
            }

            return;
        }

        Selection.Clear();
        Selection.Add(sceneObject);
    }

    public void SelectMany(IEnumerable<SceneObject> objects)
    {
        Selection.Clear();
        foreach (var item in objects)
        {
            Selection.Add(item);
        }
    }

    public void ClearSelection()
    {
        Selection.Clear();
    }

    public void MarkDirty()
    {
        ChangeVersion++;
        IsDirty = true;
        OnPropertyChanged(nameof(Document));
        DocumentChanged?.Invoke(this, EventArgs.Empty);
    }

    public void NotifyChanged()
    {
        OnPropertyChanged(nameof(Document));
        DocumentChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetStatus(string text)
    {
        StatusText = text;
    }

    public string CreateDefaultName(SceneObject sceneObject)
    {
        var prefix = sceneObject switch
        {
            ShapeObject shape => shape.Kind switch
            {
                ShapeKind.Rectangle or ShapeKind.RoundedRectangle => "矩形",
                ShapeKind.Ellipse => "橢圓",
                ShapeKind.Line => "直線",
                ShapeKind.Arrow => "箭頭",
                ShapeKind.Triangle => "三角形",
                ShapeKind.Diamond => "菱形",
                ShapeKind.Pentagon => "五邊形",
                ShapeKind.Hexagon => "六邊形",
                ShapeKind.Star => "星形",
                ShapeKind.SpeechBubble => "對話框",
                _ => "圖形",
            },
            TextObject => "文字",
            ImageObject => "圖片",
            PathObject => "筆畫",
            _ => "物件",
        };

        var index = 1;
        var existing = _document.Objects.Select(o => o.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        while (existing.Contains($"{prefix} {index}"))
        {
            index++;
        }

        return $"{prefix} {index}";
    }
}

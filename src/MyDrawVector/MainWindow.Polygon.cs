using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using MyDrawVector.Controls;
using MyDrawVector.Core.Geometry;
using MyDrawVector.Core.Models;
using MyDrawVector.Core.Serialization;
using MyDrawVector.Services;
using Windows.Foundation;
using Windows.System;
using Windows.UI;
using ColorHelper = MyDrawVector.Helpers.ColorHelper;
using PointD = MyDrawVector.Core.Models.PointD;
using XamlPath = Microsoft.UI.Xaml.Shapes.Path;

namespace MyDrawVector;

public sealed partial class MainWindow
{
    private const double PolygonStartSnapRadius = 12;
    private const double PolygonVertexHitRadius = 10;
    private const double PolygonEdgeHitRadius = 8;
    private const double PolygonDuplicateClickDistance = 5;
    private const string PolygonStyleKey = "Polygon";

    private bool _isDrawingPolygon;
    private bool _isDrawingCurvedPolygon;
    private readonly List<PointD> _polygonDraftPoints = [];
    private PointD _polygonDraftCursorWorld;
    private bool _polygonSnapToStart;

    private PolygonObject? _editingPolygon;
    private int _selectedVertexIndex = -1;
    private int _hoveredVertexIndex = -1;
    private (int EdgeIndex, PointD WorldPoint)? _hoveredEdgeHit;
    private List<PointD> _editingVertexWorldPoints = [];
    private List<bool> _editingVertexSmoothFlags = [];
    private bool _suppressNextPolygonDoubleTap;

    private static bool IsPolygonTool(EditorTool tool)
    {
        return tool is EditorTool.Polygon or EditorTool.CurvedPolygon;
    }

    private ShapeToolStyle GetPolygonStyle()
    {
        if (!_settings.Current.ShapeToolStyles.TryGetValue(PolygonStyleKey, out var style) || style is null)
        {
            style = new ShapeToolStyle();
            _settings.Current.ShapeToolStyles[PolygonStyleKey] = style;
        }

        return style;
    }

    private void SavePolygonStyle(PolygonObject polygon)
    {
        var style = GetPolygonStyle();
        style.Fill = polygon.Fill;
        style.Stroke = polygon.Stroke;
        style.StrokeWidth = polygon.StrokeWidth;
        style.Dash = polygon.Dash;
        style.IsFilled = polygon.IsFilled;
        style.IsStroked = polygon.IsStroked;
    }

    // ---------------------------------------------------------------- rendering

    private void UpdatePolygonPath(XamlPath path, PolygonObject polygon)
    {
        path.Data = BuildPolygonGeometry(polygon);
        path.Fill = polygon.IsFilled ? ColorHelper.Brush(polygon.Fill) : null;
        path.Stroke = polygon.IsStroked ? ColorHelper.Brush(polygon.Stroke) : null;
        path.StrokeThickness = Math.Max(0, polygon.StrokeWidth);
        path.StrokeDashArray = polygon.Dash switch
        {
            "dash" => new DoubleCollection { 4, 2 },
            "dot" => new DoubleCollection { 1, 1 },
            _ => null,
        };
        path.StrokeStartLineCap = PenLineCap.Round;
        path.StrokeEndLineCap = PenLineCap.Round;
        path.StrokeLineJoin = PenLineJoin.Round;
        path.StrokeDashCap = PenLineCap.Round;
    }

    private static Geometry BuildPolygonGeometry(PolygonObject polygon)
    {
        var segments = PolygonGeometry.BuildSegments(polygon);
        return BuildPolygonSegmentsGeometry(segments, isClosed: true);
    }

    private static PathGeometry BuildPolygonSegmentsGeometry(IReadOnlyList<PolygonSegment> segments, bool isClosed)
    {
        var geometry = new PathGeometry();
        if (segments.Count == 0)
        {
            return geometry;
        }

        var figure = new PathFigure
        {
            StartPoint = new Point(segments[0].Start.X, segments[0].Start.Y),
            IsClosed = isClosed,
            IsFilled = isClosed,
        };

        foreach (var seg in segments)
        {
            if (seg.IsCurve)
            {
                figure.Segments.Add(new BezierSegment
                {
                    Point1 = new Point(seg.Control1.X, seg.Control1.Y),
                    Point2 = new Point(seg.Control2.X, seg.Control2.Y),
                    Point3 = new Point(seg.End.X, seg.End.Y),
                });
            }
            else
            {
                figure.Segments.Add(new LineSegment
                {
                    Point = new Point(seg.End.X, seg.End.Y),
                });
            }
        }

        geometry.Figures.Add(figure);
        return geometry;
    }

    // ---------------------------------------------------------------- drawing mode

    private PointD ComputePolygonDraftPoint(PointD worldPoint, bool constrainAngle)
    {
        var target = worldPoint;
        if (constrainAngle && _polygonDraftPoints.Count > 0)
        {
            var anchor = _polygonDraftPoints[^1];
            var dx = target.X - anchor.X;
            var dy = target.Y - anchor.Y;
            var distance = double.Hypot(dx, dy);
            if (distance > 1e-3)
            {
                var angle = Math.Atan2(dy, dx);
                var snappedAngle = Math.Round(angle / (Math.PI / 4.0)) * (Math.PI / 4.0);
                target = new PointD(
                    anchor.X + (Math.Cos(snappedAngle) * distance),
                    anchor.Y + (Math.Sin(snappedAngle) * distance));
            }
        }

        if (_settings.Current.SnapToGrid)
        {
            target = new PointD(
                GridSnap.Snap(target.X, GridSize, SnapThreshold),
                GridSnap.Snap(target.Y, GridSize, SnapThreshold));
        }

        return target;
    }

    private bool IsNearDraftStart(Point screenPoint)
    {
        if (_polygonDraftPoints.Count < 3)
        {
            return false;
        }

        var startScreen = WorldToScreen(_polygonDraftPoints[0]);
        return double.Hypot(screenPoint.X - startScreen.X, screenPoint.Y - startScreen.Y) <= PolygonStartSnapRadius;
    }

    private void HandlePolygonDraftClick(Point screenPoint, PointD worldPoint, bool isShift)
    {
        if (!_isDrawingPolygon)
        {
            if (!CanCreateOnCurrentLayer())
            {
                return;
            }

            ExitPolygonEditMode();
            _viewModel.ClearSelection();
            _isDrawingPolygon = true;
            _isDrawingCurvedPolygon = _tool == EditorTool.CurvedPolygon;
            _polygonDraftPoints.Clear();
            var firstPoint = ComputePolygonDraftPoint(worldPoint, constrainAngle: false);
            _polygonDraftPoints.Add(firstPoint);
            _polygonDraftCursorWorld = firstPoint;
            _polygonSnapToStart = false;
            _viewModel.SetStatus("點擊新增頂點，點起點／雙擊／Enter 完成封閉，右鍵／Backspace 退回，Esc 取消");
            RenderOverlay();
            return;
        }

        if (IsNearDraftStart(screenPoint))
        {
            FinishPolygonDraft();
            return;
        }

        if (_polygonDraftPoints.Count > 0)
        {
            var lastScreen = WorldToScreen(_polygonDraftPoints[^1]);
            if (double.Hypot(screenPoint.X - lastScreen.X, screenPoint.Y - lastScreen.Y) <= PolygonDuplicateClickDistance)
            {
                return;
            }
        }

        var nextPoint = ComputePolygonDraftPoint(worldPoint, isShift);
        _polygonDraftPoints.Add(nextPoint);
        _polygonDraftCursorWorld = nextPoint;
        _polygonSnapToStart = false;
        RenderOverlay();
    }

    private void UpdatePolygonDraftCursor(Point screenPoint, PointD worldPoint)
    {
        _polygonSnapToStart = IsNearDraftStart(screenPoint);
        _polygonDraftCursorWorld = _polygonSnapToStart
            ? _polygonDraftPoints[0]
            : ComputePolygonDraftPoint(worldPoint, IsKeyDown(VirtualKey.Shift));
        RenderOverlay();
    }

    private void UndoLastDraftPolygonVertex()
    {
        if (!_isDrawingPolygon)
        {
            return;
        }

        if (_polygonDraftPoints.Count <= 1)
        {
            CancelPolygonDraft();
            return;
        }

        _polygonDraftPoints.RemoveAt(_polygonDraftPoints.Count - 1);
        _polygonSnapToStart = false;
        RenderOverlay();
    }

    private void FinishPolygonDraft()
    {
        if (!_isDrawingPolygon)
        {
            return;
        }

        if (_polygonDraftPoints.Count < 3)
        {
            _viewModel.SetStatus("至少需要 3 個頂點才能建立多邊形");
            return;
        }

        var style = GetPolygonStyle();
        var isCurved = _isDrawingCurvedPolygon;
        var smoothFlags = Enumerable.Repeat(isCurved, _polygonDraftPoints.Count).ToList();
        var polygon = new PolygonObject
        {
            Name = isCurved ? "曲線多邊形" : "直線多邊形",
            Fill = style.Fill,
            Stroke = style.Stroke,
            StrokeWidth = style.StrokeWidth,
            Dash = style.Dash,
            IsFilled = style.IsFilled,
            IsStroked = style.IsStroked,
            Smoothness = 0.5,
        };

        PolygonGeometry.ApplyWorldVertices(polygon, _polygonDraftPoints, smoothFlags);

        _isDrawingPolygon = false;
        _polygonDraftPoints.Clear();
        _polygonSnapToStart = false;

        _viewModel.AddObject(polygon);
        SetTool(EditorTool.Select);
        _viewModel.SetStatus(isCurved ? "已建立曲線多邊形，雙擊可編輯頂點" : "已建立直線多邊形，雙擊可編輯頂點");
    }

    private void CancelPolygonDraft()
    {
        if (!_isDrawingPolygon)
        {
            return;
        }

        _isDrawingPolygon = false;
        _polygonDraftPoints.Clear();
        _polygonSnapToStart = false;
        RenderOverlay();
    }

    private void RenderPolygonDraftOverlay()
    {
        HideSelectionActions();
        if (_polygonDraftPoints.Count == 0)
        {
            return;
        }

        var accentBrush = new SolidColorBrush(Colors.DodgerBlue);
        var style = GetPolygonStyle();

        // Build preview points (include live cursor if not snapping to start and distinct from last point)
        var previewWorld = new List<PointD>(_polygonDraftPoints);
        if (!_polygonSnapToStart)
        {
            var last = _polygonDraftPoints[^1];
            if (double.Hypot(_polygonDraftCursorWorld.X - last.X, _polygonDraftCursorWorld.Y - last.Y) > 1e-2)
            {
                previewWorld.Add(_polygonDraftCursorWorld);
            }
        }

        var previewScreen = previewWorld.Select(p =>
        {
            var sp = WorldToScreen(p);
            return new PointD(sp.X, sp.Y);
        }).ToList();

        if (previewScreen.Count >= 3)
        {
            var flags = Enumerable.Repeat(_isDrawingCurvedPolygon, previewScreen.Count).ToList();
            var segments = PolygonGeometry.BuildSegments(previewScreen, flags, 0.5, isClosed: true);
            var previewFillColor = ColorHelper.FromHex(style.Fill);
            var translucentFill = new SolidColorBrush(Color.FromArgb((byte)(previewFillColor.A * 0.25), previewFillColor.R, previewFillColor.G, previewFillColor.B));

            var previewPath = new XamlPath
            {
                Data = BuildPolygonSegmentsGeometry(segments, isClosed: true),
                Fill = style.IsFilled ? translucentFill : null,
                Stroke = accentBrush,
                StrokeThickness = 1.75,
                StrokeLineJoin = PenLineJoin.Round,
                IsHitTestVisible = false,
            };
            OverlayCanvas.Children.Add(previewPath);
        }
        else if (previewScreen.Count == 2)
        {
            var line = new Line
            {
                X1 = previewScreen[0].X,
                Y1 = previewScreen[0].Y,
                X2 = previewScreen[1].X,
                Y2 = previewScreen[1].Y,
                Stroke = accentBrush,
                StrokeThickness = 1.75,
                StrokeDashArray = new DoubleCollection { 4, 3 },
                IsHitTestVisible = false,
            };
            OverlayCanvas.Children.Add(line);
        }

        // Draw placed vertex markers
        for (var i = 0; i < _polygonDraftPoints.Count; i++)
        {
            var sp = WorldToScreen(_polygonDraftPoints[i]);
            var isStart = i == 0;
            if (isStart && _polygonSnapToStart)
            {
                var snapRing = new Ellipse
                {
                    Width = 20,
                    Height = 20,
                    Fill = new SolidColorBrush(Color.FromArgb(64, 30, 144, 255)),
                    Stroke = accentBrush,
                    StrokeThickness = 2,
                    IsHitTestVisible = false,
                };
                Canvas.SetLeft(snapRing, sp.X - 10);
                Canvas.SetTop(snapRing, sp.Y - 10);
                OverlayCanvas.Children.Add(snapRing);
            }

            AddVertexHandleVisual(sp, _isDrawingCurvedPolygon, isSelected: isStart, isHovered: isStart && _polygonSnapToStart);
        }
    }

    // ---------------------------------------------------------------- vertex editing mode

    private void EnterPolygonEditMode(PolygonObject polygon)
    {
        var layer = _viewModel.Document.FindLayer(polygon.LayerId);
        if (polygon.IsLocked || !polygon.IsVisible || layer is null || layer.IsLocked || !layer.IsVisible)
        {
            return;
        }

        CancelPolygonDraft();
        if (_tool != EditorTool.Select)
        {
            SetTool(EditorTool.Select);
        }

        if (_viewModel.Selection.Count != 1 || !ReferenceEquals(_viewModel.Selection[0], polygon))
        {
            _viewModel.Select(polygon, additive: false);
        }

        _editingPolygon = polygon;
        _selectedVertexIndex = -1;
        _hoveredVertexIndex = -1;
        _hoveredEdgeHit = null;
        SyncEditVerticesToggle();
        _viewModel.SetStatus("頂點編輯模式：拖曳頂點、點擊邊線加點、雙擊頂點切換尖角／平滑、右鍵或 Delete 刪點，Esc 完成");
        RenderOverlay();
    }

    private void ExitPolygonEditMode()
    {
        if (_editingPolygon is null)
        {
            return;
        }

        _editingPolygon = null;
        _selectedVertexIndex = -1;
        _hoveredVertexIndex = -1;
        _hoveredEdgeHit = null;
        _editingVertexWorldPoints.Clear();
        _editingVertexSmoothFlags.Clear();
        SyncEditVerticesToggle();
        RenderOverlay();
        UpdateCursor();
    }

    private void EnsureEditingPolygonStillValid()
    {
        if (_editingPolygon is null)
        {
            return;
        }

        if (_viewModel.Selection.Count != 1 || !ReferenceEquals(_viewModel.Selection[0], _editingPolygon))
        {
            _editingPolygon = null;
            _selectedVertexIndex = -1;
            _hoveredVertexIndex = -1;
            _hoveredEdgeHit = null;
            SyncEditVerticesToggle();
            return;
        }

        if (_selectedVertexIndex >= _editingPolygon.Vertices.Count)
        {
            _selectedVertexIndex = -1;
        }

        if (_hoveredVertexIndex >= _editingPolygon.Vertices.Count)
        {
            _hoveredVertexIndex = -1;
        }
    }

    private void SyncEditVerticesToggle()
    {
        if (EditVerticesToggle is null)
        {
            return;
        }

        var prev = _isApplyingInspector;
        _isApplyingInspector = true;
        EditVerticesToggle.IsChecked = _editingPolygon is not null;
        _isApplyingInspector = prev;
    }

    private int HitTestPolygonVertex(PolygonObject polygon, Point screenPoint)
    {
        var worldVertices = PolygonGeometry.GetWorldVertices(polygon);
        var bestIndex = -1;
        var bestDist = PolygonVertexHitRadius;

        for (var i = 0; i < worldVertices.Count; i++)
        {
            var sp = WorldToScreen(worldVertices[i]);
            var dist = double.Hypot(screenPoint.X - sp.X, screenPoint.Y - sp.Y);
            if (dist <= bestDist)
            {
                bestDist = dist;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private (int EdgeIndex, PointD WorldPoint)? HitTestPolygonEdge(PolygonObject polygon, Point screenPoint)
    {
        var world = ScreenToWorld(screenPoint);
        var local = polygon.ToLocal(world);
        var segments = PolygonGeometry.BuildSegments(polygon);
        var hit = PolygonGeometry.FindClosestEdge(local, segments);
        if (hit is null)
        {
            return null;
        }

        var hitWorld = polygon.Transform.LocalToWorld(hit.Value.Point);
        var hitScreen = WorldToScreen(hitWorld);
        var screenDist = double.Hypot(screenPoint.X - hitScreen.X, screenPoint.Y - hitScreen.Y);
        if (screenDist > PolygonEdgeHitRadius)
        {
            return null;
        }

        return (hit.Value.EdgeIndex, hitWorld);
    }

    private bool HandlePolygonEditRightClick(Point screenPoint)
    {
        if (_editingPolygon is null)
        {
            return false;
        }

        var vertexIndex = HitTestPolygonVertex(_editingPolygon, screenPoint);
        if (vertexIndex < 0)
        {
            return false;
        }

        DeletePolygonVertex(_editingPolygon, vertexIndex);
        return true;
    }

    private bool HandlePolygonEditPointerPressed(Point screenPoint, Pointer pointer)
    {
        if (_editingPolygon is null)
        {
            return false;
        }

        var polygon = _editingPolygon;
        var vertexIndex = HitTestPolygonVertex(polygon, screenPoint);
        if (vertexIndex >= 0)
        {
            _selectedVertexIndex = vertexIndex;
            BeginPolygonVertexDrag(polygon, vertexIndex, pointer);
            RenderOverlay();
            return true;
        }

        var edgeHit = HitTestPolygonEdge(polygon, screenPoint);
        if (edgeHit is not null)
        {
            var insertIndex = edgeHit.Value.EdgeIndex + 1;
            var worldPoints = PolygonGeometry.GetWorldVertices(polygon);
            var smoothFlags = polygon.Vertices.Select(v => v.IsSmooth).ToList();
            var segStartSmooth = smoothFlags[edgeHit.Value.EdgeIndex];
            var segEndSmooth = smoothFlags[(edgeHit.Value.EdgeIndex + 1) % smoothFlags.Count];
            var newIsSmooth = segStartSmooth || segEndSmooth;

            _gestureBeforeJson = DocumentSerializer.SerializeCompact(_viewModel.Document);
            _gestureChanged = true;

            worldPoints.Insert(insertIndex, edgeHit.Value.WorldPoint);
            smoothFlags.Insert(insertIndex, newIsSmooth);
            PolygonGeometry.ApplyWorldVertices(polygon, worldPoints, smoothFlags);
            UpdatePolygonVisualImmediate(polygon);

            _selectedVertexIndex = insertIndex;
            _hoveredVertexIndex = insertIndex;
            _hoveredEdgeHit = null;
            _editingVertexWorldPoints = worldPoints;
            _editingVertexSmoothFlags = smoothFlags;
            _dragMode = DragMode.PolygonVertex;
            _suppressNextPolygonDoubleTap = true;
            CanvasHost.CapturePointer(pointer);
            RenderOverlay();
            return true;
        }

        var world = ScreenToWorld(screenPoint);
        if (polygon.HitTest(world))
        {
            _selectedVertexIndex = -1;
            RenderOverlay();
            return true;
        }

        ExitPolygonEditMode();
        return false;
    }

    private void BeginPolygonVertexDrag(PolygonObject polygon, int vertexIndex, Pointer pointer)
    {
        _gestureBeforeJson = DocumentSerializer.SerializeCompact(_viewModel.Document);
        _gestureChanged = false;
        _editingVertexWorldPoints = PolygonGeometry.GetWorldVertices(polygon);
        _editingVertexSmoothFlags = polygon.Vertices.Select(v => v.IsSmooth).ToList();
        _selectedVertexIndex = vertexIndex;
        _hoveredEdgeHit = null;
        _dragMode = DragMode.PolygonVertex;
        CanvasHost.CapturePointer(pointer);
    }

    private void DragPolygonVertex(PointD worldPoint)
    {
        if (_editingPolygon is null || _selectedVertexIndex < 0 || _selectedVertexIndex >= _editingVertexWorldPoints.Count)
        {
            return;
        }

        var target = worldPoint;
        if (_settings.Current.SnapToGrid)
        {
            target = new PointD(
                GridSnap.Snap(target.X, GridSize, SnapThreshold),
                GridSnap.Snap(target.Y, GridSize, SnapThreshold));
        }

        var prev = _editingVertexWorldPoints[_selectedVertexIndex];
        if (Math.Abs(target.X - prev.X) < 1e-4 && Math.Abs(target.Y - prev.Y) < 1e-4)
        {
            return;
        }

        _editingVertexWorldPoints[_selectedVertexIndex] = target;
        PolygonGeometry.ApplyWorldVertices(_editingPolygon, _editingVertexWorldPoints, _editingVertexSmoothFlags);
        _gestureChanged = true;
        UpdatePolygonVisualImmediate(_editingPolygon);
        RenderOverlay();
    }

    private void FinishPolygonVertexDrag()
    {
        if (_gestureChanged && _gestureBeforeJson is not null)
        {
            _viewModel.History.Push(_gestureBeforeJson);
            _viewModel.MarkDirty();
        }

        _gestureBeforeJson = null;
        _gestureChanged = false;
        _editingVertexWorldPoints.Clear();
        _editingVertexSmoothFlags.Clear();
    }

    private void UpdatePolygonEditHover(Point screenPoint)
    {
        if (_editingPolygon is null)
        {
            return;
        }

        var prevVertex = _hoveredVertexIndex;
        var prevEdge = _hoveredEdgeHit;

        _hoveredVertexIndex = HitTestPolygonVertex(_editingPolygon, screenPoint);
        _hoveredEdgeHit = _hoveredVertexIndex >= 0
            ? null
            : HitTestPolygonEdge(_editingPolygon, screenPoint);

        if (_hoveredVertexIndex >= 0)
        {
            CanvasHost.SetCursor(InputSystemCursorShape.Hand);
        }
        else if (_hoveredEdgeHit is not null)
        {
            CanvasHost.SetCursor(InputSystemCursorShape.Cross);
        }
        else
        {
            CanvasHost.SetCursor(InputSystemCursorShape.Arrow);
        }

        var edgeChanged = (prevEdge is null) != (_hoveredEdgeHit is null)
            || (prevEdge is not null && _hoveredEdgeHit is not null &&
                (prevEdge.Value.EdgeIndex != _hoveredEdgeHit.Value.EdgeIndex ||
                 double.Hypot(prevEdge.Value.WorldPoint.X - _hoveredEdgeHit.Value.WorldPoint.X,
                     prevEdge.Value.WorldPoint.Y - _hoveredEdgeHit.Value.WorldPoint.Y) > 0.5));

        if (prevVertex != _hoveredVertexIndex || edgeChanged)
        {
            RenderOverlay();
        }
    }

    private void TogglePolygonVertexSmooth(PolygonObject polygon, int vertexIndex)
    {
        if (vertexIndex < 0 || vertexIndex >= polygon.Vertices.Count)
        {
            return;
        }

        _viewModel.PushUndo();
        polygon.Vertices[vertexIndex].IsSmooth = !polygon.Vertices[vertexIndex].IsSmooth;
        _selectedVertexIndex = vertexIndex;
        _viewModel.MarkDirty();
        _viewModel.SetStatus(polygon.Vertices[vertexIndex].IsSmooth ? "頂點已切換為平滑曲線" : "頂點已切換為直線尖角");
    }

    private void DeletePolygonVertex(PolygonObject polygon, int vertexIndex)
    {
        if (vertexIndex < 0 || vertexIndex >= polygon.Vertices.Count)
        {
            return;
        }

        if (polygon.Vertices.Count <= 3)
        {
            _viewModel.SetStatus("多邊形至少需要保留 3 個頂點");
            return;
        }

        _viewModel.PushUndo();
        var worldPoints = PolygonGeometry.GetWorldVertices(polygon);
        var smoothFlags = polygon.Vertices.Select(v => v.IsSmooth).ToList();
        worldPoints.RemoveAt(vertexIndex);
        smoothFlags.RemoveAt(vertexIndex);
        PolygonGeometry.ApplyWorldVertices(polygon, worldPoints, smoothFlags);
        _selectedVertexIndex = Math.Min(vertexIndex, polygon.Vertices.Count - 1);
        _hoveredVertexIndex = -1;
        _hoveredEdgeHit = null;
        _viewModel.MarkDirty();
    }

    private void ConvertPolygonVertices(bool smooth)
    {
        if (PrimarySelection is not PolygonObject polygon)
        {
            return;
        }

        if (polygon.Vertices.All(v => v.IsSmooth == smooth))
        {
            return;
        }

        _viewModel.PushUndo();
        foreach (var vertex in polygon.Vertices)
        {
            vertex.IsSmooth = smooth;
        }

        _viewModel.MarkDirty();
        _viewModel.SetStatus(smooth ? "已將全部頂點轉為平滑曲線" : "已將全部頂點轉為直線尖角");
    }

    private void UpdatePolygonVisualImmediate(PolygonObject polygon)
    {
        if (_visuals.TryGetValue(polygon.Id, out var visual))
        {
            UpdateVisual(visual, polygon);
        }
    }

    private void RenderPolygonEditOverlay(PolygonObject polygon)
    {
        HideSelectionActions();
        var accentBrush = new SolidColorBrush(Colors.DodgerBlue);

        // Draw screen-space contour guide
        var segments = PolygonGeometry.BuildSegments(polygon);
        var screenSegments = new List<PolygonSegment>(segments.Count);
        foreach (var seg in segments)
        {
            var s = WorldToScreen(polygon.Transform.LocalToWorld(seg.Start));
            var e = WorldToScreen(polygon.Transform.LocalToWorld(seg.End));
            if (seg.IsCurve)
            {
                var c1 = WorldToScreen(polygon.Transform.LocalToWorld(seg.Control1));
                var c2 = WorldToScreen(polygon.Transform.LocalToWorld(seg.Control2));
                screenSegments.Add(new PolygonSegment(
                    new PointD(s.X, s.Y),
                    new PointD(e.X, e.Y),
                    true,
                    new PointD(c1.X, c1.Y),
                    new PointD(c2.X, c2.Y)));
            }
            else
            {
                screenSegments.Add(new PolygonSegment(
                    new PointD(s.X, s.Y),
                    new PointD(e.X, e.Y),
                    false,
                    new PointD(s.X, s.Y),
                    new PointD(e.X, e.Y)));
            }
        }

        var guidePath = new XamlPath
        {
            Data = BuildPolygonSegmentsGeometry(screenSegments, isClosed: true),
            Fill = null,
            Stroke = accentBrush,
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 4, 3 },
            IsHitTestVisible = false,
        };
        OverlayCanvas.Children.Add(guidePath);

        // Draw hovered edge insertion preview point
        if (_hoveredEdgeHit is not null && _dragMode == DragMode.None)
        {
            var sp = WorldToScreen(_hoveredEdgeHit.Value.WorldPoint);
            var insertPreview = new Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = new SolidColorBrush(Color.FromArgb(180, 30, 144, 255)),
                Stroke = new SolidColorBrush(Colors.White),
                StrokeThickness = 1.5,
                IsHitTestVisible = false,
            };
            Canvas.SetLeft(insertPreview, sp.X - 5);
            Canvas.SetTop(insertPreview, sp.Y - 5);
            OverlayCanvas.Children.Add(insertPreview);
        }

        // Draw vertex handles (square = sharp corner, circle = smooth curve)
        var worldVertices = PolygonGeometry.GetWorldVertices(polygon);
        for (var i = 0; i < worldVertices.Count; i++)
        {
            var sp = WorldToScreen(worldVertices[i]);
            var isSmooth = polygon.Vertices[i].IsSmooth;
            var isSelected = i == _selectedVertexIndex;
            var isHovered = i == _hoveredVertexIndex;
            AddVertexHandleVisual(sp, isSmooth, isSelected, isHovered);
        }
    }

    private void AddVertexHandleVisual(Point screenPoint, bool isSmooth, bool isSelected, bool isHovered)
    {
        var size = isSelected || isHovered ? 12.0 : 10.0;
        var half = size / 2.0;
        var accent = new SolidColorBrush(Colors.DodgerBlue);
        var fill = isSelected
            ? accent
            : isHovered
                ? new SolidColorBrush(Color.FromArgb(255, 219, 234, 254))
                : new SolidColorBrush(Colors.White);
        var stroke = isSelected ? new SolidColorBrush(Colors.White) : accent;

        Shape handle = isSmooth
            ? new Ellipse
            {
                Width = size,
                Height = size,
                Fill = fill,
                Stroke = stroke,
                StrokeThickness = 2,
                IsHitTestVisible = false,
            }
            : new Rectangle
            {
                Width = size,
                Height = size,
                Fill = fill,
                Stroke = stroke,
                StrokeThickness = 2,
                IsHitTestVisible = false,
            };

        Canvas.SetLeft(handle, screenPoint.X - half);
        Canvas.SetTop(handle, screenPoint.Y - half);
        OverlayCanvas.Children.Add(handle);
    }

    // ---------------------------------------------------------------- inspector events

    private void EditVerticesToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_isApplyingInspector)
        {
            return;
        }

        if (EditVerticesToggle.IsChecked == true && PrimarySelection is PolygonObject polygon)
        {
            EnterPolygonEditMode(polygon);
        }
        else
        {
            ExitPolygonEditMode();
        }
    }

    private void ConvertAllStraight_Click(object sender, RoutedEventArgs e)
    {
        ConvertPolygonVertices(smooth: false);
    }

    private void ConvertAllCurved_Click(object sender, RoutedEventArgs e)
    {
        ConvertPolygonVertices(smooth: true);
    }

    private void PolygonSmoothnessSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isApplyingInspector || PrimarySelection is not PolygonObject polygon)
        {
            return;
        }

        var percent = Math.Clamp(Math.Round(e.NewValue), 0, 100);
        ApplyPolygonSmoothness(polygon, percent);
    }

    private void PolygonSmoothnessSlider_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        // Smoothness changes already push coalesced undo entries in ApplyPolygonSmoothness.
    }

    private void PolygonSmoothnessBox_ValueChanged(object? sender, NumericFieldValueChangedEventArgs args)
    {
        if (_isApplyingInspector || PrimarySelection is not PolygonObject polygon || double.IsNaN(args.Value))
        {
            return;
        }

        var percent = Math.Clamp(Math.Round(args.Value), 0, 100);
        ApplyPolygonSmoothness(polygon, percent);
    }

    private void ApplyPolygonSmoothness(PolygonObject polygon, double percent)
    {
        var smoothness = percent / 100.0;
        if (Math.Abs(polygon.Smoothness - smoothness) < 1e-4)
        {
            return;
        }

        _viewModel.PushUndo("polygonSmoothness");
        polygon.Smoothness = smoothness;

        _isApplyingInspector = true;
        PolygonSmoothnessSlider.Value = percent;
        PolygonSmoothnessBox.Value = percent;
        PolygonSmoothnessValueText.Text = $"{percent:0}%";
        _isApplyingInspector = false;

        _viewModel.MarkDirty();
    }
}

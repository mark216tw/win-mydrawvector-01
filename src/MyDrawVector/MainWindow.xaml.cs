using System.Numerics;
using Microsoft.Extensions.Logging;
using Microsoft.Windows.ApplicationModel.Resources;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using MyDrawVector.Core.Geometry;
using MyDrawVector.Core.Models;
using MyDrawVector.Controls;
using MyDrawVector.Helpers;
using MyDrawVector.Services;
using MyDrawVector.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using WinRT.Interop;
using PlatformFileOpenPicker = Microsoft.Windows.Storage.Pickers.FileOpenPicker;
using PlatformFileSavePicker = Microsoft.Windows.Storage.Pickers.FileSavePicker;
using XamlPath = Microsoft.UI.Xaml.Shapes.Path;
using IOPath = System.IO.Path;
using PointD = MyDrawVector.Core.Models.PointD;
using ColorHelper = MyDrawVector.Helpers.ColorHelper;

namespace MyDrawVector;

public sealed partial class MainWindow : Window
{
    private const double MinObjectSize = 8;
    private const double HandleRadius = 9;
    private const double GridSize = 20;
    private const double MajorGridSize = 100;
    private const double SnapThreshold = 8;
    private const double MinimumVisiblePage = 40;
    private const int VkOemPlus = 0xBB;
    private const int VkOemMinus = 0xBD;
    private const string MoreShapesGlyph = "\uED58";

    private static readonly Dictionary<EditorTool, string> MoreShapeIconTemplates = new()
    {
        [EditorTool.RoundedRectangle] = "RoundedRectangleToolIconTemplate",
        [EditorTool.Arrow] = "ArrowToolIconTemplate",
        [EditorTool.Triangle] = "TriangleToolIconTemplate",
        [EditorTool.Diamond] = "DiamondToolIconTemplate",
        [EditorTool.Pentagon] = "PentagonToolIconTemplate",
        [EditorTool.Hexagon] = "HexagonToolIconTemplate",
        [EditorTool.Star] = "StarToolIconTemplate",
        [EditorTool.SpeechBubble] = "SpeechBubbleToolIconTemplate",
    };

    private static readonly string[] CuratedFonts =
    [
        "Microsoft JhengHei",
        "Microsoft JhengHei UI",
        "標楷體",
        "新細明體",
        "微軟雅黑",
        "Segoe UI",
        "Arial",
        "Times New Roman",
        "Calibri",
        "Verdana",
        "Comic Sans MS",
        "Consolas",
    ];

    private static readonly string[] AccentResourceKeys =
    [
        "SystemAccentColor",
        "SystemAccentColorLight1",
        "SystemAccentColorLight2",
        "SystemAccentColorLight3",
        "SystemAccentColorDark1",
        "SystemAccentColorDark2",
        "SystemAccentColorDark3",
        "AccentFillColorDefaultBrush",
        "AccentFillColorSecondaryBrush",
        "AccentFillColorTertiaryBrush",
        "AccentFillColorDisabledBrush",
        "AccentTextFillColorPrimaryBrush",
        "AccentTextFillColorSecondaryBrush",
        "AccentTextFillColorTertiaryBrush",
        "AccentTextFillColorDisabledBrush",
        "AccentFillColorSelectedTextBackgroundBrush",
        "TextOnAccentFillColorSelectedTextBrush",
        "TextOnAccentFillColorPrimaryBrush",
        "TextOnAccentFillColorSecondaryBrush",
        "TextOnAccentFillColorDisabledBrush",
        "AccentButtonForeground",
        "AccentButtonForegroundPointerOver",
        "AccentButtonForegroundPressed",
        "AccentButtonForegroundDisabled",
    ];

    private readonly EditorViewModel _viewModel;
    private readonly FileService _fileService;
    private readonly ImageAssetStore _assets;
    private readonly FileAssociationService _association;
    private readonly SettingsService _settings;
    private readonly AutosaveService _autosave;
    private readonly ILogger<MainWindow> _logger;
    private readonly ResourceLoader _resourceLoader = new();
    private readonly AccessibilitySettings _accessibilitySettings = new();
    private readonly AppWindow _appWindow;
    private readonly DispatcherTimer _autosaveTimer;
    private readonly DispatcherTimer _deleteToastTimer;
    private readonly Dictionary<string, FrameworkElement> _visuals = new(StringComparer.Ordinal);
    private readonly Dictionary<EditorTool, ToggleButton> _toolButtons = new();
    private readonly Dictionary<string, Button> _canvasBackgroundButtons = new(StringComparer.Ordinal);
    private readonly List<string> _fonts = [.. CuratedFonts];

    private EditorTool _tool = EditorTool.Select;
    private DragMode _dragMode;
    private double _zoom = 1;
    private Vector2 _pan;
    private bool _hasFitted;
    private bool _isLoaded;
    private bool _isApplyingInspector;
    private DateTime _colorGuardUntil;
    private string _themeColor = AppThemePalette.DefaultId;
    private bool _isApplyingZoom;
    private bool _isPanning;
    private bool _isClosingPrompt;
    private bool _isAutosaving;
    private bool _isPickingImage;
    private bool _allowClose;
    private readonly UISettings _uiSettings = new();
    private readonly RectangleGeometry _objectClip = new();
    private readonly RectangleGeometry _previewClip = new();
    private string _themeMode = "System";
    private bool _gestureChanged;
    private string? _startupFile;
    private string? _gestureBeforeJson;
    private int _lastAutosavedVersion = -1;
    private double _gridWidth = -1;
    private double _gridHeight = -1;
    private bool _gridShowGrid;
    private bool _gridInitialized;
    private Size _selectionActionsSize;
    private bool _selectionActionsMulti;
    private double _selectionActionsScale = -1;
    private bool _selectionActionsMeasured;
    private string _pencilStroke = "#FF1F2937";
    private double _pencilStrokeWidth = 3;
    private string _pencilDash = "solid";
    private bool _suppressInspectorAutoSwitch;

    private Point _panStart;
    private Vector2 _panOrigin;
    private Point _dragStartScreen;
    private PointD _dragStartWorld;
    private int _resizeHandle = -1;
    private double _startAngle;
    private double _startRotation;
    private List<(SceneObject Object, ObjectTransform Transform)> _dragStartStates = [];
    private SceneObject? _dragObject;
    private List<SceneObject> _objectClipboard = [];
    private List<PointD> _pencilWorldPoints = [];
    private XamlPath? _previewShape;
    private Polyline? _previewPolyline;
    private Rectangle? _marqueePreview;
    private bool _marqueeAdditive;

    public MainWindow(
        EditorViewModel viewModel,
        FileService fileService,
        ImageAssetStore assets,
        FileAssociationService association,
        SettingsService settings,
        AutosaveService autosave,
        ILogger<MainWindow> logger)
    {
        _viewModel = viewModel;
        _fileService = fileService;
        _assets = assets;
        _association = association;
        _settings = settings;
        _autosave = autosave;
        _logger = logger;
        _pencilStroke = settings.Current.PencilStroke;
        _pencilStrokeWidth = settings.Current.PencilStrokeWidth;
        _pencilDash = settings.Current.PencilDash;
        _themeMode = settings.Current.ThemeMode;
        _themeColor = settings.Current.ThemeColor;
        ApplyThemeColor();

        InitializeComponent();
        InitializeToolButtons();
        InitializeFillPresetColors();
        InitializeCanvasBackgroundColors();
        _isApplyingZoom = true;
        ZoomSlider.Maximum = 800;
        ZoomSlider.Minimum = 10;
        ZoomSlider.Value = 100;
        _isApplyingZoom = false;

        _appWindow = GetAppWindow();
        _autosaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _autosaveTimer.Tick += AutosaveTimer_Tick;
        _deleteToastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
        _deleteToastTimer.Tick += (_, _) => HideDeleteToast();
        InitializeWindow();
        InitializeEvents();

        _uiSettings.ColorValuesChanged += OnSystemColorValuesChanged;
        ApplyTheme();
        FontFamilyCombo.ItemsSource = _fonts;
        BuildRecentFilesMenu();
        SetAccessibleNames();

        UpdateCursor();

        _viewModel.DocumentChanged += (_, _) => RenderAll();
        _viewModel.SelectionChanged += (_, _) =>
        {
            RenderOverlay();
            UpdateInspector();
            if (!_suppressInspectorAutoSwitch && _viewModel.Selection.Count > 0)
            {
                InspectorTabs.SelectedItem = InspectorObjectTab;
            }

            UpdateChrome();
        };

        RenderAll();
    }

    public async Task OpenFileFromStartupAsync(string path)
    {
        if (_isLoaded)
        {
            await OpenPathAsync(path, confirm: false);
        }
        else
        {
            _startupFile = path;
        }
    }

    public async Task OpenFileFromActivationAsync(string path)
    {
        if (_isLoaded)
        {
            await OpenPathAsync(path, confirm: true);
        }
        else
        {
            _startupFile = path;
        }
    }

    private AppWindow GetAppWindow()
    {
        var handle = WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(handle);
        return AppWindow.GetFromWindowId(windowId);
    }

    private void InitializeWindow()
    {
        try
        {
            var iconPath = IOPath.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
            if (File.Exists(iconPath))
            {
                _appWindow.SetIcon(iconPath);
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Unable to set window icon");
        }

        var area = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        var width = Math.Clamp(_settings.Current.WindowWidth, 1024, Math.Max(1024, area.Width));
        var height = Math.Clamp(_settings.Current.WindowHeight, 700, Math.Max(700, area.Height));
        var x = _settings.Current.WindowX < 0
            ? area.X + ((area.Width - width) / 2)
            : Math.Clamp(_settings.Current.WindowX, area.X, area.X + Math.Max(0, area.Width - width));
        var y = _settings.Current.WindowY < 0
            ? area.Y + ((area.Height - height) / 2)
            : Math.Clamp(_settings.Current.WindowY, area.Y, area.Y + Math.Max(0, area.Height - height));

        _appWindow.MoveAndResize(new RectInt32(x, y, width, height));
        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = 1024;
            presenter.PreferredMinimumHeight = 700;
            if (_settings.Current.IsWindowMaximized)
            {
                presenter.Maximize();
            }
        }
    }

    private void InitializeEvents()
    {
        _appWindow.Closing += OnAppWindowClosing;
        Closed += OnWindowClosed;
    }

    private void SetAccessibleNames()
    {
        AutomationProperties.SetName(OpacitySlider, "物件透明度");
    }

    private void InitializeToolButtons()
    {
        _toolButtons[EditorTool.Select] = SelectToolButton;
        _toolButtons[EditorTool.Rectangle] = RectangleToolButton;
        _toolButtons[EditorTool.RoundedRectangle] = RoundedRectangleToolButton;
        _toolButtons[EditorTool.Ellipse] = EllipseToolButton;
        _toolButtons[EditorTool.Line] = LineToolButton;
        _toolButtons[EditorTool.Arrow] = ArrowToolButton;
        _toolButtons[EditorTool.Triangle] = TriangleToolButton;
        _toolButtons[EditorTool.Diamond] = DiamondToolButton;
        _toolButtons[EditorTool.Pentagon] = PentagonToolButton;
        _toolButtons[EditorTool.Hexagon] = HexagonToolButton;
        _toolButtons[EditorTool.Star] = StarToolButton;
        _toolButtons[EditorTool.SpeechBubble] = SpeechBubbleToolButton;
        _toolButtons[EditorTool.Text] = TextToolButton;
        _toolButtons[EditorTool.Image] = ImageToolButton;
        _toolButtons[EditorTool.Pencil] = PencilToolButton;
        _toolButtons[EditorTool.Pan] = PanToolButton;

        foreach (var (tool, button) in _toolButtons)
        {
            if (TryCreateMoreShapeIcon(tool, out var icon))
            {
                button.Content = icon;
            }
        }
    }

    private void InitializeFillPresetColors()
    {
        BuildPresetColorGrid(FillPresetGrid, PresetFillColor_Click);
        BuildPresetColorGrid(StrokePresetGrid, PresetStrokeColor_Click);
        BuildPresetColorGrid(PencilPresetGrid, PresetPencilColor_Click);
        BuildPresetColorGrid(TextPresetGrid, PresetTextColor_Click);
    }

    private void BuildPresetColorGrid(Grid target, RoutedEventHandler handler)
    {
        for (var index = 0; index < FillColorPalette.All.Count; index++)
        {
            var option = FillColorPalette.All[index];
            var name = _resourceLoader.GetString(option.NameResourceKey);
            var button = new Button
            {
                Width = 32,
                Height = 32,
                Padding = new Thickness(6),
                Tag = option.Id,
                Content = new Border
                {
                    Width = 20,
                    Height = 20,
                    Background = new SolidColorBrush(option.Color),
                    CornerRadius = new CornerRadius(4),
                },
            };
            button.Click += handler;
            AutomationProperties.SetName(button, name);
            ToolTipService.SetToolTip(button, name);
            Grid.SetRow(button, index / 5);
            Grid.SetColumn(button, index % 5);
            target.Children.Add(button);
        }
    }

    private void InitializeCanvasBackgroundColors()
    {
        for (var index = 0; index < CanvasBackgroundPalette.All.Count; index++)
        {
            var option = CanvasBackgroundPalette.All[index];
            var name = _resourceLoader.GetString(option.NameResourceKey);
            var button = new Button
            {
                Width = 38,
                Height = 38,
                Padding = new Thickness(6),
                Background = new SolidColorBrush(Colors.Transparent),
                BorderBrush = new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(6),
                Tag = option.Id,
                Content = CreateCanvasBackgroundSwatch(option),
            };
            button.Click += CanvasBackground_Click;
            AutomationProperties.SetName(button, name);
            ToolTipService.SetToolTip(button, name);
            Grid.SetRow(button, index / 5);
            Grid.SetColumn(button, index % 5);
            _canvasBackgroundButtons[option.Id] = button;
            CanvasBackgroundGrid.Children.Add(button);
        }

        UpdateCanvasBackgroundSelection();
    }

    private static FrameworkElement CreateCanvasBackgroundSwatch(CanvasBackgroundPalette.Option option)
    {
        if (option.Color.A != 0)
        {
            return new Border
            {
                Width = 24,
                Height = 24,
                Background = new SolidColorBrush(option.Color),
                BorderBrush = new SolidColorBrush(Color.FromArgb(64, 0, 0, 0)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
            };
        }

        var checker = new Canvas { Width = 24, Height = 24 };
        var light = new SolidColorBrush(CanvasBackgroundPalette.CheckerLightColor);
        var dark = new SolidColorBrush(CanvasBackgroundPalette.CheckerDarkColor);
        for (var row = 0; row < 2; row++)
        {
            for (var column = 0; column < 2; column++)
            {
                var square = new Rectangle
                {
                    Width = 12,
                    Height = 12,
                    Fill = (row + column) % 2 == 0 ? light : dark,
                };
                Canvas.SetLeft(square, column * 12);
                Canvas.SetTop(square, row * 12);
                checker.Children.Add(square);
            }
        }

        return new Border
        {
            Width = 24,
            Height = 24,
            Child = checker,
            BorderBrush = new SolidColorBrush(Color.FromArgb(64, 0, 0, 0)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
        };
    }

    private void UpdateCanvasBackgroundSelection()
    {
        var selected = CanvasBackgroundPalette.Match(_viewModel.Document.Canvas.Background);
        var accent = new SolidColorBrush(AppThemePalette.Get(_themeColor).Color);
        foreach (var (id, button) in _canvasBackgroundButtons)
        {
            button.BorderBrush = selected?.Id == id
                ? accent
                : new SolidColorBrush(Colors.Transparent);
        }
    }

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_allowClose || !_viewModel.IsDirty || _isClosingPrompt)
        {
            return;
        }

        args.Cancel = true;
        _ = PromptCloseAsync();
    }

    private async Task PromptCloseAsync()
    {
        if (_isClosingPrompt)
        {
            return;
        }

        _isClosingPrompt = true;
        try
        {
            if (await ConfirmSaveAsync())
            {
                _autosave.Delete();
                _allowClose = true;
                Close();
            }
        }
        finally
        {
            _isClosingPrompt = false;
        }
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        _uiSettings.ColorValuesChanged -= OnSystemColorValuesChanged;
        _autosaveTimer.Stop();
        if (!_viewModel.IsDirty)
        {
            _autosave.Delete();
        }

        var presenterState = (_appWindow.Presenter as OverlappedPresenter)?.State;
        if (presenterState == OverlappedPresenterState.Maximized)
        {
            _settings.Current.IsWindowMaximized = true;
        }
        else
        {
            _settings.Current.IsWindowMaximized = false;
            var size = _appWindow.Size;
            var position = _appWindow.Position;
            _settings.Current.WindowWidth = size.Width;
            _settings.Current.WindowHeight = size.Height;
            _settings.Current.WindowX = position.X;
            _settings.Current.WindowY = position.Y;
        }

        _settings.Current.PencilStroke = _pencilStroke;
        _settings.Current.PencilStrokeWidth = _pencilStrokeWidth;
        _settings.Current.PencilDash = _pencilDash;
        _settings.Save();
    }

    private async void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        ApplyTheme();
        RenderAll();
        DispatcherQueue.TryEnqueue(() =>
        {
            FitToPage();
            FocusSink.Focus(FocusState.Programmatic);
        });

        _autosaveTimer.Start();
        var recovered = await TryRecoverAutosaveAsync();
        if (!recovered && !string.IsNullOrWhiteSpace(_startupFile))
        {
            var path = _startupFile;
            _startupFile = null;
            _ = OpenPathAsync(path, confirm: false);
        }
    }

    private ElementTheme CurrentDialogTheme => _themeMode switch
    {
        "Dark" => ElementTheme.Dark,
        "Light" => ElementTheme.Light,
        _ => ElementTheme.Default,
    };

    private void ApplyTheme()
    {
        var theme = CurrentDialogTheme;
        RootGrid.RequestedTheme = theme;
        FillColorPicker.RequestedTheme = theme;
        StrokeColorPicker.RequestedTheme = theme;
        TextColorPicker.RequestedTheme = theme;
        ApplyThemeColor();
        if (_canvasBackgroundButtons.Count > 0)
        {
            UpdateCanvasBackgroundSelection();
        }
    }

    private void ApplyThemeColor()
    {
        var resources = Application.Current.Resources;
        foreach (var key in AccentResourceKeys)
        {
            resources.Remove(key);
        }

        if (_accessibilitySettings.HighContrast)
        {
            return;
        }

        var color = AppThemePalette.Get(_themeColor).Color;
        var foreground = UsesDarkForeground(color) ? Colors.Black : Colors.White;
        foreach (var mergedDictionary in resources.MergedDictionaries)
        {
            if (mergedDictionary is not XamlControlsResources controlsResources)
            {
                continue;
            }

            UpdateAccentThemeDictionary(controlsResources.ThemeDictionaries["Default"] as ResourceDictionary, color, foreground, isLight: false);
            UpdateAccentThemeDictionary(controlsResources.ThemeDictionaries["Light"] as ResourceDictionary, color, foreground, isLight: true);
            break;
        }
    }

    private static void UpdateAccentThemeDictionary(ResourceDictionary? resources, Color color, Color foreground, bool isLight)
    {
        if (resources is null)
        {
            return;
        }

        var accentText = isLight ? Blend(color, Colors.Black, 0.2) : Blend(color, Colors.White, 0.35);
        SetBrushColor(resources, "SystemControlBackgroundAccentBrush", color);
        SetBrushColor(resources, "SystemControlHighlightListAccentVeryHighBrush", color);
        SetBrushColor(resources, "SystemControlHighlightListAccentMediumLowBrush", color);
        SetBrushColor(resources, "AccentFillColorSelectedTextBackgroundBrush", color);
        SetBrushColor(resources, "AccentFillColorDefaultBrush", color);
        SetBrushColor(resources, "AccentFillColorSecondaryBrush", color);
        SetBrushColor(resources, "AccentFillColorTertiaryBrush", color);
        SetBrushColor(resources, "AccentFillColorDisabledBrush", WithAlpha(color, 102));
        SetBrushColor(resources, "AccentTextFillColorPrimaryBrush", accentText);
        SetBrushColor(resources, "AccentTextFillColorSecondaryBrush", WithAlpha(accentText, 230));
        SetBrushColor(resources, "AccentTextFillColorTertiaryBrush", WithAlpha(accentText, 204));
        SetBrushColor(resources, "AccentTextFillColorDisabledBrush", WithAlpha(accentText, 102));
        SetBrushColor(resources, "TextOnAccentFillColorSelectedTextBrush", foreground);
        SetBrushColor(resources, "TextOnAccentFillColorPrimaryBrush", foreground);
        SetBrushColor(resources, "TextOnAccentFillColorSecondaryBrush", WithAlpha(foreground, 179));
        SetBrushColor(resources, "TextOnAccentFillColorDisabledBrush", WithAlpha(foreground, 128));
    }

    private static void SetBrushColor(ResourceDictionary resources, string key, Color color)
    {
        if (resources.TryGetValue(key, out var value) && value is SolidColorBrush brush)
        {
            brush.Color = color;
        }
    }

    private static Color Blend(Color source, Color target, double amount)
    {
        return Color.FromArgb(
            source.A,
            (byte)Math.Round(source.R + ((target.R - source.R) * amount)),
            (byte)Math.Round(source.G + ((target.G - source.G) * amount)),
            (byte)Math.Round(source.B + ((target.B - source.B) * amount)));
    }

    private static Color WithAlpha(Color color, byte alpha)
    {
        return Color.FromArgb(alpha, color.R, color.G, color.B);
    }

    private static bool UsesDarkForeground(Color color)
    {
        var luminance = ((0.299 * color.R) + (0.587 * color.G) + (0.114 * color.B)) / 255;
        return luminance > 0.62;
    }

    private void OnSystemColorValuesChanged(UISettings sender, object args)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_themeMode == "System")
            {
                ApplyTheme();
            }
        });
    }

    // ---------------------------------------------------------------- rendering

    private void RenderAll()
    {
        if (!_isLoaded)
        {
            return;
        }

        var document = _viewModel.Document;
        UpdatePage(document);
        var liveIds = new HashSet<string>(StringComparer.Ordinal);
        var visualIndex = 0;
        foreach (var item in document.OrderedObjects())
        {
            liveIds.Add(item.Id);
            var visual = GetOrCreateVisual(item);
            UpdateVisual(visual, item);
            var layer = document.FindLayer(item.LayerId);
            visual.Visibility = item.IsVisible && (layer?.IsVisible ?? true)
                ? Visibility.Visible
                : Visibility.Collapsed;
            Canvas.SetZIndex(visual, visualIndex++);
        }

        foreach (var id in _visuals.Keys.Where(id => !liveIds.Contains(id)).ToList())
        {
            ObjectCanvas.Children.Remove(_visuals[id]);
            _visuals.Remove(id);
        }

        CanvasHint.Visibility = document.Objects.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        RenderOverlay();
        UpdateInspector();
        UpdateChrome();
    }

    private void UpdatePage(Document document)
    {
        var width = Math.Max(1, document.Canvas.Width);
        var height = Math.Max(1, document.Canvas.Height);
        SceneRoot.Width = width;
        SceneRoot.Height = height;

        PageShadow.Width = width;
        PageShadow.Height = height;
        Canvas.SetLeft(PageShadow, 7);
        Canvas.SetTop(PageShadow, 7);

        PageBackground.Width = width;
        PageBackground.Height = height;
        Canvas.SetLeft(PageBackground, 0);
        Canvas.SetTop(PageBackground, 0);
        PageBackground.Background = ColorHelper.Brush(document.Canvas.Background);

        ObjectCanvas.Width = width;
        ObjectCanvas.Height = height;
        _objectClip.Rect = new Rect(0, 0, width, height);
        if (!ReferenceEquals(ObjectCanvas.Clip, _objectClip))
        {
            ObjectCanvas.Clip = _objectClip;
        }

        UpdateGrid(width, height);
        UpdateCheckerboard(width, height);
    }

    private void UpdateCheckerboard(double width, double height)
    {
        if (ColorHelper.FromHex(_viewModel.Document.Canvas.Background).A != 0)
        {
            CheckerCanvas.Visibility = Visibility.Collapsed;
            return;
        }

        var needsRebuild =
            Math.Abs(CheckerCanvas.Width - width) > 0.5 ||
            Math.Abs(CheckerCanvas.Height - height) > 0.5;
        CheckerCanvas.Width = width;
        CheckerCanvas.Height = height;
        CheckerCanvas.Visibility = Visibility.Visible;
        if (!needsRebuild && CheckerCanvas.Children.Count > 0)
        {
            return;
        }

        CheckerCanvas.Children.Clear();
        const double Square = 40;
        var light = new SolidColorBrush(CanvasBackgroundPalette.CheckerLightColor);
        var dark = new SolidColorBrush(CanvasBackgroundPalette.CheckerDarkColor);
        for (var y = 0.0; y < height; y += Square)
        {
            for (var x = 0.0; x < width; x += Square)
            {
                var isDark = ((int)(x / Square) + (int)(y / Square)) % 2 == 1;
                var square = new Rectangle
                {
                    Width = Square,
                    Height = Square,
                    Fill = isDark ? dark : light,
                    IsHitTestVisible = false,
                };
                Canvas.SetLeft(square, x);
                Canvas.SetTop(square, y);
                CheckerCanvas.Children.Add(square);
            }
        }
    }

    private void UpdateGrid(double width, double height)
    {
        var showGrid = _settings.Current.ShowGrid;
        if (_gridInitialized &&
            Math.Abs(_gridWidth - width) < 0.5 &&
            Math.Abs(_gridHeight - height) < 0.5 &&
            _gridShowGrid == showGrid)
        {
            return;
        }

        _gridInitialized = true;
        _gridWidth = width;
        _gridHeight = height;
        _gridShowGrid = showGrid;

        GridCanvas.Children.Clear();
        GridCanvas.Width = width;
        GridCanvas.Height = height;
        if (!showGrid)
        {
            return;
        }

        var minorBrush = new SolidColorBrush(Color.FromArgb(26, 96, 96, 96));
        var majorBrush = new SolidColorBrush(Color.FromArgb(56, 96, 96, 96));
        for (var x = GridSize; x < width; x += GridSize)
        {
            var isMajor = Math.Abs(x % MajorGridSize) < 0.001;
            GridCanvas.Children.Add(new Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = height,
                Stroke = isMajor ? majorBrush : minorBrush,
                StrokeThickness = isMajor ? 1.2 : 1,
                IsHitTestVisible = false,
            });
        }

        for (var y = GridSize; y < height; y += GridSize)
        {
            var isMajor = Math.Abs(y % MajorGridSize) < 0.001;
            GridCanvas.Children.Add(new Line
            {
                X1 = 0,
                Y1 = y,
                X2 = width,
                Y2 = y,
                Stroke = isMajor ? majorBrush : minorBrush,
                StrokeThickness = isMajor ? 1.2 : 1,
                IsHitTestVisible = false,
            });
        }
    }

    private FrameworkElement GetOrCreateVisual(SceneObject item)
    {
        if (_visuals.TryGetValue(item.Id, out var existing))
        {
            return existing;
        }

        FrameworkElement visual = item switch
        {
            ShapeObject shape => CreateShapeVisual(shape),
            TextObject => new TextBlock { TextWrapping = TextWrapping.Wrap, IsHitTestVisible = false },
            ImageObject => new Image { Stretch = Stretch.Fill, IsHitTestVisible = false },
            PathObject => new XamlPath { IsHitTestVisible = false },
            _ => new Border { IsHitTestVisible = false },
        };

        visual.IsHitTestVisible = false;
        ObjectCanvas.Children.Add(visual);
        _visuals[item.Id] = visual;
        return visual;
    }

    private static FrameworkElement CreateShapeVisual(ShapeObject shape)
    {
        return shape.Kind switch
        {
            ShapeKind.Rectangle or ShapeKind.RoundedRectangle => new Rectangle { IsHitTestVisible = false },
            ShapeKind.Ellipse => new Ellipse { IsHitTestVisible = false },
            _ => new XamlPath { IsHitTestVisible = false },
        };
    }

    private void UpdateVisual(FrameworkElement visual, SceneObject item)
    {
        visual.Opacity = Math.Clamp(item.Opacity, 0, 1);
        visual.Visibility = item.IsVisible ? Visibility.Visible : Visibility.Collapsed;
        visual.RenderTransformOrigin = new Point(0, 0);
        visual.RenderTransform = new MatrixTransform { Matrix = ToXamlMatrix(item.Transform.ToMatrix()) };

        switch (item)
        {
            case ShapeObject shape when visual is Rectangle rectangle:
                UpdateRectangle(rectangle, shape);
                break;
            case ShapeObject shape when visual is Ellipse ellipse:
                UpdateEllipse(ellipse, shape);
                break;
            case ShapeObject shape when visual is XamlPath path:
                UpdateShapePath(path, shape);
                break;
            case TextObject text when visual is TextBlock textBlock:
                UpdateTextBlock(textBlock, text);
                break;
            case ImageObject image when visual is Image imageControl:
                UpdateImage(imageControl, image);
                break;
            case PathObject freehand when visual is XamlPath path:
                UpdateFreehandPath(path, freehand);
                break;
        }
    }

    private void UpdateRectangle(Rectangle rectangle, ShapeObject shape)
    {
        rectangle.Width = shape.Transform.Width;
        rectangle.Height = shape.Transform.Height;
        rectangle.RadiusX = shape.Kind == ShapeKind.RoundedRectangle ? shape.CornerRadius : 0;
        rectangle.RadiusY = rectangle.RadiusX;
        ApplyShapeStyle(rectangle, shape);
    }

    private void UpdateEllipse(Ellipse ellipse, ShapeObject shape)
    {
        ellipse.Width = shape.Transform.Width;
        ellipse.Height = shape.Transform.Height;
        ApplyShapeStyle(ellipse, shape);
    }

    private void ApplyShapeStyle(Shape shape, ShapeObject model)
    {
        shape.Fill = model.IsFilled ? ColorHelper.Brush(model.Fill) : null;
        shape.Stroke = model.IsStroked ? ColorHelper.Brush(model.Stroke) : null;
        shape.StrokeThickness = Math.Max(0, model.StrokeWidth);
        shape.StrokeDashArray = model.Dash switch
        {
            "dash" => new DoubleCollection { 4, 2 },
            "dot" => new DoubleCollection { 1, 1 },
            _ => null,
        };
        shape.StrokeStartLineCap = PenLineCap.Round;
        shape.StrokeEndLineCap = PenLineCap.Round;
        shape.StrokeLineJoin = PenLineJoin.Round;
        shape.StrokeDashCap = PenLineCap.Round;
    }

    private void UpdateShapePath(XamlPath path, ShapeObject shape)
    {
        path.Data = BuildShapeGeometry(shape);
        path.Fill = shape.IsFilled ? ColorHelper.Brush(shape.Fill) : null;
        path.Stroke = shape.IsStroked ? ColorHelper.Brush(shape.Stroke) : null;
        path.StrokeThickness = Math.Max(0, shape.StrokeWidth);
        path.StrokeDashArray = shape.Dash switch
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

    private void UpdateTextBlock(TextBlock block, TextObject model)
    {
        block.Width = model.Transform.Width;
        block.Height = model.Transform.Height;
        block.Text = model.Text;
        block.FontFamily = new FontFamily(model.FontFamily);
        block.FontSize = Math.Clamp(model.FontSize, 6, 300);
        block.FontWeight = ToFontWeight(GetEffectiveFontWeight(model));
        block.FontStyle = model.Italic ? Windows.UI.Text.FontStyle.Italic : Windows.UI.Text.FontStyle.Normal;
        block.TextDecorations = model.Underline ? Windows.UI.Text.TextDecorations.Underline : Windows.UI.Text.TextDecorations.None;
        block.Foreground = ColorHelper.Brush(model.Color);
        block.TextAlignment = model.Align switch
        {
            TextAlign.Center => TextAlignment.Center,
            TextAlign.Right => TextAlignment.Right,
            _ => TextAlignment.Left,
        };
        block.TextWrapping = TextWrapping.Wrap;
        block.LineHeight = Math.Max(1, model.FontSize * model.LineSpacing);
        block.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
        block.VerticalAlignment = VerticalAlignment.Top;
        block.IsTextSelectionEnabled = false;
    }

    private static int GetEffectiveFontWeight(TextObject text)
    {
        return text.FontWeight > 0 ? text.FontWeight : text.Bold ? 700 : 400;
    }

    private static int GetFontWeightIndex(int weight)
    {
        return weight switch
        {
            <= 300 => 0,
            <= 400 => 1,
            <= 600 => 2,
            <= 700 => 3,
            _ => 4,
        };
    }

    private static Windows.UI.Text.FontWeight ToFontWeight(int weight)
    {
        return weight switch
        {
            <= 300 => Microsoft.UI.Text.FontWeights.Light,
            <= 400 => Microsoft.UI.Text.FontWeights.Normal,
            <= 600 => Microsoft.UI.Text.FontWeights.SemiBold,
            <= 700 => Microsoft.UI.Text.FontWeights.Bold,
            _ => Microsoft.UI.Text.FontWeights.Black,
        };
    }

    private void UpdateImage(Image image, ImageObject model)
    {
        image.Width = model.Transform.Width;
        image.Height = model.Transform.Height;
        var asset = _assets.Find(model.AssetId);
        image.Source = asset?.Bitmap;
        image.Opacity = asset is null ? 0.25 : 1;
    }

    private void UpdateFreehandPath(XamlPath path, PathObject model)
    {
        path.Data = BuildFreehandGeometry(model);
        path.Fill = null;
        path.Stroke = ColorHelper.Brush(model.Stroke);
        path.StrokeThickness = Math.Max(0.5, model.StrokeWidth);
        path.StrokeStartLineCap = PenLineCap.Round;
        path.StrokeEndLineCap = PenLineCap.Round;
        path.StrokeLineJoin = PenLineJoin.Round;
        path.StrokeDashArray = model.Dash switch
        {
            "dash" => new DoubleCollection { 4, 2 },
            "dot" => new DoubleCollection { 1, 1 },
            _ => null,
        };
    }

    private static Geometry BuildShapeGeometry(ShapeObject shape)
    {
        var width = Math.Max(1, shape.Transform.Width);
        var height = Math.Max(1, shape.Transform.Height);
        var points = new List<Point>();

        switch (shape.Kind)
        {
            case ShapeKind.Triangle:
                points.Add(new Point(width / 2, 0));
                points.Add(new Point(width, height));
                points.Add(new Point(0, height));
                break;
            case ShapeKind.Diamond:
                points.Add(new Point(width / 2, 0));
                points.Add(new Point(width, height / 2));
                points.Add(new Point(width / 2, height));
                points.Add(new Point(0, height / 2));
                break;
            case ShapeKind.Pentagon:
                AddRegularPolygon(points, width, height, 5);
                break;
            case ShapeKind.Hexagon:
                AddRegularPolygon(points, width, height, 6);
                break;
            case ShapeKind.Star:
                AddStar(points, width, height, 5);
                break;
            case ShapeKind.Line:
                return BuildLineGeometry(width, height, arrow: false);
            case ShapeKind.Arrow:
                return BuildLineGeometry(width, height, arrow: true);
            case ShapeKind.SpeechBubble:
                points.Add(new Point(0, 0));
                points.Add(new Point(width, 0));
                points.Add(new Point(width, height * 0.78));
                points.Add(new Point(width * 0.42, height * 0.78));
                points.Add(new Point(width * 0.28, height));
                points.Add(new Point(width * 0.28, height * 0.78));
                points.Add(new Point(0, height * 0.78));
                break;
            default:
                points.Add(new Point(0, 0));
                points.Add(new Point(width, 0));
                points.Add(new Point(width, height));
                points.Add(new Point(0, height));
                break;
        }

        return CreatePolygonGeometry(points, closed: true);
    }

    private static Geometry BuildLineGeometry(double width, double height, bool arrow)
    {
        var figure = new PathFigure
        {
            StartPoint = new Point(0, 0),
            IsClosed = false,
            IsFilled = false,
        };
        figure.Segments.Add(new LineSegment { Point = new Point(width, height) });

        if (arrow)
        {
            var dx = width;
            var dy = height;
            var length = Math.Sqrt((dx * dx) + (dy * dy));
            if (length > 0.001)
            {
                var ux = dx / length;
                var uy = dy / length;
                var head = Math.Min(16, length * 0.35);
                var px = -uy;
                var py = ux;
                var tip = new Point(width, height);
                var left = new Point(
                    tip.X - (ux * head) + (px * head * 0.55),
                    tip.Y - (uy * head) + (py * head * 0.55));
                var right = new Point(
                    tip.X - (ux * head) - (px * head * 0.55),
                    tip.Y - (uy * head) - (py * head * 0.55));
                figure.Segments.Add(new LineSegment { Point = left });
                figure.Segments.Add(new LineSegment { Point = tip });
                figure.Segments.Add(new LineSegment { Point = right });
            }
        }

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }

    private static Geometry BuildFreehandGeometry(PathObject path)
    {
        var width = Math.Max(1, path.Transform.Width);
        var height = Math.Max(1, path.Transform.Height);
        if (path.Points.Count == 0)
        {
            return new PathGeometry();
        }

        var figure = new PathFigure
        {
            StartPoint = new Point(path.Points[0].X * width, path.Points[0].Y * height),
            IsClosed = false,
            IsFilled = false,
        };
        for (var i = 1; i < path.Points.Count; i++)
        {
            figure.Segments.Add(new LineSegment
            {
                Point = new Point(path.Points[i].X * width, path.Points[i].Y * height),
            });
        }

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }

    private static Geometry CreatePolygonGeometry(IReadOnlyList<Point> points, bool closed)
    {
        var geometry = new PathGeometry();
        if (points.Count == 0)
        {
            return geometry;
        }

        var figure = new PathFigure
        {
            StartPoint = points[0],
            IsClosed = closed,
            IsFilled = closed,
        };
        for (var i = 1; i < points.Count; i++)
        {
            figure.Segments.Add(new LineSegment { Point = points[i] });
        }

        geometry.Figures.Add(figure);
        return geometry;
    }

    private static void AddRegularPolygon(List<Point> points, double width, double height, int count)
    {
        var rx = width / 2;
        var ry = height / 2;
        for (var i = 0; i < count; i++)
        {
            var angle = (-Math.PI / 2) + (i * 2 * Math.PI / count);
            points.Add(new Point(rx + (rx * Math.Cos(angle)), ry + (ry * Math.Sin(angle))));
        }
    }

    private static void AddStar(List<Point> points, double width, double height, int pointsCount)
    {
        var rx = width / 2;
        var ry = height / 2;
        for (var i = 0; i < pointsCount * 2; i++)
        {
            var angle = (-Math.PI / 2) + (i * Math.PI / pointsCount);
            var factor = i % 2 == 0 ? 1.0 : 0.42;
            points.Add(new Point(rx + (rx * factor * Math.Cos(angle)), ry + (ry * factor * Math.Sin(angle))));
        }
    }

    private static Microsoft.UI.Xaml.Media.Matrix ToXamlMatrix(Matrix3x2 matrix)
    {
        return new Microsoft.UI.Xaml.Media.Matrix(
            matrix.M11,
            matrix.M12,
            matrix.M21,
            matrix.M22,
            matrix.M31,
            matrix.M32);
    }

    // ---------------------------------------------------------------- view and overlay

    private Matrix3x2 ViewMatrix => Matrix3x2.CreateScale((float)_zoom) * Matrix3x2.CreateTranslation(_pan.X, _pan.Y);

    private PointD ScreenToWorld(Point point)
    {
        if (!Matrix3x2.Invert(ViewMatrix, out var inverse))
        {
            return new PointD(0, 0);
        }

        var result = Vector2.Transform(new Vector2((float)point.X, (float)point.Y), inverse);
        return new PointD(result.X, result.Y);
    }

    private Point WorldToScreen(PointD point)
    {
        var result = Vector2.Transform(new Vector2((float)point.X, (float)point.Y), ViewMatrix);
        return new Point(result.X, result.Y);
    }

    private void ApplyView()
    {
        ConstrainPan();
        SceneRoot.RenderTransformOrigin = new Point(0, 0);
        SceneRoot.RenderTransform = new MatrixTransform { Matrix = ToXamlMatrix(ViewMatrix) };

        var canvas = _viewModel.Document.Canvas;
        _previewClip.Rect = new Rect(
            _pan.X,
            _pan.Y,
            Math.Max(1, canvas.Width * _zoom),
            Math.Max(1, canvas.Height * _zoom));
        if (!ReferenceEquals(PreviewCanvas.Clip, _previewClip))
        {
            PreviewCanvas.Clip = _previewClip;
        }
        _isApplyingZoom = true;
        ZoomSlider.Value = Math.Clamp(_zoom * 100, ZoomSlider.Minimum, ZoomSlider.Maximum);
        _isApplyingZoom = false;
        ZoomText.Text = $"{_zoom * 100:0}%";
        RenderOverlay();
        UpdateChrome();
    }

    private void ConstrainPan()
    {
        if (CanvasHost.ActualWidth <= 0 || CanvasHost.ActualHeight <= 0)
        {
            return;
        }

        var canvas = _viewModel.Document.Canvas;
        var scaledWidth = Math.Max(1, canvas.Width * _zoom);
        var scaledHeight = Math.Max(1, canvas.Height * _zoom);
        var visibleWidth = Math.Min(MinimumVisiblePage, scaledWidth);
        var visibleHeight = Math.Min(MinimumVisiblePage, scaledHeight);
        _pan.X = (float)Math.Clamp(
            _pan.X,
            visibleWidth - scaledWidth,
            CanvasHost.ActualWidth - visibleWidth);
        _pan.Y = (float)Math.Clamp(
            _pan.Y,
            visibleHeight - scaledHeight,
            CanvasHost.ActualHeight - visibleHeight);
    }

    private void FitToPage()
    {
        if (CanvasHost.ActualWidth < 20 || CanvasHost.ActualHeight < 20)
        {
            return;
        }

        var document = _viewModel.Document;
        var availableWidth = Math.Max(40, CanvasHost.ActualWidth - 100);
        var availableHeight = Math.Max(40, CanvasHost.ActualHeight - 100);
        _zoom = Math.Clamp(
            Math.Min(availableWidth / Math.Max(1, document.Canvas.Width), availableHeight / Math.Max(1, document.Canvas.Height)),
            0.1,
            2.0);
        _pan = new Vector2(
            (float)((CanvasHost.ActualWidth - (document.Canvas.Width * _zoom)) / 2),
            (float)((CanvasHost.ActualHeight - (document.Canvas.Height * _zoom)) / 2));
        _hasFitted = true;
        ApplyView();
    }

    private void RenderOverlay()
    {
        OverlayCanvas.Children.Clear();
        var selection = _viewModel.Selection;
        if (selection.Count == 0)
        {
            HideSelectionActions();
            return;
        }

        var selectionBrush = new SolidColorBrush(Colors.DodgerBlue);
        if (selection.Count > 1)
        {
            var bounds = _viewModel.GetSelectionBounds();
            var topLeft = WorldToScreen(new PointD(bounds.X, bounds.Y));
            var bottomRight = WorldToScreen(new PointD(bounds.Right, bounds.Bottom));
            var outline = new Rectangle
            {
                Width = Math.Max(1, bottomRight.X - topLeft.X),
                Height = Math.Max(1, bottomRight.Y - topLeft.Y),
                Stroke = selectionBrush,
                StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection { 4, 3 },
                Fill = null,
                IsHitTestVisible = false,
            };
            Canvas.SetLeft(outline, topLeft.X);
            Canvas.SetTop(outline, topLeft.Y);
            OverlayCanvas.Children.Add(outline);
            PositionSelectionActions();
            return;
        }

        var selected = selection[0];
        var selectedLayer = _viewModel.Document.FindLayer(selected.LayerId);
        if (selected.IsLocked || !selected.IsVisible || selectedLayer is null || selectedLayer.IsLocked || !selectedLayer.IsVisible)
        {
            HideSelectionActions();
            return;
        }

        var corners = selected.Transform.GetWorldCorners().Select(WorldToScreen).ToList();
        var polygon = new Polygon
        {
            Stroke = selectionBrush,
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 4, 3 },
            Fill = null,
            IsHitTestVisible = false,
        };
        foreach (var point in corners)
        {
            polygon.Points.Add(point);
        }

        OverlayCanvas.Children.Add(polygon);

        var handles = GetHandleScreenPoints(selected);
        foreach (var point in handles)
        {
            AddHandleVisual(point);
        }

        var rotation = GetRotationHandleScreenPoints(selected);
        if (rotation is not null)
        {
            var (topCenter, rotatePoint) = rotation.Value;
            var rotationLine = new Line
            {
                X1 = topCenter.X,
                Y1 = topCenter.Y,
                X2 = rotatePoint.X,
                Y2 = rotatePoint.Y,
                Stroke = selectionBrush,
                StrokeThickness = 1.5,
                IsHitTestVisible = false,
            };
            OverlayCanvas.Children.Add(rotationLine);

            var rotateHandle = new Ellipse
            {
                Width = 14,
                Height = 14,
                Fill = new SolidColorBrush(Colors.White),
                Stroke = selectionBrush,
                StrokeThickness = 2,
                IsHitTestVisible = false,
            };
            Canvas.SetLeft(rotateHandle, rotatePoint.X - 7);
            Canvas.SetTop(rotateHandle, rotatePoint.Y - 7);
            OverlayCanvas.Children.Add(rotateHandle);
        }

        PositionSelectionActions();
    }

    private void PositionSelectionActions()
    {
        if (!_viewModel.HasSelection || _dragMode != DragMode.None || _isPanning)
        {
            HideSelectionActions();
            return;
        }

        var multi = _viewModel.Selection.Count > 1;
        var multiVisibility = multi ? Visibility.Collapsed : Visibility.Visible;
        ActionRotateButton.Visibility = multiVisibility;
        ActionFlipHorizontalButton.Visibility = multiVisibility;
        ActionFlipVerticalButton.Visibility = multiVisibility;

        var bounds = _viewModel.GetSelectionBounds();
        var topLeft = WorldToScreen(new PointD(bounds.X, bounds.Y));
        var topRight = WorldToScreen(new PointD(bounds.Right, bounds.Y));
        var bottomLeft = WorldToScreen(new PointD(bounds.X, bounds.Bottom));

        SelectionActions.Visibility = Visibility.Visible;
        var scale = RootGrid.XamlRoot?.RasterizationScale ?? 1;
        if (!_selectionActionsMeasured ||
            _selectionActionsMulti != multi ||
            Math.Abs(_selectionActionsScale - scale) > 0.001)
        {
            SelectionActions.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            _selectionActionsSize = SelectionActions.DesiredSize;
            _selectionActionsMulti = multi;
            _selectionActionsScale = scale;
            _selectionActionsMeasured = true;
        }

        var size = _selectionActionsSize;
        var centerX = (topLeft.X + topRight.X) / 2;
        var left = Math.Clamp(
            centerX - (size.Width / 2),
            8,
            Math.Max(8, CanvasHost.ActualWidth - size.Width - 8));
        var top = bottomLeft.Y + 12;
        if (top + size.Height > CanvasHost.ActualHeight - 8)
        {
            top = Math.Max(8, topLeft.Y - size.Height - 12);
        }

        Canvas.SetLeft(SelectionActions, left);
        Canvas.SetTop(SelectionActions, top);
    }

    private void HideSelectionActions()
    {
        SelectionActions.Visibility = Visibility.Collapsed;
    }

    private void AddHandleVisual(Point point)
    {
        var handle = new Rectangle
        {
            Width = 9,
            Height = 9,
            RadiusX = 1,
            RadiusY = 1,
            Fill = new SolidColorBrush(Colors.White),
            Stroke = new SolidColorBrush(Colors.DodgerBlue),
            StrokeThickness = 1.5,
            IsHitTestVisible = false,
        };
        Canvas.SetLeft(handle, point.X - 4.5);
        Canvas.SetTop(handle, point.Y - 4.5);
        OverlayCanvas.Children.Add(handle);
    }

    private Point[] GetHandleScreenPoints(SceneObject item)
    {
        var transform = item.Transform;
        var local = new[]
        {
            new PointD(0, 0),
            new PointD(transform.Width / 2, 0),
            new PointD(transform.Width, 0),
            new PointD(transform.Width, transform.Height / 2),
            new PointD(transform.Width, transform.Height),
            new PointD(transform.Width / 2, transform.Height),
            new PointD(0, transform.Height),
            new PointD(0, transform.Height / 2),
        };

        return local.Select(point => WorldToScreen(transform.LocalToWorld(point))).ToArray();
    }

    private (Point Anchor, Point Handle)? GetRotationHandleScreenPoints(SceneObject item)
    {
        var transform = item.Transform;
        var offset = 24 / Math.Max(0.1, _zoom);
        var anchorY = transform.FlipV ? transform.Height : 0;
        var handleY = transform.FlipV ? transform.Height + offset : -offset;
        var anchor = WorldToScreen(transform.LocalToWorld(new PointD(transform.Width / 2, anchorY)));
        var handle = WorldToScreen(transform.LocalToWorld(new PointD(transform.Width / 2, handleY)));
        return (anchor, handle);
    }

    private int HitTestHandle(SceneObject item, Point screenPoint)
    {
        if (item.IsLocked)
        {
            return -1;
        }

        var handles = GetHandleScreenPoints(item);
        for (var i = 0; i < handles.Length; i++)
        {
            if (Distance(handles[i], screenPoint) <= HandleRadius)
            {
                return i;
            }
        }

        var rotate = GetRotationHandleScreenPoints(item);
        if (rotate is not null && Distance(rotate.Value.Handle, screenPoint) <= HandleRadius + 3)
        {
            return 8;
        }

        return -1;
    }

    private static double Distance(Point a, Point b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    // ---------------------------------------------------------------- pointer input

    private void CanvasHost_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        FocusSink.Focus(FocusState.Programmatic);
        var screenPoint = e.GetCurrentPoint(CanvasHost).Position;
        var worldPoint = ScreenToWorld(screenPoint);
        var properties = e.GetCurrentPoint(CanvasHost).Properties;
        var isCtrl = IsKeyDown(VirtualKey.Control);
        var isShift = IsKeyDown(VirtualKey.Shift);

        if (properties.IsMiddleButtonPressed ||
            properties.IsRightButtonPressed ||
            _tool == EditorTool.Pan ||
            IsKeyDown(VirtualKey.Space))
        {
            BeginPan(screenPoint);
            CanvasHost.CapturePointer(e.Pointer);
            e.Handled = true;
            return;
        }

        if (!properties.IsLeftButtonPressed)
        {
            return;
        }

        if (_viewModel.Selection.Count == 1)
        {
            var handle = HitTestHandle(_viewModel.Selection[0], screenPoint);
            if (handle >= 0)
            {
                BeginTransformGesture(handle, screenPoint, worldPoint);
                CanvasHost.CapturePointer(e.Pointer);
                e.Handled = true;
                return;
            }
        }

        var hit = HitTestObject(worldPoint);
        if (hit is not null)
        {
            if (hit.LayerId is not null && _viewModel.Document.FindLayer(hit.LayerId) is { } hitLayer)
            {
                _viewModel.SetCurrentLayer(hitLayer);
            }

            if (_settings.Current.AutoSelectToolOnObjectClick && _tool != EditorTool.Select)
            {
                SetTool(EditorTool.Select);
            }

            if (isCtrl || isShift)
            {
                _viewModel.Select(hit, additive: true);
                e.Handled = true;
                return;
            }

            if (!_viewModel.Selection.Contains(hit))
            {
                _viewModel.Select(hit, additive: false);
            }

            BeginMoveGesture(screenPoint, worldPoint);
            CanvasHost.CapturePointer(e.Pointer);
            e.Handled = true;
            return;
        }

        switch (_tool)
        {
            case EditorTool.Select:
                BeginMarquee(screenPoint, isCtrl || isShift);
                CanvasHost.CapturePointer(e.Pointer);
                e.Handled = true;
                break;
            case EditorTool.Text:
                if (CanCreateOnCurrentLayer())
                {
                    _ = AddTextAtAsync(worldPoint);
                }

                e.Handled = true;
                break;
            case EditorTool.Pencil:
                if (CanCreateOnCurrentLayer())
                {
                    BeginPencil(screenPoint, worldPoint);
                    CanvasHost.CapturePointer(e.Pointer);
                }

                e.Handled = true;
                break;
            case EditorTool.Rectangle:
            case EditorTool.RoundedRectangle:
            case EditorTool.Ellipse:
            case EditorTool.Line:
            case EditorTool.Arrow:
            case EditorTool.Triangle:
            case EditorTool.Diamond:
            case EditorTool.Pentagon:
            case EditorTool.Hexagon:
            case EditorTool.Star:
            case EditorTool.SpeechBubble:
                if (CanCreateOnCurrentLayer())
                {
                    BeginShape(screenPoint, worldPoint);
                    CanvasHost.CapturePointer(e.Pointer);
                }

                e.Handled = true;
                break;
        }
    }

    private void BeginPan(Point screenPoint)
    {
        HideSelectionActions();
        _dragMode = DragMode.Pan;
        _isPanning = true;
        _panStart = screenPoint;
        _panOrigin = _pan;
        CanvasHost.SetCursor(InputSystemCursorShape.SizeAll);
    }

    private bool CanCreateOnCurrentLayer()
    {
        var layer = _viewModel.CurrentLayer;
        if (layer.IsLocked)
        {
            _viewModel.SetStatus("目前圖層已鎖定，請先解鎖");
            return false;
        }

        if (!layer.IsVisible)
        {
            _viewModel.SetStatus("目前圖層已隱藏，請先顯示");
            return false;
        }

        return true;
    }

    private void UpdateCursor()
    {
        CanvasHost.SetCursor(_tool == EditorTool.Pan
            ? InputSystemCursorShape.SizeAll
            : InputSystemCursorShape.Arrow);
    }

    private void CanvasHost_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var screenPoint = e.GetCurrentPoint(CanvasHost).Position;
        var worldPoint = ScreenToWorld(screenPoint);

        switch (_dragMode)
        {
            case DragMode.Pan:
                if (_isPanning)
                {
                    _pan = _panOrigin + new Vector2(
                        (float)(screenPoint.X - _panStart.X),
                        (float)(screenPoint.Y - _panStart.Y));
                    ApplyView();
                }

                break;
            case DragMode.Move:
                MoveSelection(worldPoint);
                break;
            case DragMode.Resize:
                var lockAspect = _dragObject is ImageObject
                    ? !IsKeyDown(VirtualKey.Shift)
                    : IsKeyDown(VirtualKey.Shift);
                ResizeSelection(worldPoint, lockAspect);
                break;
            case DragMode.Rotate:
                RotateSelection(worldPoint, IsKeyDown(VirtualKey.Shift));
                break;
            case DragMode.Marquee:
                UpdateMarquee(screenPoint);
                break;
            case DragMode.Shape:
                UpdateShapePreview(screenPoint);
                break;
            case DragMode.Pencil:
                AppendPencilPoint(screenPoint, worldPoint);
                break;
        }
    }

    private void CanvasHost_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        var screenPoint = e.GetCurrentPoint(CanvasHost).Position;
        var worldPoint = ScreenToWorld(screenPoint);

        switch (_dragMode)
        {
            case DragMode.Move:
            case DragMode.Resize:
            case DragMode.Rotate:
                FinishTransformGesture();
                break;
            case DragMode.Marquee:
                FinishMarquee(screenPoint);
                break;
            case DragMode.Shape:
                FinishShape(worldPoint);
                break;
            case DragMode.Pencil:
                FinishPencil();
                break;
        }

        _dragMode = DragMode.None;
        _isPanning = false;
        CanvasHost.ReleasePointerCapture(e.Pointer);
        PreviewCanvas.Children.Clear();
        RenderOverlay();
        UpdateCursor();
    }

    private void CanvasHost_PointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        CancelInteraction();
        CanvasHost.ReleasePointerCapture(e.Pointer);
        UpdateCursor();
    }

    private void CanvasHost_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        if (_dragMode == DragMode.None)
        {
            return;
        }

        CancelInteraction();
        UpdateCursor();
    }

    private void CancelInteraction()
    {
        if (_gestureBeforeJson is not null)
        {
            _viewModel.SetDocument(
                Core.Serialization.DocumentSerializer.Deserialize(_gestureBeforeJson),
                _viewModel.CurrentFilePath,
                resetHistory: false);
        }

        _dragMode = DragMode.None;
        _isPanning = false;
        _gestureBeforeJson = null;
        _gestureChanged = false;
        PreviewCanvas.Children.Clear();
        RenderAll();
    }

    private void CanvasHost_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(CanvasHost);
        var delta = point.Properties.MouseWheelDelta;
        if (delta == 0)
        {
            return;
        }

        var screenPoint = point.Position;
        var worldBefore = ScreenToWorld(screenPoint);
        var factor = delta > 0 ? 1.12 : 1 / 1.12;
        _zoom = Math.Clamp(_zoom * factor, 0.1, 8.0);
        var screenAfter = WorldToScreen(worldBefore);
        _pan += new Vector2(
            (float)(screenPoint.X - screenAfter.X),
            (float)(screenPoint.Y - screenAfter.Y));

        ApplyView();
        e.Handled = true;
    }

    private void CanvasHost_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_hasFitted && CanvasHost.ActualWidth > 20 && CanvasHost.ActualHeight > 20)
        {
            FitToPage();
        }
        else
        {
            ApplyView();
        }
    }

    private void CanvasHost_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        var world = ScreenToWorld(e.GetPosition(CanvasHost));
        if (HitTestObject(world) is TextObject text)
        {
            if (_viewModel.Document.FindLayer(text.LayerId) is { } layer)
            {
                _viewModel.SetCurrentLayer(layer);
            }

            SetTool(EditorTool.Select);
            _viewModel.Select(text, additive: false);
            _ = EditTextAsync(text);
            e.Handled = true;
        }
    }

    private void CanvasHost_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
        e.DragUIOverride.Caption = "放開以插入圖片或開啟檔案";
        e.DragUIOverride.IsCaptionVisible = true;
    }

    private async void CanvasHost_Drop(object sender, DragEventArgs e)
    {
        try
        {
            var world = ScreenToWorld(e.GetPosition(CanvasHost));
            var items = await e.DataView.GetStorageItemsAsync();
            foreach (var item in items.OfType<StorageFile>())
            {
                if (FileAssociationService.IsSupportedFile(item.Path))
                {
                    await OpenPathAsync(item.Path, confirm: true);
                    return;
                }

                if (IsImageExtension(item.FileType))
                {
                    if (!CanCreateOnCurrentLayer())
                    {
                        return;
                    }

                    var bytes = await ReadStorageFileBytesAsync(item);
                    await AddImageObjectAsync(bytes, item.FileType, world);
                }
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Drop failed");
            await ShowMessageAsync("無法插入", exception.Message);
        }
    }

    // ---------------------------------------------------------------- gestures

    private void BeginTransformGesture(int handle, Point screenPoint, PointD worldPoint)
    {
        HideSelectionActions();
        var item = _viewModel.Selection[0];
        _dragMode = handle == 8 ? DragMode.Rotate : DragMode.Resize;
        _resizeHandle = handle;
        _dragObject = item;
        _dragStartScreen = screenPoint;
        _dragStartWorld = worldPoint;
        _dragStartStates = [(item, item.Transform.Clone())];
        _gestureBeforeJson = Core.Serialization.DocumentSerializer.SerializeCompact(_viewModel.Document);
        _gestureChanged = false;
        _startRotation = item.Transform.Rotation;
        _startAngle = AngleFromCenter(item.Transform, worldPoint);
    }

    private void BeginMoveGesture(Point screenPoint, PointD worldPoint)
    {
        HideSelectionActions();
        _dragMode = DragMode.Move;
        _dragStartScreen = screenPoint;
        _dragStartWorld = worldPoint;
        _dragStartStates = _viewModel.Selection
            .Select(item => (item, item.Transform.Clone()))
            .ToList();
        _gestureBeforeJson = Core.Serialization.DocumentSerializer.SerializeCompact(_viewModel.Document);
        _gestureChanged = false;
    }

    private bool IsSnappingEnabled =>
        _settings.Current.SnapToGrid && !IsKeyDown(VirtualKey.Menu);

    private void MoveSelection(PointD worldPoint)
    {
        var dx = worldPoint.X - _dragStartWorld.X;
        var dy = worldPoint.Y - _dragStartWorld.Y;
        if (IsSnappingEnabled && _dragStartStates.Count > 0)
        {
            var anchor = _dragStartStates[0].Transform;
            dx = GridSnap.Snap(anchor.CenterX + dx, GridSize, SnapThreshold) - anchor.CenterX;
            dy = GridSnap.Snap(anchor.CenterY + dy, GridSize, SnapThreshold) - anchor.CenterY;
        }

        foreach (var (item, start) in _dragStartStates)
        {
            item.Transform.CenterX = start.CenterX + dx;
            item.Transform.CenterY = start.CenterY + dy;
            if (_visuals.TryGetValue(item.Id, out var visual))
            {
                UpdateVisual(visual, item);
            }
        }

        _gestureChanged = true;
        RenderOverlay();
    }

    private void ResizeSelection(PointD worldPoint, bool lockAspect)
    {
        if (_dragObject is null || _dragStartStates.Count == 0)
        {
            return;
        }

        var start = _dragStartStates[0].Transform;
        var matrix = start.ToMatrix();
        if (!Matrix3x2.Invert(matrix, out var inverse))
        {
            return;
        }

        var local = Vector2.Transform(new Vector2((float)worldPoint.X, (float)worldPoint.Y), inverse);
        var newWidth = start.Width;
        var newHeight = start.Height;
        var handle = _resizeHandle;
        var changesX = handle is 0 or 2 or 3 or 4 or 6 or 7;
        var changesY = handle is 0 or 1 or 2 or 4 or 5 or 6;

        if (handle is 0 or 6 or 7)
        {
            newWidth = start.Width - local.X;
        }
        else if (handle is 2 or 3 or 4)
        {
            newWidth = local.X;
        }

        if (handle is 0 or 1 or 2)
        {
            newHeight = start.Height - local.Y;
        }
        else if (handle is 4 or 5 or 6)
        {
            newHeight = local.Y;
        }

        if (lockAspect)
        {
            var scaleX = start.Width > 0 ? newWidth / start.Width : 1;
            var scaleY = start.Height > 0 ? newHeight / start.Height : 1;
            var scale = changesX && changesY
                ? Math.Max(scaleX, scaleY)
                : changesX
                    ? scaleX
                    : scaleY;
            scale = Math.Max(scale, MinObjectSize / Math.Max(1, Math.Min(start.Width, start.Height)));
            newWidth = start.Width * scale;
            newHeight = start.Height * scale;
        }

        if (!changesX)
        {
            newWidth = start.Width;
        }

        if (!changesY)
        {
            newHeight = start.Height;
        }

        if (IsSnappingEnabled)
        {
            newWidth = GridSnap.Snap(newWidth, GridSize, SnapThreshold);
            newHeight = GridSnap.Snap(newHeight, GridSize, SnapThreshold);
        }

        newWidth = Math.Max(MinObjectSize, newWidth);
        newHeight = Math.Max(MinObjectSize, newHeight);

        var deltaX = 0.0;
        var deltaY = 0.0;
        if (handle is 0 or 6 or 7)
        {
            deltaX = (start.Width - newWidth) / 2;
        }
        else if (handle is 2 or 3 or 4)
        {
            deltaX = -(start.Width - newWidth) / 2;
        }

        if (handle is 0 or 1 or 2)
        {
            deltaY = (start.Height - newHeight) / 2;
        }
        else if (handle is 4 or 5 or 6)
        {
            deltaY = -(start.Height - newHeight) / 2;
        }

        var ex = Vector2.TransformNormal(new Vector2(1, 0), matrix);
        var ey = Vector2.TransformNormal(new Vector2(0, 1), matrix);
        var centerDelta = (ex * (float)deltaX) + (ey * (float)deltaY);

        var target = _dragObject.Transform;
        target.Width = newWidth;
        target.Height = newHeight;
        target.CenterX = start.CenterX + centerDelta.X;
        target.CenterY = start.CenterY + centerDelta.Y;

        if (_visuals.TryGetValue(_dragObject.Id, out var visual))
        {
            UpdateVisual(visual, _dragObject);
        }

        _gestureChanged = true;
        RenderOverlay();
    }

    private void RotateSelection(PointD worldPoint, bool snap)
    {
        if (_dragObject is null)
        {
            return;
        }

        var current = AngleFromCenter(_dragObject.Transform, worldPoint);
        var rotation = _startRotation + ((current - _startAngle) * 180 / Math.PI);
        if (snap)
        {
            rotation = Math.Round(rotation / 15) * 15;
        }

        rotation %= 360;
        if (rotation < 0)
        {
            rotation += 360;
        }

        _dragObject.Transform.Rotation = rotation;
        if (_visuals.TryGetValue(_dragObject.Id, out var visual))
        {
            UpdateVisual(visual, _dragObject);
        }

        _gestureChanged = true;
        RenderOverlay();
    }

    private void FinishTransformGesture()
    {
        if (_gestureChanged && _gestureBeforeJson is not null)
        {
            _viewModel.History.Push(_gestureBeforeJson);
            _viewModel.MarkDirty();
        }

        _gestureBeforeJson = null;
        _gestureChanged = false;
        _dragStartStates.Clear();
        _dragObject = null;
        _resizeHandle = -1;
    }

    private void BeginMarquee(Point screenPoint, bool additive)
    {
        HideSelectionActions();
        _dragMode = DragMode.Marquee;
        _dragStartScreen = screenPoint;
        _marqueeAdditive = additive;
        if (!additive)
        {
            _viewModel.ClearSelection();
        }

        _marqueePreview = new Rectangle
        {
            Stroke = new SolidColorBrush(Colors.DodgerBlue),
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 4, 3 },
            Fill = new SolidColorBrush(Color.FromArgb(30, 0, 120, 215)),
            IsHitTestVisible = false,
        };
        PreviewCanvas.Children.Clear();
        PreviewCanvas.Children.Add(_marqueePreview);
    }

    private void UpdateMarquee(Point screenPoint)
    {
        if (_marqueePreview is null)
        {
            return;
        }

        var x = Math.Min(_dragStartScreen.X, screenPoint.X);
        var y = Math.Min(_dragStartScreen.Y, screenPoint.Y);
        var width = Math.Abs(screenPoint.X - _dragStartScreen.X);
        var height = Math.Abs(screenPoint.Y - _dragStartScreen.Y);
        Canvas.SetLeft(_marqueePreview, x);
        Canvas.SetTop(_marqueePreview, y);
        _marqueePreview.Width = width;
        _marqueePreview.Height = height;
    }

    private void FinishMarquee(Point screenPoint)
    {
        var start = ScreenToWorld(_dragStartScreen);
        var end = ScreenToWorld(screenPoint);
        var rect = new RectD(
            Math.Min(start.X, end.X),
            Math.Min(start.Y, end.Y),
            Math.Abs(end.X - start.X),
            Math.Abs(end.Y - start.Y));

        var selected = _viewModel.Document.OrderedObjects()
            .Where(item => item.IsVisible && !item.IsLocked)
            .Where(item =>
            {
                var layer = _viewModel.Document.FindLayer(item.LayerId);
                return layer is not null && layer.IsVisible && !layer.IsLocked;
            })
            .Where(item => Intersects(rect, item.WorldBounds))
            .ToList();

        if (_marqueeAdditive)
        {
            foreach (var item in selected)
            {
                if (!_viewModel.Selection.Contains(item))
                {
                    _viewModel.Selection.Add(item);
                }
            }
        }
        else
        {
            _viewModel.SelectMany(selected);
        }

        _marqueePreview = null;
    }

    private static bool Intersects(RectD a, RectD b)
    {
        return a.X <= b.Right && a.Right >= b.X && a.Y <= b.Bottom && a.Bottom >= b.Y;
    }

    private static double AngleFromCenter(ObjectTransform transform, PointD point)
    {
        return Math.Atan2(point.Y - transform.CenterY, point.X - transform.CenterX);
    }

    // ---------------------------------------------------------------- shape / pencil / text

    private void BeginShape(Point screenPoint, PointD worldPoint)
    {
        HideSelectionActions();
        _dragMode = DragMode.Shape;
        _dragStartScreen = screenPoint;
        _dragStartWorld = worldPoint;
        PreviewCanvas.Children.Clear();
        _previewShape = new XamlPath
        {
            Stroke = new SolidColorBrush(Colors.DodgerBlue),
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 4, 3 },
            Fill = new SolidColorBrush(Color.FromArgb(55, 79, 156, 255)),
            IsHitTestVisible = false,
        };
        PreviewCanvas.Children.Add(_previewShape);
        UpdateShapePreview(screenPoint);
    }

    private void UpdateShapePreview(Point screenPoint)
    {
        if (_previewShape is null)
        {
            return;
        }

        if (_tool is EditorTool.Line or EditorTool.Arrow)
        {
            var geometry = new PathGeometry();
            var figure = new PathFigure
            {
                StartPoint = new Point(_dragStartScreen.X, _dragStartScreen.Y),
                IsClosed = false,
                IsFilled = false,
            };
            figure.Segments.Add(new LineSegment { Point = new Point(screenPoint.X, screenPoint.Y) });
            geometry.Figures.Add(figure);
            _previewShape.Data = geometry;
            Canvas.SetLeft(_previewShape, 0);
            Canvas.SetTop(_previewShape, 0);
            return;
        }

        var x = Math.Min(_dragStartScreen.X, screenPoint.X);
        var y = Math.Min(_dragStartScreen.Y, screenPoint.Y);
        var width = Math.Abs(screenPoint.X - _dragStartScreen.X);
        var height = Math.Abs(screenPoint.Y - _dragStartScreen.Y);
        if (IsKeyDown(VirtualKey.Shift))
        {
            width = Math.Max(width, height);
            height = width;
        }

        var kind = _tool switch
        {
            EditorTool.RoundedRectangle => ShapeKind.RoundedRectangle,
            EditorTool.Ellipse => ShapeKind.Ellipse,
            EditorTool.Triangle => ShapeKind.Triangle,
            EditorTool.Diamond => ShapeKind.Diamond,
            EditorTool.Pentagon => ShapeKind.Pentagon,
            EditorTool.Hexagon => ShapeKind.Hexagon,
            EditorTool.Star => ShapeKind.Star,
            EditorTool.SpeechBubble => ShapeKind.SpeechBubble,
            _ => ShapeKind.Rectangle,
        };

        Canvas.SetLeft(_previewShape, x);
        Canvas.SetTop(_previewShape, y);
        if (kind == ShapeKind.Ellipse)
        {
            _previewShape.Data = new EllipseGeometry
            {
                Center = new Point(width / 2, height / 2),
                RadiusX = width / 2,
                RadiusY = height / 2,
            };
        }
        else if (kind == ShapeKind.Rectangle)
        {
            _previewShape.Data = new RectangleGeometry { Rect = new Rect(0, 0, width, height) };
        }
        else
        {
            var model = new ShapeObject
            {
                Kind = kind,
                Transform = new ObjectTransform { Width = width, Height = height },
            };
            _previewShape.Data = BuildShapeGeometry(model);
        }
    }

    private void FinishShape(PointD worldPoint)
    {
        PreviewCanvas.Children.Clear();
        _previewShape = null;
        var start = _dragStartWorld;
        var width = Math.Abs(worldPoint.X - start.X);
        var height = Math.Abs(worldPoint.Y - start.Y);
        var centerX = (start.X + worldPoint.X) / 2;
        var centerY = (start.Y + worldPoint.Y) / 2;

        if (width < 8 || height < 8)
        {
            width = 160;
            height = 110;
            centerX = start.X;
            centerY = start.Y;
        }

        if (IsSnappingEnabled)
        {
            width = Math.Max(MinObjectSize, GridSnap.Snap(width, GridSize, SnapThreshold));
            height = Math.Max(MinObjectSize, GridSnap.Snap(height, GridSize, SnapThreshold));
            centerX = GridSnap.Snap(centerX, GridSize, SnapThreshold);
            centerY = GridSnap.Snap(centerY, GridSize, SnapThreshold);
        }

        var kind = _tool switch
        {
            EditorTool.Rectangle => ShapeKind.Rectangle,
            EditorTool.RoundedRectangle => ShapeKind.RoundedRectangle,
            EditorTool.Ellipse => ShapeKind.Ellipse,
            EditorTool.Line => ShapeKind.Line,
            EditorTool.Arrow => ShapeKind.Arrow,
            EditorTool.Triangle => ShapeKind.Triangle,
            EditorTool.Diamond => ShapeKind.Diamond,
            EditorTool.Pentagon => ShapeKind.Pentagon,
            EditorTool.Hexagon => ShapeKind.Hexagon,
            EditorTool.Star => ShapeKind.Star,
            EditorTool.SpeechBubble => ShapeKind.SpeechBubble,
            _ => ShapeKind.Rectangle,
        };

        if (IsKeyDown(VirtualKey.Shift) && kind is not (ShapeKind.Line or ShapeKind.Arrow))
        {
            width = Math.Max(width, height);
            height = width;
        }

        var isLine = kind is ShapeKind.Line or ShapeKind.Arrow;
        var style = GetShapeStyle(kind);
        var shape = new ShapeObject
        {
            Kind = kind,
            Fill = style.Fill,
            Stroke = style.Stroke,
            Dash = style.Dash,
            CornerRadius = style.CornerRadius,
            StrokeWidth = isLine ? Math.Max(1, style.StrokeWidth) : style.StrokeWidth,
            IsFilled = !isLine && style.IsFilled,
            IsStroked = isLine || (style.IsStroked && style.StrokeWidth > 0),
            Transform = new ObjectTransform
            {
                CenterX = centerX,
                CenterY = centerY,
                Width = Math.Max(MinObjectSize, width),
                Height = Math.Max(MinObjectSize, height),
                FlipH = isLine && worldPoint.X < start.X,
                FlipV = isLine && worldPoint.Y < start.Y,
            },
        };

        _viewModel.PushUndo();
        _viewModel.AddObject(shape);
        _dragMode = DragMode.None;
    }

    private void BeginPencil(Point screenPoint, PointD worldPoint)
    {
        HideSelectionActions();
        _dragMode = DragMode.Pencil;
        _dragStartScreen = screenPoint;
        _pencilWorldPoints = [worldPoint];
        PreviewCanvas.Children.Clear();
        _previewPolyline = new Polyline
        {
            Stroke = ColorHelper.Brush(_pencilStroke),
            StrokeThickness = Math.Max(1, _pencilStrokeWidth * _zoom),
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            IsHitTestVisible = false,
        };
        _previewPolyline.Points.Add(screenPoint);
        PreviewCanvas.Children.Add(_previewPolyline);
    }

    private void AppendPencilPoint(Point screenPoint, PointD worldPoint)
    {
        if (_pencilWorldPoints.Count == 0)
        {
            return;
        }

        var last = _pencilWorldPoints[^1];
        var distance = Math.Sqrt(
            Math.Pow(worldPoint.X - last.X, 2) + Math.Pow(worldPoint.Y - last.Y, 2));
        if (distance * _zoom < 2)
        {
            return;
        }

        _pencilWorldPoints.Add(worldPoint);
        _previewPolyline?.Points.Add(screenPoint);
    }

    private void FinishPencil()
    {
        PreviewCanvas.Children.Clear();
        _previewPolyline = null;
        if (_pencilWorldPoints.Count < 2)
        {
            if (!CanCreateOnCurrentLayer() || _pencilWorldPoints.Count == 0)
            {
                _pencilWorldPoints.Clear();
                _dragMode = DragMode.None;
                return;
            }

            var dotPoint = _pencilWorldPoints[0];
            var dotSize = Math.Max(2, _pencilStrokeWidth);
            var dot = new ShapeObject
            {
                Name = "筆畫",
                Kind = ShapeKind.Ellipse,
                Fill = _pencilStroke,
                IsStroked = false,
                Transform = new ObjectTransform
                {
                    CenterX = dotPoint.X,
                    CenterY = dotPoint.Y,
                    Width = dotSize,
                    Height = dotSize,
                },
            };
            _viewModel.PushUndo();
            _viewModel.AddObject(dot);
            _pencilWorldPoints.Clear();
            _dragMode = DragMode.None;
            return;
        }

        var minX = _pencilWorldPoints.Min(p => p.X);
        var minY = _pencilWorldPoints.Min(p => p.Y);
        var maxX = _pencilWorldPoints.Max(p => p.X);
        var maxY = _pencilWorldPoints.Max(p => p.Y);
        var width = Math.Max(1, maxX - minX);
        var height = Math.Max(1, maxY - minY);
        var points = _pencilWorldPoints
            .Select(p => new PointD((p.X - minX) / width, (p.Y - minY) / height))
            .ToList();

        var path = new PathObject
        {
            Points = points,
            Stroke = _pencilStroke,
            StrokeWidth = _pencilStrokeWidth,
            Dash = _pencilDash,
            Transform = new ObjectTransform
            {
                CenterX = (minX + maxX) / 2,
                CenterY = (minY + maxY) / 2,
                Width = width,
                Height = height,
            },
        };

        _viewModel.PushUndo();
        _viewModel.AddObject(path);
        _pencilWorldPoints.Clear();
        _dragMode = DragMode.None;
    }

    private async Task AddTextAtAsync(PointD world)
    {
        var text = new TextObject
        {
            Text = "文字",
            Transform = new ObjectTransform
            {
                CenterX = world.X,
                CenterY = world.Y,
                Width = 320,
                Height = 90,
            },
        };
        ApplyTextStyle(text);

        if (!await EditTextAsync(text, commitUndo: false))
        {
            return;
        }

        _viewModel.PushUndo();
        _viewModel.AddObject(text);
    }

    private async Task<bool> EditTextAsync(TextObject text, bool commitUndo = true)
    {
        var editor = new TextBox
        {
            Text = text.Text,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 180,
            MinWidth = 380,
        };

        var dialog = new ContentDialog
        {
            XamlRoot = RootGrid.XamlRoot,
            RequestedTheme = CurrentDialogTheme,
            Title = "編輯文字",
            PrimaryButtonText = "確定",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            Content = editor,
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return false;
        }

        if (editor.Text != text.Text)
        {
            if (commitUndo)
            {
                _viewModel.PushUndo();
            }

            text.Text = editor.Text;
            _viewModel.MarkDirty();
        }

        return true;
    }

    // ---------------------------------------------------------------- selection and hit testing

    private SceneObject? HitTestObject(PointD world)
    {
        foreach (var layer in _viewModel.Document.Layers.OrderByDescending(l => l.Order))
        {
            if (!layer.IsVisible || layer.IsLocked)
            {
                continue;
            }

            var hit = _viewModel.Document.ObjectsInLayer(layer.Id)
                .Reverse()
                .FirstOrDefault(item => item.HitTest(world));
            if (hit is not null)
            {
                return hit;
            }
        }

        return null;
    }

    // ---------------------------------------------------------------- inspector

    private void UpdateInspector()
    {
        var inspectorOffset = InspectorScroll.VerticalOffset;
        _isApplyingInspector = true;
        _colorGuardUntil = DateTime.UtcNow.AddMilliseconds(300);
        try
        {
            var selection = _viewModel.Selection;
            var hasSelection = selection.Count > 0;
            ShowGridToggle.IsOn = _settings.Current.ShowGrid;
            SnapToGridToggle.IsOn = _settings.Current.SnapToGrid;
            AutoSelectToolToggle.IsOn = _settings.Current.AutoSelectToolOnObjectClick;
            CanvasSizeCombo.SelectedIndex = _viewModel.Document.Canvas.Preset switch
            {
                "A4-portrait" => 1,
                "A3-landscape" => 2,
                "Letter-landscape" => 3,
                "16-9" => 4,
                _ => 0,
            };
            UpdateCanvasBackgroundSelection();
            NoSelectionText.Visibility = selection.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            ObjectSection.Visibility = hasSelection ? Visibility.Visible : Visibility.Collapsed;
            OpacitySection.Visibility = hasSelection ? Visibility.Visible : Visibility.Collapsed;

            var primary = selection.Count == 1 ? selection[0] : null;
            ShapeSection.Visibility = primary is ShapeObject ? Visibility.Visible : Visibility.Collapsed;
            PencilSection.Visibility = primary is PathObject ? Visibility.Visible : Visibility.Collapsed;
            TextSection.Visibility = primary is TextObject ? Visibility.Visible : Visibility.Collapsed;
            ImageSection.Visibility = primary is ImageObject ? Visibility.Visible : Visibility.Collapsed;
            OpacitySlider.IsEnabled = primary is not null;

            if (selection.Count > 1)
            {
                NoSelectionText.Text = "已選取多個物件。可移動、對齊、調整物件順序；縮放與旋轉請單獨操作。";
                NoSelectionText.Visibility = Visibility.Visible;
            }
            else if (primary is not null)
            {
                OpacitySlider.Value = Math.Clamp(primary.Opacity, 0, 1);
                NoSelectionText.Text = "選取物件後可調整位置、大小、旋轉與樣式。";

                switch (primary)
                {
                    case ShapeObject shape:
                        FillColorPicker.Color = ColorHelper.FromHex(shape.Fill);
                        StrokeColorPicker.Color = ColorHelper.FromHex(shape.Stroke);
                        FillSwatch.Background = ColorHelper.Brush(shape.Fill);
                        StrokeSwatch.Background = ColorHelper.Brush(shape.Stroke);
                        FillEnabledToggle.IsChecked = shape.IsFilled;
                        StrokeEnabledToggle.IsChecked = shape.IsStroked;
                        FillColorButton.IsEnabled = shape.IsFilled;
                        StrokeColorButton.IsEnabled = shape.IsStroked;
                        FillSwatch.Opacity = shape.IsFilled ? 1 : 0.35;
                        StrokeSwatch.Opacity = shape.IsStroked ? 1 : 0.35;
                        StrokeWidthBox.Value = shape.StrokeWidth;
                        CornerRadiusBox.Value = shape.CornerRadius;
                        CornerRadiusBox.Visibility = shape.Kind == ShapeKind.RoundedRectangle
                            ? Visibility.Visible
                            : Visibility.Collapsed;
                        DashCombo.SelectedIndex = shape.Dash switch
                        {
                            "dash" => 1,
                            "dot" => 2,
                            _ => 0,
                        };
                        break;
                    case PathObject path:
                        PencilColorPicker.Color = ColorHelper.FromHex(path.Stroke);
                        PencilSwatch.Background = ColorHelper.Brush(path.Stroke);
                        PencilWidthBox.Value = path.StrokeWidth;
                        PencilDashCombo.SelectedIndex = path.Dash switch
                        {
                            "dash" => 1,
                            "dot" => 2,
                            _ => 0,
                        };
                        break;
                    case TextObject text:
                        if (!_fonts.Contains(text.FontFamily, StringComparer.OrdinalIgnoreCase))
                        {
                            _fonts.Add(text.FontFamily);
                            FontFamilyCombo.ItemsSource = null;
                            FontFamilyCombo.ItemsSource = _fonts;
                        }

                        FontFamilyCombo.SelectedItem = _fonts.FirstOrDefault(
                            font => string.Equals(font, text.FontFamily, StringComparison.OrdinalIgnoreCase));
                        FontSizeBox.Value = text.FontSize;
                        var effectiveWeight = GetEffectiveFontWeight(text);
                        FontWeightCombo.SelectedIndex = GetFontWeightIndex(effectiveWeight);
                        TextColorPicker.Color = ColorHelper.FromHex(text.Color);
                        TextSwatch.Background = ColorHelper.Brush(text.Color);
                        ItalicToggle.IsChecked = text.Italic;
                        UnderlineToggle.IsChecked = text.Underline;
                        AlignCombo.SelectedIndex = text.Align switch
                        {
                            TextAlign.Center => 1,
                            TextAlign.Right => 2,
                            _ => 0,
                        };
                        LineSpacingBox.Value = text.LineSpacing;
                        break;
                    case ImageObject:
                        break;
                }
            }
            else
            {
                NoSelectionText.Text = "選取物件後可調整位置、大小、旋轉與樣式。";
            }

            var layers = _viewModel.Document.Layers.OrderByDescending(l => l.Order).ToList();
            if (LayersList.ItemsSource is not IEnumerable<Layer> currentLayers ||
                !currentLayers.Select(l => l.Id).SequenceEqual(layers.Select(l => l.Id)) ||
                !currentLayers.Select(l => l.Name).SequenceEqual(layers.Select(l => l.Name)) ||
                !currentLayers.Select(l => l.IsVisible).SequenceEqual(layers.Select(l => l.IsVisible)) ||
                !currentLayers.Select(l => l.IsLocked).SequenceEqual(layers.Select(l => l.IsLocked)))
            {
                LayersList.ItemsSource = layers;
            }

            if (!ReferenceEquals(LayersList.SelectedItem, _viewModel.CurrentLayer))
            {
                LayersList.SelectedItem = _viewModel.CurrentLayer;
            }

            var objects = _viewModel.Document.ObjectsInLayer(_viewModel.CurrentLayer.Id)
                .Reverse()
                .ToList();
            if (ObjectsList.ItemsSource is not IEnumerable<SceneObject> currentObjects ||
                !currentObjects.Select(o => o.Id).SequenceEqual(objects.Select(o => o.Id)) ||
                !currentObjects.Select(o => o.Name).SequenceEqual(objects.Select(o => o.Name)) ||
                !currentObjects.Select(o => o.ZIndex).SequenceEqual(objects.Select(o => o.ZIndex)) ||
                !currentObjects.Select(o => o.IsVisible).SequenceEqual(objects.Select(o => o.IsVisible)) ||
                !currentObjects.Select(o => o.IsLocked).SequenceEqual(objects.Select(o => o.IsLocked)))
            {
                ObjectsList.ItemsSource = objects;
            }

            var selectedObject = primary is not null && primary.LayerId == _viewModel.CurrentLayer.Id
                ? primary
                : null;
            if (!ReferenceEquals(ObjectsList.SelectedItem, selectedObject))
            {
                ObjectsList.SelectedItem = selectedObject;
            }
            CurrentLayerInfo.Text =
                $"目前圖層：{_viewModel.CurrentLayer.Name}（{_viewModel.CurrentLayerObjectCount} 個物件）" +
                (_viewModel.CurrentLayer.IsVisible ? string.Empty : "（隱藏）") +
                (_viewModel.CurrentLayer.IsLocked ? "（鎖定）" : string.Empty);
            UndoMenuItem.IsEnabled = _viewModel.History.CanUndo;
            RedoMenuItem.IsEnabled = _viewModel.History.CanRedo;
            UndoButton.IsEnabled = _viewModel.History.CanUndo;
            RedoButton.IsEnabled = _viewModel.History.CanRedo;
        }
        finally
        {
            _isApplyingInspector = false;
        }

        if (_isLoaded)
        {
            DispatcherQueue.TryEnqueue(() =>
                InspectorScroll.ChangeView(null, inspectorOffset, null, disableAnimation: true));
        }
    }

    private void UpdateChrome()
    {
        var fileName = string.IsNullOrWhiteSpace(_viewModel.CurrentFilePath)
            ? "未命名"
            : IOPath.GetFileName(_viewModel.CurrentFilePath);
        Title = $"{fileName}{(_viewModel.IsDirty ? " *" : string.Empty)} - 我的向量畫布";
        DirtyText.Text = _viewModel.IsDirty ? "● 未儲存" : string.Empty;
        StatusTextBlock.Text =
            $"{_viewModel.StatusText}　｜　圖層：{_viewModel.CurrentLayer.Name}　｜　{_viewModel.SelectionSummary}";
        CanvasInfoText.Text =
            $"{GetPageDisplayName()}　｜　格線 {(_settings.Current.ShowGrid ? "開" : "關")}　｜　吸附 {(_settings.Current.SnapToGrid ? "開" : "關")}";
        UndoMenuItem.IsEnabled = _viewModel.History.CanUndo;
        RedoMenuItem.IsEnabled = _viewModel.History.CanRedo;
        UndoButton.IsEnabled = _viewModel.History.CanUndo;
        RedoButton.IsEnabled = _viewModel.History.CanRedo;
        CutMenuItem.IsEnabled = _viewModel.HasSelection;
        CopyMenuItem.IsEnabled = _viewModel.HasSelection;
        DuplicateMenuItem.IsEnabled = _viewModel.HasSelection;
        DeleteMenuItem.IsEnabled = _viewModel.HasSelection;
    }

    private string GetPageDisplayName()
    {
        var canvas = _viewModel.Document.Canvas;
        return canvas.Preset switch
        {
            "A4-portrait" => "A4 直式",
            "A4-landscape" => "A4 橫式",
            "A3-landscape" => "A3 橫式",
            "Letter-landscape" => "Letter 橫式",
            "16-9" => "16:9",
            _ => $"{canvas.Width:0} × {canvas.Height:0}",
        };
    }

    private SceneObject? PrimarySelection => _viewModel.PrimarySelection;

    private void ShowGridToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isApplyingInspector)
        {
            return;
        }

        var showGrid = ShowGridToggle.IsOn;
        if (_settings.Current.ShowGrid == showGrid)
        {
            return;
        }

        _settings.Current.ShowGrid = showGrid;
        _settings.Save();
        UpdateGrid(
            Math.Max(1, _viewModel.Document.Canvas.Width),
            Math.Max(1, _viewModel.Document.Canvas.Height));
        UpdateChrome();
    }

    private void SnapToGridToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isApplyingInspector)
        {
            return;
        }

        var snapToGrid = SnapToGridToggle.IsOn;
        if (_settings.Current.SnapToGrid == snapToGrid)
        {
            return;
        }

        _settings.Current.SnapToGrid = snapToGrid;
        _settings.Save();
        UpdateChrome();
    }

    private void AutoSelectToolToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isApplyingInspector)
        {
            return;
        }

        var autoSelect = AutoSelectToolToggle.IsOn;
        if (_settings.Current.AutoSelectToolOnObjectClick == autoSelect)
        {
            return;
        }

        _settings.Current.AutoSelectToolOnObjectClick = autoSelect;
        _settings.Save();
        UpdateChrome();
    }

    private void CanvasSizeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded || _isApplyingInspector)
        {
            return;
        }

        if ((CanvasSizeCombo.SelectedItem as ComboBoxItem)?.Tag is not string preset)
        {
            return;
        }

        ApplyPagePreset(preset);
    }

    private void CanvasBackground_Click(object sender, RoutedEventArgs e)
    {
        if (_isApplyingInspector || sender is not Button { Tag: string id })
        {
            return;
        }

        if (CanvasBackgroundPalette.Get(id) is not { } option)
        {
            return;
        }

        var background = ColorHelper.ToHex(option.Color);
        if (string.Equals(_viewModel.Document.Canvas.Background, background, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _viewModel.PushUndo("canvas-background");
        _viewModel.Document.Canvas.Background = background;
        _viewModel.MarkDirty();
        UpdateCanvasBackgroundSelection();
        _viewModel.SetStatus($"畫布背景已設為{_resourceLoader.GetString(option.NameResourceKey)}");
    }

    // ---------------------------------------------------------------- inspector handlers

    private void Rotate0_Click(object sender, RoutedEventArgs e) => SetRotation(0);

    private void Rotate90_Click(object sender, RoutedEventArgs e) => SetRotation(90);

    private void Rotate180_Click(object sender, RoutedEventArgs e) => SetRotation(180);

    private void Rotate270_Click(object sender, RoutedEventArgs e) => SetRotation(270);

    private void SetRotation(double angle)
    {
        if (PrimarySelection is null)
        {
            return;
        }

        _viewModel.PushUndo();
        PrimarySelection.Transform.Rotation = angle;
        _viewModel.MarkDirty();
    }

    private void FlipHorizontal_Click(object sender, RoutedEventArgs e)
    {
        if (PrimarySelection is null)
        {
            return;
        }

        _viewModel.PushUndo();
        PrimarySelection.Transform.FlipH = !PrimarySelection.Transform.FlipH;
        _viewModel.MarkDirty();
    }

    private void FlipVertical_Click(object sender, RoutedEventArgs e)
    {
        if (PrimarySelection is null)
        {
            return;
        }

        _viewModel.PushUndo();
        PrimarySelection.Transform.FlipV = !PrimarySelection.Transform.FlipV;
        _viewModel.MarkDirty();
    }

    private void ActionDuplicate_Click(object sender, RoutedEventArgs e)
    {
        DuplicateSelection();
    }

    private void ActionRotate90_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.HasSelection)
        {
            return;
        }

        _viewModel.PushUndo();
        foreach (var item in _viewModel.Selection)
        {
            item.Transform.Rotation = (item.Transform.Rotation + 90) % 360;
        }

        _viewModel.MarkDirty();
    }

    private void ActionFlipHorizontal_Click(object sender, RoutedEventArgs e)
    {
        FlipHorizontal_Click(sender, e);
    }

    private void ActionFlipVertical_Click(object sender, RoutedEventArgs e)
    {
        FlipVertical_Click(sender, e);
    }

    private void ActionDelete_Click(object sender, RoutedEventArgs e)
    {
        DeleteSelection();
    }

    private void InspectorToggle_Click(object sender, RoutedEventArgs e)
    {
        SetInspectorVisible(InspectorToggle.IsChecked == true);
    }

    private void SetInspectorVisible(bool visible)
    {
        InspectorToggle.IsChecked = visible;
        InspectorColumn.Width = new GridLength(visible ? 320 : 0);
        InspectorPanelHost.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        PositionSelectionActions();
        if (!visible)
        {
            FocusSink.Focus(FocusState.Programmatic);
        }
    }

    private void OpacitySlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isApplyingInspector || PrimarySelection is null)
        {
            return;
        }

        _viewModel.PushUndo("opacity");
        PrimarySelection.Opacity = Math.Clamp(e.NewValue, 0, 1);
        _viewModel.MarkDirty();
    }

    private void BringForward_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.PushUndo();
        _viewModel.BringForward();
    }

    private void SendBackward_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.PushUndo();
        _viewModel.SendBackward();
    }

    private void BringToFront_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.PushUndo();
        _viewModel.BringToFront();
    }

    private void SendToBack_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.PushUndo();
        _viewModel.SendToBack();
    }

    private void AlignLeft_Click(object sender, RoutedEventArgs e) => Align(AlignKind.Left);

    private void AlignCenterX_Click(object sender, RoutedEventArgs e) => Align(AlignKind.CenterX);

    private void AlignRight_Click(object sender, RoutedEventArgs e) => Align(AlignKind.Right);

    private void AlignTop_Click(object sender, RoutedEventArgs e) => Align(AlignKind.Top);

    private void AlignCenterY_Click(object sender, RoutedEventArgs e) => Align(AlignKind.CenterY);

    private void AlignBottom_Click(object sender, RoutedEventArgs e) => Align(AlignKind.Bottom);

    private void Align(AlignKind kind)
    {
        if (!_viewModel.HasSelection)
        {
            return;
        }

        _viewModel.PushUndo();
        _viewModel.AlignSelection(kind);
    }

    private ShapeToolStyle GetShapeStyle(ShapeKind kind)
    {
        var key = kind.ToString();
        if (!_settings.Current.ShapeToolStyles.TryGetValue(key, out var style) || style is null)
        {
            style = new ShapeToolStyle();
            _settings.Current.ShapeToolStyles[key] = style;
        }

        return style;
    }

    private void SaveShapeStyle(ShapeObject shape)
    {
        var style = GetShapeStyle(shape.Kind);
        style.Fill = shape.Fill;
        style.Stroke = shape.Stroke;
        style.StrokeWidth = shape.StrokeWidth;
        style.Dash = shape.Dash;
        style.CornerRadius = shape.CornerRadius;
        style.IsFilled = shape.IsFilled;
        style.IsStroked = shape.IsStroked;
    }

    private void ApplyTextStyle(TextObject text)
    {
        var style = _settings.Current.TextStyle;
        text.FontFamily = style.FontFamily;
        text.FontSize = Math.Clamp(style.FontSize, 8, 200);
        text.FontWeight = style.FontWeight;
        text.Bold = style.Bold;
        text.Italic = style.Italic;
        text.Underline = style.Underline;
        text.Color = style.Color;
        text.Align = style.Align;
        text.LineSpacing = Math.Clamp(style.LineSpacing, 0.8, 3);
    }

    private void SaveTextStyle(TextObject text)
    {
        var style = _settings.Current.TextStyle;
        style.FontFamily = text.FontFamily;
        style.FontSize = text.FontSize;
        style.FontWeight = text.FontWeight;
        style.Bold = text.Bold;
        style.Italic = text.Italic;
        style.Underline = text.Underline;
        style.Color = text.Color;
        style.Align = text.Align;
        style.LineSpacing = text.LineSpacing;
    }

    private void FillColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
    {
        if (_isApplyingInspector || DateTime.UtcNow < _colorGuardUntil || PrimarySelection is not ShapeObject shape)
        {
            return;
        }

        ApplyFillColor(shape, args.NewColor);
    }

    private void PresetFillColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string id } ||
            FillColorPalette.Get(id) is not { } option ||
            PrimarySelection is not ShapeObject shape)
        {
            return;
        }

        _isApplyingInspector = true;
        FillColorPicker.Color = option.Color;
        _isApplyingInspector = false;
        ApplyFillColor(shape, option.Color);
    }

    private void CloseFillColorFlyout_Click(object sender, RoutedEventArgs e)
    {
        FillColorFlyout.Hide();
    }

    private void ApplyFillColor(ShapeObject shape, Color color)
    {
        var fill = ColorHelper.ToHex(color);
        var isFilled = color.A > 0;
        if (shape.Fill == fill && shape.IsFilled == isFilled)
        {
            return;
        }

        _viewModel.PushUndo("fill");
        shape.Fill = fill;
        shape.IsFilled = isFilled;
        SaveShapeStyle(shape);
        FillSwatch.Background = new SolidColorBrush(color);
        _viewModel.MarkDirty();
    }

    private void FillEnabledToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_isApplyingInspector || PrimarySelection is not ShapeObject shape)
        {
            return;
        }

        var enabled = FillEnabledToggle.IsChecked == true;
        if (shape.IsFilled == enabled)
        {
            return;
        }

        _viewModel.PushUndo("fill");
        shape.IsFilled = enabled;
        if (enabled && ColorHelper.FromHex(shape.Fill).A == 0)
        {
            var fallbackFill = GetShapeStyle(shape.Kind).Fill;
            shape.Fill = ColorHelper.FromHex(fallbackFill).A > 0 ? fallbackFill : "#FF4F9CFF";
        }

        SaveShapeStyle(shape);
        _viewModel.MarkDirty();
    }

    private void StrokeEnabledToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_isApplyingInspector || PrimarySelection is not ShapeObject shape)
        {
            return;
        }

        var enabled = StrokeEnabledToggle.IsChecked == true;
        if (shape.IsStroked == enabled)
        {
            return;
        }

        _viewModel.PushUndo("stroke");
        shape.IsStroked = enabled;
        if (enabled)
        {
            if (ColorHelper.FromHex(shape.Stroke).A == 0)
            {
                var fallbackStroke = GetShapeStyle(shape.Kind).Stroke;
                shape.Stroke = ColorHelper.FromHex(fallbackStroke).A > 0 ? fallbackStroke : "#FF1F2937";
            }

            if (shape.StrokeWidth <= 0)
            {
                shape.StrokeWidth = 2;
            }
        }

        SaveShapeStyle(shape);
        _viewModel.MarkDirty();
    }

    private void StrokeColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
    {
        if (_isApplyingInspector || DateTime.UtcNow < _colorGuardUntil || PrimarySelection is not ShapeObject shape)
        {
            return;
        }

        ApplyStrokeColor(shape, args.NewColor);
    }

    private void PresetStrokeColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string id } ||
            FillColorPalette.Get(id) is not { } option ||
            PrimarySelection is not ShapeObject shape)
        {
            return;
        }

        _isApplyingInspector = true;
        StrokeColorPicker.Color = option.Color;
        _isApplyingInspector = false;
        ApplyStrokeColor(shape, option.Color);
    }

    private void CloseStrokeColorFlyout_Click(object sender, RoutedEventArgs e)
    {
        StrokeColorFlyout.Hide();
    }

    private void ApplyStrokeColor(ShapeObject shape, Color color)
    {
        var stroke = ColorHelper.ToHex(color);
        var isStroked = color.A > 0 && shape.StrokeWidth > 0;
        if (shape.Stroke == stroke && shape.IsStroked == isStroked)
        {
            return;
        }

        _viewModel.PushUndo("stroke");
        shape.Stroke = stroke;
        shape.IsStroked = isStroked;
        SaveShapeStyle(shape);
        StrokeSwatch.Background = new SolidColorBrush(color);
        _viewModel.MarkDirty();
    }

    private void StrokeWidthBox_ValueChanged(object? sender, NumericFieldValueChangedEventArgs args)
    {
        if (_isApplyingInspector || PrimarySelection is not ShapeObject shape || double.IsNaN(args.Value))
        {
            return;
        }

        _viewModel.PushUndo("strokeWidth");
        shape.StrokeWidth = Math.Max(0, args.Value);
        shape.IsStroked = shape.StrokeWidth > 0;
        SaveShapeStyle(shape);
        _viewModel.MarkDirty();
    }

    private void CornerRadiusBox_ValueChanged(object? sender, NumericFieldValueChangedEventArgs args)
    {
        if (_isApplyingInspector || PrimarySelection is not ShapeObject shape || double.IsNaN(args.Value))
        {
            return;
        }

        _viewModel.PushUndo("cornerRadius");
        shape.CornerRadius = Math.Max(0, args.Value);
        SaveShapeStyle(shape);
        _viewModel.MarkDirty();
    }

    private void DashCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isApplyingInspector || DateTime.UtcNow < _colorGuardUntil || PrimarySelection is not ShapeObject shape)
        {
            return;
        }

        var item = DashCombo.SelectedItem as ComboBoxItem;
        var value = item?.Tag as string ?? "solid";
        if (shape.Dash == value)
        {
            return;
        }

        _viewModel.PushUndo();
        shape.Dash = value;
        SaveShapeStyle(shape);
        _viewModel.MarkDirty();
    }

    private void PencilColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
    {
        if (_isApplyingInspector || DateTime.UtcNow < _colorGuardUntil || PrimarySelection is not PathObject path)
        {
            return;
        }

        ApplyPencilColor(path, args.NewColor);
    }

    private void PresetPencilColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string id } ||
            FillColorPalette.Get(id) is not { } option ||
            PrimarySelection is not PathObject path)
        {
            return;
        }

        _isApplyingInspector = true;
        PencilColorPicker.Color = option.Color;
        _isApplyingInspector = false;
        ApplyPencilColor(path, option.Color);
    }

    private void ClosePencilColorFlyout_Click(object sender, RoutedEventArgs e)
    {
        PencilColorFlyout.Hide();
    }

    private void ApplyPencilColor(PathObject path, Color color)
    {
        var stroke = ColorHelper.ToHex(color);
        if (string.Equals(path.Stroke, stroke, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _viewModel.PushUndo("pencilStroke");
        path.Stroke = stroke;
        _pencilStroke = stroke;
        PencilSwatch.Background = new SolidColorBrush(color);
        _viewModel.MarkDirty();
    }

    private void PencilWidthBox_ValueChanged(object? sender, NumericFieldValueChangedEventArgs args)
    {
        if (_isApplyingInspector || PrimarySelection is not PathObject path || double.IsNaN(args.Value))
        {
            return;
        }

        _viewModel.PushUndo("pencilWidth");
        path.StrokeWidth = Math.Clamp(args.Value, 1, 50);
        _pencilStrokeWidth = path.StrokeWidth;
        _viewModel.MarkDirty();
    }

    private void PencilDashCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isApplyingInspector || PrimarySelection is not PathObject path)
        {
            return;
        }

        var item = PencilDashCombo.SelectedItem as ComboBoxItem;
        var value = item?.Tag as string ?? "solid";
        if (string.Equals(path.Dash, value, StringComparison.Ordinal))
        {
            return;
        }

        _viewModel.PushUndo();
        path.Dash = value;
        _pencilDash = value;
        _viewModel.MarkDirty();
    }

    private void FontFamilyCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isApplyingInspector || PrimarySelection is not TextObject text)
        {
            return;
        }

        if (FontFamilyCombo.SelectedItem is not string font ||
            string.Equals(text.FontFamily, font, StringComparison.Ordinal))
        {
            return;
        }

        _viewModel.PushUndo();
        text.FontFamily = font;
        SaveTextStyle(text);
        _viewModel.MarkDirty();
    }

    private void FontSizeBox_ValueChanged(object? sender, NumericFieldValueChangedEventArgs args)
    {
        if (_isApplyingInspector || PrimarySelection is not TextObject text || double.IsNaN(args.Value))
        {
            return;
        }

        _viewModel.PushUndo("fontSize");
        text.FontSize = Math.Clamp(args.Value, 8, 200);
        SaveTextStyle(text);
        _viewModel.MarkDirty();
    }

    private void TextColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
    {
        if (_isApplyingInspector || DateTime.UtcNow < _colorGuardUntil || PrimarySelection is not TextObject text)
        {
            return;
        }

        ApplyTextColor(text, args.NewColor);
    }

    private void PresetTextColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string id } ||
            FillColorPalette.Get(id) is not { } option ||
            PrimarySelection is not TextObject text)
        {
            return;
        }

        _isApplyingInspector = true;
        TextColorPicker.Color = option.Color;
        _isApplyingInspector = false;
        ApplyTextColor(text, option.Color);
    }

    private void CloseTextColorFlyout_Click(object sender, RoutedEventArgs e)
    {
        TextColorFlyout.Hide();
    }

    private void ApplyTextColor(TextObject text, Color color)
    {
        var value = ColorHelper.ToHex(color);
        if (string.Equals(text.Color, value, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _viewModel.PushUndo("textColor");
        text.Color = value;
        TextSwatch.Background = new SolidColorBrush(color);
        SaveTextStyle(text);
        _viewModel.MarkDirty();
    }

    private void FontWeightCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isApplyingInspector || PrimarySelection is not TextObject text)
        {
            return;
        }

        if (FontWeightCombo.SelectedItem is not ComboBoxItem { Tag: string tag } ||
            !int.TryParse(tag, out var weight) ||
            GetEffectiveFontWeight(text) == weight)
        {
            return;
        }

        _viewModel.PushUndo("fontWeight");
        text.FontWeight = weight;
        text.Bold = weight >= 700;
        SaveTextStyle(text);
        _viewModel.MarkDirty();
    }

    private void ItalicToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_isApplyingInspector || PrimarySelection is not TextObject text)
        {
            return;
        }

        _viewModel.PushUndo();
        text.Italic = ItalicToggle.IsChecked == true;
        SaveTextStyle(text);
        _viewModel.MarkDirty();
    }

    private void UnderlineToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_isApplyingInspector || PrimarySelection is not TextObject text)
        {
            return;
        }

        _viewModel.PushUndo();
        text.Underline = UnderlineToggle.IsChecked == true;
        SaveTextStyle(text);
        _viewModel.MarkDirty();
    }

    private void AlignCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isApplyingInspector || PrimarySelection is not TextObject text)
        {
            return;
        }

        var item = AlignCombo.SelectedItem as ComboBoxItem;
        var value = item?.Tag as string ?? "Left";
        if (string.Equals(value, text.Align.ToString(), StringComparison.Ordinal))
        {
            return;
        }

        _viewModel.PushUndo();
        text.Align = value switch
        {
            "Center" => TextAlign.Center,
            "Right" => TextAlign.Right,
            _ => TextAlign.Left,
        };
        SaveTextStyle(text);
        _viewModel.MarkDirty();
    }

    private void LineSpacingBox_ValueChanged(object? sender, NumericFieldValueChangedEventArgs args)
    {
        if (_isApplyingInspector || PrimarySelection is not TextObject text || double.IsNaN(args.Value))
        {
            return;
        }

        _viewModel.PushUndo("lineSpacing");
        text.LineSpacing = Math.Clamp(args.Value, 0.8, 3);
        SaveTextStyle(text);
        _viewModel.MarkDirty();
    }

    private async void ReplaceImage_Click(object sender, RoutedEventArgs e)
    {
        if (PrimarySelection is not ImageObject image || _isPickingImage)
        {
            return;
        }

        _isPickingImage = true;
        try
        {
            Activate();
            var path = await PickImageFilePathAsync();
            if (path is null)
            {
                return;
            }

            var bytes = await File.ReadAllBytesAsync(path);
            var info = await _assets.AddAsync(bytes, IOPath.GetExtension(path), IOPath.GetFileName(path));
            _viewModel.PushUndo();
            _viewModel.Document.AddAsset(info);
            image.AssetId = info.Id;
            _viewModel.MarkDirty();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Replace image failed");
            await ShowMessageAsync("替換圖片失敗", exception.Message);
        }
        finally
        {
            _isPickingImage = false;
        }
    }

    private void LayersList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isApplyingInspector)
        {
            return;
        }

        if (LayersList.SelectedItem is Layer layer &&
            !string.Equals(layer.Id, _viewModel.CurrentLayerId, StringComparison.Ordinal))
        {
            _viewModel.SelectLayer(layer);
        }
    }

    private void ObjectsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isApplyingInspector)
        {
            return;
        }

        if (ObjectsList.SelectedItem is SceneObject sceneObject)
        {
            if (_viewModel.Selection.Count == 1 && ReferenceEquals(_viewModel.Selection[0], sceneObject))
            {
                return;
            }

            var layer = _viewModel.Document.FindLayer(sceneObject.LayerId);
            if (layer is null || layer.IsLocked || !layer.IsVisible)
            {
                _viewModel.SetStatus("目前圖層已鎖定或隱藏，請先解除");
                return;
            }

            _suppressInspectorAutoSwitch = true;
            try
            {
                _viewModel.Select(sceneObject, additive: false);
            }
            finally
            {
                _suppressInspectorAutoSwitch = false;
            }

            RenderOverlay();
        }
    }

    private async void AddLayer_Click(object sender, RoutedEventArgs e)
    {
        var input = new TextBox
        {
            Text = $"圖層 {_viewModel.Document.Layers.Count + 1}",
            MinWidth = 280,
        };
        var dialog = new ContentDialog
        {
            XamlRoot = RootGrid.XamlRoot,
            RequestedTheme = CurrentDialogTheme,
            Title = "新增圖層",
            PrimaryButtonText = "新增",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            Content = input,
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            _viewModel.PushUndo();
            _viewModel.AddLayer(input.Text);
        }
    }

    private async void RenameLayer_Click(object sender, RoutedEventArgs e)
    {
        if (LayersList.SelectedItem is not Layer layer)
        {
            return;
        }

        var editor = new TextBox { Text = layer.Name, MinWidth = 280 };
        var dialog = new ContentDialog
        {
            XamlRoot = RootGrid.XamlRoot,
            RequestedTheme = CurrentDialogTheme,
            Title = "重新命名圖層",
            PrimaryButtonText = "確定",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            Content = editor,
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            _viewModel.PushUndo();
            if (!_viewModel.RenameLayer(layer, editor.Text))
            {
                _viewModel.SetStatus("圖層名稱不能空白");
            }
        }
    }

    private async void DeleteLayer_Click(object sender, RoutedEventArgs e)
    {
        if (LayersList.SelectedItem is not Layer layer)
        {
            return;
        }

        if (_viewModel.Document.Layers.Count <= 1)
        {
            _viewModel.SetStatus("至少保留一個圖層");
            return;
        }

        var count = _viewModel.Document.CountObjects(layer.Id);
        if (count > 0)
        {
            var dialog = new ContentDialog
            {
                XamlRoot = RootGrid.XamlRoot,
                RequestedTheme = CurrentDialogTheme,
                Title = "刪除圖層",
                Content = $"圖層「{layer.Name}」內有 {count} 個物件，刪除圖層會一併刪除這些物件。要繼續嗎？",
                PrimaryButtonText = "刪除",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Close,
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }
        }

        _viewModel.PushUndo();
        _viewModel.DeleteLayer(layer);
    }

    private void LayerUp_Click(object sender, RoutedEventArgs e)
    {
        if (LayersList.SelectedItem is Layer layer)
        {
            _viewModel.PushUndo();
            _viewModel.MoveLayer(layer, up: true);
        }
    }

    private void LayerDown_Click(object sender, RoutedEventArgs e)
    {
        if (LayersList.SelectedItem is Layer layer)
        {
            _viewModel.PushUndo();
            _viewModel.MoveLayer(layer, up: false);
        }
    }

    private void ToggleCurrentLayerVisibility_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.PushUndo();
        _viewModel.ToggleLayerVisibility(_viewModel.CurrentLayer);
    }

    private void ToggleCurrentLayerLock_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.PushUndo();
        _viewModel.ToggleLayerLock(_viewModel.CurrentLayer);
    }

    private void ToggleObjectVisibility_Click(object sender, RoutedEventArgs e)
    {
        if (ObjectsList.SelectedItem is SceneObject sceneObject)
        {
            ToggleObjectVisibility(sceneObject);
        }
    }

    private void ToggleObjectLock_Click(object sender, RoutedEventArgs e)
    {
        if (ObjectsList.SelectedItem is SceneObject sceneObject)
        {
            ToggleObjectLock(sceneObject);
        }
    }

    private void ObjectRowVisibility_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: SceneObject sceneObject })
        {
            ToggleObjectVisibility(sceneObject);
        }
    }

    private void ObjectRowLock_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: SceneObject sceneObject })
        {
            ToggleObjectLock(sceneObject);
        }
    }

    private void ToggleObjectVisibility(SceneObject sceneObject)
    {
        _viewModel.PushUndo();
        sceneObject.IsVisible = !sceneObject.IsVisible;
        _viewModel.MarkDirty();
    }

    private void ToggleObjectLock(SceneObject sceneObject)
    {
        _viewModel.PushUndo();
        sceneObject.IsLocked = !sceneObject.IsLocked;
        if (sceneObject.IsLocked)
        {
            _viewModel.Selection.Remove(sceneObject);
        }

        _viewModel.MarkDirty();
    }

    private void LayerRowVisibility_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: Layer layer })
        {
            _viewModel.PushUndo();
            _viewModel.ToggleLayerVisibility(layer);
        }
    }

    private void LayerRowLock_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: Layer layer })
        {
            _viewModel.PushUndo();
            _viewModel.ToggleLayerLock(layer);
        }
    }

    private async void RenameObject_Click(object sender, RoutedEventArgs e)
    {
        if (ObjectsList.SelectedItem is not SceneObject sceneObject)
        {
            return;
        }

        var editor = new TextBox { Text = sceneObject.Name, MinWidth = 280 };
        var dialog = new ContentDialog
        {
            XamlRoot = RootGrid.XamlRoot,
            RequestedTheme = CurrentDialogTheme,
            Title = "重新命名物件",
            PrimaryButtonText = "確定",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            Content = editor,
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary &&
            !string.IsNullOrWhiteSpace(editor.Text) &&
            editor.Text != sceneObject.Name)
        {
            _viewModel.PushUndo();
            sceneObject.Name = editor.Text.Trim();
            _viewModel.MarkDirty();
        }
    }

    private void ZoomSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isApplyingZoom)
        {
            return;
        }

        SetZoomAtCenter(e.NewValue / 100);
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e)
    {
        SetZoomAtCenter(_zoom * 1.25);
    }

    private void ZoomOut_Click(object sender, RoutedEventArgs e)
    {
        SetZoomAtCenter(_zoom / 1.25);
    }

    private void Zoom100_Click(object sender, RoutedEventArgs e)
    {
        SetZoomAtCenter(1.0);
    }

    private void SetZoomAtCenter(double zoom)
    {
        var center = new Point(CanvasHost.ActualWidth / 2, CanvasHost.ActualHeight / 2);
        var world = ScreenToWorld(center);
        _zoom = Math.Clamp(zoom, 0.1, 8);
        var after = WorldToScreen(world);
        _pan += new Vector2(
            (float)(center.X - after.X),
            (float)(center.Y - after.Y));
        ApplyView();
    }

    private void FitButton_Click(object sender, RoutedEventArgs e)
    {
        FitToPage();
    }

    // ---------------------------------------------------------------- toolbar and commands

    private void ToolButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton button || button.Tag is not string tag)
        {
            return;
        }

        if (!Enum.TryParse<EditorTool>(tag, out var tool))
        {
            return;
        }

        if (tool == EditorTool.Image)
        {
            SetTool(_tool);
            MoreShapesFlyout.Hide();
            RequestInsertImage();
            return;
        }

        SetTool(tool);
        MoreShapesFlyout.Hide();
    }

    private void MoreShapesButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton button)
        {
            return;
        }

        button.IsChecked = IsMoreShapeTool(_tool);
        if (MoreShapesFlyout.IsOpen)
        {
            MoreShapesFlyout.Hide();
            return;
        }

        FlyoutBase.ShowAttachedFlyout(button);
    }

    private void SetTool(EditorTool tool)
    {
        _tool = tool;
        foreach (var (buttonTool, button) in _toolButtons)
        {
            button.IsChecked = buttonTool == tool;
        }

        MoreShapesButton.IsChecked = IsMoreShapeTool(tool);
        UpdateMoreShapesIcon();
        UpdateCursor();
    }

    private static bool IsMoreShapeTool(EditorTool tool)
    {
        return MoreShapeIconTemplates.ContainsKey(tool);
    }

    private void UpdateMoreShapesIcon()
    {
        MoreShapesButton.Content = TryCreateMoreShapeIcon(_tool, out var icon)
            ? icon
            : new FontIcon { Glyph = MoreShapesGlyph };
    }

    private bool TryCreateMoreShapeIcon(EditorTool tool, out FrameworkElement icon)
    {
        if (MoreShapeIconTemplates.TryGetValue(tool, out var templateKey) &&
            RootGrid.Resources.TryGetValue(templateKey, out var resource) &&
            resource is DataTemplate template &&
            template.LoadContent() is FrameworkElement content)
        {
            icon = content;
            return true;
        }

        icon = null!;
        return false;
    }

    private void RequestInsertImage()
    {
        if (_isPickingImage || !CanCreateOnCurrentLayer())
        {
            return;
        }

        var canvas = _viewModel.Document.Canvas;
        var center = new PointD(canvas.Width / 2, canvas.Height / 2);
        DispatcherQueue.TryEnqueue(() => _ = PickImageAtAsync(center));
    }

    private async Task PickImageAtAsync(PointD center)
    {
        if (_isPickingImage)
        {
            return;
        }

        _isPickingImage = true;
        try
        {
            Activate();
            var path = await PickImageFilePathAsync();
            if (path is null)
            {
                return;
            }

            var bytes = await File.ReadAllBytesAsync(path);
            await AddImageObjectAsync(bytes, IOPath.GetExtension(path), center);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Insert image failed");
            await ShowMessageAsync("插入圖片失敗", exception.Message);
        }
        finally
        {
            _isPickingImage = false;
        }
    }

    private async Task AddImageObjectAsync(byte[] bytes, string extension, PointD center)
    {
        if (!CanCreateOnCurrentLayer())
        {
            return;
        }

        var info = await _assets.AddAsync(bytes, extension);
        _viewModel.PushUndo();
        _viewModel.Document.AddAsset(info);

        var page = _viewModel.Document.Canvas;
        var maxWidth = page.Width * 0.55;
        var maxHeight = page.Height * 0.55;
        var scale = Math.Min(1, Math.Min(maxWidth / Math.Max(1, info.Width), maxHeight / Math.Max(1, info.Height)));
        var image = new ImageObject
        {
            AssetId = info.Id,
            Transform = new ObjectTransform
            {
                CenterX = center.X,
                CenterY = center.Y,
                Width = Math.Max(1, info.Width * scale),
                Height = Math.Max(1, info.Height * scale),
            },
        };
        _viewModel.AddObject(image);
    }

    private async Task<string?> PickImageFilePathAsync()
    {
        var picker = new PlatformFileOpenPicker(_appWindow.Id)
        {
            Title = "插入圖片",
            CommitButtonText = "插入",
        };

        var pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        if (!string.IsNullOrWhiteSpace(pictures) && Directory.Exists(pictures))
        {
            picker.SuggestedStartFolder = pictures;
        }

        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".bmp");
        picker.FileTypeFilter.Add(".gif");
        var result = await picker.PickSingleFileAsync();
        return result?.Path;
    }

    private static bool IsImageExtension(string extension)
    {
        return extension.ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif";
    }

    private static async Task<byte[]> ReadStorageFileBytesAsync(StorageFile file)
    {
        var buffer = await FileIO.ReadBufferAsync(file);
        var bytes = new byte[buffer.Length];
        using var reader = DataReader.FromBuffer(buffer);
        reader.ReadBytes(bytes);
        return bytes;
    }

    private async void NewButton_Click(object sender, RoutedEventArgs e)
    {
        await NewDocumentAsync();
    }

    private async Task NewDocumentAsync()
    {
        if (!await ConfirmSaveAsync())
        {
            return;
        }

        _viewModel.NewDocument();
        _autosave.Delete();
        AutosaveText.Text = string.Empty;
        HideDeleteToast();
        _hasFitted = false;
        DispatcherQueue.TryEnqueue(() =>
        {
            RenderAll();
            FitToPage();
        });
    }

    private async void OpenMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await PickAndOpenAsync();
    }

    private async Task PickAndOpenAsync()
    {
        try
        {
            var picker = new PlatformFileOpenPicker(_appWindow.Id)
            {
                Title = "開啟檔案",
                CommitButtonText = "開啟",
            };

            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (!string.IsNullOrWhiteSpace(documents) && Directory.Exists(documents))
            {
                picker.SuggestedStartFolder = documents;
            }

            picker.FileTypeFilter.Add(FileAssociationService.Extension);
            var file = await picker.PickSingleFileAsync();
            if (file is not null)
            {
                await OpenPathAsync(file.Path, confirm: true);
            }
        }
        catch (Exception exception)
        {
            await ShowMessageAsync("開啟失敗", exception.Message);
        }
    }

    private void BuildRecentFilesMenu()
    {
        if (RecentFilesMenu is null)
        {
            return;
        }

        RecentFilesMenu.Items.Clear();
        var recent = _settings.Current.RecentFiles;
        if (recent.Count == 0)
        {
            RecentFilesMenu.Items.Add(new MenuFlyoutItem
            {
                Text = "尚無最近開啟的檔案",
                IsEnabled = false,
            });
            return;
        }

        foreach (var path in recent)
        {
            var exists = File.Exists(path);
            var item = new MenuFlyoutItem
            {
                Text = exists
                    ? IOPath.GetFileName(path)
                    : $"{IOPath.GetFileName(path)}（找不到）",
                Tag = path,
                IsEnabled = exists,
            };
            ToolTipService.SetToolTip(item, path);
            item.Click += RecentFile_Click;
            RecentFilesMenu.Items.Add(item);
        }

        RecentFilesMenu.Items.Add(new MenuFlyoutSeparator());
        var clearItem = new MenuFlyoutItem { Text = "清除最近紀錄" };
        clearItem.Click += ClearRecentFiles_Click;
        RecentFilesMenu.Items.Add(clearItem);
    }

    private async void RecentFile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { Tag: string path })
        {
            await OpenPathAsync(path, confirm: true);
        }
    }

    private void ClearRecentFiles_Click(object sender, RoutedEventArgs e)
    {
        _settings.Current.RecentFiles.Clear();
        _settings.Save();
        BuildRecentFilesMenu();
    }

    private async Task OpenPathAsync(string path, bool confirm, bool addToRecent = true)
    {
        if (confirm && !await ConfirmSaveAsync())
        {
            return;
        }

        try
        {
            var (document, assetBytes) = await _fileService.OpenAsync(path);
            foreach (var asset in document.Assets)
            {
                if (assetBytes.TryGetValue(asset.Id, out var bytes))
                {
                    await _assets.LoadAsync(asset, bytes);
                }
            }

            _viewModel.SetDocument(document, path, resetHistory: true);
            if (addToRecent)
            {
                _settings.AddRecentFile(path);
                BuildRecentFilesMenu();
            }

            _hasFitted = false;
            AutosaveText.Text = string.Empty;
            HideDeleteToast();
            DispatcherQueue.TryEnqueue(() =>
            {
                RenderAll();
                FitToPage();
            });
            _viewModel.SetStatus($"已開啟 {IOPath.GetFileName(path)}");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Open failed");
            await ShowMessageAsync("無法開啟檔案", exception.Message);
        }
    }

    private async Task<bool> TryRecoverAutosaveAsync()
    {
        var info = _autosave.LoadInfo();
        if (info is null)
        {
            return false;
        }

        var source = string.IsNullOrWhiteSpace(info.OriginalPath)
            ? "未命名文件"
            : IOPath.GetFileName(info.OriginalPath);
        var dialog = new ContentDialog
        {
            XamlRoot = RootGrid.XamlRoot,
            RequestedTheme = CurrentDialogTheme,
            Title = "發現未儲存的工作",
            Content = $"上次編輯的「{source}」尚未儲存（{info.SavedAt:yyyy/MM/dd HH:mm}）。是否要恢復？",
            PrimaryButtonText = "恢復",
            SecondaryButtonText = "捨棄",
            CloseButtonText = "稍後",
            DefaultButton = ContentDialogButton.Primary,
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            await OpenPathAsync(_autosave.DocumentPath, confirm: false, addToRecent: false);
            _viewModel.CurrentFilePath = info.OriginalPath;
            _viewModel.MarkDirty();
            _viewModel.SetStatus("已恢復上次未儲存的內容");
            return true;
        }

        if (result == ContentDialogResult.Secondary)
        {
            _autosave.Delete();
        }

        return false;
    }

    private async void AutosaveTimer_Tick(object? sender, object e)
    {
        var version = _viewModel.ChangeVersion;
        if (!_viewModel.IsDirty ||
            _isAutosaving ||
            _isClosingPrompt ||
            !_isLoaded ||
            version == _lastAutosavedVersion)
        {
            return;
        }

        _isAutosaving = true;
        try
        {
            await _autosave.SaveAsync(_viewModel.Document, _assets.Images, _viewModel.CurrentFilePath);
            _lastAutosavedVersion = version;
            AutosaveText.Text = $"已自動儲存 {DateTime.Now:HH:mm}";
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Autosave failed");
        }
        finally
        {
            _isAutosaving = false;
        }
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        await SaveAsync(saveAs: false);
    }

    private async void SaveAsButton_Click(object sender, RoutedEventArgs e)
    {
        await SaveAsync(saveAs: true);
    }

    private async Task<bool> SaveAsync(bool saveAs)
    {
        try
        {
            var path = _viewModel.CurrentFilePath;
            if (saveAs || string.IsNullOrWhiteSpace(path))
            {
                var picker = new PlatformFileSavePicker(_appWindow.Id)
                {
                    SuggestedFileName = "我的作品",
                    CommitButtonText = "儲存",
                };

                var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (!string.IsNullOrWhiteSpace(documents) && Directory.Exists(documents))
                {
                    picker.SuggestedStartFolder = documents;
                }

                picker.FileTypeChoices.Add("MyDrawVector 向量圖", [FileAssociationService.Extension]);
                var file = await picker.PickSaveFileAsync();
                if (file is null)
                {
                    return false;
                }

                path = file.Path;
            }

            if (!path.EndsWith(FileAssociationService.Extension, StringComparison.OrdinalIgnoreCase))
            {
                path += FileAssociationService.Extension;
            }

            await _fileService.SaveAsync(_viewModel.Document, _assets.Images, path);
            _viewModel.CurrentFilePath = path;
            _viewModel.IsDirty = false;
            _autosave.Delete();
            AutosaveText.Text = string.Empty;
            _settings.AddRecentFile(path);
            BuildRecentFilesMenu();
            _viewModel.SetStatus($"已儲存 {IOPath.GetFileName(path)}");
            UpdateChrome();
            return true;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Save failed");
            await ShowMessageAsync("儲存失敗", exception.Message);
            return false;
        }
    }

    private async Task<bool> ConfirmSaveAsync()
    {
        if (!_viewModel.IsDirty)
        {
            return true;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = RootGrid.XamlRoot,
            RequestedTheme = CurrentDialogTheme,
            Title = "尚有未儲存的變更",
            Content = "是否要先儲存目前的文件？",
            PrimaryButtonText = "儲存",
            SecondaryButtonText = "不儲存",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
        };

        var result = await dialog.ShowAsync();
        return result switch
        {
            ContentDialogResult.Primary => await SaveAsync(saveAs: false),
            ContentDialogResult.Secondary => true,
            _ => false,
        };
    }

    private async void ExportScale_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { Tag: string tag } && double.TryParse(tag, out var scale))
        {
            await ExportPngAsync(scale);
        }
    }

    private async Task ExportPngAsync(double scale = 2.0)
    {
        try
        {
            var picker = new PlatformFileSavePicker(_appWindow.Id)
            {
                SuggestedFileName = "我的作品",
                CommitButtonText = "匯出",
            };

            var pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            if (!string.IsNullOrWhiteSpace(pictures) && Directory.Exists(pictures))
            {
                picker.SuggestedStartFolder = pictures;
            }

            picker.FileTypeChoices.Add("PNG 圖片", [".png"]);
            var file = await picker.PickSaveFileAsync();
            if (file is null)
            {
                return;
            }

            var document = _viewModel.Document;
            var pixelWidth = (int)Math.Max(1, Math.Round(document.Canvas.Width * scale));
            var pixelHeight = (int)Math.Max(1, Math.Round(document.Canvas.Height * scale));

            var oldTransform = SceneRoot.RenderTransform;
            var oldShadowVisibility = PageShadow.Visibility;
            var oldGridVisibility = GridCanvas.Visibility;
            var oldCheckerVisibility = CheckerCanvas.Visibility;
            var oldSelectionVisibility = OverlayCanvas.Visibility;
            var oldPreviewVisibility = PreviewCanvas.Visibility;
            try
            {
                SceneRoot.RenderTransform = new MatrixTransform { Matrix = Microsoft.UI.Xaml.Media.Matrix.Identity };
                SceneRoot.Width = document.Canvas.Width;
                SceneRoot.Height = document.Canvas.Height;
                PageShadow.Visibility = Visibility.Collapsed;
                GridCanvas.Visibility = Visibility.Collapsed;
                CheckerCanvas.Visibility = Visibility.Collapsed;
                OverlayCanvas.Visibility = Visibility.Collapsed;
                PreviewCanvas.Visibility = Visibility.Collapsed;

                var bitmap = new RenderTargetBitmap();
                await bitmap.RenderAsync(SceneRoot, pixelWidth, pixelHeight);
                var buffer = await bitmap.GetPixelsAsync();
                var bytes = new byte[buffer.Length];
                using (var reader = DataReader.FromBuffer(buffer))
                {
                    reader.ReadBytes(bytes);
                }

                using var stream = new InMemoryRandomAccessStream();
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
                encoder.SetPixelData(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied,
                    (uint)bitmap.PixelWidth,
                    (uint)bitmap.PixelHeight,
                    96 * scale,
                    96 * scale,
                    bytes);
                await encoder.FlushAsync();
                stream.Seek(0);
                var encoded = new byte[(int)stream.Size];
                using (var reader = new DataReader(stream.GetInputStreamAt(0)))
                {
                    await reader.LoadAsync((uint)stream.Size);
                    reader.ReadBytes(encoded);
                }

                await File.WriteAllBytesAsync(file.Path, encoded);
            }
            finally
            {
                SceneRoot.RenderTransform = oldTransform;
                PageShadow.Visibility = oldShadowVisibility;
                GridCanvas.Visibility = oldGridVisibility;
                CheckerCanvas.Visibility = oldCheckerVisibility;
                OverlayCanvas.Visibility = oldSelectionVisibility;
                PreviewCanvas.Visibility = oldPreviewVisibility;
                RenderAll();
            }

            _viewModel.SetStatus($"已匯出 {IOPath.GetFileName(file.Path)}");
            UpdateChrome();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Export failed");
            await ShowMessageAsync("匯出失敗", exception.Message);
        }
    }

    private async void UndoButton_Click(object sender, RoutedEventArgs e)
    {
        await Task.Yield();
        _viewModel.Undo();
    }

    private void RedoButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.Redo();
    }

    private void DeleteSelection()
    {
        var count = _viewModel.Selection.Count;
        if (count == 0)
        {
            return;
        }

        _viewModel.PushUndo();
        _viewModel.RemoveSelection();
        _viewModel.SetStatus($"已刪除 {count} 個物件");
        ShowDeleteToast(count);
    }

    private void ShowDeleteToast(int count)
    {
        DeleteToastText.Text = count == 1 ? "已刪除 1 個物件" : $"已刪除 {count} 個物件";
        DeleteToast.Visibility = Visibility.Visible;
        _deleteToastTimer.Stop();
        _deleteToastTimer.Start();
    }

    private void HideDeleteToast()
    {
        _deleteToastTimer.Stop();
        DeleteToast.Visibility = Visibility.Collapsed;
    }

    private void DeleteToastUndo_Click(object sender, RoutedEventArgs e)
    {
        HideDeleteToast();
        _viewModel.Undo();
    }

    private void DeleteToastClose_Click(object sender, RoutedEventArgs e)
    {
        HideDeleteToast();
    }

    // ---------------------------------------------------------------- clipboard

    private void CopySelection()
    {
        if (!_viewModel.HasSelection)
        {
            return;
        }

        _objectClipboard = _viewModel.CloneSelection();
        _viewModel.SetStatus("已複製選取物件");
    }

    private void CutSelection()
    {
        if (!_viewModel.HasSelection)
        {
            return;
        }

        CopySelection();
        _viewModel.PushUndo();
        _viewModel.RemoveSelection();
        _viewModel.SetStatus("已剪下選取物件");
    }

    private async Task PasteAsync()
    {
        if (!CanCreateOnCurrentLayer())
        {
            return;
        }

        if (_objectClipboard.Count > 0)
        {
            _viewModel.PushUndo();
            foreach (var image in _objectClipboard.OfType<ImageObject>())
            {
                var stored = _assets.Find(image.AssetId);
                if (stored is not null)
                {
                    _viewModel.Document.AddAsset(stored.Info);
                }
            }

            _viewModel.PasteClones(_objectClipboard);
            _viewModel.SetStatus("已貼上物件");
            return;
        }

        try
        {
            var data = Clipboard.GetContent();
            if (data.Contains(StandardDataFormats.Bitmap))
            {
                var reference = await data.GetBitmapAsync();
                using var stream = await reference.OpenReadAsync();
                var bytes = await ReadStreamBytesAsync(stream);
                await AddImageObjectAsync(
                    bytes,
                    ".png",
                    ScreenToWorld(new Point(CanvasHost.ActualWidth / 2, CanvasHost.ActualHeight / 2)));
                _viewModel.SetStatus("已貼上圖片");
                return;
            }

            if (data.Contains(StandardDataFormats.Text))
            {
                var text = await data.GetTextAsync();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    var center = ScreenToWorld(new Point(CanvasHost.ActualWidth / 2, CanvasHost.ActualHeight / 2));
                    _viewModel.PushUndo();
                    var textObject = new TextObject
                    {
                        Text = text,
                        Transform = new ObjectTransform
                        {
                            CenterX = center.X,
                            CenterY = center.Y,
                            Width = Math.Max(240, Math.Min(640, text.Length * 16)),
                            Height = 100,
                        },
                    };
                    ApplyTextStyle(textObject);
                    _viewModel.AddObject(textObject);
                    _viewModel.SetStatus("已貼上文字");
                    return;
                }
            }

            _viewModel.SetStatus("剪貼簿沒有可貼上的圖片或文字");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Paste failed");
            await ShowMessageAsync("貼上失敗", exception.Message);
        }
    }

    private static async Task<byte[]> ReadStreamBytesAsync(IRandomAccessStream stream)
    {
        var size = (uint)stream.Size;
        using var reader = new DataReader(stream.GetInputStreamAt(0));
        await reader.LoadAsync(size);
        var bytes = new byte[size];
        reader.ReadBytes(bytes);
        return bytes;
    }

    // ---------------------------------------------------------------- keyboard

    private void RootGrid_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Menu)
        {
            e.Handled = true;
            return;
        }

        if (IsKeyDown(VirtualKey.Control))
        {
            if (IsTextInputFocused() &&
                e.Key is VirtualKey.C or VirtualKey.V or VirtualKey.X or VirtualKey.A or VirtualKey.Z or VirtualKey.Y or VirtualKey.D)
            {
                return;
            }

            switch (e.Key)
            {
                case VirtualKey.Z:
                    if (IsKeyDown(VirtualKey.Shift))
                    {
                        _viewModel.Redo();
                    }
                    else
                    {
                        _viewModel.Undo();
                    }

                    break;
                case VirtualKey.Y:
                    _viewModel.Redo();
                    break;
                case VirtualKey.S:
                    _ = SaveAsync(saveAs: IsKeyDown(VirtualKey.Shift));
                    break;
                case VirtualKey.O:
                    _ = PickAndOpenAsync();
                    break;
                case VirtualKey.N:
                    _ = NewDocumentAsync();
                    break;
                case VirtualKey.C:
                    CopySelection();
                    break;
                case VirtualKey.V:
                    _ = PasteAsync();
                    break;
                case VirtualKey.X:
                    CutSelection();
                    break;
                case VirtualKey.D:
                    DuplicateSelection();
                    break;
                case VirtualKey.A:
                    SelectAllObjects();
                    break;
                case VirtualKey.Number0:
                case VirtualKey.NumberPad0:
                    SetZoomAtCenter(1.0);
                    break;
                case VirtualKey.Number1:
                case VirtualKey.NumberPad1:
                    FitToPage();
                    break;
                case VirtualKey.Add:
                case (VirtualKey)VkOemPlus:
                    SetZoomAtCenter(_zoom * 1.25);
                    break;
                case VirtualKey.Subtract:
                case (VirtualKey)VkOemMinus:
                    SetZoomAtCenter(_zoom / 1.25);
                    break;
                default:
                    return;
            }

            e.Handled = true;
            return;
        }

        if (e.Key == VirtualKey.Space)
        {
            return;
        }

        if (IsTextInputFocused())
        {
            return;
        }

        if (!IsKeyDown(VirtualKey.Menu))
        {
            var shortcutTool = e.Key switch
            {
                VirtualKey.V => EditorTool.Select,
                VirtualKey.R => EditorTool.Rectangle,
                VirtualKey.E => EditorTool.Ellipse,
                VirtualKey.L => EditorTool.Line,
                VirtualKey.T => EditorTool.Text,
                VirtualKey.I => EditorTool.Image,
                VirtualKey.P => EditorTool.Pencil,
                VirtualKey.H => EditorTool.Pan,
                _ => (EditorTool?)null,
            };

            if (shortcutTool is not null)
            {
                if (shortcutTool.Value == EditorTool.Image)
                {
                    RequestInsertImage();
                }
                else
                {
                    SetTool(shortcutTool.Value);
                }

                e.Handled = true;
                return;
            }
        }

        if (e.Key == VirtualKey.Tab && FocusSink.FocusState != FocusState.Unfocused)
        {
            if (CycleSelection(IsKeyDown(VirtualKey.Shift)))
            {
                e.Handled = true;
            }

            return;
        }

        if (e.Key == VirtualKey.Delete || e.Key == VirtualKey.Back)
        {
            DeleteSelection();
            e.Handled = true;
            return;
        }

        if (e.Key == VirtualKey.Escape)
        {
            CancelInteraction();
            _viewModel.ClearSelection();
            e.Handled = true;
            return;
        }

        if (!_viewModel.HasSelection)
        {
            return;
        }

        var step = IsKeyDown(VirtualKey.Shift) ? 10.0 : 1.0;
        var dx = e.Key switch
        {
            VirtualKey.Left => -step,
            VirtualKey.Right => step,
            _ => 0.0,
        };
        var dy = e.Key switch
        {
            VirtualKey.Up => -step,
            VirtualKey.Down => step,
            _ => 0.0,
        };

        if (dx == 0 && dy == 0)
        {
            return;
        }

        _viewModel.PushUndo();
        foreach (var item in _viewModel.Selection)
        {
            item.Transform.CenterX += dx;
            item.Transform.CenterY += dy;
        }

        _viewModel.MarkDirty();
        e.Handled = true;
    }

    private void RootGrid_KeyUp(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Space)
        {
            e.Handled = false;
        }
    }

    private bool IsTextInputFocused()
    {
        var focused = FocusManager.GetFocusedElement(RootGrid.XamlRoot);
        return focused is TextBox or RichEditBox or NumberBox or ComboBox;
    }

    private bool CycleSelection(bool backwards)
    {
        var selectable = _viewModel.Document.OrderedObjects()
            .Where(item =>
                item.IsVisible &&
                _viewModel.Document.FindLayer(item.LayerId) is { IsVisible: true, IsLocked: false })
            .ToList();
        if (selectable.Count == 0)
        {
            return false;
        }

        var current = PrimarySelection;
        var index = current is null ? -1 : selectable.FindIndex(item => item.Id == current.Id);
        var next = backwards
            ? (index <= 0 ? selectable.Count - 1 : index - 1)
            : (index + 1) % selectable.Count;
        var target = selectable[next];
        if (_viewModel.Document.FindLayer(target.LayerId) is { } layer)
        {
            _viewModel.SetCurrentLayer(layer);
        }

        _viewModel.Select(target, additive: false);
        return true;
    }

    private static bool IsKeyDown(VirtualKey key)
    {
        return InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(CoreVirtualKeyStates.Down);
    }

    private void CutMenuItem_Click(object sender, RoutedEventArgs e)
    {
        CutSelection();
    }

    private void CopyMenuItem_Click(object sender, RoutedEventArgs e)
    {
        CopySelection();
    }

    private async void PasteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await PasteAsync();
    }

    private void DuplicateMenuItem_Click(object sender, RoutedEventArgs e)
    {
        DuplicateSelection();
    }

    private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        DeleteSelection();
    }

    private void SelectAllMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SelectAllObjects();
    }

    private void DuplicateSelection()
    {
        if (!_viewModel.HasSelection || !CanCreateOnCurrentLayer())
        {
            return;
        }

        _viewModel.PushUndo();
        _viewModel.DuplicateSelection();
    }

    private void SelectAllObjects()
    {
        _viewModel.SelectMany(_viewModel.Document.OrderedObjects().Where(o =>
        {
            var layer = _viewModel.Document.FindLayer(o.LayerId);
            return o.IsVisible && !o.IsLocked && layer is not null && layer.IsVisible && !layer.IsLocked;
        }));
    }

    // ---------------------------------------------------------------- menus

    private void RegisterAssociation_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _association.Register();
            _viewModel.SetStatus("已建立／更新 .mydraw 檔案關聯");
            UpdateChrome();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Register association failed");
            _ = ShowMessageAsync("建立檔案關聯失敗", exception.Message);
        }
    }

    private void UnregisterAssociation_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _association.Unregister();
            _viewModel.SetStatus("已移除 .mydraw 檔案關聯");
            UpdateChrome();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unregister association failed");
            _ = ShowMessageAsync("移除檔案關聯失敗", exception.Message);
        }
    }

    private async void Preferences_Click(object sender, RoutedEventArgs e)
    {
        var themeCombo = new ComboBox
        {
            Header = _resourceLoader.GetString("PreferencesThemeHeader"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        themeCombo.Items.Add(new ComboBoxItem { Content = _resourceLoader.GetString("ThemeSystem"), Tag = "System" });
        themeCombo.Items.Add(new ComboBoxItem { Content = _resourceLoader.GetString("ThemeLight"), Tag = "Light" });
        themeCombo.Items.Add(new ComboBoxItem { Content = _resourceLoader.GetString("ThemeDark"), Tag = "Dark" });
        themeCombo.SelectedIndex = _themeMode switch
        {
            "Light" => 1,
            "Dark" => 2,
            _ => 0,
        };

        var themeColorCombo = new ComboBox
        {
            Header = _resourceLoader.GetString("PreferencesThemeColorHeader"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        foreach (var option in AppThemePalette.All)
        {
            var name = _resourceLoader.GetString(option.NameResourceKey);
            var itemContent = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
            };
            itemContent.Children.Add(new Border
            {
                Width = 20,
                Height = 20,
                Background = new SolidColorBrush(option.Color),
                CornerRadius = new CornerRadius(4),
            });
            itemContent.Children.Add(new TextBlock
            {
                Text = name,
                VerticalAlignment = VerticalAlignment.Center,
            });

            var item = new ComboBoxItem
            {
                Content = itemContent,
                Tag = option.Id,
            };
            AutomationProperties.SetName(item, name);
            themeColorCombo.Items.Add(item);
        }

        themeColorCombo.SelectedIndex = Math.Max(
            0,
            AppThemePalette.All.ToList().FindIndex(option => option.Id == _themeColor));
        themeColorCombo.SelectionChanged += ThemeColorCombo_SelectionChanged;

        var registerButton = new Button { Content = _resourceLoader.GetString("RegisterAssociation") };
        registerButton.Click += RegisterAssociation_Click;
        var unregisterButton = new Button { Content = _resourceLoader.GetString("UnregisterAssociation") };
        unregisterButton.Click += UnregisterAssociation_Click;

        var associationButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
        };
        associationButtons.Children.Add(registerButton);
        associationButtons.Children.Add(unregisterButton);

        var content = new StackPanel
        {
            Width = 380,
            Margin = new Thickness(0, 0, 16, 0),
            Spacing = 16,
        };
        content.Children.Add(themeCombo);
        content.Children.Add(themeColorCombo);
        content.Children.Add(new TextBlock { Text = _resourceLoader.GetString("AssociationHeader"), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        content.Children.Add(associationButtons);

        var scrollContent = new ScrollViewer
        {
            MaxHeight = 520,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollMode = ScrollMode.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = content,
        };

        var dialog = new ContentDialog
        {
            XamlRoot = RootGrid.XamlRoot,
            RequestedTheme = CurrentDialogTheme,
            Title = _resourceLoader.GetString("PreferencesTitle"),
            Content = scrollContent,
            PrimaryButtonText = _resourceLoader.GetString("Apply"),
            CloseButtonText = _resourceLoader.GetString("Cancel"),
            DefaultButton = ContentDialogButton.Primary,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var mode = (themeCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "System";
        _themeMode = mode;
        _settings.Current.ThemeMode = mode;
        _settings.Save();
        ApplyTheme();
        _viewModel.SetStatus("已套用偏好設定");
        UpdateChrome();
    }

    private void ThemeColorCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox { SelectedItem: ComboBoxItem { Tag: string id } })
        {
            return;
        }

        var themeColor = AppThemePalette.Normalize(id);
        if (themeColor == _themeColor)
        {
            return;
        }

        _themeColor = themeColor;
        _settings.Current.ThemeColor = themeColor;
        _settings.Save();
        ApplyTheme();
    }

    private void ApplyPagePreset(string preset)
    {
        var (width, height, name) = preset switch
        {
            "A4P" => (794d, 1123d, "A4-portrait"),
            "A3L" => (1587d, 1123d, "A3-landscape"),
            "LetterL" => (1056d, 816d, "Letter-landscape"),
            "Wide" => (1280d, 720d, "16-9"),
            _ => (1123d, 794d, "A4-landscape"),
        };

        var canvas = _viewModel.Document.Canvas;
        if (canvas.Width == width && canvas.Height == height)
        {
            return;
        }

        _viewModel.PushUndo();
        canvas.Width = width;
        canvas.Height = height;
        canvas.Preset = name;
        _hasFitted = false;
        _viewModel.MarkDirty();
        DispatcherQueue.TryEnqueue(() =>
        {
            RenderAll();
            FitToPage();
        });
        _viewModel.SetStatus($"畫布已切換為 {width:0} × {height:0}");
    }

    private async void About_Click(object sender, RoutedEventArgs e)
    {
        var version = typeof(App).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
        await ShowMessageAsync(
            "關於 MyDrawVector",
            $"MyDrawVector（我的向量畫布）{version}\n\n" +
            "給國小、國中、高中學生的向量繪圖工具。\n" +
            "所有作品與圖片都只留在本機，不會上傳網路。\n\n" +
            "檔案格式：.mydraw（ZIP：JSON + 圖片）\n" +
            "免安裝版本，可攜帶資料夾使用。");
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        try
        {
            var dialog = new ContentDialog
            {
                XamlRoot = RootGrid.XamlRoot,
                RequestedTheme = CurrentDialogTheme,
                Title = title,
                Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                CloseButtonText = "關閉",
            };
            await dialog.ShowAsync();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to show dialog");
        }
    }

    private async void Shortcuts_Click(object sender, RoutedEventArgs e)
    {
        var lines = new[]
        {
            "檔案",
            "　Ctrl+N 新增　Ctrl+O 開啟　Ctrl+S 儲存　Ctrl+Shift+S 另存",
            "　Ctrl+0 重設 100%　Ctrl+1 適合視窗　Ctrl++ / Ctrl+- 縮放",
            "",
            "編輯",
            "　Ctrl+Z 復原　Ctrl+Y / Ctrl+Shift+Z 重做",
            "　Ctrl+C 複製　Ctrl+V 貼上　Ctrl+X 剪下　Ctrl+D 再製",
            "　Ctrl+A 全選　Delete 刪除　Esc 取消／取消選取　Tab 切換物件",
            "",
            "工具",
            "　V 選取　R 矩形　E 橢圓　L 直線　T 文字　I 插入圖片　P 鉛筆　H 平移",
            "",
            "畫布",
            "　滾輪以游標為中心縮放　空白鍵＋拖曳／中鍵拖曳平移",
            "　方向鍵微調 1 DIP　Shift＋方向鍵微調 10 DIP",
            "　Shift＋拖曳 正方形／正圓　Shift＋旋轉 每 15° 吸附",
            "　拖曳時按住 Alt 暫時關閉格線吸附",
        };

        try
        {
            var dialog = new ContentDialog
            {
                XamlRoot = RootGrid.XamlRoot,
                RequestedTheme = CurrentDialogTheme,
                Title = "快捷鍵一覽",
                Content = new ScrollViewer
                {
                    MaxHeight = 420,
                    Content = new TextBlock
                    {
                        Text = string.Join(Environment.NewLine, lines),
                        TextWrapping = TextWrapping.Wrap,
                        LineHeight = 24,
                    },
                },
                CloseButtonText = "關閉",
            };
            await dialog.ShowAsync();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to show shortcuts dialog");
        }
    }

    private enum DragMode
    {
        None,
        Pan,
        Move,
        Resize,
        Rotate,
        Marquee,
        Shape,
        Pencil,
    }
}


using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace MyDrawVector.Controls;

public sealed partial class ToolPathIcon : UserControl
{
    public static readonly DependencyProperty DataProperty = DependencyProperty.Register(
        nameof(Data),
        typeof(Geometry),
        typeof(ToolPathIcon),
        new PropertyMetadata(null));

    public ToolPathIcon()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    public Geometry? Data
    {
        get => (Geometry?)GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var presenter = FindAncestor<ContentPresenter>(this);
        if (presenter is null)
        {
            IconPath.Stroke = Foreground;
            return;
        }

        IconPath.SetBinding(Microsoft.UI.Xaml.Shapes.Shape.StrokeProperty, new Binding
        {
            Source = presenter,
            Path = new PropertyPath("Foreground"),
            Mode = BindingMode.OneWay,
        });
    }

    private static T? FindAncestor<T>(DependencyObject start)
        where T : DependencyObject
    {
        var parent = VisualTreeHelper.GetParent(start);
        while (parent is not null)
        {
            if (parent is T match)
            {
                return match;
            }

            parent = VisualTreeHelper.GetParent(parent);
        }

        return null;
    }
}

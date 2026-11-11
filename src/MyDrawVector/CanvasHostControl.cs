using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace MyDrawVector;

public sealed class CanvasHostControl : Grid
{
    protected override Size ArrangeOverride(Size finalSize)
    {
        Clip = new RectangleGeometry
        {
            Rect = new Rect(0, 0, finalSize.Width, finalSize.Height),
        };
        return base.ArrangeOverride(finalSize);
    }

    public void SetCursor(InputSystemCursorShape shape)
    {
        ProtectedCursor = InputSystemCursor.Create(shape);
    }
}

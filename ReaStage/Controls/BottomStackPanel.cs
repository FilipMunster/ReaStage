using Avalonia;
using Avalonia.Controls;
using System;

namespace ReaStage.Controls;

// Stacks children upwards from the bottom edge. VerticalAlignment="Bottom" cannot do
// this: alignment only positions a child inside the space it was given, so content
// taller than the container is laid out from the top and overflows at the bottom.
// Here the overflow runs off the top instead and the clipping parent hides it, which
// keeps the songs nearest the current one on screen.
public class BottomStackPanel : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        Size childAvailable = new Size(availableSize.Width, double.PositiveInfinity);
        double width = 0;
        double height = 0;

        foreach (Control child in Children)
        {
            child.Measure(childAvailable);
            width = Math.Max(width, child.DesiredSize.Width);
            height += child.DesiredSize.Height;
        }

        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double bottom = finalSize.Height;

        for (int i = Children.Count - 1; i >= 0; i--)
        {
            Control child = Children[i];
            double height = child.DesiredSize.Height;
            bottom -= height;
            child.Arrange(new Rect(0, bottom, finalSize.Width, height));
        }

        return finalSize;
    }
}

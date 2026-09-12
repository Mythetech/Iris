namespace Iris.Sagas.Layout;

/// <summary>
/// The rectangle an edge label's text covers on the canvas.
/// </summary>
public readonly record struct LabelBounds(double Left, double Top, double Right, double Bottom)
{
    public bool Overlaps(NodeLayout node)
        => Left < node.Right && Right > node.X && Top < node.Bottom && Bottom > node.Y;

    public bool Overlaps(LabelBounds other)
        => Left < other.Right && Right > other.Left && Top < other.Bottom && Bottom > other.Top;
}

namespace Iris.Sagas.Layout;

public sealed record NodeLayout(string State, double X, double Y, double Width, double Height)
{
    public double CenterX => X + Width / 2;
    public double CenterY => Y + Height / 2;
    public double Right => X + Width;
    public double Bottom => Y + Height;
}

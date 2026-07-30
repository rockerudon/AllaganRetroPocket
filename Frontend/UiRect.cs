namespace AllaganPocket.Frontend;

internal readonly record struct UiRect(Vector2 Min, Vector2 Max)
{
    public Vector2 Size => Max - Min;
    public float Width => Max.X - Min.X;
    public float Height => Max.Y - Min.Y;
    public Vector2 Center => (Min + Max) * 0.5f;
}

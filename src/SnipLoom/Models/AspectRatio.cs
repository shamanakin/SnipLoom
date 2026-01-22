namespace SnipLoom.Models;

public class AspectRatio
{
    public string Name { get; }
    public int WidthRatio { get; }
    public int HeightRatio { get; }
    public double Ratio => (double)WidthRatio / HeightRatio;

    public AspectRatio(string name, int widthRatio, int heightRatio)
    {
        Name = name;
        WidthRatio = widthRatio;
        HeightRatio = heightRatio;
    }

    /// <summary>
    /// Calculate width given a height while maintaining aspect ratio
    /// </summary>
    public int GetWidth(int height) => (int)(height * Ratio);

    /// <summary>
    /// Calculate height given a width while maintaining aspect ratio
    /// </summary>
    public int GetHeight(int width) => (int)(width / Ratio);

    /// <summary>
    /// Common aspect ratio presets
    /// </summary>
    public static readonly AspectRatio[] Presets = new[]
    {
        new AspectRatio("16:9 (Widescreen)", 16, 9),
        new AspectRatio("9:16 (Vertical/Mobile)", 9, 16),
        new AspectRatio("1:1 (Square)", 1, 1),
        new AspectRatio("4:3 (Standard)", 4, 3),
        new AspectRatio("5:4 (Photo)", 5, 4),
        new AspectRatio("21:9 (Ultrawide)", 21, 9),
    };

    public static AspectRatio Freeform => new("Freeform", 0, 0);

    public override string ToString() => Name;
}

namespace EchoUI.Core;

/// <summary>布局计算结果：绝对坐标 + 尺寸</summary>
public readonly record struct LayoutBox(float X, float Y, float Width, float Height)
{
    public static readonly LayoutBox Zero = new();
}

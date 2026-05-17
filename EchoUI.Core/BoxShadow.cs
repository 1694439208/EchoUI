namespace EchoUI.Core;

/// <summary>阴影描述（与平台无关）</summary>
public readonly record struct BoxShadow(Color Color, float OffsetY, float Blur = 0)
{
    public static readonly BoxShadow None = default;

    public bool IsVisible => Color.A > 0 && (OffsetY != 0 || Blur > 0);
}

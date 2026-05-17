namespace EchoUI.Core;

/// <summary>绘制命令抽象基类</summary>
public abstract record RenderCommand(LayoutBox Layout);

/// <summary>填充色块（含圆角）</summary>
public sealed record DrawRect(LayoutBox Layout, Color? BackgroundColor, float BorderRadius) : RenderCommand(Layout);

/// <summary>绘制文本</summary>
public sealed record DrawText(LayoutBox Layout, string Text, Color Color, string? FontFamily, float FontSize, string? FontWeight, bool NoWrap = true) : RenderCommand(Layout);

/// <summary>绘制边框</summary>
public sealed record DrawBorder(LayoutBox Layout, Color Color, float Width, float Radius, BorderStyle Style) : RenderCommand(Layout);

/// <summary>绘制 Y 偏移阴影（跟随圆角）</summary>
public sealed record DrawShadow(LayoutBox Layout, Color Color, float OffsetY, float BorderRadius, float Blur = 0) : RenderCommand(Layout);

/// <summary>设置裁剪区域</summary>
public sealed record PushClip(LayoutBox Layout) : RenderCommand(Layout);

/// <summary>恢复裁剪区域</summary>
public sealed record PopClip : RenderCommand
{
    public PopClip() : base(LayoutBox.Zero) { }
}

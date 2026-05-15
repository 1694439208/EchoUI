namespace EchoUI.Core;

/// <summary>
/// 布局系统的默认值定义。所有渲染器和内部模型应引用此类，而非各自硬编码。
/// 
/// 注意：EchoUI 的默认值与 CSS Flexbox 有以下刻意差异：
/// - Direction 默认 Vertical（CSS 默认 row），因为纵向流更适合通用 UI
/// - FlexGrow 默认 0（与 CSS 一致）
/// 
/// 以下默认值与 CSS Flexbox 对齐：
/// - FlexShrink 默认 1（子元素溢出时自动收缩）
/// - AlignItems 默认 Stretch（子元素撑满交叉轴）
/// </summary>
public static class LayoutDefaults
{
    public const float FlexGrow = 0f;
    public const float FlexShrink = 0f;
    public const float Gap = 0f;
    public const float BorderWidth = 0f;
    public const float BorderRadius = 0f;
    public const float FontSize = 14f;

    public static readonly LayoutDirection Direction = LayoutDirection.Vertical;
    public static readonly JustifyContent JustifyContent = JustifyContent.Start;
    public static readonly AlignItems AlignItems = AlignItems.Stretch;
    public static readonly Overflow Overflow = Overflow.Visible;
    public static readonly BorderStyle BorderStyle = BorderStyle.None;
}

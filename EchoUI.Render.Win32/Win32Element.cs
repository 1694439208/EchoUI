using System.Drawing;
using EchoUI.Core;

namespace EchoUI.Render.Win32
{
    /// <summary>
    /// Win32 自绘元素节点，存储布局结果、样式属性和事件处理器。
    /// 每个 Win32Element 对应 EchoUI 元素树中的一个原生元素。
    /// </summary>
    internal class Win32Element
    {
        /// <summary>
        /// 元素类型：Container / Text / Input 或自定义原生类型
        /// </summary>
        public string ElementType { get; set; }

        /// <summary>
        /// 子元素列表
        /// </summary>
        public List<Win32Element> Children { get; } = [];

        /// <summary>
        /// 父元素引用
        /// </summary>
        public Win32Element? Parent { get; set; }

        // --- 布局结果（由 FlexLayout 计算） ---
        public float LayoutX { get; set; }
        public float LayoutY { get; set; }
        public float LayoutWidth { get; set; }
        public float LayoutHeight { get; set; }

        /// <summary>
        /// 绝对坐标（相对于窗口客户区）
        /// </summary>
        public float AbsoluteX { get; set; }
        public float AbsoluteY { get; set; }

        // --- 尺寸属性 ---
        public Dimension? Width { get; set; }
        public Dimension? Height { get; set; }
        public Dimension? MinWidth { get; set; }
        public Dimension? MinHeight { get; set; }
        public Dimension? MaxWidth { get; set; }
        public Dimension? MaxHeight { get; set; }

        // --- 间距 ---
        public Spacing? Margin { get; set; }
        public Spacing? Padding { get; set; }

        // --- Flex 布局 ---
        public LayoutDirection Direction { get; set; } = LayoutDirection.Vertical;
        public JustifyContent JustifyContent { get; set; } = JustifyContent.Start;
        public AlignItems AlignItems { get; set; } = AlignItems.Start;
        public float FlexGrow { get; set; }
        public float FlexShrink { get; set; }
        public float Gap { get; set; }
        public bool Float { get; set; }
        public Overflow Overflow { get; set; } = Overflow.Visible;

        // --- 外观 ---
        public Core.Color? BackgroundColor { get; set; }
        public Core.Color? BorderColor { get; set; }
        public Core.Color? FocusedBorderColor { get; set; }
        public BorderStyle BorderStyle { get; set; } = BorderStyle.None;
        public float BorderWidth { get; set; }
        public float BorderRadius { get; set; }

        // --- 文本属性 (Text 元素) ---
        public string? Text { get; set; }
        public string? FontFamily { get; set; }
        public float FontSize { get; set; } = 14;
        public Core.Color? TextColor { get; set; }
        public string? FontWeight { get; set; }
        public bool MouseThrough { get; set; } = true;
        public bool NoWrap { get; set; }

        // --- Input 属性 ---
        public string? InputValue { get; set; }
        public Core.Point? InputMethodAnchorPoint { get; set; }
        public nint EditHwnd { get; set; }
        public nint NativeFontHandle { get; set; }
        public nint NativeBrushHandle { get; set; }

        // --- 事件处理器 ---
        public Action<MouseButton>? OnClick { get; set; }
        public Action<Core.Point>? OnMouseMove { get; set; }
        public Action<MouseEvent>? OnPointerDown { get; set; }
        public Action<MouseEvent>? OnPointerMove { get; set; }
        public Action<MouseEvent>? OnPointerUp { get; set; }
        public Action? OnMouseEnter { get; set; }
        public Action? OnMouseLeave { get; set; }
        public Action? OnMouseDown { get; set; }
        public Action? OnMouseUp { get; set; }
        public Action<int>? OnKeyDown { get; set; }
        public Action<int>? OnKeyUp { get; set; }
        public Action<string>? OnTextInput { get; set; }
        public Action<TextCompositionEvent>? OnTextComposition { get; set; }
        public Action? OnFocus { get; set; }
        public Action? OnBlur { get; set; }
        public Action<string>? OnValueChanged { get; set; }

        // --- 滚动 ---
        public float ScrollOffsetX { get; set; }
        public float ScrollOffsetY { get; set; }

        // --- 状态 ---
        public bool IsHovered { get; set; }
        public bool IsFocused { get; set; }

        public Win32Element(string elementType)
        {
            ElementType = elementType;
        }

        public Image? NativeImage { get; set; }

        /// <summary>
        /// 获取元素在窗口中的绝对边界矩形
        /// </summary>
        public RectangleF GetAbsoluteBounds()
        {
            return new RectangleF(AbsoluteX, AbsoluteY, LayoutWidth, LayoutHeight);
        }
    }
}

namespace EchoUI.Core
{
    public static class ElementCoreName
    {
        public const string Container = "EchoUI-Container";
        public const string Text = "EchoUI-Text";
        public const string Input = "EchoUI-Input";
    }

    public record class NativeProps : Props
    {
        public string Type { get; init; } = null!;
        public ValueDictionary<string, object?>? Properties { get; init; }
    }

    /// <summary>
    /// 容器属性模块：整合了布局、外观、交互和子元素布局的完整设置。
    /// </summary>
    public record class ContainerProps : Props
    {
        // --- 布局 (尺寸与外边距) ---
        /// <summary>
        /// 元素的宽度。默认为 null (自动)。
        /// </summary>
        public Dimension? Width { get; init; }
        /// <summary>
        /// 元素的高度。
        /// </summary>
        public Dimension? Height { get; init; }
        /// <summary>
        /// 元素的最小宽度。
        /// </summary>
        public Dimension? MinWidth { get; init; }
        /// <summary>
        /// 元素的最小高度。
        /// </summary>
        public Dimension? MinHeight { get; init; }
        /// <summary>
        /// 元素的最大宽度。
        /// </summary>
        public Dimension? MaxWidth { get; init; }
        /// <summary>
        /// 元素的最大高度。
        /// </summary>
        public Dimension? MaxHeight { get; init; }
        /// <summary>
        /// 元素的外边距。
        /// </summary>
        public Spacing? Margin { get; init; }

        /// <summary>
        /// 是否浮动（不占据布局空间）。
        /// </summary>
        public bool Float { get; init; } = false;

        /// <summary>
        /// 内容溢出处理方式。
        /// </summary>
        public Overflow? Overflow { get; init; }

        // --- 背景 ---
        /// <summary>
        /// 元素的背景颜色。
        /// </summary>
        public Color? BackgroundColor { get; init; }

        // --- 边框 ---
        /// <summary>
        /// 边框的样式。
        /// </summary>
        public BorderStyle? BorderStyle { get; init; }
        /// <summary>
        /// 边框的颜色。
        /// </summary>
        public Color? BorderColor { get; init; }
        /// <summary>
        /// 边框的宽度。
        /// </summary>
        public float? BorderWidth { get; init; }
        /// <summary>
        /// 边框的圆角半径。
        /// </summary>
        public float? BorderRadius { get; init; }

        // --- 动画 ---
        /// <summary>
        /// 定义当指定属性发生变化时应用的过渡动画。
        /// Key: 使用 nameof() 指定的属性名 (例如, nameof(BackgroundColor))。
        /// Value: 描述动画的 Transition 对象。
        /// </summary>
        public ValueDictionary<string, Transition>? Transitions { get; init; }

        // --- 通用交互与状态 ---
        /// <summary>
        /// 点击事件，参数描述了点击的鼠标按键。
        /// </summary>
        public Action<MouseButton>? OnClick { get; init; }

        /// <summary>
        /// 鼠标移动事件，参数为鼠标在控件内的坐标。
        /// </summary>
        public Action<Point>? OnMouseMove { get; init; }
        public Action<MouseEvent>? OnPointerDown { get; init; }
        public Action<MouseEvent>? OnPointerMove { get; init; }
        public Action<MouseEvent>? OnPointerUp { get; init; }

        /// <summary>
        /// 鼠标进入元素区域时触发。
        /// </summary>
        public Action? OnMouseEnter { get; init; }
        /// <summary>
        /// 鼠标离开元素区域时触发。
        /// </summary>
        public Action? OnMouseLeave { get; init; }
        /// <summary>
        /// 在元素上按下鼠标按键时触发。
        /// </summary>
        public Action? OnMouseDown { get; init; }
        /// <summary>
        /// 在元素上释放鼠标按键时触发。
        /// </summary>
        public Action? OnMouseUp { get; init; }

        /// <summary>
        /// 键盘按下事件，参数为按键的虚拟码。
        /// </summary>
        public Action<int>? OnKeyDown { get; init; }

        /// <summary>
        /// 键盘抬起事件，参数为按键的虚拟码。
        /// </summary>
        public Action<int>? OnKeyUp { get; init; }

        public Action<string>? OnTextInput { get; init; }
        public Action<TextCompositionEvent>? OnTextComposition { get; init; }
        public Point? InputMethodAnchorPoint { get; init; }
        public bool SuppressContextMenu { get; init; } = false;
        public Action? OnFocus { get; init; }
        public Action? OnBlur { get; init; }

        // --- 子元素布局与内边距 ---
        /// <summary>
        /// 子元素的布局方向。未设置时默认为 Vertical（纵向排列）。
        /// </summary>
        public LayoutDirection? Direction { get; init; } = LayoutDirection.Vertical;
        /// <summary>
        /// 子元素在主轴上的对齐方式。未设置时默认为 Start。
        /// </summary>
        public JustifyContent? JustifyContent { get; init; }
        /// <summary>
        /// 子元素在交叉轴上的对齐方式。未设置时默认为 Stretch（子元素撑满交叉轴）。
        /// </summary>
        public AlignItems? AlignItems { get; init; }
        /// <summary>
        /// 子元素在主轴上的放大比例。未设置时默认为 0（不自动增长）。
        /// </summary>
        public float? FlexGrow { get; init; }
        /// <summary>
        /// 子元素在主轴上的缩小比例。未设置时默认为 0（不自动收缩）。
        /// 注意：CSS Flexbox 默认为 1，但 EchoUI 的 FlexLayout 中 shrink 会压缩用于判断溢出的
        /// 内容尺寸，导致 Overflow.Auto/Scroll 失效，故默认关闭。
        /// </summary>
        public float? FlexShrink { get; init; }
        /// <summary>
        /// 子元素之间的间距。
        /// </summary>
        public float? Gap { get; init; }
        /// <summary>
        /// 元素的内边距。
        /// </summary>
        public Spacing? Padding { get; init; }
        /// <summary>影子颜色（通过底部边框模拟 3D 投影）</summary>
        public Color? ShadowColor { get; init; }
        /// <summary>光标样式 (例如 "pointer", "not-allowed")</summary>
        public string? Cursor { get; init; }
        /// <summary>元素透明度</summary>
        public float? Opacity { get; init; }
    }

    /// <summary>
    /// 文本属性模块：负责文本内容和样式。
    /// </summary>
    public record class TextProps : Props
    {
        /// <summary>
        /// 显示的文本内容。
        /// </summary>
        public string Text { get; init; } = "";
        /// <summary>
        /// 字体家族的名称。
        /// </summary>
        public string? FontFamily { get; init; }
        /// <summary>
        /// 字体的大小。
        /// </summary>
        public float? FontSize { get; init; }
        /// <summary>
        /// 文本的颜色。
        /// </summary>
        public Color? Color { get; init; }
        /// <summary>
        /// 字体粗细。
        /// </summary>
        public string? FontWeight { get; init; }
        /// <summary>
        /// 设置为 true 时，该文本将不会拦截鼠标事件，事件会"穿透"到下层控件。
        /// </summary>
        public bool MouseThrough { get; set; } = true;
        public bool NoWrap { get; init; } = false;
    }

    public partial class Elements
    {
        /// <summary>
        /// 创建一个空元素，用于占位。
        /// </summary>
        public static Element Empty() => new(ElementCoreName.Container, new ContainerProps { Width = Dimension.ZeroPixels, Height = Dimension.ZeroPixels });

        // 创建组件Element的工厂
        public static Element Create(Component component, Props props) => new(component, props);
        public static Element Create(AsyncComponent asyncComponent, Props props) => new(asyncComponent, props);

        // 创建备忘录组件的工厂
        public static Element Memo(Component component, Props props) => new(component, props with { AreEqual = (old, @new) => old.Equals(@new) });
        public static Element Memo(AsyncComponent asyncComponent, Props props) => new(asyncComponent, props with { AreEqual = (old, @new) => old.Equals(@new) });

        // 创建原生元素Element的工厂
        [Element(DefaultProperty = nameof(ContainerProps.Children))]
        public static Element Container(ContainerProps props) => new(ElementCoreName.Container, props);

        [Element(DefaultProperty = nameof(TextProps.Text))]
        public static Element Text(TextProps props) => new(ElementCoreName.Text, props);

        [Element(DefaultProperty = nameof(NativeProps.Type))]
        public static Element Native(NativeProps props) => new(props.Type, props);
    }
}

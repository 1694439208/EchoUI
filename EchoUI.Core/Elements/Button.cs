namespace EchoUI.Core
{
    /// <summary>
    /// 按钮的属性。
    /// </summary>
    public record class ButtonProps : Props
    {
        public string Text { get; init; }
        /// <summary>
        /// 元素的宽度。
        /// </summary>
        public Dimension? Width { get; init; }
        /// <summary>
        /// 元素的高度。
        /// </summary>
        public Dimension? Height { get; init; }
        public Action<MouseButton>? OnClick { get; init; }
        public Color? BackgroundColor { get; init; }
        public Color? HoverColor { get; init; }
        public Color? PressedColor { get; init; }
        public Color? TextColor { get; init; }
        public Spacing? Padding { get; init; }
        public float? BorderRadius { get; init; }
    }

    public partial class Elements
    {
        /// <summary>
        /// Button 组件的渲染函数。
        /// </summary>
        [Element(DefaultProperty = nameof(ButtonProps.Text))]
        public static Element Button(ButtonProps props)
        {
            var (isHovered, setIsHovered, _) = Hooks.State(false);
            var (isPressed, setIsPressed, _) = Hooks.State(false);

            const float textFontSize = 14f;
            const string textFontWeight = "600";

            var padding = props.Padding ?? new Spacing(Dimension.Pixels(14), Dimension.Pixels(9));
            var baseBackgroundColor = props.BackgroundColor ?? Color.FromHex("#2563EB");
            var baseBorderColor = Darken(baseBackgroundColor, 0.18f);
            var defaultTextColor = GetPreferredTextColor(baseBackgroundColor);

            var backgroundColor = baseBackgroundColor;
            var borderColor = baseBorderColor;
            var textColor = props.TextColor ?? defaultTextColor;

            if (isPressed.Value)
            {
                backgroundColor = props.PressedColor ?? Darken(baseBackgroundColor, 0.14f);
                borderColor = Darken(baseBorderColor, 0.08f);
            }
            else if (isHovered.Value)
            {
                backgroundColor = props.HoverColor ?? Lighten(baseBackgroundColor, 0.06f);
                borderColor = Lighten(baseBorderColor, 0.03f);
            }

            var measuredTextWidth = Hooks.MeasureText(new TextMeasurementRequest
            {
                Text = props.Text,
                FontSize = textFontSize,
                FontWeight = textFontWeight
            }).Width;
            var autoWidth = Math.Max(88f, measuredTextWidth + GetButtonHorizontalPadding(padding) + 24f);

            var transitions = new ValueDictionary<string, Transition>(new Dictionary<string, Transition>
            {
                [nameof(ContainerProps.BackgroundColor)] = new(140, Easing.EaseOut),
                [nameof(ContainerProps.BorderColor)] = new(140, Easing.EaseOut)
            });

            return Container(new ContainerProps
            {
                Key = props.Key,
                Width = props.Width ?? Dimension.Pixels(autoWidth),
                Height = props.Height ?? Dimension.Pixels(36),
                MinWidth = Dimension.Pixels(88),
                JustifyContent = JustifyContent.Center,
                AlignItems = AlignItems.Center,
                Padding = padding,
                BackgroundColor = backgroundColor,
                BorderStyle = BorderStyle.Solid,
                BorderColor = borderColor,
                BorderWidth = 1,
                BorderRadius = props.BorderRadius ?? 8,
                Overflow = Overflow.Hidden,
                Transitions = transitions,
                OnMouseEnter = () => setIsHovered(true),
                OnMouseLeave = () =>
                {
                    setIsHovered(false);
                    setIsPressed(false);
                },
                OnMouseDown = () => setIsPressed(true),
                OnMouseUp = () => setIsPressed(false),
                OnClick = button => props.OnClick?.Invoke(button),
                Children =
                [
                    Text(new TextProps
                    {
                        Text = props.Text,
                        Color = textColor,
                        FontSize = textFontSize,
                        FontWeight = textFontWeight,
                        NoWrap = true
                    })
                ]
            });
        }

        private static float GetButtonHorizontalPadding(Spacing padding)
        {
            return GetButtonPixelValue(padding.Left) + GetButtonPixelValue(padding.Right);
        }

        private static float GetButtonPixelValue(Dimension dimension)
        {
            return dimension.Unit == DimensionUnit.Pixels ? dimension.Value : 0;
        }

        private static Color Lighten(Color color, float amount)
        {
            amount = Math.Clamp(amount, 0f, 1f);
            return new Color(
                Lerp(color.R, 255, amount),
                Lerp(color.G, 255, amount),
                Lerp(color.B, 255, amount),
                color.A);
        }

        private static Color Darken(Color color, float amount)
        {
            amount = Math.Clamp(amount, 0f, 1f);
            return new Color(
                Lerp(color.R, 0, amount),
                Lerp(color.G, 0, amount),
                Lerp(color.B, 0, amount),
                color.A);
        }

        private static Color GetPreferredTextColor(Color backgroundColor)
        {
            var luminance = (0.2126 * backgroundColor.R) + (0.7152 * backgroundColor.G) + (0.0722 * backgroundColor.B);
            return luminance >= 160 ? Color.Black : Color.White;
        }

        private static byte Lerp(byte from, int to, float amount)
        {
            return (byte)Math.Clamp(Math.Round(from + ((to - from) * amount)), 0, 255);
        }
    }
}

namespace EchoUI.Core
{
    /// <summary>
    /// Switch (开关) 组件的属性。
    /// </summary>
    public record class SwitchProps : Props
    {
        /// <summary>
        /// 开关的初始状态。默认为 false (关)。
        /// </summary>
        public bool DefaultIsOn { get; init; } = false;

        /// <summary>
        /// 当开关状态因用户点击而改变时触发的回调，参数为新的状态。
        /// </summary>
        public Action<bool>? OnToggle { get; init; }

        /// <summary>
        /// "开"状态下的背景颜色。
        /// </summary>
        public Color? OnColor { get; init; }

        /// <summary>
        /// "关"状态下的背景颜色。
        /// </summary>
        public Color? OffColor { get; init; }

        /// <summary>
        /// 滑块的颜色。
        /// </summary>
        public Color? ThumbColor { get; init; }

        /// <summary>
        /// 组件的宽度。
        /// </summary>
        public Dimension? Width { get; init; }

        /// <summary>
        /// 组件的高度。
        /// </summary>
        public Dimension? Height { get; init; }

        /// <summary>
        /// 切换动画的持续时间（毫秒）。
        /// </summary>
        public int AnimationDuration { get; init; } = 150; // ms
    }

    public partial class Elements
    {
        /// <summary>
        /// Switch (开关) 组件，使用可配置曲线动画
        /// </summary>
        [Element]
        public static Element Switch(SwitchProps props)
        {
            var (isOn, setIsOn, _) = Hooks.State(props.DefaultIsOn);

            var widthPx = props.Width?.Unit == DimensionUnit.Pixels ? props.Width.Value.Value : 50f;
            var heightPx = props.Height?.Unit == DimensionUnit.Pixels ? props.Height.Value.Value : 26f;
            const float paddingPx = 3f;
            var thumbSizePx = heightPx - (2 * paddingPx);
            var slidableWidth = widthPx - (2 * paddingPx) - thumbSizePx;

            // 动画
            var trackTransitions = new ValueDictionary<string, Transition>(new Dictionary<string, Transition>
            {
                [nameof(ContainerProps.BackgroundColor)] = new(props.AnimationDuration, Easing.EaseInOut)
            });

            var thumbTransitions = new ValueDictionary<string, Transition>(new Dictionary<string, Transition>
            {
                [nameof(ContainerProps.Margin)] = new(props.AnimationDuration, Easing.EaseInOut)
            });

            return Container(new ContainerProps()
            {
                Direction = LayoutDirection.Horizontal,
                Key = props.Key,
                Width = Dimension.Pixels(widthPx),
                Height = Dimension.Pixels(heightPx),
                BackgroundColor = isOn.Value ? (props.OnColor ?? DesignTokens.Success) : (props.OffColor ?? DesignTokens.ShadowInput),
                Transitions = trackTransitions,
                BorderRadius = heightPx / 2,
                Padding = new Spacing(Dimension.Pixels(paddingPx)),
                JustifyContent = JustifyContent.Start,
                AlignItems = AlignItems.Center,
                OnClick = _ =>
                {
                    var newState = !isOn.Value;
                    setIsOn(newState);
                    props.OnToggle?.Invoke(newState);
                },
                Children =
                [
                    Container(new ContainerProps()
                    {
                        Width = Dimension.Pixels(thumbSizePx),
                        Height = Dimension.Pixels(thumbSizePx),
                        BackgroundColor = props.ThumbColor ?? Color.White,
                        BorderRadius = thumbSizePx / 2,
                        Margin = new Spacing(Dimension.Pixels(isOn.Value ? slidableWidth : 0f), Dimension.Pixels(0), Dimension.Pixels(isOn.Value ? 0f : slidableWidth), Dimension.Pixels(0)),
                        Transitions = thumbTransitions
                    })
                ]
            });
        }
    }
}

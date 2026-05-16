namespace EchoUI.Core
{
    /// <summary>
    /// Tabs 组件的属性。
    /// </summary>
    public record class TabProps : Props
    {
        /// <summary>
        /// 每个 Tab 的标题。
        /// </summary>
        public IReadOnlyList<string> Titles { get; init; } = Array.Empty<string>();

        /// <summary>
        /// 一个根据索引生成 Tab 内容的函数。
        /// </summary>
        public Func<int, Element> Content { get; init; } = _ => Elements.Container(new ContainerProps());

        /// <summary>
        /// 初始选中的 Tab 索引。
        /// </summary>
        public int InitialIndex { get; init; } = 0;

        /// <summary>
        /// 当 Tab 切换时触发的回调。
        /// </summary>
        public Action<int>? OnTabChanged { get; init; }

        // --- Styling ---

        /// <summary>
        /// 激活状态 Tab 的背景颜色。
        /// </summary>
        public Color? ActiveTabBackgroundColor { get; init; }

        /// <summary>
        /// 非激活状态 Tab 的背景颜色。
        /// </summary>
        public Color? InactiveTabBackgroundColor { get; init; }

        /// <summary>
        /// 激活状态 Tab 的文本颜色。
        /// </summary>
        public Color? ActiveTabTextColor { get; init; }

        /// <summary>
        /// 非激活状态 Tab 的文本颜色。
        /// </summary>
        public Color? InactiveTabTextColor { get; init; }

        /// <summary>
        /// 切换动画的持续时间（毫秒）。
        /// </summary>
        public int AnimationDuration { get; init; } = 250; // ms
    }

    public partial class Elements
    {
        /// <summary>
        /// A Tabs component with a simplified sliding animation.
        /// </summary>
        [Element(DefaultProperty = nameof(TabProps.Titles))]
        public static Element Tabs(TabProps props)
        {
            var (currentIndex, setCurrentIndex, _) = Hooks.State(props.InitialIndex);

            // The click handler is now much simpler.
            Action<int> selectTab = (newIndex) =>
            {
                if (newIndex == currentIndex.Value) return;

                setCurrentIndex(newIndex);
                props.OnTabChanged?.Invoke(newIndex);
            };

            // --- Tab Headers ---
            var tabHeaders = new List<Element>();
            for (var i = 0; i < props.Titles.Count; i++)
            {
                var index = i;
                var isActive = currentIndex.Value == index;
                tabHeaders.Add(
                    Container(new ContainerProps
                    {
                        Key = props.Titles[index],
                        Width = Dimension.Percent(100.0f / props.Titles.Count),
                        JustifyContent = JustifyContent.Center,
                        AlignItems = AlignItems.Center,
                        Padding = new Spacing(Dimension.Pixels(12), Dimension.Pixels(8)),
                        BackgroundColor = isActive
                            ? (props.ActiveTabBackgroundColor ?? DesignTokens.PrimaryBg)
                            : props.InactiveTabBackgroundColor,
                        OnClick = _ => selectTab(index),
                        Children =
                        [
                            Text(new TextProps
                            {
                                Text = props.Titles[index],
                                Color = isActive
                                    ? (props.ActiveTabTextColor ?? Color.Black)
                                    : (props.InactiveTabTextColor ?? Color.Gray)
                            })
                        ]
                    })
                );
            }

            // --- Tab Content ---
            // 1. Create all panels.
            var allPanels = new List<Element>();
            for (int i = 0; i < props.Titles.Count; i++)
            {
                allPanels.Add(Container(new ContainerProps
                {
                    Key = props.Titles[i],
                    Children = [props.Content(i)],
                    Width = Dimension.Percent(currentIndex.Value == i ? 100 : 0),
                    Transitions = new ValueDictionary<string, Transition>(new Dictionary<string, Transition>
                    {
                        [nameof(ContainerProps.Width)] = new(props.AnimationDuration, Easing.EaseInOut)
                    }),
                    Overflow = Overflow.Hidden
                }));
            }

            // 2. Place all panels into a single wide track.
            var contentTrack = Container(new ContainerProps
            {
                Direction = LayoutDirection.Horizontal,
                Children = allPanels,
                Width = Dimension.Percent(100),
                FlexGrow = 1
            });

            return Container(new ContainerProps
            {
                Key = props.Key,
                Direction = LayoutDirection.Vertical,
                Children =
                [
                    // Tab Header container
                    Container(new ContainerProps
                    {
                        Direction = LayoutDirection.Horizontal,
                        Width = Dimension.Percent(100),
                        BorderColor = DesignTokens.Border,
                        BorderWidth = 1,
                        BorderStyle = BorderStyle.Solid,
                        Children = tabHeaders
                    }),

                    // Content Viewport (acts as a mask for the sliding track)
                    Container(new ContainerProps
                    {
                        Direction = LayoutDirection.Horizontal,
                        Children = [contentTrack],
                        FlexGrow = 1,
                        Width = Dimension.Percent(100)
                    })
                ]
            });
        }
    }
}

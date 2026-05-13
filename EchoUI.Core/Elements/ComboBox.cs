using static EchoUI.Core.Hooks;

namespace EchoUI.Core
{
    /// <summary>
    /// ComboBox (下拉选择框) 组件的属性。
    /// </summary>
    public record class ComboBoxProps : Props
    {
        /// <summary>
        /// 所有可选项的文本列表。
        /// </summary>
        public IReadOnlyList<string> Options { get; init; } = Array.Empty<string>();

        /// <summary>
        /// 当前选中的选项的索引。
        /// </summary>
        public int SelectedIndex { get; init; } = 0;

        /// <summary>
        /// 当选项改变时触发的回调。
        /// </summary>
        public Action<int>? OnSelectionChanged { get; init; }

        public Color? BackgroundColor { get; init; }
        public Color? TextColor { get; init; }
        public Color? BorderColor { get; init; }

        /// <summary>
        /// 下拉菜单的背景颜色。
        /// </summary>
        public Color? DropdownBackgroundColor { get; init; }
    }

    public partial class Elements
    {
        /// <summary>
        /// ComboBox (下拉选择框) 组件。
        /// </summary>
        [Element(DefaultProperty = nameof(ComboBoxProps.Options))]
        public static Element ComboBox(ComboBoxProps props)
        {
            var (isOpen, setIsOpen, _) = Hooks.State(false);
            var (selectIndex, setSelectIndex, _) = State(props.SelectedIndex);
            var (hoverIndex, setHoverIndex, _) = State(-1);

            var backgroundColor = props.BackgroundColor ?? Color.White;
            var textColor = props.TextColor ?? Color.FromHex("1F2937");
            var borderColor = props.BorderColor ?? Color.FromHex("D0D7E2");
            var dropdownBackgroundColor = props.DropdownBackgroundColor ?? Color.White;
            var accentColor = Color.FromHex("4F8CFF");
            var hoverBackgroundColor = Color.FromHex("F3F7FF");
            var selectedBackgroundColor = Color.FromHex("EAF2FF");
            var mutedTextColor = Color.FromHex("6B7280");

            var selectedOptionText = (selectIndex.Value >= 0 && selectIndex.Value < props.Options.Count)
                ? props.Options[selectIndex.Value]
                : "Select...";

            var dropdownItems = new List<Element>();
            if (isOpen.Value)
            {
                for (var i = 0; i < props.Options.Count; i++)
                {
                    var index = i;
                    var isSelected = selectIndex.Value == index;
                    var isHovered = hoverIndex.Value == index;

                    dropdownItems.Add(Container(new ContainerProps
                    {
                        Key = props.Options[index],
                        Width = Dimension.Percent(100),
                        Height = Dimension.Pixels(36),
                        Direction = LayoutDirection.Horizontal,
                        JustifyContent = JustifyContent.SpaceBetween,
                        AlignItems = AlignItems.Center,
                        Padding = new Spacing(Dimension.Pixels(12), Dimension.Pixels(8)),
                        BackgroundColor = isHovered ? hoverBackgroundColor : (isSelected ? selectedBackgroundColor : Color.Transparent),
                        BorderRadius = 6,
                        OnMouseEnter = () => setHoverIndex(index),
                        OnMouseLeave = () =>
                        {
                            if (hoverIndex.Value == index)
                                setHoverIndex(-1);
                        },
                        OnClick = _ =>
                        {
                            setSelectIndex(index);
                            setHoverIndex(-1);
                            props.OnSelectionChanged?.Invoke(index);
                            setIsOpen(false);
                        },
                        Children =
                        [
                            Text(new TextProps
                            {
                                Text = props.Options[index],
                                Color = isSelected ? accentColor : textColor
                            }),
                            Text(new TextProps
                            {
                                Text = isSelected ? "✓" : string.Empty,
                                Color = accentColor,
                                FontSize = 11,
                                MouseThrough = true
                            })
                        ]
                    }));
                }
            }

            var visibleOptionCount = Math.Min(props.Options.Count, 6);
            var shouldScroll = props.Options.Count > visibleOptionCount;
            var dropdownHeight = isOpen.Value
                ? Dimension.Pixels(visibleOptionCount * 36 + Math.Max(0, visibleOptionCount - 1) * 4 + 8 + 2)
                : Dimension.ZeroPixels;

            return Container(new ContainerProps
            {
                Key = props.Key,
                Direction = LayoutDirection.Vertical,
                Overflow = Overflow.Visible,
                OnBlur = () =>
                {
                    setIsOpen(false);
                    setHoverIndex(-1);
                },
                Children =
                [
                    Container(new ContainerProps
                    {
                        Width = Dimension.Percent(100),
                        Height = Dimension.Pixels(36),
                        Direction = LayoutDirection.Horizontal,
                        JustifyContent = JustifyContent.SpaceBetween,
                        AlignItems = AlignItems.Center,
                        Padding = new Spacing(Dimension.Pixels(12), Dimension.Pixels(8)),
                        BackgroundColor = backgroundColor,
                        BorderWidth = 1,
                        BorderStyle = BorderStyle.Solid,
                        BorderColor = isOpen.Value ? accentColor : borderColor,
                        BorderRadius = 6,
                        OnClick = _ =>
                        {
                            setHoverIndex(-1);
                            setIsOpen(!isOpen.Value);
                        },
                        Children =
                        [
                            Text(new TextProps
                            {
                                Text = selectedOptionText,
                                Color = textColor,
                                NoWrap = true
                            }),
                            Text(new TextProps
                            {
                                Text = isOpen.Value ? "▲" : "▼",
                                FontSize = 10,
                                Color = isOpen.Value ? accentColor : mutedTextColor,
                                MouseThrough = true
                            })
                        ]
                    }),
                    Container(new ContainerProps
                    {
                        Float = true,
                        Width = Dimension.Percent(100),
                        Margin = new Spacing(Dimension.ZeroPixels, Dimension.Pixels(4), Dimension.ZeroPixels, Dimension.ZeroPixels),
                        Children =
                        [
                            Container(new ContainerProps
                            {
                                Width = Dimension.Percent(100),
                                Height = dropdownHeight,
                                Padding = new Spacing(Dimension.Pixels(4)),
                                Direction = LayoutDirection.Vertical,
                                Gap = 4,
                                Overflow = Overflow.Auto,
                                BackgroundColor = dropdownBackgroundColor,
                                BorderWidth = isOpen.Value ? 1 : 0,
                                BorderStyle = BorderStyle.Solid,
                                BorderColor = borderColor,
                                BorderRadius = 8,
                                Transitions =
                                [
                                    [nameof(ContainerProps.Height), new Transition(150, Easing.EaseInOut)]
                                ],
                                Children = dropdownItems
                            })
                        ]
                    })
                ]
            });
        }
    }
}

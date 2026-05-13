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

            var selectedOptionText = (selectIndex.Value >= 0 && selectIndex.Value < props.Options.Count)
                ? props.Options[selectIndex.Value]
                : "Select...";

            var (moveIndex, setMoveIndex, _) = State(props.SelectedIndex);
            // Build the dropdown items list when open
            var dropdownItems = new List<Element>();
            if (isOpen.Value)
            {
                for (var i = 0; i < props.Options.Count; i++)
                {
                    var index = i;
                    dropdownItems.Add(Container(new ContainerProps
                    {
                        Key = props.Options[index],
                        Width = Dimension.Percent(100),
                        Height = Dimension.Pixels(35),
                        JustifyContent = JustifyContent.Center,
                        Padding = new Spacing(Dimension.Pixels(8), Dimension.Pixels(6)),
                        BackgroundColor = moveIndex.Value == index ? Color.Gray : Color.White,
                        OnClick = _ =>
                        {
                            setSelectIndex(index);
                            props.OnSelectionChanged?.Invoke(index);
                            setIsOpen(false); // Close after selection
                        },
                        OnMouseMove = _ => setMoveIndex(index),
                        Children = [Text(new TextProps { Text = props.Options[index], Color = props.TextColor ?? Color.Black })]
                    }));
                }
            }

            return Container(new ContainerProps // Main wrapper
            {
                Key = props.Key,
                Direction = LayoutDirection.Vertical,
                Overflow = Overflow.Visible,
                Children =
                [
                    // Display box
                    Container(new ContainerProps
                    {
                        Width = Dimension.Percent(100),
                        Direction = LayoutDirection.Horizontal,
                        JustifyContent = JustifyContent.SpaceBetween,
                        AlignItems = AlignItems.Center,
                        Padding = new Spacing(Dimension.Pixels(8), Dimension.Pixels(6)),
                        BackgroundColor = props.BackgroundColor ?? Color.White,
                        BorderWidth = 1,
                        BorderStyle = BorderStyle.Solid,
                        BorderColor = props.BorderColor ?? Color.Gray,
                        OnClick = _ => setIsOpen(!isOpen.Value),
                        Children =
                        [
                            Text(new TextProps { Text = selectedOptionText, Color = props.TextColor ?? Color.Black }),
                            Text(new TextProps { Text = "▼", FontSize = 10, Color = props.TextColor ?? Color.Gray })
                        ]
                    }),

                    // Dropdown list wrapper (Floating)
                    Container(new ContainerProps
                    {
                        Float = true,
                        Width = Dimension.Percent(100),
                        Children = [
                            Container(new ContainerProps
                            {
                                Width = Dimension.Percent(100),
                                Height = isOpen.Value ? Dimension.Pixels(35 * props.Options.Count + 2) : Dimension.ZeroPixels,
                                Transitions = [
                                    [nameof(ContainerProps.Height), new Transition(150, Easing.EaseInOut)]
                                ],
                                Direction = LayoutDirection.Vertical,
                                BackgroundColor = props.DropdownBackgroundColor ?? Color.White,
                                BorderWidth = isOpen.Value ? 1 : 0,
                                BorderStyle = BorderStyle.Solid,
                                BorderColor = props.BorderColor ?? Color.Gray,
                                Children = dropdownItems
                            })
                        ]
                    })
                ]
            });
        }
    }
}

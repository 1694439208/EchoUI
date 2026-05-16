namespace EchoUI.Core
{
    /// <summary>
    /// CheckBox (复选框) 组件的属性。
    /// </summary>
    public record class CheckBoxProps : Props
    {
        /// <summary>
        /// 复选框是否被选中。
        /// </summary>
        public bool IsChecked { get; init; } = false;

        /// <summary>
        /// 当复选框状态改变时触发的回调。
        /// </summary>
        public Action<bool>? OnToggle { get; init; }

        /// <summary>
        /// 显示在复选框旁边的文本标签。
        /// </summary>
        public string? Label { get; init; }

        /// <summary>
        /// 勾选标记的颜色。
        /// </summary>
        public Color? CheckColor { get; init; }

        /// <summary>
        /// 边框的颜色。
        /// </summary>
        public Color? BorderColor { get; init; }
    }

    public partial class Elements
    {
        /// <summary>
        /// CheckBox (复选框) 组件。
        /// </summary>
        [Element(DefaultProperty = nameof(CheckBoxProps.Label))]
        public static Element CheckBox(CheckBoxProps props)
        {
            var (check, _, updateCheck) = Hooks.State(props.IsChecked);

            return Container(new ContainerProps
            {
                Key = props.Key,
                Direction = LayoutDirection.Horizontal,
                AlignItems = AlignItems.Center,
                Gap = 8,
                OnClick = _ =>
                {
                    updateCheck(v => !v);
                    props.OnToggle?.Invoke(check.Value);
                },
                Children =
                [
                    Container(new ContainerProps
                    {
                        Width = Dimension.Pixels(20),
                        Height = Dimension.Pixels(20),
                        BorderWidth = 2,
                        BorderStyle = BorderStyle.Solid,
                        BorderColor = props.BorderColor ?? Color.Gray,
                        JustifyContent = JustifyContent.Center,
                        AlignItems = AlignItems.Center,
                        Children =
                        [
                            check.Value
                                ? Text(new TextProps
                                {
                                    Text = "✓",
                                    FontSize = 16,
                                    Color = props.CheckColor ?? DesignTokens.Primary
                                })
                                : Empty()
                        ]
                    }),
                    string.IsNullOrEmpty(props.Label)
                        ? Empty()
                        : Text(new TextProps { Text = props.Label })
                    ]
            });
        }
    }
}

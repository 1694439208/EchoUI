namespace EchoUI.Demo;

using EchoUI.Core;
using static EchoUI.Core.Elements;
using static EchoUI.Core.Hooks;

public static class AnimationDemo
{
    private const int Fast = 360;
    private const int Normal = 720;
    private const int Slow = 980;

    public static Element Create(Props props)
    {
        var (playing, setPlaying, _) = State(false);
        var on = playing.Value;
        Action toggle = () => setPlaying(!on);

        return Container(new ContainerProps
        {
            Width = Dimension.Percent(100),
            Height = Dimension.ViewportHeight(100),
            BackgroundColor = Color.FromHex("#08111F"),
            Padding = new Spacing(Dimension.Pixels(24)),
            Direction = LayoutDirection.Vertical,
            Gap = 18,
            Overflow = Overflow.Auto,
            Children =
            [
                Hero(on, toggle),
                ShowcaseGrid(on, toggle),
                EasingTheater(on),
                BuiltInControls(),
                SupportedProperties()
            ]
        });
    }

    private static Element Hero(bool on, Action toggle)
    {
        return Container(new ContainerProps
        {
            Width = Dimension.Percent(100),
            Height = Dimension.Pixels(on ? 250 : 210),
            BackgroundColor = on ? Color.FromHex("#172554") : Color.FromHex("#111827"),
            BorderColor = on ? Color.FromHex("#60A5FA") : Color.FromHex("#243044"),
            BorderStyle = BorderStyle.Solid,
            BorderWidth = on ? 2 : 1,
            BorderRadius = on ? 32 : 18,
            Padding = new Spacing(Dimension.Pixels(on ? 30 : 22)),
            Direction = LayoutDirection.Horizontal,
            JustifyContent = JustifyContent.SpaceBetween,
            AlignItems = AlignItems.Center,
            Gap = on ? 42 : 22,
            OnClick = _ => toggle(),
            Transitions = Transitions(
                (nameof(ContainerProps.Height), new Transition(Slow, Easing.EaseInOut)),
                (nameof(ContainerProps.BackgroundColor), new Transition(Slow, Easing.EaseInOut)),
                (nameof(ContainerProps.BorderColor), new Transition(Slow, Easing.EaseInOut)),
                (nameof(ContainerProps.BorderWidth), new Transition(Slow, Easing.EaseInOut)),
                (nameof(ContainerProps.BorderRadius), new Transition(Slow, Easing.EaseInOut)),
                (nameof(ContainerProps.Padding), new Transition(Slow, Easing.EaseInOut)),
                (nameof(ContainerProps.Gap), new Transition(Slow, Easing.EaseInOut))),
            Children =
            [
                Container(new ContainerProps
                {
                    Direction = LayoutDirection.Vertical,
                    Gap = 12,
                    FlexGrow = 1,
                    Children =
                    [
                        Text(new TextProps { Text = "EchoUI Motion Showcase", Color = Color.White, FontSize = 30, FontWeight = "800" }),
                        Text(new TextProps { Text = "点击 Play / Reverse，观察颜色、尺寸、圆角、边距、间距和约束同时过渡。", Color = Color.FromHex("#C7D2FE"), FontSize = 15 }),
                        Container(new ContainerProps
                        {
                            Direction = LayoutDirection.Horizontal,
                            Gap = 10,
                            Children =
                            [
                                Badge("Color"),
                                Badge("Spacing"),
                                Badge("Size"),
                                Badge("Radius"),
                                Badge("Easing")
                            ]
                        })
                    ]
                }),
                Button(new ButtonProps
                {
                    Text = on ? "Reverse" : "Play",
                    Width = Dimension.Pixels(on ? 148 : 128),
                    Height = Dimension.Pixels(on ? 50 : 44),
                    BorderRadius = on ? 18 : 12,
                    BackgroundColor = on ? Color.FromHex("#7C3AED") : Color.FromHex("#2563EB"),
                    HoverColor = on ? Color.FromHex("#8B5CF6") : Color.FromHex("#3B82F6"),
                    PressedColor = Color.FromHex("#1D4ED8"),
                    OnClick = _ => toggle()
                }),
                HeroShape(on)
            ]
        });
    }

    private static Element HeroShape(bool on)
    {
        return Container(new ContainerProps
        {
            Width = Dimension.Pixels(on ? 178 : 126),
            Height = Dimension.Pixels(on ? 126 : 178),
            MinWidth = Dimension.Pixels(on ? 178 : 126),
            BackgroundColor = on ? Color.FromHex("#F97316") : Color.FromHex("#06B6D4"),
            BorderColor = on ? Color.FromHex("#FDBA74") : Color.FromHex("#67E8F9"),
            BorderStyle = BorderStyle.Solid,
            BorderWidth = on ? 8 : 3,
            BorderRadius = on ? 62 : 28,
            Padding = new Spacing(Dimension.Pixels(on ? 18 : 10)),
            JustifyContent = JustifyContent.Center,
            AlignItems = AlignItems.Center,
            Transitions = Transitions(
                (nameof(ContainerProps.Width), new Transition(Slow, Easing.EaseInOut)),
                (nameof(ContainerProps.Height), new Transition(Slow, Easing.EaseInOut)),
                (nameof(ContainerProps.MinWidth), new Transition(Slow, Easing.EaseInOut)),
                (nameof(ContainerProps.BackgroundColor), new Transition(Slow, Easing.EaseInOut)),
                (nameof(ContainerProps.BorderColor), new Transition(Slow, Easing.EaseInOut)),
                (nameof(ContainerProps.BorderWidth), new Transition(Slow, Easing.EaseInOut)),
                (nameof(ContainerProps.BorderRadius), new Transition(Slow, Easing.EaseInOut)),
                (nameof(ContainerProps.Padding), new Transition(Slow, Easing.EaseInOut))),
            Children =
            [
                Container(new ContainerProps
                {
                    Width = Dimension.Percent(100),
                    Height = Dimension.Percent(100),
                    BackgroundColor = on ? Color.FromHex("#111827") : Color.White,
                    BorderRadius = on ? 36 : 18,
                    Transitions = Transitions(
                        (nameof(ContainerProps.BackgroundColor), new Transition(Slow, Easing.EaseInOut)),
                        (nameof(ContainerProps.BorderRadius), new Transition(Slow, Easing.EaseInOut)))
                })
            ]
        });
    }

    private static Element ShowcaseGrid(bool on, Action toggle)
    {
        return Section("Visual scenes", "把动画能力组合成真实 UI 动效场景", [
            SceneCard("Morph card", "颜色 + 边框 + 圆角 + Padding + Size", MorphCard(on, toggle)),
            SceneCard("Slide dock", "Margin + Gap 带来位移和展开", SlideDock(on, toggle)),
            SceneCard("Accordion", "Height / MaxHeight / Padding 做折叠展开", Accordion(on, toggle)),
            SceneCard("Constraint panel", "MinWidth / MaxWidth / MinHeight / MaxHeight 约束动画", ConstraintPanel(on, toggle))
        ]);
    }

    private static Element MorphCard(bool on, Action toggle)
    {
        return Stage([
            Container(new ContainerProps
            {
                Width = Dimension.Pixels(on ? 380 : 210),
                Height = Dimension.Pixels(on ? 112 : 78),
                BackgroundColor = on ? Color.FromHex("#BE123C") : Color.FromHex("#1D4ED8"),
                BorderColor = on ? Color.FromHex("#FDA4AF") : Color.FromHex("#93C5FD"),
                BorderStyle = BorderStyle.Solid,
                BorderWidth = on ? 7 : 2,
                BorderRadius = on ? 34 : 10,
                Padding = new Spacing(Dimension.Pixels(on ? 24 : 12)),
                Direction = LayoutDirection.Vertical,
                Gap = on ? 10 : 4,
                OnClick = _ => toggle(),
                Transitions = Transitions(
                    (nameof(ContainerProps.Width), new Transition(Normal, Easing.EaseInOut)),
                    (nameof(ContainerProps.Height), new Transition(Normal, Easing.EaseInOut)),
                    (nameof(ContainerProps.BackgroundColor), new Transition(Normal, Easing.EaseInOut)),
                    (nameof(ContainerProps.BorderColor), new Transition(Normal, Easing.EaseInOut)),
                    (nameof(ContainerProps.BorderWidth), new Transition(Normal, Easing.EaseInOut)),
                    (nameof(ContainerProps.BorderRadius), new Transition(Normal, Easing.EaseInOut)),
                    (nameof(ContainerProps.Padding), new Transition(Normal, Easing.EaseInOut)),
                    (nameof(ContainerProps.Gap), new Transition(Normal, Easing.EaseInOut))),
                Children =
                [
                    Text(new TextProps { Text = "Interactive card", Color = Color.White, FontSize = 16, FontWeight = "800" }),
                    Text(new TextProps { Text = on ? "Expanded state" : "Compact state", Color = Color.FromHex("#E0E7FF"), FontSize = 12 })
                ]
            })
        ]);
    }

    private static Element SlideDock(bool on, Action toggle)
    {
        return Stage([
            Container(new ContainerProps
            {
                Direction = LayoutDirection.Horizontal,
                AlignItems = AlignItems.Center,
                Gap = on ? 24 : 6,
                Padding = new Spacing(Dimension.Pixels(12)),
                Transitions = TransitionOf(nameof(ContainerProps.Gap), Normal, Easing.EaseOut),
                OnClick = _ => toggle(),
                Children =
                [
                    DockItem("A", Color.FromHex("#EF4444"), on ? 0 : 0),
                    DockItem("B", Color.FromHex("#22C55E"), on ? 42 : 0),
                    DockItem("C", Color.FromHex("#3B82F6"), on ? 84 : 0),
                    DockItem("D", Color.FromHex("#A855F7"), on ? 126 : 0)
                ]
            })
        ]);
    }

    private static Element Accordion(bool on, Action toggle)
    {
        return Stage([
            Container(new ContainerProps
            {
                Width = Dimension.Pixels(420),
                Height = Dimension.Pixels(on ? 126 : 52),
                MaxHeight = Dimension.Pixels(on ? 126 : 52),
                BackgroundColor = Color.FromHex("#111827"),
                BorderColor = on ? Color.FromHex("#38BDF8") : Color.FromHex("#334155"),
                BorderStyle = BorderStyle.Solid,
                BorderWidth = 1,
                BorderRadius = 16,
                Padding = new Spacing(Dimension.Pixels(on ? 18 : 12)),
                Direction = LayoutDirection.Vertical,
                Gap = on ? 12 : 4,
                Overflow = Overflow.Hidden,
                OnClick = _ => toggle(),
                Transitions = Transitions(
                    (nameof(ContainerProps.Height), new Transition(Normal, Easing.EaseInOut)),
                    (nameof(ContainerProps.MaxHeight), new Transition(Normal, Easing.EaseInOut)),
                    (nameof(ContainerProps.BorderColor), new Transition(Normal, Easing.EaseInOut)),
                    (nameof(ContainerProps.Padding), new Transition(Normal, Easing.EaseInOut)),
                    (nameof(ContainerProps.Gap), new Transition(Normal, Easing.EaseInOut))),
                Children =
                [
                    Text(new TextProps { Text = on ? "Accordion expanded" : "Accordion collapsed", Color = Color.White, FontWeight = "800" }),
                    Text(new TextProps { Text = "这类动效适合设置面板、详情卡片、下拉内容。", Color = Color.FromHex("#CBD5E1"), FontSize = 13 }),
                    Text(new TextProps { Text = "Height + MaxHeight + Padding 同步过渡。", Color = Color.FromHex("#94A3B8"), FontSize = 12 })
                ]
            })
        ]);
    }

    private static Element ConstraintPanel(bool on, Action toggle)
    {
        return Stage([
            Container(new ContainerProps
            {
                Direction = LayoutDirection.Horizontal,
                AlignItems = AlignItems.Center,
                Gap = 14,
                OnClick = _ => toggle(),
                Children =
                [
                    ConstraintBox("Min", on ? 260 : 118, on ? 86 : 42, true),
                    ConstraintBox("Max", on ? 260 : 118, on ? 86 : 42, false)
                ]
            })
        ]);
    }

    private static Element ConstraintBox(string label, float width, float height, bool min)
    {
        return Container(new ContainerProps
        {
            Width = min ? null : Dimension.Pixels(280),
            Height = min ? null : Dimension.Pixels(120),
            MinWidth = min ? Dimension.Pixels(width) : null,
            MinHeight = min ? Dimension.Pixels(height) : null,
            MaxWidth = min ? null : Dimension.Pixels(width),
            MaxHeight = min ? null : Dimension.Pixels(height),
            Padding = new Spacing(Dimension.Pixels(12)),
            BackgroundColor = min ? Color.FromHex("#0EA5E9") : Color.FromHex("#10B981"),
            BorderRadius = 14,
            JustifyContent = JustifyContent.Center,
            AlignItems = AlignItems.Center,
            Transitions = Transitions(
                (nameof(ContainerProps.MinWidth), new Transition(Normal, Easing.EaseInOut)),
                (nameof(ContainerProps.MinHeight), new Transition(Normal, Easing.EaseInOut)),
                (nameof(ContainerProps.MaxWidth), new Transition(Normal, Easing.EaseInOut)),
                (nameof(ContainerProps.MaxHeight), new Transition(Normal, Easing.EaseInOut))),
            Children = [Text(new TextProps { Text = label, Color = Color.White, FontWeight = "800" })]
        });
    }

    private static Element DockItem(string text, Color color, float offset)
    {
        return Container(new ContainerProps
        {
            Width = Dimension.Pixels(54),
            Height = Dimension.Pixels(54),
            Margin = new Spacing(Dimension.Pixels(offset), Dimension.ZeroPixels, Dimension.ZeroPixels, Dimension.ZeroPixels),
            BackgroundColor = color,
            BorderRadius = 27,
            JustifyContent = JustifyContent.Center,
            AlignItems = AlignItems.Center,
            Transitions = TransitionOf(nameof(ContainerProps.Margin), Normal, Easing.EaseOut),
            Children = [Text(new TextProps { Text = text, Color = Color.White, FontWeight = "800" })]
        });
    }

    private static Element EasingTheater(bool on)
    {
        return Section("Easing theater", "同样的 Width 动画，不同缓动曲线的速度差异", [
            EasingRow(Easing.Linear, on, Color.FromHex("#38BDF8")),
            EasingRow(Easing.Ease, on, Color.FromHex("#A78BFA")),
            EasingRow(Easing.EaseIn, on, Color.FromHex("#F97316")),
            EasingRow(Easing.EaseOut, on, Color.FromHex("#22C55E")),
            EasingRow(Easing.EaseInOut, on, Color.FromHex("#EC4899"))
        ]);
    }

    private static Element EasingRow(Easing easing, bool on, Color color)
    {
        return Container(new ContainerProps
        {
            Width = Dimension.Percent(100),
            Height = Dimension.Pixels(58),
            BackgroundColor = Color.FromHex("#020617"),
            BorderColor = Color.FromHex("#1E293B"),
            BorderStyle = BorderStyle.Solid,
            BorderWidth = 1,
            BorderRadius = 10,
            Padding = new Spacing(Dimension.Pixels(12)),
            Direction = LayoutDirection.Horizontal,
            AlignItems = AlignItems.Center,
            Gap = 14,
            Children =
            [
                Container(new ContainerProps
                {
                    Width = Dimension.Pixels(118),
                    FlexShrink = 0,
                    Children = [Text(new TextProps { Text = easing.ToString(), Color = Color.FromHex("#CBD5E1"), FontWeight = "800" })]
                }),
                Container(new ContainerProps
                {
                    FlexGrow = 1,
                    Height = Dimension.Pixels(34),
                    BackgroundColor = Color.FromHex("#0F172A"),
                    BorderRadius = 17,
                    Padding = new Spacing(Dimension.Pixels(4)),
                    AlignItems = AlignItems.Center,
                    Children =
                    [
                        Container(new ContainerProps
                        {
                            Width = Dimension.Pixels(on ? 420 : 54),
                            Height = Dimension.Pixels(26),
                            BackgroundColor = color,
                            BorderRadius = 13,
                            Transitions = TransitionOf(nameof(ContainerProps.Width), Slow, easing)
                        })
                    ]
                })
            ]
        });
    }

    private static Element BuiltInControls()
    {
        return Section("Built-in animated controls", "控件自身组合出的动画：点击 Switch / ComboBox / Tabs 查看", [
            Container(new ContainerProps
            {
                Width = Dimension.Percent(100),
                Direction = LayoutDirection.Horizontal,
                Gap = 18,
                AlignItems = AlignItems.Start,
                Children =
                [
                    BuiltInCard("Switch", Switch(new SwitchProps { DefaultIsOn = false, OnColor = Color.FromHex("#7C3AED"), OffColor = Color.FromHex("#334155") })),
                    BuiltInCard("ComboBox", ComboBox(new ComboBoxProps { Options = ["Color", "Spacing", "Size", "Easing"], SelectedIndex = 0 })),
                    BuiltInCard("Tabs", Tabs(new TabProps
                    {
                        Titles = ["Color", "Size", "Layout"],
                        Content = i => Container(new ContainerProps
                        {
                            Height = Dimension.Pixels(92),
                            BackgroundColor = i switch
                            {
                                0 => Color.FromHex("#1D4ED8"),
                                1 => Color.FromHex("#047857"),
                                _ => Color.FromHex("#B45309")
                            },
                            BorderRadius = 10,
                            JustifyContent = JustifyContent.Center,
                            AlignItems = AlignItems.Center,
                            Children = [Text(new TextProps { Text = $"Panel {i + 1}", Color = Color.White, FontWeight = "800" })]
                        })
                    }))
                ]
            })
        ]);
    }

    private static Element SupportedProperties()
    {
        return Section("Supported animation surface", "当前 Win32AnimationManager 支持插值的属性和值类型", [
            Container(new ContainerProps
            {
                Direction = LayoutDirection.Vertical,
                Gap = 10,
                Children =
                [
                    SupportRow([
                        SupportBadge(nameof(ContainerProps.BackgroundColor), "Color"),
                        SupportBadge(nameof(ContainerProps.BorderColor), "Color"),
                        SupportBadge(nameof(ContainerProps.BorderWidth), "float"),
                        SupportBadge(nameof(ContainerProps.BorderRadius), "float")
                    ]),
                    SupportRow([
                        SupportBadge(nameof(ContainerProps.Margin), "Spacing"),
                        SupportBadge(nameof(ContainerProps.Padding), "Spacing"),
                        SupportBadge(nameof(ContainerProps.Gap), "float")
                    ]),
                    SupportRow([
                        SupportBadge(nameof(ContainerProps.Width), "Dimension"),
                        SupportBadge(nameof(ContainerProps.Height), "Dimension"),
                        SupportBadge(nameof(ContainerProps.MinWidth), "Dimension"),
                        SupportBadge(nameof(ContainerProps.MinHeight), "Dimension"),
                        SupportBadge(nameof(ContainerProps.MaxWidth), "Dimension"),
                        SupportBadge(nameof(ContainerProps.MaxHeight), "Dimension")
                    ])
                ]
            })
        ]);
    }

    private static Element Section(string title, string subtitle, IReadOnlyList<Element> children)
    {
        return Container(new ContainerProps
        {
            Width = Dimension.Percent(100),
            BackgroundColor = Color.FromHex("#111827"),
            BorderColor = Color.FromHex("#1F2937"),
            BorderStyle = BorderStyle.Solid,
            BorderWidth = 1,
            BorderRadius = 18,
            Padding = new Spacing(Dimension.Pixels(18)),
            Direction = LayoutDirection.Vertical,
            Gap = 14,
            Children =
            [
                Text(new TextProps { Text = title, Color = Color.White, FontSize = 20, FontWeight = "800" }),
                Text(new TextProps { Text = subtitle, Color = Color.FromHex("#94A3B8"), FontSize = 13 }),
                Container(new ContainerProps
                {
                    Width = Dimension.Percent(100),
                    Direction = LayoutDirection.Vertical,
                    Gap = 12,
                    Children = children
                })
            ]
        });
    }

    private static Element SceneCard(string title, string subtitle, Element visual)
    {
        return Container(new ContainerProps
        {
            Width = Dimension.Percent(100),
            BackgroundColor = Color.FromHex("#0B1120"),
            BorderColor = Color.FromHex("#243044"),
            BorderStyle = BorderStyle.Solid,
            BorderWidth = 1,
            BorderRadius = 14,
            Padding = new Spacing(Dimension.Pixels(14)),
            Direction = LayoutDirection.Horizontal,
            AlignItems = AlignItems.Center,
            Gap = 16,
            Children =
            [
                Container(new ContainerProps
                {
                    Width = Dimension.Pixels(210),
                    FlexShrink = 0,
                    Direction = LayoutDirection.Vertical,
                    Gap = 5,
                    Children =
                    [
                        Text(new TextProps { Text = title, Color = Color.White, FontSize = 15, FontWeight = "800" }),
                        Text(new TextProps { Text = subtitle, Color = Color.FromHex("#64748B"), FontSize = 12 })
                    ]
                }),
                Container(new ContainerProps
                {
                    FlexGrow = 1,
                    FlexShrink = 1,
                    Children = [visual]
                })
            ]
        });
    }

    private static Element Stage(IReadOnlyList<Element> children, float height = 150)
    {
        return Container(new ContainerProps
        {
            Width = Dimension.Percent(100),
            Height = Dimension.Pixels(height),
            BackgroundColor = Color.FromHex("#020617"),
            BorderColor = Color.FromHex("#1E293B"),
            BorderStyle = BorderStyle.Solid,
            BorderWidth = 1,
            BorderRadius = 12,
            Padding = new Spacing(Dimension.Pixels(12)),
            Direction = LayoutDirection.Horizontal,
            AlignItems = AlignItems.Center,
            Overflow = Overflow.Hidden,
            Children = children
        });
    }

    private static Element BuiltInCard(string title, Element content)
    {
        return Container(new ContainerProps
        {
            Width = Dimension.Percent(33),
            MinHeight = Dimension.Pixels(178),
            BackgroundColor = Color.FromHex("#020617"),
            BorderColor = Color.FromHex("#1E293B"),
            BorderStyle = BorderStyle.Solid,
            BorderWidth = 1,
            BorderRadius = 12,
            Padding = new Spacing(Dimension.Pixels(14)),
            Direction = LayoutDirection.Vertical,
            Gap = 12,
            Children =
            [
                Text(new TextProps { Text = title, Color = Color.White, FontWeight = "800" }),
                content
            ]
        });
    }

    private static Element Badge(string text)
    {
        return Container(new ContainerProps
        {
            BackgroundColor = Color.FromHex("#1E293B"),
            BorderRadius = 99,
            Padding = new Spacing(Dimension.Pixels(10), Dimension.Pixels(5)),
            Children = [Text(new TextProps { Text = text, Color = Color.FromHex("#CBD5E1"), FontSize = 12, FontWeight = "700" })]
        });
    }

    private static Element SupportRow(IReadOnlyList<Element> children)
    {
        return Container(new ContainerProps
        {
            Direction = LayoutDirection.Horizontal,
            Gap = 10,
            Children = children
        });
    }

    private static Element SupportBadge(string name, string type)
    {
        return Container(new ContainerProps
        {
            BackgroundColor = Color.FromHex("#020617"),
            BorderColor = Color.FromHex("#243044"),
            BorderStyle = BorderStyle.Solid,
            BorderWidth = 1,
            BorderRadius = 10,
            Padding = new Spacing(Dimension.Pixels(10), Dimension.Pixels(8)),
            Direction = LayoutDirection.Vertical,
            Gap = 3,
            Children =
            [
                Text(new TextProps { Text = name, Color = Color.White, FontSize = 12, FontWeight = "800" }),
                Text(new TextProps { Text = type, Color = Color.FromHex("#64748B"), FontSize = 11 })
            ]
        });
    }

    private static ValueDictionary<string, Transition> TransitionOf(string propertyName, int durationMs = Normal, Easing easing = Easing.EaseInOut)
    {
        return new ValueDictionary<string, Transition>(new Dictionary<string, Transition>
        {
            [propertyName] = new(durationMs, easing)
        });
    }

    private static ValueDictionary<string, Transition> Transitions(params (string PropertyName, Transition Transition)[] items)
    {
        var values = new Dictionary<string, Transition>();
        foreach (var item in items)
            values[item.PropertyName] = item.Transition;

        return new ValueDictionary<string, Transition>(values);
    }
}

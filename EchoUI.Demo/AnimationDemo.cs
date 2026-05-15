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
                Section("Interactive Controls", "All built-in controls with live state — Button / Input / TextInput / CheckBox / Switch / ComboBox / RadioGroup", [
                    new Element((Component)ControlsInteract, new Props())
                ]),
                Section("Layout Showcase", "Flex layout visualization — Direction / JustifyContent / AlignItems with live selector", [
                    new Element((Component)LayoutShowcase, new Props())
                ]),
                Section("Counter Demo", "Reactive state management with conditional styling", [
                    new Element((Component)CounterDemo, new Props())
                ]),
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

    // --- Interactive section components ---

    private static Element? ControlsInteract(Props _)
    {
        var (btnClicks, _, updateBtnClicks) = State(0);
        var (inputVal, setInputVal, _) = State("");
        var (textInputVal, setTextInputVal, _) = State("");
        var (comboIdx, setComboIdx, _) = State(0);
        var (radioIdx, setRadioIdx, _) = State(0);

        var colors = new[] { "Red", "Green", "Blue", "Purple" };
        var sizes = new[] { "XS", "SM", "MD", "LG", "XL" };

        return Container(new ContainerProps
        {
            Width = Dimension.Percent(100),
            Direction = LayoutDirection.Vertical,
            Gap = 14,
            Children =
            [
                Container(new ContainerProps
                {
                    Direction = LayoutDirection.Horizontal,
                    Gap = 10,
                    AlignItems = AlignItems.Center,
                    Children =
                    [
                        Button(new ButtonProps { Text = $"Click ({btnClicks.Value})", OnClick = _ => updateBtnClicks(v => v + 1) }),
                        Button(new ButtonProps { Text = "Primary", BackgroundColor = Color.FromHex("#2563EB"), TextColor = Color.White }),
                        Button(new ButtonProps { Text = "Danger", BackgroundColor = Color.FromHex("#EF4444"), TextColor = Color.White }),
                        Button(new ButtonProps { Text = "Ghost", BackgroundColor = Color.FromHex("#1E293B"), TextColor = Color.FromHex("#CBD5E1") })
                    ]
                }),
                Container(new ContainerProps
                {
                    Direction = LayoutDirection.Horizontal,
                    Gap = 10,
                    AlignItems = AlignItems.Center,
                    Children =
                    [
                        Container(new ContainerProps { FlexGrow = 1, Children = [ Input(new InputProps { Value = inputVal.Value, OnValueChanged = v => setInputVal(v), BackgroundColor = Color.FromHex("#1E293B"), TextColor = Color.FromHex("#E2E8F0") }) ] }),
                        Container(new ContainerProps { Width = Dimension.Pixels(60), Children = [ Text(new TextProps { Text = $"{inputVal.Value.Length} chars", Color = Color.FromHex("#94A3B8"), FontSize = 11 })] })
                    ]
                }),
                Container(new ContainerProps { Width = Dimension.Percent(100), Children = [ TextInput(new TextInputProps { Value = textInputVal.Value, OnValueChanged = v => setTextInputVal(v), Width = Dimension.Percent(100) }) ] }),
                Container(new ContainerProps
                {
                    Direction = LayoutDirection.Horizontal,
                    Gap = 18,
                    AlignItems = AlignItems.Center,
                    Children =
                    [
                        Container(new ContainerProps { Direction = LayoutDirection.Horizontal, AlignItems = AlignItems.Center, Gap = 6, Children = [ Switch(new SwitchProps { DefaultIsOn = false, OnColor = Color.FromHex("#7C3AED") }), Text(new TextProps { Text = "Enable", Color = Color.FromHex("#94A3B8"), FontSize = 12 }) ] }),
                        CheckBox(new CheckBoxProps { Label = "Remember", IsChecked = true, CheckColor = Color.FromHex("#22C55E") }),
                        Container(new ContainerProps { Width = Dimension.Pixels(150), Children = [ ComboBox(new ComboBoxProps { Options = colors, SelectedIndex = comboIdx.Value, OnSelectionChanged = v => setComboIdx(v) }) ] })
                    ]
                }),
                Container(new ContainerProps
                {
                    Direction = LayoutDirection.Horizontal,
                    AlignItems = AlignItems.Center,
                    Gap = 16,
                    Children =
                    [
                        RadioGroup(new RadioGroupProps { Options = sizes, SelectedIndex = radioIdx.Value, OnSelectionChanged = v => setRadioIdx(v), Direction = LayoutDirection.Horizontal, SelectedColor = Color.FromHex("#7C3AED") }),
                        Text(new TextProps { Text = $"Size: {sizes[radioIdx.Value]}  |  Color: {colors[comboIdx.Value]}", Color = Color.FromHex("#94A3B8"), FontSize = 12 })
                    ]
                })
            ]
        });
    }

    private static Element? LayoutShowcase(Props _)
    {
        var (dirIdx, setDirIdx, _) = State(0);
        var (justifyIdx, setJustifyIdx, _) = State(0);
        var (alignIdx, setAlignIdx, _) = State(0);
        Action<int> onDirChange = idx => setDirIdx(idx);
        Action<int> onJustifyChange = idx => setJustifyIdx(idx);
        Action<int> onAlignChange = idx => setAlignIdx(idx);

        var direction = dirIdx == 0 ? LayoutDirection.Vertical : LayoutDirection.Horizontal;
        var justifyValues = new[] { JustifyContent.Start, JustifyContent.Center, JustifyContent.End, JustifyContent.SpaceBetween, JustifyContent.SpaceAround };
        var alignValues = new[] { AlignItems.Start, AlignItems.Center, AlignItems.End, AlignItems.Stretch };

        var c1 = Color.FromHex("#EF4444"); var c2 = Color.FromHex("#22C55E");
        var c3 = Color.FromHex("#3B82F6"); var c4 = Color.FromHex("#A855F7");

        return Container(new ContainerProps
        {
            Width = Dimension.Percent(100),
            Direction = LayoutDirection.Vertical,
            Gap = 18,
            Children =
            [
                // Direction
                FlexDemo("Direction", dirIdx, onDirChange, ["Vertical", "Horizontal"], c =>
                    Container(new ContainerProps
                    {
                        Width = Dimension.Percent(100), Height = Dimension.Pixels(90),
                        BackgroundColor = Color.FromHex("#1E293B"), BorderRadius = 8,
                        Padding = new Spacing(Dimension.Pixels(10)),
                        Direction = direction, Gap = 6,
                        Children =
                        [
                            Box(c1, direction == LayoutDirection.Horizontal ? Dimension.Pixels(50) : null, direction == LayoutDirection.Vertical ? Dimension.Pixels(50) : null),
                            Box(c2, direction == LayoutDirection.Horizontal ? Dimension.Pixels(70) : null, direction == LayoutDirection.Vertical ? Dimension.Pixels(70) : null),
                            Box(c3, direction == LayoutDirection.Horizontal ? Dimension.Pixels(60) : null, direction == LayoutDirection.Vertical ? Dimension.Pixels(60) : null),
                            Box(c4, direction == LayoutDirection.Horizontal ? Dimension.Pixels(44) : null, direction == LayoutDirection.Vertical ? Dimension.Pixels(44) : null)
                        ]
                    })
                ),
                // JustifyContent
                FlexDemo("JustifyContent", justifyIdx, onJustifyChange, ["Start", "Center", "End", "SpaceBetween", "SpaceAround"], c =>
                    Container(new ContainerProps
                    {
                        Width = Dimension.Percent(100), Height = Dimension.Pixels(50),
                        BackgroundColor = Color.FromHex("#1E293B"), BorderRadius = 8,
                        Padding = new Spacing(Dimension.Pixels(10)),
                        Direction = LayoutDirection.Horizontal,
                        JustifyContent = justifyValues[justifyIdx], Gap = 4,
                        Children =
                        [
                            Container(new ContainerProps { Width = Dimension.Pixels(28), Height = Dimension.Pixels(28), BackgroundColor = c1, BorderRadius = 6 }),
                            Container(new ContainerProps { Width = Dimension.Pixels(28), Height = Dimension.Pixels(28), BackgroundColor = c2, BorderRadius = 6 }),
                            Container(new ContainerProps { Width = Dimension.Pixels(28), Height = Dimension.Pixels(28), BackgroundColor = c3, BorderRadius = 6 })
                        ]
                    })
                ),
                // AlignItems
                FlexDemo("AlignItems", alignIdx, onAlignChange, ["Start", "Center", "End", "Stretch"], c =>
                    Container(new ContainerProps
                    {
                        Width = Dimension.Percent(100), Height = Dimension.Pixels(80),
                        BackgroundColor = Color.FromHex("#1E293B"), BorderRadius = 8,
                        Padding = new Spacing(Dimension.Pixels(10)),
                        Direction = LayoutDirection.Horizontal,
                        AlignItems = alignValues[alignIdx], Gap = 4,
                        Children =
                        [
                            Container(new ContainerProps { Width = Dimension.Pixels(40), Height = Dimension.Pixels(28), BackgroundColor = c1, BorderRadius = 6, Children = [Text(new TextProps { Text = "28", Color = Color.White, FontSize = 10 })] }),
                            Container(new ContainerProps { Width = Dimension.Pixels(40), Height = Dimension.Pixels(56), BackgroundColor = c2, BorderRadius = 6, Children = [Text(new TextProps { Text = "56", Color = Color.White, FontSize = 10 })] }),
                            Container(new ContainerProps { Width = Dimension.Pixels(40), Height = Dimension.Pixels(40), BackgroundColor = c3, BorderRadius = 6, Children = [Text(new TextProps { Text = "40", Color = Color.White, FontSize = 10 })] })
                        ]
                    })
                )
            ]
        });
    }

    private static Element FlexDemo(string label, int idx, Action<int> setIdx, string[] options, Func<int, Element> content)
    {
        return Container(new ContainerProps
        {
            Direction = LayoutDirection.Vertical,
            Gap = 8,
            Children =
            [
                Container(new ContainerProps
                {
                    Direction = LayoutDirection.Horizontal,
                    AlignItems = AlignItems.Center,
                    Gap = 12,
                    Children =
                    [
                        Text(new TextProps { Text = $"{label}:", Color = Color.FromHex("#CBD5E1"), FontWeight = "700" }),
                        Container(new ContainerProps { Width = Dimension.Pixels(180), Children = [ ComboBox(new ComboBoxProps { Options = options, SelectedIndex = idx, OnSelectionChanged = v => setIdx(v), BackgroundColor = Color.FromHex("#1E293B"), TextColor = Color.FromHex("#E2E8F0"), BorderColor = Color.FromHex("#334155") }) ] })
                    ]
                }),
                content(idx)
            ]
        });
    }

    private static Element Box(Color color, Dimension? w, Dimension? h)
    {
        return Container(new ContainerProps
        {
            Width = w ?? Dimension.Pixels(36),
            Height = h ?? Dimension.Pixels(36),
            BackgroundColor = color, BorderRadius = 6,
            JustifyContent = JustifyContent.Center,
            AlignItems = AlignItems.Center,
            Children = [Text(new TextProps { Text = "#", Color = Color.White, FontSize = 14, FontWeight = "700" })]
        });
    }

    private static Element? CounterDemo(Props _)
    {
        var (count, setCount, updateCount) = State(0);

        return Container(new ContainerProps
        {
            Direction = LayoutDirection.Vertical,
            AlignItems = AlignItems.Center,
            Gap = 18,
            Padding = new Spacing(Dimension.Pixels(24)),
            Width = Dimension.Percent(100),
            Children =
            [
                Text(new TextProps
                {
                    Text = $"{count.Value}",
                    FontSize = 48,
                    FontWeight = "Bold",
                    Color = count.Value == 0 ? Color.FromHex("#CBD5E1") : (count.Value < 0 ? Color.FromHex("#EF4444") : Color.FromHex("#22C55E"))
                }),
                Container(new ContainerProps
                {
                    Direction = LayoutDirection.Horizontal,
                    Gap = 14,
                    AlignItems = AlignItems.Center,
                    Children =
                    [
                        Button(new ButtonProps { Text = "\u2212", Width = Dimension.Pixels(56), Height = Dimension.Pixels(44), OnClick = _ => updateCount(v => v - 1) }),
                        Button(new ButtonProps { Text = "Reset", Width = Dimension.Pixels(96), Height = Dimension.Pixels(44), BackgroundColor = Color.FromHex("#475569"), TextColor = Color.White, OnClick = _ => setCount(0) }),
                        Button(new ButtonProps { Text = "+", Width = Dimension.Pixels(56), Height = Dimension.Pixels(44), OnClick = _ => updateCount(v => v + 1) })
                    ]
                }),
                Container(new ContainerProps
                {
                    Width = Dimension.Percent(100),
                    Height = Dimension.Pixels(4),
                    BackgroundColor = count.Value == 0 ? Color.FromHex("#334155") : (count.Value < 0 ? Color.FromHex("#7F1D1D") : Color.FromHex("#064E3B")),
                    BorderRadius = 2,
                    Transitions = [ [nameof(ContainerProps.BackgroundColor), new Transition(250, Easing.EaseInOut)] ]
                }),
                Text(new TextProps
                {
                    Text = count.Value == 0 ? "Zero \u2014 click + or \u2212 to start" : count.Value < 0 ? $"Negative ({count.Value})" : $"Positive (+{count.Value})",
                    FontSize = 13,
                    Color = count.Value == 0 ? Color.FromHex("#64748B") : (count.Value < 0 ? Color.FromHex("#FCA5A5") : Color.FromHex("#86EFAC"))
                })
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

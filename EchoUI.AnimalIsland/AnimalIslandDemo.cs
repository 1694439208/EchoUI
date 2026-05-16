using EchoUI.Core;
using static EchoUI.Core.Elements;
using static EchoUI.Core.Hooks;

/// <summary>
/// Animal Island UI styled demo — showcases all EchoUI components
/// styled per the Animal Island Design Tokens.
/// </summary>
public static class AnimalIslandDemo
{
    public static Element? Render(Props _)
    {
        var (tabIdx, setTabIdx, _) = State(0);
        var tabTitles = new[] { "Components", "Cards", "Playground" };

        return Container(new ContainerProps
        {
            Width = Dimension.Percent(100),
            Height = Dimension.Percent(100),
            BackgroundColor = DesignTokens.BgMain,
            Direction = LayoutDirection.Vertical,
            Children =
            [
                NavBar(),
                Container(new ContainerProps
                {
                    FlexGrow = 1,
                    Width = Dimension.Percent(100),
                    Padding = new Spacing(Dimension.Pixels(0), Dimension.Pixels(10)),
                    Children =
                    [
                        Tabs(new TabProps
                        {
                            Titles = tabTitles,
                            InitialIndex = 0,
                            OnTabChanged = v => setTabIdx(v),
                            ActiveTabBackgroundColor = DesignTokens.PrimaryBg,
                            ActiveTabTextColor = DesignTokens.Primary,
                            InactiveTabTextColor = DesignTokens.TextMuted,
                            Content = i => tabTitles[i] switch
                            {
                                "Components" => ComponentsPage(),
                                "Cards" => CardsPage(),
                                _ => PlaygroundPage(),
                            }
                        })
                    ]
                })
            ]
        });
    }

    private static Element NavBar()
    {
        return Container(new ContainerProps
        {
            Width = Dimension.Percent(100),
            Padding = new Spacing(Dimension.Pixels(24), Dimension.Pixels(16)),
            Direction = LayoutDirection.Horizontal,
            JustifyContent = JustifyContent.SpaceBetween,
            AlignItems = AlignItems.Center,
            BackgroundColor = DesignTokens.BgContent,
            BorderWidth = 0,
            BorderRadius = 0,
            Children =
            [
                Container(new ContainerProps
                {
                    Direction = LayoutDirection.Horizontal,
                    AlignItems = AlignItems.Center,
                    Gap = 12,
                    Children =
                    [
                        Container(new ContainerProps
                        {
                            Width = Dimension.Pixels(36),
                            Height = Dimension.Pixels(36),
                            BackgroundColor = DesignTokens.Primary,
                            BorderRadius = 10,
                        }),
                        Text(new TextProps
                        {
                            Text = "Animal Island UI",
                            FontSize = 24,
                            Color = DesignTokens.TextTitle,
                            FontWeight = "800"
                        })
                    ]
                }),
                Text(new TextProps
                {
                    Text = "Warm . Rounded . Playful",
                    FontSize = 13,
                    Color = DesignTokens.TextSecondary,
                    FontWeight = "500"
                })
            ]
        });
    }

    private static Element ComponentsPage()
    {
        var (btnDisabled, setBtnDisabled, _) = State(true);
        var (inputVal, setInputVal, _) = State("");
        var (textInputVal, setTextInputVal, _) = State("");
        var (checkState, setCheckState, _) = State(true);
        var (switchState, setSwitchState, _) = State(false);
        var (comboIdx, setComboIdx, _) = State(0);
        var (radioIdx, setRadioIdx, _) = State(0);

        return Container(new ContainerProps
        {
            Width = Dimension.Percent(100),
            FlexGrow = 1,
            Padding = new Spacing(Dimension.Pixels(30)),
            Direction = LayoutDirection.Vertical,
            Gap = 28,
            Overflow = Overflow.Auto,
            Children =
            [
                SectionTitle("Buttons"),
                Container(new ContainerProps
                {
                    Direction = LayoutDirection.Vertical,
                    Gap = 14,
                    Children =
                    [
                        Container(new ContainerProps
                        {
                            Direction = LayoutDirection.Horizontal,
                            Gap = 14,
                            AlignItems = AlignItems.Center,
                            Children =
                            [
                                AIButton(new ButtonProps { Text = "Small", Size = "small" }),
                                AIButton(new ButtonProps { Text = "Middle" }),
                                AIButton(new ButtonProps { Text = "Large", Size = "large" })
                            ]
                        }),
                        Container(new ContainerProps
                        {
                            Direction = LayoutDirection.Horizontal,
                            Gap = 14,
                            AlignItems = AlignItems.Center,
                            Children =
                            [
                                AIButton(new ButtonProps { Text = "Primary" }),
                                AIButton(new ButtonProps { Text = "Success", BackgroundColor = DesignTokens.Success }),
                                AIButton(new ButtonProps { Text = "Warning", BackgroundColor = DesignTokens.Warning }),
                                AIButton(new ButtonProps { Text = "Error", BackgroundColor = DesignTokens.Error, HoverColor = DesignTokens.ErrorActive }),
                                AIButton(new ButtonProps { Text = "Disabled", Disabled = true }),
                            ]
                        }),
                    ]
                }),

                SectionTitle("Inputs"),
                Container(new ContainerProps
                {
                    Width = Dimension.Percent(100),
                    Direction = LayoutDirection.Vertical,
                    Gap = 12,
                    Children =
                    [
                        AIInput(new InputProps { Value = inputVal.Value, OnValueChanged = v => setInputVal(v), BackgroundColor = DesignTokens.BgContent, TextColor = DesignTokens.TextBody, BorderColor = DesignTokens.Border, FocusedBorderColor = DesignTokens.BorderFocus }),
                        AITextInput(new TextInputProps { Value = textInputVal.Value, OnValueChanged = v => setTextInputVal(v), Placeholder = "Type here...", Width = Dimension.Percent(100) }),
                    ]
                }),

                SectionTitle("Selection Controls"),
                Container(new ContainerProps
                {
                    Direction = LayoutDirection.Horizontal,
                    Gap = 30,
                    AlignItems = AlignItems.Center,
                    Children =
                    [
                        CheckBox(new CheckBoxProps { Label = "Remember me", IsChecked = checkState.Value, OnToggle = v => setCheckState(v), CheckColor = DesignTokens.Primary, BorderColor = DesignTokens.Border }),
                        Container(new ContainerProps { Direction = LayoutDirection.Horizontal, AlignItems = AlignItems.Center, Gap = 8, Children = [ Text(new TextProps { Text = "Notify", Color = DesignTokens.TextBody, FontWeight = "600" }), Switch(new SwitchProps { DefaultIsOn = switchState.Value, OnToggle = v => setSwitchState(v), OnColor = DesignTokens.Success, OffColor = DesignTokens.ShadowInput }) ] }),
                        Container(new ContainerProps { Width = Dimension.Pixels(180), Children = [ ComboBox(new ComboBoxProps { Options = ["Frog", "Rabbit", "Bear", "Owl", "Fox"], SelectedIndex = comboIdx.Value, OnSelectionChanged = v => setComboIdx(v), BackgroundColor = DesignTokens.BgContent, TextColor = DesignTokens.TextBody, BorderColor = DesignTokens.Border }) ] })
                    ]
                }),

                SectionTitle("Radio Group"),
                RadioGroup(new RadioGroupProps
                {
                    Options = ["Puppy", "Kitten", "Bunny"],
                    SelectedIndex = radioIdx.Value,
                    OnSelectionChanged = v => setRadioIdx(v),
                    Direction = LayoutDirection.Horizontal,
                    SelectedColor = DesignTokens.Primary,
                    BorderColor = DesignTokens.Border
                }),

                SectionTitle("Tabs"),
                Text(new TextProps
                {
                    Text = "(See tab navigation at top of page -- built-in Tabs component)",
                    FontSize = 12,
                    Color = DesignTokens.TextDisabled,
                    FontWeight = "500"
                }),
            ]
        });
    }

    private static Element CardsPage()
    {
        var animals = new[] {
            (Name: "Frog", Emoji: "\U0001F438", Color: Color.FromHex("#6fba2c"), Desc: "Green, hoppy, loves ponds"),
            (Name: "Rabbit", Emoji: "\U0001F430", Color: Color.FromHex("#f5c31c"), Desc: "Fluffy, fast, loves carrots"),
            (Name: "Bear", Emoji: "\U0001F43B", Color: Color.FromHex("#c4b89e"), Desc: "Big, warm, loves honey"),
            (Name: "Owl", Emoji: "\U0001F989", Color: Color.FromHex("#19c8b9"), Desc: "Wise, nocturnal, hoots"),
            (Name: "Fox", Emoji: "\U0001F98A", Color: Color.FromHex("#e05a5a"), Desc: "Clever, orange, sneaky"),
        };

        var cards = new List<Element>();
        foreach (var a in animals)
        {
            cards.Add(AICard(a.Name, a.Emoji, a.Color, a.Desc));
        }

        return Container(new ContainerProps
        {
            Width = Dimension.Percent(100),
            FlexGrow = 1,
            Padding = new Spacing(Dimension.Pixels(30)),
            Direction = LayoutDirection.Vertical,
            Gap = 20,
            Overflow = Overflow.Auto,
            Children =
            [
                SectionTitle("Animal Cards"),
                Container(new ContainerProps
                {
                    Direction = LayoutDirection.Horizontal,
                    Gap = 18,
                    Children = cards
                }),
            ]
        });
    }

    private static Element PlaygroundPage()
    {
        var (count, setCount, updateCount) = State(0);

        return Container(new ContainerProps
        {
            Width = Dimension.Percent(100),
            FlexGrow = 1,
            Padding = new Spacing(Dimension.Pixels(30)),
            Direction = LayoutDirection.Vertical,
            Gap = 24,
            Overflow = Overflow.Auto,
            Children =
            [
                SectionTitle("Counter Demo"),
                Container(new ContainerProps
                {
                    Direction = LayoutDirection.Vertical,
                    AlignItems = AlignItems.Center,
                    Gap = 20,
                    Padding = new Spacing(Dimension.Pixels(24)),
                    BackgroundColor = DesignTokens.BgContent,
                    BorderRadius = DesignTokens.RadiusLg,
                    Children =
                    [
                        Text(new TextProps
                        {
                            Text = $"{count.Value}",
                            FontSize = 56,
                            FontWeight = "900",
                            Color = count.Value == 0 ? DesignTokens.TextBody : (count.Value < 0 ? DesignTokens.Error : DesignTokens.Success)
                        }),
                        Container(new ContainerProps
                        {
                            Direction = LayoutDirection.Horizontal,
                            Gap = 14,
                            AlignItems = AlignItems.Center,
                            Children =
                            [
                                AIButton(new ButtonProps { Text = "-", Size = "small", Width = Dimension.Pixels(56), OnClick = _ => updateCount(v => v - 1) }),
                                AIButton(new ButtonProps { Text = "Reset", BackgroundColor = DesignTokens.Primary, HoverColor = DesignTokens.PrimaryHover, OnClick = _ => setCount(0) }),
                                AIButton(new ButtonProps { Text = "+", Size = "small", Width = Dimension.Pixels(56), OnClick = _ => updateCount(v => v + 1) }),
                            ]
                        }),
                        Container(new ContainerProps
                        {
                            Width = Dimension.Pixels(200),
                            Height = Dimension.Pixels(6),
                            BackgroundColor = count.Value == 0 ? DesignTokens.Border : (count.Value < 0 ? DesignTokens.Error : DesignTokens.Success),
                            BorderRadius = 3,
                            Transitions =
                            [
                                [nameof(ContainerProps.BackgroundColor), new Transition(200, Easing.EaseInOut)]
                            ]
                        }),
                        Text(new TextProps
                        {
                            Text = count.Value == 0 ? "Tap + or - to start!" : $"You clicked {(count.Value < 0 ? -count.Value : count.Value)} time{((count.Value == 1 || count.Value == -1) ? "" : "s")}",
                            FontSize = 13,
                            Color = DesignTokens.TextSecondary,
                            FontWeight = "500"
                        })
                    ]
                }),

                SectionTitle("About"),
                AICard("Animal Island UI", "\U0001F33F", DesignTokens.Primary,
                    "A warm, rounded, playful UI design system built with EchoUI. " +
                    "Inspired by soft natural colors, 3D button shadows, and friendly " +
                    "interactions -- no harsh angles, no cold blacks.")
            ]
        });
    }

    private static Element SectionTitle(string text)
    {
        return Text(new TextProps
        {
            Text = text,
            FontSize = 18,
            Color = DesignTokens.TextTitle,
            FontWeight = "700"
        });
    }

    public static Element AIButton(ButtonProps props)
    {
        var (isHovered, setIsHovered, _) = State(false);
        var (isPressed, setIsPressed, _) = State(false);

        var sz = DesignTokens.PrimaryButton(props.Size ?? "middle");
        var disabled = props.Disabled == true;

        var baseBg = props.BackgroundColor ?? DesignTokens.Primary;
        var bg = disabled ? DesignTokens.BgDisabled : baseBg;
        var textColor = props.TextColor ?? (disabled ? DesignTokens.TextDisabled : DesignTokens.TextInverse);
        var radius = props.BorderRadius ?? sz.Radius;

        var shadowBase = DesignTokens.ShadowBtn;
        float shadowH = 5f;

        if (!disabled)
        {
            if (isPressed.Value)
            {
                bg = props.PressedColor ?? DesignTokens.PrimaryActive;
                shadowH = 2f;
            }
            else if (isHovered.Value)
            {
                bg = props.HoverColor ?? DesignTokens.PrimaryHover;
                shadowH = 6f;
            }
        }

        var autoWidth = Hooks.MeasureText(new TextMeasurementRequest
        {
            Text = props.Text,
            FontSize = sz.FontSize,
            FontWeight = "600"
        }).Width + sz.PaddingX * 2 + 24f;

        var btnWidth = props.Width ?? Dimension.Pixels(autoWidth);
        var btnHeight = props.Height ?? Dimension.Pixels(sz.Height);
        const float maxShadow = 6f;

        var transitions = new ValueDictionary<string, Transition>(new Dictionary<string, Transition>
        {
            [nameof(ContainerProps.BackgroundColor)] = new(140, Easing.EaseOut),
            [nameof(ContainerProps.BorderWidth)] = new(140, Easing.EaseOut),
            [nameof(ContainerProps.Margin)] = new(140, Easing.EaseOut),
        });

        // translateY: 只在内部 margin 偏移，外层 layout 固定不挤压同级
        var topMargin = isPressed.Value
            ? Dimension.Pixels(2f)
            : isHovered.Value && !disabled
                ? Dimension.Pixels(-1f)
                : Dimension.Pixels(0f);

        // 外层容器：固定 layout footprint
        return Container(new ContainerProps
        {
            Key = props.Key,
            Width = btnWidth,
            Height = Dimension.Pixels(sz.Height + maxShadow),
            MinWidth = Dimension.Pixels(72),
            Children =
            [
                Container(new ContainerProps
                {
                    Width = Dimension.Percent(100),
                    Height = btnHeight,
                    JustifyContent = JustifyContent.Center,
                    AlignItems = AlignItems.Center,
                    Padding = new Spacing(Dimension.Pixels(sz.PaddingX), Dimension.Pixels(0)),
                    BackgroundColor = bg,
                    BorderWidth = shadowH,
                    BorderRadius = radius,
                    ShadowColor = shadowBase,
                    Cursor = disabled ? "not-allowed" : null,
                    Opacity = disabled ? 0.5f : 1f,
                    Margin = new Spacing(topMargin, Dimension.Pixels(0), Dimension.Pixels(0), Dimension.Pixels(0)),
                    Transitions = transitions,
                    OnMouseEnter = disabled ? null : () => setIsHovered(true),
                    OnMouseLeave = () => { setIsHovered(false); setIsPressed(false); },
                    OnMouseDown = disabled ? null : () => setIsPressed(true),
                    OnMouseUp = disabled ? null : () => setIsPressed(false),
                    OnClick = disabled ? null : (Action<MouseButton>)(btn => props.OnClick?.Invoke(btn)),
                    Children =
                    [
                        Text(new TextProps
                        {
                            Text = props.Text,
                            Color = textColor,
                            FontSize = sz.FontSize,
                            FontWeight = "600",
                            NoWrap = true
                        })
                    ]
                })
            ]
        });
    }

    public static Element AIInput(InputProps props)
    {
        const float inputH = 40f;
        const float shadowH = 3f;

        // 阴影层 (parent): 底部多出 shadowH 像素展示 ShadowInput 颜色
        return Container(new ContainerProps
        {
            Width = Dimension.Pixels(280),
            Height = Dimension.Pixels(inputH + shadowH),
            BackgroundColor = DesignTokens.ShadowInput,
            BorderRadius = 50,
            Direction = LayoutDirection.Vertical,
            JustifyContent = JustifyContent.Start,
            Children =
            [
                // 实际输入区 (child): 40px, 覆盖顶部, 底部留出阴影
                Container(new ContainerProps
                {
                    Width = Dimension.Percent(100),
                    Height = Dimension.Pixels(inputH),
                    FlexShrink = 0,
                    FlexGrow = 0,
                    BackgroundColor = props.BackgroundColor ?? DesignTokens.BgContent,
                    BorderStyle = BorderStyle.Solid,
                    BorderColor = props.BorderColor ?? DesignTokens.Border,
                    BorderWidth = 2.5f,
                    BorderRadius = 50,
                    Padding = new Spacing(Dimension.Pixels(18), Dimension.Pixels(0)),
                    Children =
                    [
                        new Element(ElementCoreName.Input, new InputProps
                        {
                            Value = props.Value,
                            OnValueChanged = props.OnValueChanged,
                            BackgroundColor = Color.Transparent,
                            TextColor = DesignTokens.TextBody,
                            BorderColor = DesignTokens.Border,
                            FocusedBorderColor = DesignTokens.BorderFocus,
                        })
                    ]
                })
            ]
        });
    }

    public static Element AITextInput(TextInputProps props)
    {
        const float inputH = 40f;
        const float shadowH = 3f;

        return Container(new ContainerProps
        {
            Width = props.Width ?? Dimension.Pixels(280),
            Height = Dimension.Pixels(inputH + shadowH),
            BackgroundColor = DesignTokens.ShadowInput,
            BorderRadius = 50,
            Direction = LayoutDirection.Vertical,
            JustifyContent = JustifyContent.Start,
            Children =
            [
                Container(new ContainerProps
                {
                    Width = Dimension.Percent(100),
                    Height = Dimension.Pixels(inputH),
                    FlexShrink = 0,
                    FlexGrow = 0,
                    Children =
                    [
                        TextInput(new TextInputProps
                        {
                            Value = props.Value,
                            OnValueChanged = props.OnValueChanged,
                            Placeholder = props.Placeholder,
                            Width = Dimension.Percent(100),
                            Height = Dimension.Pixels(inputH),
                            BackgroundColor = DesignTokens.BgContent,
                            TextColor = DesignTokens.TextBody,
                            PlaceholderColor = DesignTokens.TextDisabled,
                            BorderColor = DesignTokens.Border,
                            FocusedBorderColor = DesignTokens.BorderFocus,
                            CaretColor = DesignTokens.Primary,
                            BorderRadius = 50,
                            Padding = new Spacing(Dimension.Pixels(18), Dimension.Pixels(10)),
                            FontSize = 14,
                            FontWeight = "500"
                        })
                    ]
                })
            ]
        });
    }

    public static Element AICard(string title, string emoji, Color accent, string description)
    {
        // 静态 border-as-shadow，无 hover 动画（避免 layout 抖动）
        var shadowCol = new Color(107, 92, 67, 107); // rgba(107,92,67,0.42)
        const float shadowH = 4f;

        return Container(new ContainerProps
        {
            Width = Dimension.Pixels(200),
            Padding = new Spacing(Dimension.Pixels(24)),
            BackgroundColor = DesignTokens.BgContent,
            BorderRadius = 20,
            BorderStyle = BorderStyle.Solid,
            BorderColor = shadowCol,
            BorderWidth = shadowH,
            Direction = LayoutDirection.Vertical,
            Gap = 12,
            AlignItems = AlignItems.Center,
            Children =
            [
                Container(new ContainerProps
                {
                    Width = Dimension.Pixels(64),
                    Height = Dimension.Pixels(64),
                    BackgroundColor = accent.WithAlpha(25),
                    BorderRadius = 32,
                    JustifyContent = JustifyContent.Center,
                    AlignItems = AlignItems.Center,
                    Children =
                    [
                        Text(new TextProps { Text = emoji, FontSize = 28 })
                    ]
                }),
                Text(new TextProps
                {
                    Text = title,
                    FontSize = 16,
                    Color = DesignTokens.TextTitle,
                    FontWeight = "700"
                }),
                Text(new TextProps
                {
                    Text = description,
                    FontSize = 12,
                    Color = DesignTokens.TextSecondary,
                    FontWeight = "500",
                    NoWrap = false
                })
            ]
        });
    }
}

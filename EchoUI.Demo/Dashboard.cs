using EchoUI.Core;
using static EchoUI.Core.Elements;
using static EchoUI.Core.Hooks;

namespace EchoUI.Demo
{
    public static class Dashboard
    {
        public static Element Create(Props props)
        {
            // Lift state up to control the view from the Sidebar
            var (activePage, setActivePage, _) = State("Dashboard");

            return Container(new ContainerProps
            {
                Width = Dimension.Percent(100),
                Height = Dimension.ViewportHeight(100),
                Direction = LayoutDirection.Horizontal,
                BackgroundColor = Color.FromHex("#f3f4f6"), // Light gray background
                Children =
                [
                    // Sidebar with state control
                    Sidebar(activePage.Value, p => setActivePage(p)),

                    // Main Content Area
                    Container(new ContainerProps
                    {
                        FlexGrow = 1,
                        FlexShrink = 1,
                        Height = Dimension.Percent(100), // Fill remaining height
                        Padding = new Spacing(Dimension.Pixels(30)),
                        Direction = LayoutDirection.Vertical,
                        Gap = 30,
                        AlignItems = AlignItems.Stretch,
                        Overflow = Overflow.Auto, // Auto allows scrolling only when needed
                        Children =
                        [
                            // Render content based on active page
                            activePage.Value switch
                            {
                                "Dashboard" => DashboardContent(),
                                "Analytics" => AnalyticsContent(),
                                "Settings" => SettingsContent(),
                                "Documentation" => DocumentationContent(),
                                _ => Text(new TextProps { Text = "Page not found" })
                            }
                        ]
                    })
                ]
            });
        }

        private static Element Sidebar(string activeItem, Action<string> onNavigate)
        {
            return Container(new ContainerProps
            {
                Width = Dimension.Pixels(250),
                Height = Dimension.Percent(100),
                FlexShrink = 0,
                BackgroundColor = Color.FromHex("#1f2937"), // Dark gray
                Padding = new Spacing(Dimension.Pixels(20)),
                Direction = LayoutDirection.Vertical,
                Gap = 10,
                Children =
                [
                    // Logo
                    Container(new ContainerProps 
                    { 
                        Direction = LayoutDirection.Horizontal, 
                        AlignItems = AlignItems.Center,
                        Gap = 10,
                        Children = [
                            Container(new ContainerProps { Width = Dimension.Pixels(32), Height = Dimension.Pixels(32), BackgroundColor = Color.FromHex("#4f46e5"), BorderRadius = 8 }),
                            Text(new TextProps { Text = "EchoUI", Color = Color.White, FontSize = 24, FontFamily = "Segoe UI", FontWeight = "Bold" })
                        ] 
                    }),
                    
                    Container(new ContainerProps { Height = Dimension.Pixels(30) }), // Spacer

                    // Menu Items
                    MenuItem("Dashboard", activeItem == "Dashboard", () => onNavigate("Dashboard"), "🏠"),
                    MenuItem("Analytics", activeItem == "Analytics", () => onNavigate("Analytics"), "📊"),
                    MenuItem("Settings", activeItem == "Settings", () => onNavigate("Settings"), "⚙️"),
                    MenuItem("Documentation", activeItem == "Documentation", () => onNavigate("Documentation"), "📚"),
                ]
            });
        }

        private static Element MenuItem(string title, bool active, Action onClick, string icon)
        {
            return Container(new ContainerProps
            {
                Width = Dimension.Percent(100),
                Padding = new Spacing(Dimension.Pixels(12)),
                BackgroundColor = active ? Color.FromHex("#374151") : Color.Transparent,
                BorderRadius = 6,
                OnClick = _ => onClick(),
                Direction = LayoutDirection.Horizontal,
                Gap = 12,
                AlignItems = AlignItems.Center,
                Children = [
                    Text(new TextProps { Text = icon, FontSize = 16 }),
                    Text(new TextProps 
                    { 
                        Text = title, 
                        Color = active ? Color.White : Color.FromHex("#9ca3af"),
                        FontSize = 14,
                        FontWeight = active ? "600" : "400"
                    })
                ],
                Transitions = new ValueDictionary<string, Transition>(new Dictionary<string, Transition>
                {
                    [nameof(ContainerProps.BackgroundColor)] = new(200, Easing.EaseOut)
                })
            });
        }

        private record UserModel(string Name, string Role, string Status);

        private static Element DashboardContent()
        {
            // State for the list of users
            var (users, setUsers, _) = State(new List<UserModel>
            {
                new("Alice Johnson", "Administrator", "Active"),
                new("Bob Smith", "Editor", "Pending"),
                new("Charlie Brown", "Viewer", "Suspended")
            });

            return Container(new ContainerProps
            {
                Width = Dimension.Percent(100),
                Direction = LayoutDirection.Vertical,
                Gap = 30,
                AlignItems = AlignItems.Stretch,
                Children = 
                [
                    Header("Dashboard Overview", "Welcome back, Administrator."),
                    
                    // Stats Row
                    Container(new ContainerProps
                    {
                        Direction = LayoutDirection.Horizontal,
                        Width = Dimension.Percent(100),
                        Gap = 20,
                        Children = 
                        [
                            StatCard("Total Users", users.Value.Count.ToString("N0"), "12% increase", Color.FromHex("#4f46e5")),
                            StatCard("Active Sessions", "842", "5% increase", Color.FromHex("#10b981")),
                            StatCard("Server Load", "24%", "Stable", Color.FromHex("#f59e0b"))
                        ]
                    }),

                    // Recent Activity & Quick Actions
                    Container(new ContainerProps 
                    {
                        Width = Dimension.Percent(100),
                        Direction = LayoutDirection.Horizontal,
                        Gap = 20,
                        AlignItems = AlignItems.Stretch,
                        Children = [
                            // Main Form / Table area
                            Container(new ContainerProps 
                            { 
                                FlexGrow = 2, 
                                FlexShrink = 1,
                                Gap = 20,
                                AlignItems = AlignItems.Stretch,
                                Children = 
                                [
                                    UserForm(newUser => 
                                    {
                                        var newList = new List<UserModel>(users.Value) { newUser };
                                        setUsers(newList);
                                    }),
                                    
                                    // User List
                                    Container(new ContainerProps
                                    {
                                        Width = Dimension.Percent(100),
                                        BackgroundColor = Color.White,
                                        Padding = new Spacing(Dimension.Pixels(24)),
                                        BorderRadius = 8,
                                        BorderWidth = 1,
                                        BorderColor = Color.FromHex("#e5e7eb"),
                                        Gap = 15,
                                        AlignItems = AlignItems.Stretch,
                                        Children = 
                                        [
                                            Text(new TextProps { Text = "Recent Users", FontSize = 18, Color = Color.Black }),
                                            Container(new ContainerProps
                                            {
                                                Width = Dimension.Percent(100),
                                                Gap = 10,
                                                AlignItems = AlignItems.Stretch,
                                                Children = users.Value.Select(u => UserListItem(u)).ToList()
                                            })
                                        ]
                                    })
                                ]
                            }),
                            
                            // Side widget
                            Container(new ContainerProps
                            {
                                FlexGrow = 1,
                                FlexShrink = 1,
                                BackgroundColor = Color.White,
                                Padding = new Spacing(Dimension.Pixels(24)),
                                BorderRadius = 8,
                                BorderWidth = 1,
                                BorderColor = Color.FromHex("#e5e7eb"),
                                Gap = 15,
                                AlignItems = AlignItems.Stretch,
                                Children = [
                                    Text(new TextProps { Text = "System Status", FontSize = 18, Color = Color.Black }),
                                    StatusItem("Database", "Online", Color.FromHex("#10b981")),
                                    StatusItem("Redis Cache", "Online", Color.FromHex("#10b981")),
                                    StatusItem("Email Service", "Degraded", Color.FromHex("#f59e0b")),
                                    StatusItem("Background Jobs", "Online", Color.FromHex("#10b981")),
                                ]
                            })
                        ]
                    })
                ]
            });
        }

        private static Element UserListItem(UserModel user)
        {
            return Container(new ContainerProps
            {
                Width = Dimension.Percent(100),
                Direction = LayoutDirection.Horizontal,
                JustifyContent = JustifyContent.SpaceBetween,
                AlignItems = AlignItems.Center,
                Padding = new Spacing(Dimension.Pixels(12)),
                BackgroundColor = Color.FromHex("#f9fafb"),
                BorderRadius = 6,
                Children = 
                [
                    Container(new ContainerProps 
                    { 
                        Gap = 4,
                        Children = [
                            Text(new TextProps { Text = user.Name, Color = Color.FromHex("#111827"), FontSize = 14, FontWeight = "500" }),
                            Text(new TextProps { Text = user.Role, Color = Color.FromHex("#6b7280"), FontSize = 12 })
                        ] 
                    }),
                    Container(new ContainerProps 
                    {
                        Padding = new Spacing(Dimension.Pixels(2), Dimension.Pixels(8)),
                        BackgroundColor = user.Status == "Active" ? Color.FromHex("#10b981").WithAlpha(25) : 
                                          user.Status == "Pending" ? Color.FromHex("#f59e0b").WithAlpha(25) : 
                                          Color.FromHex("#ef4444").WithAlpha(25),
                        BorderRadius = 12,
                        Children = [
                            Text(new TextProps 
                            { 
                                Text = user.Status, 
                                Color = user.Status == "Active" ? Color.FromHex("#10b981") : 
                                        user.Status == "Pending" ? Color.FromHex("#f59e0b") : 
                                        Color.FromHex("#ef4444"), 
                                FontSize = 12, 
                                FontWeight = "500" 
                            })
                        ]
                    })
                ]
            });
        }

        private static Element AnalyticsContent()
        {
             return Container(new ContainerProps
            {
                Width = Dimension.Percent(100),
                Direction = LayoutDirection.Vertical,
                Gap = 30,
                AlignItems = AlignItems.Stretch,
                Children = 
                [
                    Header("Analytics", "View system performance and user growth."),
                    Container(new ContainerProps 
                    { 
                        Height = Dimension.Pixels(300), 
                        Width = Dimension.Percent(100), 
                        BackgroundColor = Color.White, 
                        BorderRadius = 8,
                        BorderWidth = 1,
                        BorderColor = Color.FromHex("#e5e7eb"),
                        JustifyContent = JustifyContent.Center,
                        AlignItems = AlignItems.Center,
                        Children = [ Text(new TextProps { Text = "Chart Placeholder", Color = Color.Gray }) ]
                    })
                ]
            });
        }

        private static Element SettingsContent()
        {
             return Container(new ContainerProps
            {
                Width = Dimension.Percent(100),
                Direction = LayoutDirection.Vertical,
                Gap = 30,
                AlignItems = AlignItems.Stretch,
                Children = 
                [
                    Header("Settings", "Manage application configurations."),
                    Text(new TextProps { Text = "General Settings", FontSize = 18, Color = Color.Black }),
                    // Add more settings controls here...
                ]
            });
        }

        private static Element DocumentationContent()
        {
             return Container(new ContainerProps
            {
                Width = Dimension.Percent(100),
                Direction = LayoutDirection.Vertical,
                Gap = 30,
                AlignItems = AlignItems.Stretch,
                Children = 
                [
                    Header("Documentation", "Learn how to use EchoUI."),
                    Text(new TextProps { Text = "Getting Started", FontSize = 18, Color = Color.Black }),
                    Text(new TextProps { Text = "EchoUI is a declarative UI framework...", FontSize = 14, Color = Color.Gray }),
                ]
            });
        }

        private static Element Header(string title, string subtitle)
        {
            return Container(new ContainerProps
            {
                Children = [
                    Text(new TextProps 
                    { 
                        Text = title, 
                        Color = Color.FromHex("#111827"), 
                        FontSize = 28, 
                        FontFamily = "Segoe UI",
                        FontWeight = "SemiBold"
                    }),
                    Text(new TextProps 
                    { 
                        Text = subtitle, 
                        Color = Color.FromHex("#6b7280"), 
                        FontSize = 14 
                    })
                ]
            });
        }

        private static Element StatCard(string title, string value, string change, Color accentColor)
        {
            return Container(new ContainerProps
            {
                FlexGrow = 1,
                FlexShrink = 1,
                BackgroundColor = Color.White,
                Padding = new Spacing(Dimension.Pixels(24)),
                BorderRadius = 8,
                BorderWidth = 1,
                BorderColor = Color.FromHex("#e5e7eb"),
                Children = 
                [
                    Container(new ContainerProps 
                    { 
                        Width = Dimension.Percent(100),
                        Direction = LayoutDirection.Horizontal, 
                        JustifyContent = JustifyContent.SpaceBetween,
                        AlignItems = AlignItems.Center,
                        Children = [
                            Text(new TextProps { Text = title, Color = Color.FromHex("#6b7280"), FontSize = 14 }),
                            Container(new ContainerProps { Width = Dimension.Pixels(8), Height = Dimension.Pixels(8), BorderRadius = 4, BackgroundColor = accentColor })
                        ] 
                    }),
                    Container(new ContainerProps { Height = Dimension.Pixels(10) }),
                    Text(new TextProps { Text = value, Color = Color.FromHex("#111827"), FontSize = 30, FontWeight = "Bold" }),
                    Container(new ContainerProps { Height = Dimension.Pixels(5) }),
                    Text(new TextProps { Text = change, Color = Color.FromHex("#10b981"), FontSize = 12 }),
                ]
            });
        }

        private static Element StatusItem(string name, string status, Color color)
        {
            return Container(new ContainerProps
            {
                Direction = LayoutDirection.Horizontal,
                JustifyContent = JustifyContent.SpaceBetween,
                AlignItems = AlignItems.Center,
                Width = Dimension.Percent(100),
                Children = [
                    Text(new TextProps { Text = name, Color = Color.FromHex("#4b5563"), FontSize = 14 }),
                    Container(new ContainerProps 
                    {
                        Padding = new Spacing(Dimension.Pixels(4), Dimension.Pixels(8)),
                        BackgroundColor = color.WithAlpha(50), // Light bg
                        BorderRadius = 12,
                        Children = [
                            Text(new TextProps { Text = status, Color = color, FontSize = 12, FontWeight = "500" })
                        ]
                    })
                ]
            });
        }

        private static Element UserForm(Action<UserModel> onUserCreated)
        {
            var (username, setUsername, _) = State("");
            var (roleIndex, setRoleIndex, _) = State(0);
            var (statusIndex, setStatusIndex, _) = State(0);

            var roles = new[] { "Administrator", "Editor", "Viewer", "Guest" };
            var statuses = new[] { "Active", "Pending", "Suspended" };

            return Container(new ContainerProps
            {
                Width = Dimension.Percent(100),
                BackgroundColor = Color.White,
                Padding = new Spacing(Dimension.Pixels(24)),
                BorderRadius = 8,
                BorderWidth = 1,
                BorderColor = Color.FromHex("#e5e7eb"),
                Gap = 20,
                AlignItems = AlignItems.Stretch,
                Children =
                [
                    Text(new TextProps { Text = "Add New User", FontSize = 18, Color = Color.Black }),

                    // Grid-like layout for form
                    Container(new ContainerProps
                    {
                        Width = Dimension.Percent(100),
                        Direction = LayoutDirection.Horizontal,
                        Gap = 20,
                        AlignItems = AlignItems.Stretch,
                        Children =
                        [
                            // Left Column
                            Container(new ContainerProps
                            {
                                Width = Dimension.ZeroPixels,
                                FlexGrow = 1,
                                FlexShrink = 1,
                                Gap = 15,
                                AlignItems = AlignItems.Stretch,
                                Children = 
                                [
                                    Label("Username"),
                                    Container(new ContainerProps 
                                    { 
                                        Height = Dimension.Pixels(38),
                                        BorderWidth = 1,
                                        BorderStyle = BorderStyle.Solid,
                                        BorderColor = Color.FromHex("#d1d5db"),
                                        BorderRadius = 4,
                                        Padding = new Spacing(Dimension.Pixels(8), Dimension.Pixels(4)),
                                        Children = [
                                            Input(new InputProps { Value = username.Value, OnValueChanged = v => setUsername(v) })
                                        ]
                                    }),

                                    Label("Role"),
                                    ComboBox(new ComboBoxProps
                                    {
                                        Options = roles,
                                        SelectedIndex = roleIndex.Value,
                                        OnSelectionChanged = v => setRoleIndex(v),
                                        BorderColor = Color.FromHex("#d1d5db"),
                                        BackgroundColor = Color.White
                                    })
                                ]
                            }),

                            // Right Column
                            Container(new ContainerProps
                            {
                                Width = Dimension.ZeroPixels,
                                FlexGrow = 1,
                                FlexShrink = 1,
                                Gap = 15,
                                AlignItems = AlignItems.Stretch,
                                Children = 
                                [
                                    Label("Status"),
                                    RadioGroup(new RadioGroupProps
                                    {
                                        Options = statuses,
                                        SelectedIndex = statusIndex.Value,
                                        OnSelectionChanged = v => setStatusIndex(v),
                                        Direction = LayoutDirection.Horizontal,
                                        SelectedColor = Color.FromHex("#4f46e5")
                                    }),

                                    Label("Settings"),
                                    Container(new ContainerProps
                                    {
                                        Direction = LayoutDirection.Horizontal,
                                        AlignItems = AlignItems.Center,
                                        Gap = 10,
                                        Children = 
                                        [
                                            Switch(new SwitchProps { DefaultIsOn = true, OnColor = Color.FromHex("#4f46e5") }),
                                            Text(new TextProps { Text = "Send Welcome Email", Color = Color.FromHex("#374151") })
                                        ]
                                    })
                                ]
                            })
                        ]
                    }),

                    Container(new ContainerProps
                    {
                        Width = Dimension.Percent(100),
                        Direction = LayoutDirection.Horizontal,
                        JustifyContent = JustifyContent.End,
                        Children = 
                        [
                            Button(new ButtonProps
                            {
                                Text = "Create User",
                                BackgroundColor = Color.FromHex("#4f46e5"),
                                TextColor = Color.White,
                                Width = Dimension.Pixels(120),
                                Height = Dimension.Pixels(40),
                                BorderRadius = 4,
                                OnClick = _ => 
                                { 
                                    if (!string.IsNullOrWhiteSpace(username.Value))
                                    {
                                        onUserCreated(new UserModel(username.Value, roles[roleIndex.Value], statuses[statusIndex.Value]));
                                        setUsername(""); // Reset form
                                    }
                                }
                            })
                        ]
                    })
                ]
            });
        }

        private static Element Label(string text)
        {
            return Text(new TextProps { Text = text, Color = Color.FromHex("#374151"), FontSize = 14, FontWeight = "500" });
        }
    }
}

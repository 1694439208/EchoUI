using EchoUI.Core;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using static EchoUI.Core.Elements;
using static EchoUI.Core.Hooks;

namespace EchoUI.Tools.Serial;

public static class SerialPage
{
    private class PortHolder
    {
        public SerialPort? Port;
    }

    public static Element Create(Props props)
    {
        var (ports, setPorts, _) = State(SerialPort.GetPortNames().ToList());
        var (selectedPortIndex, setSelectedPortIndex, _) = State(0);

        var (baudRateIndex, setBaudRateIndex, _) = State(5);
        var baudRates = new List<string> { "300", "600", "1200", "2400", "4800", "9600", "14400", "19200", "38400", "57600", "115200" };

        var (dataBitsIndex, setDataBitsIndex, _) = State(3);
        var dataBitsOptions = new List<string> { "5", "6", "7", "8" };

        var (stopBitsIndex, setStopBitsIndex, _) = State(1);
        var stopBitsOptions = Enum.GetNames(typeof(StopBits)).ToList();

        var (parityIndex, setParityIndex, _) = State(0);
        var parityOptions = Enum.GetNames(typeof(Parity)).ToList();

        var (isOpen, setIsOpen, _) = State(false);
        var (receivedData, setReceivedData, updateReceivedData) = State(new StringBuilder());
        var (sendText, setSendText, _) = State("");
        var (hexDisplay, setHexDisplay, _) = State(false);
        var (hexSend, setHexSend, _) = State(false);

        var portHolder = Memo(() => new PortHolder(), []);

        Effect(() =>
        {
            if (!isOpen.Value)
                return null;

            if (ports.Value.Count == 0 || selectedPortIndex.Value < 0)
            {
                setIsOpen(false);
                return null;
            }

            var portIndex = selectedPortIndex.Value >= ports.Value.Count ? 0 : selectedPortIndex.Value;
            var portName = ports.Value[portIndex];
            var baud = baudRateIndex.Value >= 0 && baudRateIndex.Value < baudRates.Count
                ? int.Parse(baudRates[baudRateIndex.Value])
                : 9600;

            SerialPort? serialPort = null;
            try
            {
                serialPort = new SerialPort(portName, baud)
                {
                    DataBits = int.Parse(dataBitsOptions[dataBitsIndex.Value]),
                    StopBits = (StopBits)Enum.Parse(typeof(StopBits), stopBitsOptions[stopBitsIndex.Value]),
                    Parity = (Parity)Enum.Parse(typeof(Parity), parityOptions[parityIndex.Value])
                };

                serialPort.Open();
                portHolder.Port = serialPort;
            }
            catch (Exception ex)
            {
                setIsOpen(false);
                updateReceivedData(sb => sb.AppendLine($"Error: {ex.Message}"));
                return null;
            }

            var syncContext = SynchronizationContext.Current;

            void DataReceived(object sender, SerialDataReceivedEventArgs e)
            {
                var sp = (SerialPort)sender;
                try
                {
                    var bytesToRead = sp.BytesToRead;
                    var buffer = new byte[bytesToRead];
                    sp.Read(buffer, 0, bytesToRead);

                    if (buffer.Length <= 0)
                        return;

                    var textToAdd = hexDisplay.Value
                        ? BitConverter.ToString(buffer).Replace("-", " ") + " "
                        : Encoding.UTF8.GetString(buffer);

                    syncContext?.Post(_ => updateReceivedData(sb => sb.Append(textToAdd)), null);
                }
                catch
                {
                }
            }

            serialPort.DataReceived += DataReceived;
            updateReceivedData(sb => sb.AppendLine($"Connected to {portName} at {baud} ({dataBitsOptions[dataBitsIndex.Value]}{parityOptions[parityIndex.Value][0]}{stopBitsOptions[stopBitsIndex.Value]})."));

            return () =>
            {
                if (serialPort != null && serialPort.IsOpen)
                {
                    serialPort.DataReceived -= DataReceived;
                    serialPort.Close();
                    syncContext?.Post(_ => updateReceivedData(sb => sb.AppendLine("Disconnected.")), null);
                }

                serialPort?.Dispose();
                portHolder.Port = null;
            };
        }, [isOpen.Value, selectedPortIndex.Value, baudRateIndex.Value, ports.Value, dataBitsIndex.Value, stopBitsIndex.Value, parityIndex.Value]);

        void RefreshPorts()
        {
            var nextPorts = SerialPort.GetPortNames().ToList();
            setPorts(nextPorts);
            setSelectedPortIndex(nextPorts.Count == 0 ? 0 : Math.Clamp(selectedPortIndex.Value, 0, nextPorts.Count - 1));
        }

        void SendData()
        {
            var text = sendText.Value;
            if (string.IsNullOrEmpty(text))
                return;

            var port = portHolder.Port;
            if (port == null || !port.IsOpen)
                return;

            try
            {
                if (hexSend.Value)
                {
                    var hexValues = text.Split(new[] { ' ', '-', ',' }, StringSplitOptions.RemoveEmptyEntries);
                    var bytes = hexValues.Select(h => Convert.ToByte(h, 16)).ToArray();
                    port.Write(bytes, 0, bytes.Length);
                }
                else
                {
                    port.Write(text);
                }

                setSendText("");
            }
            catch (Exception ex)
            {
                updateReceivedData(sb => sb.AppendLine($"Send Error: {ex.Message}"));
            }
        }

        var portOptions = ports.Value.Count > 0 ? ports.Value : new List<string> { "No ports detected" };
        var effectivePortIndex = ports.Value.Count == 0 ? 0 : Math.Clamp(selectedPortIndex.Value, 0, ports.Value.Count - 1);
        var selectedPortName = ports.Value.Count == 0 ? "未检测到串口" : portOptions[effectivePortIndex];
        var currentBaud = baudRates[Math.Clamp(baudRateIndex.Value, 0, baudRates.Count - 1)];
        var currentDataBits = dataBitsOptions[Math.Clamp(dataBitsIndex.Value, 0, dataBitsOptions.Count - 1)];
        var currentStopBits = stopBitsOptions[Math.Clamp(stopBitsIndex.Value, 0, stopBitsOptions.Count - 1)];
        var currentParity = parityOptions[Math.Clamp(parityIndex.Value, 0, parityOptions.Count - 1)];
        var receivedText = receivedData.Value.ToString();
        var hasReceivedData = receivedText.Length > 0;
        var receivedLineCount = hasReceivedData ? receivedText.Count(c => c == '\n') + 1 : 0;
        var connectionSummary = ports.Value.Count == 0
            ? "等待检测串口设备"
            : $"{selectedPortName} · {currentBaud} · {currentDataBits}bit · {currentParity} · {currentStopBits}";

        return Container(new ContainerProps
        {
            Width = Dimension.Percent(100),
            Height = Dimension.Percent(100),
            Direction = LayoutDirection.Horizontal,
            BackgroundColor = Color.FromHex("#EEF3F8"),
            Padding = new Spacing(Dimension.Pixels(18)),
            Gap = 18,
            Children =
            [
                Container(new ContainerProps
                {
                    Width = Dimension.Pixels(320),
                    Height = Dimension.Percent(100),
                    Direction = LayoutDirection.Vertical,
                    AlignItems = AlignItems.Stretch,
                    Gap = 16,
                    BackgroundColor = Color.White,
                    BorderWidth = 1,
                    BorderColor = Color.FromHex("#D8E1EC"),
                    BorderRadius = 20,
                    Padding = new Spacing(Dimension.Pixels(18)),
                    Overflow = Overflow.Auto,
                    Children =
                    [
                        Container(new ContainerProps
                        {
                            Direction = LayoutDirection.Vertical,
                            Gap = 4,
                            Children =
                            [
                                Text(new TextProps
                                {
                                    Text = "Serial Tool",
                                    FontSize = 26,
                                    FontWeight = "Bold",
                                    Color = Color.FromHex("#0F172A")
                                }),
                                Text(new TextProps
                                {
                                    Text = "串口连接、监听与发送工作台",
                                    FontSize = 13,
                                    Color = Color.FromHex("#64748B")
                                })
                            ]
                        }),

                        CreateStatusSurface(isOpen.Value, selectedPortName, connectionSummary),

                        CreatePanel(
                            "连接",
                            "选择端口并建立连接",
                            [
                                CreateField(
                                    "串口",
                                    Container(new ContainerProps
                                    {
                                        Direction = LayoutDirection.Horizontal,
                                        AlignItems = AlignItems.Center,
                                        Gap = 10,
                                        Children =
                                        [
                                            Container(new ContainerProps
                                            {
                                                FlexGrow = 1,
                                                Children =
                                                [
                                                    ComboBox(new ComboBoxProps
                                                    {
                                                        Options = portOptions,
                                                        SelectedIndex = effectivePortIndex,
                                                        OnSelectionChanged = idx => setSelectedPortIndex(idx)
                                                    })
                                                ]
                                            }),
                                            Button(new ButtonProps
                                            {
                                                Text = "刷新",
                                                Width = Dimension.Pixels(74),
                                                OnClick = _ => RefreshPorts(),
                                                BackgroundColor = Color.FromHex("#E2E8F0"),
                                                TextColor = Color.FromHex("#0F172A")
                                            })
                                        ]
                                    }),
                                    ports.Value.Count == 0 ? "请连接设备后点击刷新。" : null),

                                Button(new ButtonProps
                                {
                                    Text = isOpen.Value ? "关闭串口" : "打开串口",
                                    Width = Dimension.Percent(100),
                                    Height = Dimension.Pixels(44),
                                    BackgroundColor = isOpen.Value ? Color.FromHex("#EF4444") : Color.FromHex("#2563EB"),
                                    HoverColor = isOpen.Value ? Color.FromHex("#DC2626") : Color.FromHex("#1D4ED8"),
                                    PressedColor = isOpen.Value ? Color.FromHex("#B91C1C") : Color.FromHex("#1E40AF"),
                                    TextColor = Color.White,
                                    BorderRadius = 12,
                                    OnClick = _ => setIsOpen(!isOpen.Value)
                                })
                            ]),

                        CreatePanel(
                            "串口参数",
                            "连接前设置基础通信参数",
                            [
                                CreateField("Baud Rate", ComboBox(new ComboBoxProps { Options = baudRates, SelectedIndex = baudRateIndex.Value, OnSelectionChanged = idx => setBaudRateIndex(idx) })),
                                CreateField("Data Bits", ComboBox(new ComboBoxProps { Options = dataBitsOptions, SelectedIndex = dataBitsIndex.Value, OnSelectionChanged = idx => setDataBitsIndex(idx) })),
                                CreateField("Stop Bits", ComboBox(new ComboBoxProps { Options = stopBitsOptions, SelectedIndex = stopBitsIndex.Value, OnSelectionChanged = idx => setStopBitsIndex(idx) })),
                                CreateField("Parity", ComboBox(new ComboBoxProps { Options = parityOptions, SelectedIndex = parityIndex.Value, OnSelectionChanged = idx => setParityIndex(idx) }))
                            ]),

                        CreatePanel(
                            "提示",
                            "使用建议",
                            [
                                Text(new TextProps
                                {
                                    Text = "· 接收区会保留当前会话日志。\n· HEX 发送使用空格、逗号或 - 分隔字节。\n· 视图模式切换只影响新收到的数据显示。",
                                    FontSize = 12,
                                    Color = Color.FromHex("#64748B")
                                })
                            ])
                    ]
                }),

                Container(new ContainerProps
                {
                    FlexGrow = 1,
                    Height = Dimension.Percent(100),
                    Direction = LayoutDirection.Vertical,
                    AlignItems = AlignItems.Stretch,
                    Gap = 16,
                    Children =
                    [
                        Container(new ContainerProps
                        {
                            Width = Dimension.Percent(100),
                            BackgroundColor = Color.White,
                            BorderWidth = 1,
                            BorderColor = Color.FromHex("#D8E1EC"),
                            BorderRadius = 20,
                            Padding = new Spacing(Dimension.Pixels(18)),
                            Direction = LayoutDirection.Horizontal,
                            JustifyContent = JustifyContent.SpaceBetween,
                            AlignItems = AlignItems.Center,
                            Children =
                            [
                                Container(new ContainerProps
                                {
                                    Direction = LayoutDirection.Vertical,
                                    Gap = 4,
                                    Children =
                                    [
                                        Text(new TextProps
                                        {
                                            Text = "Console Workspace",
                                            FontSize = 22,
                                            FontWeight = "Bold",
                                            Color = Color.FromHex("#0F172A")
                                        }),
                                        Text(new TextProps
                                        {
                                            Text = connectionSummary,
                                            FontSize = 13,
                                            Color = Color.FromHex("#64748B")
                                        })
                                    ]
                                }),
                                Container(new ContainerProps
                                {
                                    Direction = LayoutDirection.Horizontal,
                                    Gap = 10,
                                    AlignItems = AlignItems.Center,
                                    Children =
                                    [
                                        CreateModePill(hexDisplay.Value ? "HEX 视图" : "文本视图", hexDisplay.Value ? Color.FromHex("#F59E0B") : Color.FromHex("#2563EB")),
                                        CreateModePill(hexSend.Value ? "HEX 发送" : "文本发送", hexSend.Value ? Color.FromHex("#8B5CF6") : Color.FromHex("#0F766E"))
                                    ]
                                })
                            ]
                        }),

                        Container(new ContainerProps
                        {
                            Width = Dimension.Percent(100),
                            FlexGrow = 1,
                            BackgroundColor = Color.White,
                            BorderWidth = 1,
                            BorderColor = Color.FromHex("#D8E1EC"),
                            BorderRadius = 20,
                            Padding = new Spacing(Dimension.Pixels(18)),
                            Direction = LayoutDirection.Vertical,
                            Gap = 14,
                            Children =
                            [
                                Container(new ContainerProps
                                {
                                    Direction = LayoutDirection.Horizontal,
                                    JustifyContent = JustifyContent.SpaceBetween,
                                    AlignItems = AlignItems.Center,
                                    Children =
                                    [
                                        Container(new ContainerProps
                                        {
                                            Direction = LayoutDirection.Vertical,
                                            Gap = 4,
                                            Children =
                                            [
                                                Text(new TextProps
                                                {
                                                    Text = "接收数据",
                                                    FontSize = 18,
                                                    FontWeight = "Bold",
                                                    Color = Color.FromHex("#0F172A")
                                                }),
                                                Text(new TextProps
                                                {
                                                    Text = $"{receivedLineCount} 行 · {receivedText.Length} 字符",
                                                    FontSize = 12,
                                                    Color = Color.FromHex("#94A3B8")
                                                })
                                            ]
                                        }),
                                        Container(new ContainerProps
                                        {
                                            Direction = LayoutDirection.Horizontal,
                                            AlignItems = AlignItems.Center,
                                            Gap = 16,
                                            Children =
                                            [
                                                CreateToggleRow("HEX 显示", "新接收数据按 HEX 渲染", hexDisplay.Value, v => setHexDisplay(v)),
                                                Button(new ButtonProps
                                                {
                                                    Text = "清空日志",
                                                    Width = Dimension.Pixels(92),
                                                    BackgroundColor = Color.FromHex("#E2E8F0"),
                                                    TextColor = Color.FromHex("#0F172A"),
                                                    OnClick = _ => setReceivedData(new StringBuilder())
                                                })
                                            ]
                                        })
                                    ]
                                }),
                                Container(new ContainerProps
                                {
                                    FlexGrow = 1,
                                    Width = Dimension.Percent(100),
                                    BackgroundColor = Color.FromHex("#0F172A"),
                                    BorderWidth = 1,
                                    BorderColor = Color.FromHex("#1E293B"),
                                    BorderRadius = 16,
                                    Padding = new Spacing(Dimension.Pixels(14)),
                                    Overflow = Overflow.Scroll,
                                    Children =
                                    [
                                        Text(new TextProps
                                        {
                                            Text = hasReceivedData ? receivedText : "等待串口数据…",
                                            FontFamily = "Consolas, monospace",
                                            FontSize = 13,
                                            Color = hasReceivedData ? Color.FromHex("#E2E8F0") : Color.FromHex("#64748B")
                                        })
                                    ]
                                })
                            ]
                        }),

                        Container(new ContainerProps
                        {
                            Width = Dimension.Percent(100),
                            BackgroundColor = Color.White,
                            BorderWidth = 1,
                            BorderColor = Color.FromHex("#D8E1EC"),
                            BorderRadius = 20,
                            Padding = new Spacing(Dimension.Pixels(18)),
                            Direction = LayoutDirection.Vertical,
                            Gap = 14,
                            Children =
                            [
                                Container(new ContainerProps
                                {
                                    Direction = LayoutDirection.Horizontal,
                                    JustifyContent = JustifyContent.SpaceBetween,
                                    AlignItems = AlignItems.Center,
                                    Children =
                                    [
                                        Container(new ContainerProps
                                        {
                                            Direction = LayoutDirection.Vertical,
                                            Gap = 4,
                                            Children =
                                            [
                                                Text(new TextProps
                                                {
                                                    Text = "发送数据",
                                                    FontSize = 18,
                                                    FontWeight = "Bold",
                                                    Color = Color.FromHex("#0F172A")
                                                }),
                                                Text(new TextProps
                                                {
                                                    Text = "可发送文本或 HEX 字节序列",
                                                    FontSize = 12,
                                                    Color = Color.FromHex("#94A3B8")
                                                })
                                            ]
                                        }),
                                        CreateToggleRow("HEX 发送", "按字节数组写入", hexSend.Value, v => setHexSend(v))
                                    ]
                                }),
                                Container(new ContainerProps
                                {
                                    Width = Dimension.Percent(100),
                                    Direction = LayoutDirection.Horizontal,
                                    AlignItems = AlignItems.Center,
                                    Gap = 12,
                                    Children =
                                    [
                                        Container(new ContainerProps
                                        {
                                            FlexGrow = 1,
                                            Children =
                                            [
                                                TextInput(new TextInputProps
                                                {
                                                    Value = sendText.Value,
                                                    OnValueChanged = v => setSendText(v),
                                                    Placeholder = hexSend.Value ? "输入 HEX，例如 AA 01 FF" : "输入要发送的文本…",
                                                    Width = Dimension.Percent(100),
                                                    Height = Dimension.Pixels(46),
                                                    BackgroundColor = Color.FromHex("#F8FAFC"),
                                                    TextColor = Color.FromHex("#0F172A"),
                                                    PlaceholderColor = Color.FromHex("#94A3B8"),
                                                    BorderColor = Color.FromHex("#CBD5E1"),
                                                    FocusedBorderColor = Color.FromHex("#2563EB"),
                                                    CaretColor = Color.FromHex("#2563EB"),
                                                    BorderRadius = 14,
                                                    Padding = new Spacing(Dimension.Pixels(12), Dimension.Pixels(10))
                                                })
                                            ]
                                        }),
                                        Button(new ButtonProps
                                        {
                                            Text = "发送",
                                            Width = Dimension.Pixels(96),
                                            Height = Dimension.Pixels(46),
                                            BackgroundColor = Color.FromHex("#2563EB"),
                                            HoverColor = Color.FromHex("#1D4ED8"),
                                            PressedColor = Color.FromHex("#1E40AF"),
                                            TextColor = Color.White,
                                            BorderRadius = 14,
                                            OnClick = _ => SendData()
                                        })
                                    ]
                                }),
                                Text(new TextProps
                                {
                                    Text = hexSend.Value
                                        ? "HEX 发送示例：AA 01 FF 或 AA-01-FF"
                                        : "文本发送将按当前字符串直接写入串口。",
                                    FontSize = 12,
                                    Color = Color.FromHex("#64748B")
                                })
                            ]
                        })
                    ]
                })
            ]
        });
    }

    private static Element CreateStatusSurface(bool isOpen, string portName, string summary)
    {
        var accentColor = isOpen ? Color.FromHex("#10B981") : Color.FromHex("#94A3B8");
        var title = isOpen ? "已连接" : "未连接";
        var subtitle = isOpen ? portName : "等待打开串口";

        return Container(new ContainerProps
        {
            Width = Dimension.Percent(100),
            BackgroundColor = isOpen ? Color.FromHex("#ECFDF5") : Color.FromHex("#F8FAFC"),
            BorderWidth = 1,
            BorderColor = isOpen ? Color.FromHex("#A7F3D0") : Color.FromHex("#E2E8F0"),
            BorderRadius = 18,
            Padding = new Spacing(Dimension.Pixels(16)),
            Direction = LayoutDirection.Vertical,
            Gap = 10,
            Children =
            [
                Container(new ContainerProps
                {
                    Direction = LayoutDirection.Horizontal,
                    AlignItems = AlignItems.Center,
                    Gap = 10,
                    Children =
                    [
                        Container(new ContainerProps
                        {
                            Width = Dimension.Pixels(10),
                            Height = Dimension.Pixels(10),
                            BackgroundColor = accentColor,
                            BorderRadius = 999
                        }),
                        Text(new TextProps
                        {
                            Text = title,
                            FontSize = 14,
                            FontWeight = "Bold",
                            Color = Color.FromHex("#0F172A")
                        })
                    ]
                }),
                Text(new TextProps
                {
                    Text = subtitle,
                    FontSize = 16,
                    FontWeight = "Bold",
                    Color = Color.FromHex("#0F172A")
                }),
                Text(new TextProps
                {
                    Text = summary,
                    FontSize = 12,
                    Color = Color.FromHex("#64748B")
                })
            ]
        });
    }

    private static Element CreatePanel(string title, string description, IReadOnlyList<Element> children)
    {
        return Container(new ContainerProps
        {
            BackgroundColor = Color.FromHex("#F8FAFC"),
            BorderWidth = 1,
            BorderColor = Color.FromHex("#E2E8F0"),
            BorderRadius = 18,
            Padding = new Spacing(Dimension.Pixels(16)),
            Direction = LayoutDirection.Vertical,
            Gap = 12,
            Children =
            [
                Container(new ContainerProps
                {
                    Direction = LayoutDirection.Vertical,
                    Gap = 4,
                    Children =
                    [
                        Text(new TextProps
                        {
                            Text = title,
                            FontSize = 15,
                            FontWeight = "Bold",
                            Color = Color.FromHex("#0F172A")
                        }),
                        Text(new TextProps
                        {
                            Text = description,
                            FontSize = 12,
                            Color = Color.FromHex("#64748B")
                        })
                    ]
                }),
                Container(new ContainerProps
                {
                    Width = Dimension.Percent(100),
                    Direction = LayoutDirection.Vertical,
                    AlignItems = AlignItems.Stretch,
                    Gap = 12,
                    Children = children
                })
            ]
        });
    }

    private static Element CreateField(string label, Element control, string? hint = null)
    {
        return Container(new ContainerProps
        {
            Direction = LayoutDirection.Vertical,
            Gap = 6,
            Children =
            [
                Text(new TextProps
                {
                    Text = label,
                    FontSize = 12,
                    FontWeight = "Bold",
                    Color = Color.FromHex("#334155")
                }),
                control,
                string.IsNullOrWhiteSpace(hint)
                    ? Empty()
                    : Text(new TextProps
                    {
                        Text = hint,
                        FontSize = 11,
                        Color = Color.FromHex("#94A3B8")
                    })
            ]
        });
    }

    private static Element CreateToggleRow(string title, string subtitle, bool isOn, Action<bool> onToggle)
    {
        return Container(new ContainerProps
        {
            Direction = LayoutDirection.Horizontal,
            AlignItems = AlignItems.Center,
            Gap = 10,
            Children =
            [
                Switch(new SwitchProps
                {
                    DefaultIsOn = isOn,
                    Width = Dimension.Pixels(42),
                    Height = Dimension.Pixels(24),
                    OnColor = Color.FromHex("#2563EB"),
                    OffColor = Color.FromHex("#CBD5E1"),
                    ThumbColor = Color.White,
                    OnToggle = onToggle
                }),
                Container(new ContainerProps
                {
                    Direction = LayoutDirection.Vertical,
                    Gap = 2,
                    Children =
                    [
                        Text(new TextProps
                        {
                            Text = title,
                            FontSize = 12,
                            FontWeight = "Bold",
                            Color = Color.FromHex("#0F172A")
                        }),
                        Text(new TextProps
                        {
                            Text = subtitle,
                            FontSize = 11,
                            Color = Color.FromHex("#94A3B8")
                        })
                    ]
                })
            ]
        });
    }

    private static Element CreateModePill(string text, Color accentColor)
    {
        return Container(new ContainerProps
        {
            Direction = LayoutDirection.Horizontal,
            AlignItems = AlignItems.Center,
            Gap = 8,
            BackgroundColor = accentColor.WithAlpha(24),
            BorderWidth = 1,
            BorderColor = accentColor.WithAlpha(80),
            BorderRadius = 999,
            Padding = new Spacing(Dimension.Pixels(12), Dimension.Pixels(8)),
            Children =
            [
                Container(new ContainerProps
                {
                    Width = Dimension.Pixels(8),
                    Height = Dimension.Pixels(8),
                    BackgroundColor = accentColor,
                    BorderRadius = 999
                }),
                Text(new TextProps
                {
                    Text = text,
                    FontSize = 12,
                    FontWeight = "Bold",
                    Color = accentColor
                })
            ]
        });
    }
}

using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Diagnostics;
using EchoUI.Core;

namespace EchoUI.Render.Win32
{
    /// <summary>
    /// Win32 渲染器，实现 IRenderer 接口。
    /// 使用 GDI+ 自绘模式，在单个 Win32 窗口上绘制所有 UI 元素。
    /// Input 元素使用嵌入的原生 Win32 Edit 控件。
    /// </summary>
    public class Win32Renderer : IRenderer, IDisposable
    {
        private readonly Win32Window _window;
        private Win32Element? _rootElement;
        private Win32UpdateScheduler? _scheduler;
        private HitTestManager? _hitTestManager;
        private readonly List<Win32Element> _floatingElements = [];

        internal IReadOnlyList<Win32Element> FloatingElements => _floatingElements;

        /// <summary>
        /// 所有 Input 元素的 Edit HWND → Win32Element 映射
        /// </summary>
        private readonly Dictionary<nint, Win32Element> _editElements = [];

        /// <summary>
        /// 防止 Edit 控件 EN_CHANGE 通知的递归触发
        /// </summary>
        private bool _suppressEditNotification;
        private bool _disposed;
        private readonly HashSet<string> _nativeDiagnostics = [];
        private static readonly Bitmap _measureBitmap = new(1, 1);
        private static readonly Graphics _measureGraphics;
        private static readonly StringFormat _measureStringFormat;

        internal Win32Element? RootElement => _rootElement;
        internal Win32UpdateScheduler? Scheduler => _scheduler;
        internal HitTestManager HitTestManager => _hitTestManager!;

        static Win32Renderer()
        {
            _measureGraphics = Graphics.FromImage(_measureBitmap);
            _measureGraphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            _measureGraphics.SmoothingMode = SmoothingMode.AntiAlias;
            _measureStringFormat = new StringFormat
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Near,
                Trimming = StringTrimming.None,
                FormatFlags = StringFormatFlags.MeasureTrailingSpaces
            };
        }

        public Win32Renderer(Win32Window window)
        {
            _window = window;
            _hitTestManager = new HitTestManager(this);
            window.SetRenderer(this);
        }

        public object CreateElement(string type)
        {
            var element = new Win32Element(type);

            // Input 元素创建原生 Edit 控件
            if (type == ElementCoreName.Input)
            {
                element.Width = Dimension.Percent(100);
                element.Height = Dimension.Percent(100);
                CreateEditControl(element);
            }
            else if (type != ElementCoreName.Container && type != ElementCoreName.Text && type != "img")
            {
                ReportNativeDiagnostic($"[EchoUI.Win32] Native type '{type}' is not fully supported and will be rendered as a generic container.");
            }

            return element;
        }

        public void PatchProperties(object nativeElement, Props newProps, PropertyPatch patch)
        {
            var element = (Win32Element)nativeElement;

            // 始终同步事件处理器
            UpdateEventHandlers(element, newProps);

            if (patch.UpdatedProperties == null) return;

            foreach (var (propName, propValue) in patch.UpdatedProperties)
            {
                ApplyProperty(element, newProps, propName, propValue);
            }

            // 为不同类型的元素应用默认值（与 WebRenderer 保持一致）
            switch (newProps)
            {
                case ContainerProps p:
                    element.Direction = p.Direction ?? LayoutDirection.Vertical;
                    element.JustifyContent = p.JustifyContent ?? JustifyContent.Start;
                    element.AlignItems = p.AlignItems ?? AlignItems.Start;
                    element.FlexShrink = p.FlexShrink ?? 0;
                    element.FlexGrow = p.FlexGrow ?? 0;
                    break;
                case TextProps p:
                    element.MouseThrough = p.MouseThrough;
                    element.NoWrap = p.NoWrap;
                    break;
            }

            // 同步 Input 的原生 Edit 控件
            if (element.ElementType == ElementCoreName.Input && element.EditHwnd != 0)
            {
                SyncEditControl(element);
            }


        }

        public void AddChild(object parent, object child, int index)
        {
            Win32Element parentElement;
            if (parent is string)
            {
                // 根容器
                _rootElement ??= new Win32Element(ElementCoreName.Container);
                parentElement = _rootElement;
            }
            else
            {
                parentElement = (Win32Element)parent;
            }

            var childElement = (Win32Element)child;
            childElement.Parent = parentElement;

            if (index >= 0 && index < parentElement.Children.Count)
                parentElement.Children.Insert(index, childElement);
            else
                parentElement.Children.Add(childElement);


        }

        public void RemoveChild(object parent, object child)
        {
            Win32Element parentElement;
            if (parent is string)
            {
                parentElement = _rootElement!;
            }
            else
            {
                parentElement = (Win32Element)parent;
            }

            var childElement = (Win32Element)child;
            parentElement.Children.Remove(childElement);
            childElement.Parent = null;

            ReleaseElementTree(childElement);
        }

        public void MoveChild(object parent, object child, int newIndex)
        {
            Win32Element parentElement;
            if (parent is string)
            {
                parentElement = _rootElement!;
            }
            else
            {
                parentElement = (Win32Element)parent;
            }

            var childElement = (Win32Element)child;
            parentElement.Children.Remove(childElement);

            if (newIndex >= 0 && newIndex < parentElement.Children.Count)
                parentElement.Children.Insert(newIndex, childElement);
            else
                parentElement.Children.Add(childElement);


        }

        public TextMeasurementResult MeasureText(TextMeasurementRequest request)
        {
            var text = request.Text ?? string.Empty;
            var fontSize = request.FontSize ?? 14f;
            var fontStyle = ResolveFontStyle(request.FontWeight);
            var fontFamily = ResolveFontFamily(request.FontFamily, text);

            lock (_measureGraphics)
            {
                using var font = new Font(fontFamily, fontSize, fontStyle, GraphicsUnit.Pixel);
                var size = _measureGraphics.MeasureString(text, font, new PointF(0, 0), _measureStringFormat);
                return new TextMeasurementResult(size.Width, size.Height);
            }
        }

        public IUpdateScheduler GetScheduler(object rootContainer)
        {
            _scheduler = new Win32UpdateScheduler(_window.Hwnd);
            return _scheduler;
        }

        // --- 属性应用 ---

        private void ApplyProperty(Win32Element element, Props props, string propName, object? propValue)
        {
            switch (props)
            {
                case ContainerProps:
                    ApplyContainerProperty(element, propName, propValue);
                    break;
                case TextProps:
                    ApplyTextProperty(element, propName, propValue);
                    break;
                case InputProps:
                    ApplyInputProperty(element, propName, propValue);
                    break;
                case NativeProps nativeProps:
                    ApplyNativeProperty(element, nativeProps, propName, propValue);
                    break;
            }
        }

        private void ApplyContainerProperty(Win32Element element, string propName, object? propValue)
        {
            switch (propName)
            {
                // 尺寸
                case nameof(ContainerProps.Width): element.Width = propValue as Dimension?; break;
                case nameof(ContainerProps.Height): element.Height = propValue as Dimension?; break;
                case nameof(ContainerProps.MinWidth): element.MinWidth = propValue as Dimension?; break;
                case nameof(ContainerProps.MinHeight): element.MinHeight = propValue as Dimension?; break;
                case nameof(ContainerProps.MaxWidth): element.MaxWidth = propValue as Dimension?; break;
                case nameof(ContainerProps.MaxHeight): element.MaxHeight = propValue as Dimension?; break;

                // 间距
                case nameof(ContainerProps.Margin): element.Margin = propValue as Spacing?; break;
                case nameof(ContainerProps.Padding): element.Padding = propValue as Spacing?; break;

                // Flex
                case nameof(ContainerProps.Direction):
                    element.Direction = propValue is LayoutDirection dir ? dir : LayoutDirection.Vertical;
                    break;
                case nameof(ContainerProps.JustifyContent):
                    element.JustifyContent = propValue is JustifyContent jc ? jc : JustifyContent.Start;
                    break;
                case nameof(ContainerProps.AlignItems):
                    element.AlignItems = propValue is AlignItems ai ? ai : AlignItems.Start;
                    break;
                case nameof(ContainerProps.FlexGrow):
                    element.FlexGrow = propValue is float fg ? fg : 0;
                    break;
                case nameof(ContainerProps.FlexShrink):
                    element.FlexShrink = propValue is float fs ? fs : 0;
                    break;
                case nameof(ContainerProps.Gap):
                    element.Gap = propValue is float gap ? gap : 0;
                    break;
                case nameof(ContainerProps.Float):
                    element.Float = propValue is true;
                    break;
                case nameof(ContainerProps.Overflow):
                    element.Overflow = propValue is Overflow ov ? ov : Overflow.Visible;
                    break;

                // 外观
                case nameof(ContainerProps.BackgroundColor):
                    element.BackgroundColor = propValue as Core.Color?;
                    break;
                case nameof(ContainerProps.BorderColor):
                    element.BorderColor = propValue as Core.Color?;
                    break;
                case nameof(ContainerProps.BorderStyle):
                    element.BorderStyle = propValue is Core.BorderStyle bs ? bs : Core.BorderStyle.None;
                    break;
                case nameof(ContainerProps.BorderWidth):
                    element.BorderWidth = propValue is float bw ? bw : 0;
                    break;
                case nameof(ContainerProps.BorderRadius):
                    element.BorderRadius = propValue is float br ? br : 0;
                    break;
                case nameof(ContainerProps.InputMethodAnchorPoint):
                    element.InputMethodAnchorPoint = propValue is Core.Point point ? point : null;
                    if (element.IsFocused)
                        _window.UpdateImePosition(element);
                    break;

                // 事件由 UpdateEventHandlers 处理
                // Transitions 在 Win32 下暂不支持动画，直接忽略
            }
        }

        private void ApplyTextProperty(Win32Element element, string propName, object? propValue)
        {
            switch (propName)
            {
                case nameof(TextProps.Text):
                    element.Text = propValue as string;
                    break;
                case nameof(TextProps.FontFamily):
                    element.FontFamily = propValue as string;
                    break;
                case nameof(TextProps.FontSize):
                    element.FontSize = propValue is float fs ? fs : 14;
                    break;
                case nameof(TextProps.Color):
                    element.TextColor = propValue as Core.Color?;
                    break;
                case nameof(TextProps.FontWeight):
                    element.FontWeight = propValue as string;
                    break;
                case nameof(TextProps.MouseThrough):
                    element.MouseThrough = propValue is not false;
                    break;
                case nameof(TextProps.NoWrap):
                    element.NoWrap = propValue is true;
                    break;
            }
        }

        private void ApplyInputProperty(Win32Element element, string propName, object? propValue)
        {
            switch (propName)
            {
                case nameof(InputProps.Value):
                    element.InputValue = propValue as string;
                    break;
                case nameof(InputProps.BackgroundColor):
                    element.BackgroundColor = propValue as Core.Color?;
                    break;
                case nameof(InputProps.TextColor):
                    element.TextColor = propValue as Core.Color?;
                    break;
                case nameof(InputProps.BorderColor):
                    element.BorderColor = propValue as Core.Color?;
                    ApplyInputBorderDefaults(element);
                    break;
                case nameof(InputProps.FocusedBorderColor):
                    element.FocusedBorderColor = propValue as Core.Color?;
                    ApplyInputBorderDefaults(element);
                    break;
                case nameof(InputProps.Padding):
                    element.Padding = propValue as Spacing?;
                    break;
                // OnValueChanged 由 UpdateEventHandlers 处理
            }
        }

        private void ApplyInputBorderDefaults(Win32Element element)
        {
            if (element.BorderColor.HasValue || element.FocusedBorderColor.HasValue)
            {
                if (element.BorderStyle == Core.BorderStyle.None)
                    element.BorderStyle = Core.BorderStyle.Solid;
                if (element.BorderWidth <= 0)
                    element.BorderWidth = 1;
            }
            else
            {
                element.BorderStyle = Core.BorderStyle.None;
                element.BorderWidth = 0;
            }
        }

        private void ApplyNativeProperty(Win32Element element, NativeProps nativeProps, string propName, object? propValue)
        {
            if (propValue is Delegate) return;

            if (propName == "textContent" || propName == "text")
            {
                element.Text = propValue?.ToString();
                return;
            }

            if (element.ElementType == "img")
            {
                if (propName == "src")
                {
                    if (propValue is string src)
                    {
                        LoadImage(element, src);
                    }
                    else if (propValue == null && element.NativeImage != null)
                    {
                        element.NativeImage.Dispose();
                        element.NativeImage = null;
                    }
                    return;
                }

                if (propName == "style")
                {
                    if (propValue is string style)
                    {
                        ParseStyle(element, style);
                    }
                    else if (propValue == null)
                    {
                        ResetNativeStyle(element);
                    }
                    return;
                }
            }

            ReportNativeDiagnostic($"[EchoUI.Win32] Native property '{propName}' on '{nativeProps.Type}' is not supported.");
        }

        // --- 事件处理器同步 ---

        private void UpdateEventHandlers(Win32Element element, Props newProps)
        {
            switch (newProps)
            {
                case ContainerProps p:
                    element.OnClick = p.OnClick;
                    element.OnMouseMove = p.OnMouseMove;
                    element.OnMouseEnter = p.OnMouseEnter;
                    element.OnMouseLeave = p.OnMouseLeave;
                    element.OnMouseDown = p.OnMouseDown;
                    element.OnMouseUp = p.OnMouseUp;
                    element.OnKeyDown = p.OnKeyDown;
                    element.OnKeyUp = p.OnKeyUp;
                    element.OnTextInput = p.OnTextInput;
                    element.OnTextComposition = p.OnTextComposition;
                    element.OnFocus = p.OnFocus;
                    element.OnBlur = p.OnBlur;
                    break;
                case InputProps ip:
                    element.OnValueChanged = ip.OnValueChanged;
                    break;
                case NativeProps nativeProps:
                    ClearNativeEventHandlers(element);
                    if (nativeProps.Properties == null) break;

                    foreach (var item in nativeProps.Properties.Value.Data)
                    {
                        ApplyNativeEventHandler(element, item.Key, item.Value);
                    }
                    break;
            }
        }

        private static void ClearNativeEventHandlers(Win32Element element)
        {
            element.OnClick = null;
            element.OnMouseMove = null;
            element.OnMouseEnter = null;
            element.OnMouseLeave = null;
            element.OnMouseDown = null;
            element.OnMouseUp = null;
            element.OnKeyDown = null;
            element.OnKeyUp = null;
            element.OnTextInput = null;
            element.OnTextComposition = null;
            element.OnFocus = null;
            element.OnBlur = null;
            element.OnValueChanged = null;
        }

        private void ApplyNativeEventHandler(Win32Element element, string eventName, object? value)
        {
            switch (eventName)
            {
                case "click" when value is Action<MouseButton> clickHandler:
                    element.OnClick = clickHandler;
                    return;
                case "click" when value is Action clickAction:
                    element.OnClick = _ => clickAction();
                    return;
                case "mousemove" when value is Action<Core.Point> mouseMoveHandler:
                    element.OnMouseMove = mouseMoveHandler;
                    return;
                case "mouseenter" when value is Action mouseEnterHandler:
                    element.OnMouseEnter = mouseEnterHandler;
                    return;
                case "mouseleave" when value is Action mouseLeaveHandler:
                    element.OnMouseLeave = mouseLeaveHandler;
                    return;
                case "mousedown" when value is Action mouseDownHandler:
                    element.OnMouseDown = mouseDownHandler;
                    return;
                case "mouseup" when value is Action mouseUpHandler:
                    element.OnMouseUp = mouseUpHandler;
                    return;
                case "keydown" when value is Action<int> keyDownHandler:
                    element.OnKeyDown = keyDownHandler;
                    return;
                case "keyup" when value is Action<int> keyUpHandler:
                    element.OnKeyUp = keyUpHandler;
                    return;
                case "keypress" when value is Action<string> textInputHandler:
                    element.OnTextInput = textInputHandler;
                    return;
                case "textcomposition" when value is Action<TextCompositionEvent> textCompositionHandler:
                    element.OnTextComposition = textCompositionHandler;
                    return;
                case "focus" when value is Action focusHandler:
                    element.OnFocus = focusHandler;
                    return;
                case "blur" when value is Action blurHandler:
                    element.OnBlur = blurHandler;
                    return;
                case "input" when value is Action<string> inputHandler:
                    element.OnValueChanged = inputHandler;
                    return;
                default:
                    ReportNativeDiagnostic($"[EchoUI.Win32] Native event '{eventName}' is not supported.");
                    return;
            }
        }

        private static string ResolveFontFamily(string? fontFamily, string? text)
        {
            if (!string.IsNullOrWhiteSpace(fontFamily))
                return fontFamily;

            return IsLikelyEmojiOrSymbol(text) ? "Segoe UI Emoji" : "Segoe UI";
        }

        private static bool IsLikelyEmojiOrSymbol(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            foreach (var c in text)
            {
                if (char.IsSurrogate(c))
                    return true;

                if (c >= 0x2000 && c <= 0x33FF)
                    return true;
            }

            return false;
        }

        [Conditional("DEBUG")]
        private void ReportNativeDiagnostic(string message)
        {
            if (_nativeDiagnostics.Add(message))
            {
                Debug.WriteLine(message);
            }
        }

        // --- Edit 控件管理 ---

        private void CreateEditControl(Win32Element element)
        {
            if (_window.Hwnd == 0) return;

            var hwnd = NativeInterop.CreateWindowEx(
                0,
                "EDIT",
                "",
                NativeInterop.WS_CHILD | NativeInterop.WS_VISIBLE | NativeInterop.ES_AUTOHSCROLL | NativeInterop.ES_LEFT,
                0, 0, 100, 24,
                _window.Hwnd,
                0,
                NativeInterop.GetModuleHandle(null),
                0);

            if (hwnd != 0)
            {
                element.EditHwnd = hwnd;
                _editElements[hwnd] = element;
                SyncEditControl(element);
            }
        }

        private void SyncEditControl(Win32Element element)
        {
            if (element.EditHwnd == 0) return;

            // --- 同步文本值 ---
            if (element.InputValue != null)
            {
                int len = NativeInterop.GetWindowTextLength(element.EditHwnd);
                var buffer = new char[len + 1];
                NativeInterop.GetWindowText(element.EditHwnd, buffer, buffer.Length);
                var currentText = new string(buffer, 0, len);

                if (currentText != element.InputValue)
                {
                    _suppressEditNotification = true;
                    NativeInterop.SetWindowText(element.EditHwnd, element.InputValue);
                    _suppressEditNotification = false;
                }
            }
            
            // --- 同步字体 ---
            // 简单起见，每次只要属性可能变了就重建字体 (GDI 资源要注意释放)
            // 这里为了简化逻辑，我们先释放旧的
            if (element.NativeFontHandle != 0)
            {
                NativeInterop.DeleteObject(element.NativeFontHandle);
                element.NativeFontHandle = 0;
            }
            
            var fontStyle = ResolveFontStyle(element.FontWeight);

            using (var font = new Font(element.FontFamily ?? "Segoe UI", element.FontSize > 0 ? element.FontSize : 14, fontStyle, GraphicsUnit.Pixel))
            {
                element.NativeFontHandle = font.ToHfont();
                NativeInterop.SendMessage(element.EditHwnd, NativeInterop.WM_SETFONT, element.NativeFontHandle, 1);
            }

            // --- 同步背景刷 (用于 WM_CTLCOLOREDIT) ---
            if (element.NativeBrushHandle != 0)
            {
                NativeInterop.DeleteObject(element.NativeBrushHandle);
                element.NativeBrushHandle = 0;
            }
            // 如果透明背景或者默认，通常用白色，这里我们用 BackgroundColor
            var bgColor = element.BackgroundColor ?? Core.Color.Transparent; // 如果没有背景色，Input 默认可能透或者是白？通常 Input 是白的
            if (!element.BackgroundColor.HasValue) bgColor = new Core.Color(255, 255, 255, 255);
            
            int colorRef = (bgColor.B << 16) | (bgColor.G << 8) | bgColor.R;
            element.NativeBrushHandle = NativeInterop.CreateSolidBrush(colorRef);

            // 触发重绘以应用颜色
            NativeInterop.InvalidateRect(element.EditHwnd, 0, true);
        }

        /// <summary>
        /// 处理 Edit 控件的 EN_CHANGE 通知
        /// </summary>
        internal void HandleEditChange(nint editHwnd)
        {
            if (_suppressEditNotification) return;

            if (_editElements.TryGetValue(editHwnd, out var element))
            {
                var text = GetWindowText(editHwnd);
                element.OnValueChanged?.Invoke(text);

                var syncContext = SynchronizationContext.Current;
                if (syncContext != null)
                {
                    syncContext.Post(_ => RestoreControlledEditValue(element), null);
                }
                else
                {
                    RestoreControlledEditValue(element);
                }
            }
        }

        private static string GetWindowText(nint editHwnd)
        {
            int len = NativeInterop.GetWindowTextLength(editHwnd);
            var buffer = new char[len + 1];
            NativeInterop.GetWindowText(editHwnd, buffer, buffer.Length);
            return new string(buffer, 0, len);
        }

        private void RestoreControlledEditValue(Win32Element element)
        {
            if (element.EditHwnd == 0 || !NativeInterop.IsWindow(element.EditHwnd))
                return;

            var controlledValue = element.InputValue ?? string.Empty;
            var currentValue = GetWindowText(element.EditHwnd);
            if (currentValue == controlledValue)
                return;

            _suppressEditNotification = true;
            NativeInterop.SetWindowText(element.EditHwnd, controlledValue);
            _suppressEditNotification = false;
        }

        internal void HandleEditFocusChange(nint editHwnd, bool isFocused)
        {
            if (_editElements.TryGetValue(editHwnd, out var element))
            {
                element.IsFocused = isFocused;
                RequestRepaint();
            }
        }

        private void ReleaseElementTree(Win32Element element)
        {
            foreach (var child in element.Children.ToArray())
            {
                ReleaseElementTree(child);
            }

            element.Children.Clear();
            ReleasePlatformResources(element);
            ClearNativeEventHandlers(element);
            element.Parent = null;
        }

        private void ReleasePlatformResources(Win32Element element)
        {
            if (element.EditHwnd != 0)
            {
                _editElements.Remove(element.EditHwnd);

                if (element.NativeFontHandle != 0)
                {
                    NativeInterop.DeleteObject(element.NativeFontHandle);
                    element.NativeFontHandle = 0;
                }
                if (element.NativeBrushHandle != 0)
                {
                    NativeInterop.DeleteObject(element.NativeBrushHandle);
                    element.NativeBrushHandle = 0;
                }

                if (NativeInterop.IsWindow(element.EditHwnd))
                    NativeInterop.DestroyWindow(element.EditHwnd);
                element.EditHwnd = 0;
            }
            else
            {
                if (element.NativeFontHandle != 0)
                {
                    NativeInterop.DeleteObject(element.NativeFontHandle);
                    element.NativeFontHandle = 0;
                }
                if (element.NativeBrushHandle != 0)
                {
                    NativeInterop.DeleteObject(element.NativeBrushHandle);
                    element.NativeBrushHandle = 0;
                }
            }

            if (element.NativeImage != null)
            {
                element.NativeImage.Dispose();
                element.NativeImage = null;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            if (_rootElement != null)
            {
                ReleaseElementTree(_rootElement);
                _rootElement = null;
            }

            _floatingElements.Clear();
            _editElements.Clear();
        }

        // --- 布局与重绘 ---

        /// <summary>
        /// 请求重新布局并重绘
        /// </summary>
        public void RequestRelayout()
        {
            if (_rootElement == null || _window.Hwnd == 0) return;

            NativeInterop.GetClientRect(_window.Hwnd, out var rect);
            float vpW = rect.Width;
            float vpH = rect.Height;

            if (vpW > 0 && vpH > 0)
            {
                FlexLayout.ComputeLayout(_rootElement, vpW, vpH);
                UpdateEditPositions(_rootElement, vpW, vpH);
                CollectFloatingElements();
            }

            NativeInterop.InvalidateRect(_window.Hwnd, 0, false);
        }

        private void CollectFloatingElements()
        {
            _floatingElements.Clear();
            if (_rootElement == null) return;
            CollectFloatingElementsRecursive(_rootElement);
        }

        private void CollectFloatingElementsRecursive(Win32Element element)
        {
            foreach (var child in element.Children)
            {
                if (child.Float)
                {
                    _floatingElements.Add(child);
                    // 如果它是 Float 元素，我们把它作为独立的层。
                    // 它的子元素如果也是 Float，通常是相对于它的（如下级菜单），
                    // 所以我们暂时不把嵌套的 Float 提升到顶层，而是跟随这个 Float 元素。
                    // 但这里策略是：只要是 Float，就收集？
                    // 如果 A(Float) -> B(Float)，B 是 A 的子元素。
                    // 如果 Paint(A) 会 Paint(B)。
                    // 如果我们收集了 A，GdiPainter 会 Paint(A)。
                    // 此时我们不应该再收集 B，否则 B 会被画两次（一次在 A 内部，一次作为 Top Layer）。
                    // 所以：一旦遇到 Float，加入列表，并且不再遍历其子元素寻找 Float。
                }
                else
                {
                    CollectFloatingElementsRecursive(child);
                }
            }
        }

        internal void FocusWindow()
        {
            if (_window.Hwnd != 0)
                NativeInterop.SetFocus(_window.Hwnd);
        }

        /// <summary>
        /// 请求重绘（不重新布局）
        /// </summary>
        internal void RequestRepaint()
        {
            if (_window.Hwnd != 0)
                NativeInterop.InvalidateRect(_window.Hwnd, 0, false);
        }

        /// <summary>
        /// 更新所有 Edit 控件的位置以匹配布局结果（公开方法供 Win32Window 调用）
        /// </summary>
        public void UpdateAllEditPositions(float vpW, float vpH)
        {
            if (_rootElement != null)
                UpdateEditPositions(_rootElement, vpW, vpH);
        }

        /// <summary>
        /// 更新所有 Edit 控件的位置以匹配布局结果
        /// </summary>
        private void UpdateEditPositions(Win32Element element, float vpW, float vpH)
        {
            if (element.EditHwnd != 0)
            {
                var padding = ResolvePadding(element.Padding, element.LayoutWidth, vpW, vpH);
                float border = GetBorderInset(element);

                float contentX = element.AbsoluteX + padding.Left + border;
                float contentY = element.AbsoluteY + padding.Top + border;
                float contentW = Math.Max(0, element.LayoutWidth - padding.Left - padding.Right - border * 2);
                float contentH = Math.Max(0, element.LayoutHeight - padding.Top - padding.Bottom - border * 2);
                float editH = Math.Min(contentH, GetEditPreferredHeight(element));
                float editY = contentY + Math.Max(0, (contentH - editH) / 2f);

                int x = (int)Math.Floor(contentX);
                int y = (int)Math.Round(editY, MidpointRounding.AwayFromZero);
                int w = (int)Math.Ceiling(contentW);
                int h = Math.Max(1, (int)Math.Round(editH, MidpointRounding.AwayFromZero));

                var editRect = new RectangleF(x, y, w, h);
                var clipRect = GetEditClipRect(element, vpW, vpH);
                var visibleRect = RectangleF.Intersect(editRect, clipRect);

                if (visibleRect.Width <= 0 || visibleRect.Height <= 0 || w <= 0 || h <= 0)
                {
                    NativeInterop.ShowWindow(element.EditHwnd, NativeInterop.SW_HIDE);
                }
                else
                {
                    NativeInterop.ShowWindow(element.EditHwnd, NativeInterop.SW_SHOW);
                    NativeInterop.MoveWindow(
                        element.EditHwnd,
                        x,
                        y,
                        w,
                        h,
                        true);
                    ApplyEditClipRegion(element.EditHwnd, editRect, visibleRect);
                }
            }

            foreach (var child in element.Children)
            {
                UpdateEditPositions(child, vpW, vpH);
            }
        }

        private RectangleF GetEditClipRect(Win32Element element, float vpW, float vpH)
        {
            var clipRect = new RectangleF(0, 0, vpW, vpH);
            var current = element.Parent;

            while (current != null)
            {
                if (current.Overflow != Overflow.Visible)
                {
                    clipRect = RectangleF.Intersect(clipRect, current.GetAbsoluteBounds());
                }

                if (current.Float)
                    break;

                current = current.Parent;
            }

            return clipRect;
        }

        private static void ApplyEditClipRegion(nint hwnd, RectangleF editRect, RectangleF visibleRect)
        {
            if (visibleRect.Left <= editRect.Left && visibleRect.Top <= editRect.Top &&
                visibleRect.Right >= editRect.Right && visibleRect.Bottom >= editRect.Bottom)
            {
                NativeInterop.SetWindowRgn(hwnd, 0, true);
                return;
            }

            int left = Math.Max(0, (int)Math.Floor(visibleRect.Left - editRect.Left));
            int top = Math.Max(0, (int)Math.Floor(visibleRect.Top - editRect.Top));
            int right = Math.Max(left, (int)Math.Ceiling(visibleRect.Right - editRect.Left));
            int bottom = Math.Max(top, (int)Math.Ceiling(visibleRect.Bottom - editRect.Top));

            var region = NativeInterop.CreateRectRgn(left, top, right, bottom);
            if (region == 0) return;

            if (NativeInterop.SetWindowRgn(hwnd, region, true) == 0)
            {
                NativeInterop.DeleteObject(region);
            }
        }

        private static float GetEditPreferredHeight(Win32Element element)
        {
            var fontSize = element.FontSize > 0 ? element.FontSize : 14f;
            try
            {
                using var font = new Font(element.FontFamily ?? "Segoe UI", fontSize, ResolveFontStyle(element.FontWeight), GraphicsUnit.Pixel);
                return (float)Math.Ceiling(font.GetHeight()) + 1f;
            }
            catch
            {
                return fontSize + 3f;
            }
        }

        private static FontStyle ResolveFontStyle(string? fontWeight)
        {
            if (string.IsNullOrEmpty(fontWeight)) return FontStyle.Regular;

            var weight = fontWeight.ToLowerInvariant();
            return weight is "bold" or "semibold" or "500" or "600" or "700" or "800" or "900"
                ? FontStyle.Bold
                : FontStyle.Regular;
        }

        private static float GetBorderInset(Win32Element element)
        {
            return element.BorderStyle == Core.BorderStyle.None ? 0 : Math.Max(0, element.BorderWidth);
        }

        private (float Left, float Top, float Right, float Bottom) ResolvePadding(Spacing? padding, float width, float vpW, float vpH)
        {
            if (padding == null) return (0, 0, 0, 0);
            return (
                ResolveDimension(padding.Value.Left, width, vpW, vpH),
                ResolveDimension(padding.Value.Top, width, vpW, vpH),
                ResolveDimension(padding.Value.Right, width, vpW, vpH),
                ResolveDimension(padding.Value.Bottom, width, vpW, vpH)
            );
        }

        private float ResolveDimension(Dimension? d, float parentSize, float vpW, float vpH)
        {
            if (d == null) return 0;
            return d.Value.Unit switch
            {
                DimensionUnit.Pixels => d.Value.Value,
                DimensionUnit.Percent => parentSize * d.Value.Value / 100f,
                DimensionUnit.ViewportHeight => vpH * d.Value.Value / 100f,
                _ => 0
            };
        }


        private static void ResetNativeStyle(Win32Element element)
        {
            element.Width = null;
            element.Height = null;
            element.BorderRadius = 0;
        }

        private void LoadImage(Win32Element element, string src)
        {
            try
            {
                string? path = null;
                if (Path.IsPathRooted(src) && File.Exists(src))
                {
                    path = src;
                }
                else
                {
                    // 尝试从当前目录加载
                    var currentDir = AppContext.BaseDirectory;
                    var p1 = Path.Combine(currentDir, src.TrimStart('/', '\\'));
                    if (File.Exists(p1)) path = p1;
                }

                if (path != null)
                {
                    if (element.NativeImage != null) element.NativeImage.Dispose();
                    element.NativeImage = Image.FromFile(path);
                }
            }
            catch { /* 忽略加载错误 */ }
        }

        private void ParseStyle(Win32Element element, string style)
        {
            var parts = style.Split(';');
            foreach (var part in parts)
            {
                var kv = part.Split(':');
                if (kv.Length != 2) continue;
                var key = kv[0].Trim().ToLower();
                var value = kv[1].Trim().ToLower();

                if (key == "width")
                {
                    if (value.EndsWith("px") && float.TryParse(value[..^2], out float v1))
                        element.Width = Dimension.Pixels(v1);
                    else if (value.EndsWith("%") && float.TryParse(value[..^1], out float v2))
                        element.Width = Dimension.Percent(v2);
                }
                else if (key == "height")
                {
                    if (value.EndsWith("px") && float.TryParse(value[..^2], out float v3))
                        element.Height = Dimension.Pixels(v3);
                    else if (value.EndsWith("%") && float.TryParse(value[..^1], out float v4))
                        element.Height = Dimension.Percent(v4);
                }
                else if (key == "border-radius")
                {
                    if (value.EndsWith("px") && float.TryParse(value[..^2], out float v5))
                        element.BorderRadius = v5;
                }
            }
        }

        internal Win32Element? GetElementByEditHwnd(nint hwnd)
        {
            _editElements.TryGetValue(hwnd, out var element);
            return element;
        }
    }
}

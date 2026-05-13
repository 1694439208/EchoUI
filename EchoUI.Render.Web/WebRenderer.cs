using EchoUI.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices.JavaScript;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace EchoUI.Render.Web
{
    /// <summary>
    /// Implements the IRenderer interface for the web, targeting the browser's DOM.
    /// Uses JSImport/JSExport for high-performance interop with a companion JavaScript file.
    /// </summary>
    public partial class WebRenderer : IRenderer
    {
        private readonly string _rootContainerId;
        private static readonly Dictionary<(string, string), Delegate> EventHandlers = new();
        private readonly Dictionary<string, List<string>> _childrenByParent = new();
        private readonly Dictionary<string, string> _parentByChild = new();

        public WebRenderer(string rootContainerId)
        {
            _rootContainerId = rootContainerId;
        }

        public object CreateElement(string type)
        {
            var elementId = $"eui-{Guid.NewGuid()}";
            DomInterop.CreateElement(elementId, ToTag(type));
            return elementId;
        }

        private string ToTag(string type)
        {
            return type switch
            {
                ElementCoreName.Container => "div",
                ElementCoreName.Text => "span",
                ElementCoreName.Input => "input",
                _ => type
            };
        }

        public void AddChild(object parent, object child, int index)
        {
            var parentId = (parent as string) ?? _rootContainerId;
            var childId = (string)child;

            RegisterChild(parentId, childId, index);
            DomInterop.AddChild(parentId, childId, index);
        }

        public void RemoveChild(object parent, object child)
        {
            var parentId = (parent as string) ?? _rootContainerId;
            var childId = (string)child;

            ReleaseSubtree(childId);
            DomInterop.RemoveChild(parentId, childId);
        }

        public void MoveChild(object parent, object child, int newIndex)
        {
            var parentId = (parent as string) ?? _rootContainerId;
            var childId = (string)child;

            MoveRegisteredChild(parentId, childId, newIndex);
            DomInterop.MoveChild(parentId, childId, newIndex);
        }

        public TextMeasurementResult MeasureText(TextMeasurementRequest request)
        {
            var text = request.Text ?? string.Empty;
            var width = (float)DomInterop.MeasureText(text, request.FontFamily, request.FontSize ?? 14f, request.FontWeight);
            var height = request.FontSize ?? 14f;
            return new TextMeasurementResult(width, height);
        }

        public void PatchProperties(object nativeElement, Props newProps, PropertyPatch patch)
        {
            var elementId = (string)nativeElement;
            var domPatch = new DomPropertyPatch();

            // [!重要!] 始终直接从 newProps 更新 C# 端的事件处理器引用，
            // 确保即使 Reconciler 没有报告变更，我们也能拿到最新的委托实例。
            UpdateEventHandlers(elementId, newProps);

            // 处理由 Reconciler 确认需要更新的属性
            if (patch.UpdatedProperties != null)
            {
                foreach (var (propName, propValue) in patch.UpdatedProperties)
                {
                    TranslatePropertyToDomPatch(newProps, domPatch, propName, propValue);
                }
            }

            // [!重要!] 为了保持不同平台的一致性，为不同类型的元素应用默认样式
            switch (newProps)
            {
                case ContainerProps containerProps:
                    domPatch.Styles ??= new();
                    domPatch.Attributes ??= new();
                    domPatch.Styles["display"] = "flex";
                    domPatch.Styles["box-sizing"] = "border-box";
                    domPatch.Styles["position"] = containerProps.Float ? "absolute" : "relative";
                    domPatch.Styles["flex-direction"] = ToCss(containerProps.Direction ?? LayoutDirection.Vertical);
                    domPatch.Styles["justify-content"] = ToCss(containerProps.JustifyContent ?? JustifyContent.Start);
                    domPatch.Styles["align-items"] = ToCss(containerProps.AlignItems ?? AlignItems.Start);
                    domPatch.Styles["overflow"] = containerProps.Float && !containerProps.Overflow.HasValue
                        ? "visible"
                        : ToCss(containerProps.Overflow ?? Overflow.Visible);
                    domPatch.Styles["gap"] = $"{containerProps.Gap ?? 0}px";
                    domPatch.Styles["background-color"] = ToCss(containerProps.BackgroundColor ?? Color.Transparent);
                    domPatch.Styles["border-style"] = (containerProps.BorderStyle ?? BorderStyle.None).ToString().ToLowerInvariant();
                    domPatch.Styles["border-width"] = $"{containerProps.BorderWidth ?? 0}px";
                    domPatch.Styles["border-color"] = ToCss(containerProps.BorderColor ?? Color.Transparent);
                    domPatch.Styles["border-radius"] = $"{containerProps.BorderRadius ?? 0}px";
                    SetSpacingStyles(domPatch, "margin", containerProps.Margin, "0px");
                    SetSpacingStyles(domPatch, "padding", containerProps.Padding, "0px");
                    if (containerProps.Float)
                    {
                        domPatch.Styles["left"] = "0px";
                        domPatch.Styles["top"] = "0px";
                        domPatch.Styles["z-index"] = "1000";
                        if (!containerProps.Width.HasValue)
                            domPatch.Styles["width"] = "100%";
                    }
                    domPatch.Attributes["data-eui-float"] = containerProps.Float ? "true" : "false";
                    domPatch.Attributes["data-eui-float-auto-width"] = containerProps.Float && !containerProps.Width.HasValue ? "true" : "false";
                    domPatch.Styles["flex-shrink"] = containerProps.FlexShrink.HasValue ? containerProps.FlexShrink.Value.ToString() : "0";
                    domPatch.Styles["flex-grow"] = containerProps.FlexGrow.HasValue ? containerProps.FlexGrow.Value.ToString() : "0";

                    var hasImeHandler = containerProps.OnTextComposition != null;
                    var hasKeyboardHandler = HasKeyboardHandler(containerProps);
                    var requiresDomFocus = !hasImeHandler && (hasKeyboardHandler || containerProps.OnFocus != null || containerProps.OnBlur != null);
                    domPatch.Attributes["data-eui-keyboard-handler"] = hasKeyboardHandler ? "true" : "false";
                    domPatch.Attributes["data-eui-ime-handler"] = hasImeHandler ? "true" : "false";
                    SetOptionalAttribute(domPatch, "data-eui-ime-x", containerProps.InputMethodAnchorPoint?.X.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    SetOptionalAttribute(domPatch, "data-eui-ime-y", containerProps.InputMethodAnchorPoint?.Y.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    SetOptionalAttribute(domPatch, "tabindex", requiresDomFocus ? "0" : null);
                    domPatch.Styles["outline"] = requiresDomFocus ? "none" : null;
                    break;
                case TextProps textProps:
                    domPatch.Styles ??= new();
                    domPatch.Styles["user-select"] = "none";
                    domPatch.Styles["white-space"] = textProps.NoWrap ? "pre" : "pre-wrap";
                    domPatch.Styles["pointer-events"] = textProps.MouseThrough ? "none" : "auto";
                    break;
                case InputProps inputProps:
                    domPatch.Styles ??= new();
                    domPatch.Styles["width"] = "100%";
                    domPatch.Styles["height"] = "100%";
                    domPatch.Styles["box-sizing"] = "border-box";
                    domPatch.Styles["outline"] = "none";
                    domPatch.Styles["appearance"] = "none";
                    domPatch.Styles["-webkit-appearance"] = "none";
                    domPatch.Styles["margin"] = "0px";
                    domPatch.Styles["background-color"] = ToCss(inputProps.BackgroundColor ?? Color.White);
                    domPatch.Styles["color"] = ToCss(inputProps.TextColor ?? Color.Black);
                    domPatch.Styles["border-radius"] = "0px";
                    SetSpacingStyles(domPatch, "padding", inputProps.Padding, "0px");

                    var hasManagedInputBorder = inputProps.BorderColor.HasValue || inputProps.FocusedBorderColor.HasValue;
                    domPatch.Styles["border-style"] = hasManagedInputBorder ? "solid" : "none";
                    domPatch.Styles["border-width"] = hasManagedInputBorder ? "1px" : "0px";
                    domPatch.Styles["border-color"] = ToCss(inputProps.BorderColor ?? Color.Transparent);
                    SetOptionalAttribute(domPatch, "data-eui-input-border-color", ToCss(inputProps.BorderColor));
                    SetOptionalAttribute(domPatch, "data-eui-input-focused-border-color", ToCss(inputProps.FocusedBorderColor));
                    break;
            }

            // 如果 domPatch 中有内容，序列化并发送给 JS 进行 DOM 操作
            if (domPatch.HasContent())
            {
                var patchJson = JsonSerializer.Serialize(domPatch, WebRendererJsonContext.Default.DomPropertyPatch);
                DomInterop.PatchProperties(elementId, patchJson);
            }
        }

        /// <summary>
        /// 将 Reconciler 传入的单个属性变更转换为具体的、可序列化的 DOM/CSS 变更。
        /// </summary>
        private void TranslatePropertyToDomPatch(object props, DomPropertyPatch domPatch, string propName, object? propValue)
        {
            switch (props)
            {
                case ContainerProps containerProps:
                    switch (propName)
                    {
                        // --- Layout ---
                        case nameof(ContainerProps.Width): domPatch.SetStyle("width", ToCss(propValue as Dimension?)); break;
                        case nameof(ContainerProps.Height): domPatch.SetStyle("height", ToCss(propValue as Dimension?)); break;
                        case nameof(ContainerProps.MinWidth): domPatch.SetStyle("min-width", ToCss(propValue as Dimension?)); break;
                        case nameof(ContainerProps.MinHeight): domPatch.SetStyle("min-height", ToCss(propValue as Dimension?)); break;
                        case nameof(ContainerProps.MaxWidth): domPatch.SetStyle("max-width", ToCss(propValue as Dimension?)); break;
                        case nameof(ContainerProps.MaxHeight): domPatch.SetStyle("max-height", ToCss(propValue as Dimension?)); break;
                        case nameof(ContainerProps.Margin): SetSpacingStyles(domPatch, "margin", propValue as Spacing?); break;
                        case nameof(ContainerProps.Padding): SetSpacingStyles(domPatch, "padding", propValue as Spacing?); break;
                        case nameof(ContainerProps.Overflow): domPatch.SetStyle("overflow", ToCss(propValue as Overflow?)); break;
                        case nameof(ContainerProps.Float):
                            domPatch.SetAttribute("data-eui-float", propValue is true ? "true" : "false");
                            domPatch.SetAttribute("data-eui-float-auto-width", propValue is true && !containerProps.Width.HasValue ? "true" : "false");
                            if (propValue is true)
                            {
                                domPatch.SetStyle("position", "absolute");
                                domPatch.SetStyle("left", "0px");
                                domPatch.SetStyle("top", "0px");
                                domPatch.SetStyle("z-index", "1000");
                                if (!containerProps.Width.HasValue)
                                    domPatch.SetStyle("width", "100%");
                                if (!containerProps.Overflow.HasValue)
                                    domPatch.SetStyle("overflow", "visible");
                            }
                            else
                            {
                                domPatch.SetStyle("position", "relative");
                                domPatch.SetStyle("left", null);
                                domPatch.SetStyle("top", null);
                                domPatch.SetStyle("z-index", null);
                                if (!containerProps.Width.HasValue)
                                    domPatch.SetStyle("width", null);
                                domPatch.SetStyle("overflow", ToCss(containerProps.Overflow));
                            }
                            break;

                        // --- Flexbox ---
                        case nameof(ContainerProps.Direction): domPatch.SetStyle("flex-direction", ToCss(propValue is LayoutDirection direction ? direction : LayoutDirection.Vertical)); break;
                        case nameof(ContainerProps.JustifyContent): domPatch.SetStyle("justify-content", ToCss(propValue as JustifyContent?)); break;
                        case nameof(ContainerProps.AlignItems): domPatch.SetStyle("align-items", ToCss(propValue as AlignItems?)); break;
                        case nameof(ContainerProps.Gap): domPatch.SetStyle("gap", propValue != null ? $"{propValue}px" : null); break;
                        case nameof(ContainerProps.FlexGrow): domPatch.SetStyle("flex-grow", propValue != null ? propValue.ToString() : null); break;
                        case nameof(ContainerProps.FlexShrink): domPatch.SetStyle("flex-shrink", propValue != null ? propValue.ToString() : null); break;

                        // --- Appearance ---
                        case nameof(ContainerProps.BackgroundColor): domPatch.SetStyle("background-color", ToCss(propValue as Color?)); break;
                        case nameof(ContainerProps.BorderStyle): domPatch.SetStyle("border-style", (propValue as BorderStyle?)?.ToString().ToLower()); break;
                        case nameof(ContainerProps.BorderColor): domPatch.SetStyle("border-color", ToCss(propValue as Color?)); break;
                        case nameof(ContainerProps.BorderWidth): domPatch.SetStyle("border-width", propValue != null ? $"{propValue}px" : null); break;
                        case nameof(ContainerProps.BorderRadius): domPatch.SetStyle("border-radius", propValue != null ? $"{propValue}px" : null); break;

                        // --- Animation ---
                        case nameof(ContainerProps.Transitions):
                            domPatch.SetStyle("transition", ToCss(propValue as ValueDictionary<string, Transition>?));
                            break;

                        // --- Events ---
                        // Reconciler 保证只有在添加/移除时才会将事件属性放入 patch 中
                        case nameof(ContainerProps.OnClick): domPatch.UpdateEvent("click", propValue); break;
                        case nameof(ContainerProps.OnMouseMove): domPatch.UpdateEvent("mousemove", propValue); break;
                        case nameof(ContainerProps.OnMouseEnter): domPatch.UpdateEvent("mouseenter", propValue); break;
                        case nameof(ContainerProps.OnMouseLeave): domPatch.UpdateEvent("mouseleave", propValue); break;
                        case nameof(ContainerProps.OnMouseDown): domPatch.UpdateEvent("mousedown", propValue); break;
                        case nameof(ContainerProps.OnMouseUp): domPatch.UpdateEvent("mouseup", propValue); break;
                        case nameof(ContainerProps.OnKeyDown): domPatch.UpdateEvent("keydown", propValue); break;
                        case nameof(ContainerProps.OnKeyUp): domPatch.UpdateEvent("keyup", propValue); break;
                        case nameof(ContainerProps.OnTextInput): domPatch.UpdateEvent("keypress", propValue); break;
                        case nameof(ContainerProps.OnTextComposition): domPatch.UpdateEvent("textcomposition", propValue); break;
                        case nameof(ContainerProps.InputMethodAnchorPoint):
                            {
                                var point = propValue as Point?;
                                SetOptionalAttribute(domPatch, "data-eui-ime-x", point?.X.ToString(System.Globalization.CultureInfo.InvariantCulture));
                                SetOptionalAttribute(domPatch, "data-eui-ime-y", point?.Y.ToString(System.Globalization.CultureInfo.InvariantCulture));
                                break;
                            }
                        case nameof(ContainerProps.OnFocus): domPatch.UpdateEvent("focus", propValue); break;
                        case nameof(ContainerProps.OnBlur): domPatch.UpdateEvent("blur", propValue); break;
                        default:
                            break;
                    }
                    break;

                case TextProps:
                    switch (propName)
                    {
                        // --- Text ---
                        case nameof(TextProps.Text): domPatch.SetAttribute("textContent", propValue); break;
                        case nameof(TextProps.FontFamily): domPatch.SetStyle("font-family", propValue as string); break;
                        case nameof(TextProps.FontSize): domPatch.SetStyle("font-size", propValue != null ? $"{propValue}px" : null); break;
                        case nameof(TextProps.FontWeight): domPatch.SetStyle("font-weight", propValue as string); break;
                        case nameof(TextProps.Color): domPatch.SetStyle("color", ToCss(propValue as Color?)); break;
                        case nameof(TextProps.MouseThrough): domPatch.SetStyle("pointer-events", (bool?)propValue == true ? "none" : "auto"); break;
                        case nameof(TextProps.NoWrap): domPatch.SetStyle("white-space", (bool?)propValue == true ? "pre" : "pre-wrap"); break;
                        default:
                            break;
                    }
                    break;

                case InputProps:
                    switch (propName)
                    {
                        // --- Input ---
                        case nameof(InputProps.Value): domPatch.SetAttribute("value", propValue); break;
                        case nameof(InputProps.OnValueChanged): domPatch.UpdateEvent("input", propValue); break;
                        case nameof(InputProps.BackgroundColor): domPatch.SetStyle("background-color", ToCss(propValue as Color?)); break;
                        case nameof(InputProps.TextColor): domPatch.SetStyle("color", ToCss(propValue as Color?)); break;
                        case nameof(InputProps.BorderColor):
                        case nameof(InputProps.FocusedBorderColor):
                            break;
                        case nameof(InputProps.Padding): SetSpacingStyles(domPatch, "padding", propValue as Spacing?); break;
                        default:
                            break;
                    }
                    break;

                case NativeProps nativeProps:
                    var hasNativeProperty = nativeProps.Properties != null && nativeProps.Properties.Value.Data.ContainsKey(propName);
                    if (!hasNativeProperty)
                    {
                        if (IsNativeEventName(propName))
                        {
                            domPatch.UpdateEvent(propName, null);
                        }
                        else
                        {
                            domPatch.RemoveAttribute(propName);
                        }
                        break;
                    }

                    var propValueType = propValue?.GetType();
                    if (propValueType != null && typeof(Delegate).IsAssignableFrom(propValueType))
                    {
                        domPatch.UpdateEvent(propName, propValue);
                    }
                    else if (propValue == null)
                    {
                        domPatch.RemoveAttribute(propName);
                    }
                    else
                    {
                        domPatch.SetAttribute(propName, propValue);
                    }
                    break;
            }
        }

        #region CSS/DOM Converters
        private string? ToCss(Dimension? dim) => dim.HasValue ? dim.Value.Unit switch { DimensionUnit.Pixels => $"{dim.Value.Value}px", DimensionUnit.Percent => $"{dim.Value.Value}%", DimensionUnit.ViewportHeight => $"{dim.Value.Value}vh", _ => "" } : null;
        private string? ToCss(Color? color) => color.HasValue ? $"rgba({color.Value.R},{color.Value.G},{color.Value.B},{(float)color.Value.A / 255})" : null;
        private string ToCss(LayoutDirection direction) => direction == LayoutDirection.Vertical ? "column" : "row";
        private string? ToCss(JustifyContent? jc) => jc switch
        {
            JustifyContent.Start => "flex-start",
            JustifyContent.End => "flex-end",
            JustifyContent.Center => "center",
            JustifyContent.SpaceAround => "space-around",
            JustifyContent.SpaceBetween => "space-between",
            _ => null
        };
        private string? ToCss(AlignItems? ai) => ai switch
        {
            AlignItems.Start => "flex-start",
            AlignItems.End => "flex-end",
            AlignItems.Center => "center",
            AlignItems.Stretch => "stretch",
            _ => null
        };
        private string? ToCss(Overflow? overflow) => overflow?.ToString().ToLower();
        private string? ToCss(ValueDictionary<string, Transition>? transitions)
        {
            var data = transitions?.Data;
            if (data == null || data.Count == 0) return "none";

            var sb = new StringBuilder();
            foreach (var (propName, transition) in data)
            {
                var cssProp = CSharpPropToCssProp(propName);
                if (cssProp == null)
                {
                    LogDebug($"[EchoUI.Web] Unsupported transition property '{propName}' was ignored.");
                    continue;
                }

                var cssEasing = ToCss(transition.Easing);
                if (sb.Length > 0) sb.Append(", ");
                sb.Append($"{cssProp} {transition.DurationMs}ms {cssEasing}");
            }
            return sb.Length > 0 ? sb.ToString() : "none";
        }

        private string? CSharpPropToCssProp(string propName) => propName switch
        {
            nameof(ContainerProps.Width) => "width",
            nameof(ContainerProps.Height) => "height",
            nameof(ContainerProps.MinWidth) => "min-width",
            nameof(ContainerProps.MinHeight) => "min-height",
            nameof(ContainerProps.MaxWidth) => "max-width",
            nameof(ContainerProps.MaxHeight) => "max-height",
            nameof(ContainerProps.Margin) => "margin",
            nameof(ContainerProps.Padding) => "padding",
            nameof(ContainerProps.BackgroundColor) => "background-color",
            nameof(ContainerProps.BorderColor) => "border-color",
            nameof(ContainerProps.BorderWidth) => "border-width",
            nameof(ContainerProps.BorderRadius) => "border-radius",
            nameof(ContainerProps.Gap) => "gap",
            _ => null
        };

        private string ToCss(Easing easing) => easing switch
        {
            Easing.Ease => "ease",
            Easing.EaseIn => "ease-in",
            Easing.EaseOut => "ease-out",
            Easing.EaseInOut => "ease-in-out",
            _ => "linear"
        };

        private void SetSpacingStyles(DomPropertyPatch patch, string key, Spacing? spacing, string? defaultCss = null)
        {
            patch.SetStyle($"{key}-top", ToCss(spacing?.Top) ?? defaultCss);
            patch.SetStyle($"{key}-right", ToCss(spacing?.Right) ?? defaultCss);
            patch.SetStyle($"{key}-bottom", ToCss(spacing?.Bottom) ?? defaultCss);
            patch.SetStyle($"{key}-left", ToCss(spacing?.Left) ?? defaultCss);
        }

        private static void SetOptionalAttribute(DomPropertyPatch patch, string key, string? value)
        {
            if (value != null)
            {
                patch.SetAttribute(key, value);
            }
            else
            {
                patch.RemoveAttribute(key);
            }
        }

        private static bool HasKeyboardHandler(ContainerProps props)
        {
            return props.OnKeyDown != null || props.OnKeyUp != null || props.OnTextInput != null || props.OnTextComposition != null;
        }

        private static bool IsNativeEventName(string propName) => propName switch
        {
            "click" or "mousemove" or "mouseenter" or "mouseleave" or "mousedown" or "mouseup" or "keydown" or "keyup" or "keypress" or "textcomposition" or "focus" or "blur" or "input" => true,
            _ => false
        };

        [Conditional("DEBUG")]
        private static void LogDebug(string message)
        {
            Debug.WriteLine(message);
        }
        #endregion

        /// <summary>
        /// 始终同步 C# 端的事件处理器字典，确保回调使用最新的委托实例。
        /// </summary>
        private void UpdateEventHandlers(string elementId, Props newProps)
        {
            if (newProps is ContainerProps p)
            {
                UpdateHandler(elementId, "click", p.OnClick);
                UpdateHandler(elementId, "mousemove", p.OnMouseMove);
                UpdateHandler(elementId, "mousedown", p.OnMouseDown);
                UpdateHandler(elementId, "mouseup", p.OnMouseUp);
                UpdateHandler(elementId, "mouseenter", p.OnMouseEnter);
                UpdateHandler(elementId, "mouseleave", p.OnMouseLeave);
                UpdateHandler(elementId, "keydown", p.OnKeyDown);
                UpdateHandler(elementId, "keyup", p.OnKeyUp);
                UpdateHandler(elementId, "keypress", p.OnTextInput);
                UpdateHandler(elementId, "textcomposition", p.OnTextComposition);
                UpdateHandler(elementId, "focus", p.OnFocus);
                UpdateHandler(elementId, "blur", p.OnBlur);
            }
            else if (newProps is InputProps ip)
            {
                UpdateHandler(elementId, "input", ip.OnValueChanged);
            }
            else if (newProps is NativeProps nativeProps)
            {
                CleanupEventHandlersForElement(elementId);

                if (nativeProps.Properties == null)
                    return;

                foreach (var item in nativeProps.Properties.Value.Data)
                {
                    var propValueType = item.Value?.GetType();
                    if (propValueType != null && typeof(Delegate).IsAssignableFrom(propValueType))
                    {
                        UpdateHandler(elementId, item.Key, item.Value as Delegate);
                    }
                }
            }
        }

        private void UpdateHandler(string elementId, string eventName, Delegate? handler)
        {
            var key = (elementId, eventName);
            if (handler != null)
            {
                EventHandlers[key] = handler;
            }
            else
            {
                EventHandlers.Remove(key);
            }
        }

        private void RegisterChild(string parentId, string childId, int index)
        {
            if (_parentByChild.TryGetValue(childId, out var existingParentId) &&
                _childrenByParent.TryGetValue(existingParentId, out var existingSiblings))
            {
                existingSiblings.Remove(childId);
            }

            _parentByChild[childId] = parentId;

            if (!_childrenByParent.TryGetValue(parentId, out var children))
            {
                children = new List<string>();
                _childrenByParent[parentId] = children;
            }

            children.Remove(childId);
            if (index >= 0 && index < children.Count)
            {
                children.Insert(index, childId);
            }
            else
            {
                children.Add(childId);
            }
        }

        private void MoveRegisteredChild(string parentId, string childId, int newIndex)
        {
            if (!_childrenByParent.TryGetValue(parentId, out var children))
                return;

            if (!children.Remove(childId))
                return;

            if (newIndex >= 0 && newIndex < children.Count)
            {
                children.Insert(newIndex, childId);
            }
            else
            {
                children.Add(childId);
            }
        }

        private void ReleaseSubtree(string elementId)
        {
            if (_childrenByParent.TryGetValue(elementId, out var children))
            {
                foreach (var childId in children.ToArray())
                {
                    ReleaseSubtree(childId);
                }

                _childrenByParent.Remove(elementId);
            }

            CleanupEventHandlersForElement(elementId);

            if (_parentByChild.TryGetValue(elementId, out var parentId))
            {
                if (_childrenByParent.TryGetValue(parentId, out var siblings))
                {
                    siblings.Remove(elementId);
                }

                _parentByChild.Remove(elementId);
            }
        }

        private static void CleanupEventHandlersForElement(string elementId)
        {
            var keysToRemove = new List<(string, string)>();
            foreach (var key in EventHandlers.Keys)
            {
                if (key.Item1 == elementId)
                {
                    keysToRemove.Add(key);
                }
            }

            foreach (var key in keysToRemove)
            {
                EventHandlers.Remove(key);
            }
        }

        private static MouseButton MapMouseButton(int button)
        {
            return button switch
            {
                2 => MouseButton.Right,
                1 => MouseButton.Middle,
                _ => MouseButton.Left
            };
        }

        public IUpdateScheduler GetScheduler(object rootContainer) => new WebUpdateScheduler();

        public static async Task RaiseEventAsync(string elementId, string eventName, string eventArgsJson)
        {
            await Task.Yield();

            if (!EventHandlers.TryGetValue((elementId, eventName), out var handler)) return;

            switch (handler)
            {
                case Action action: action.Invoke(); break;
                case Action<string> actionStr:
                    var value = JsonSerializer.Deserialize<string>(eventArgsJson, WebRendererJsonContext.Default.String);
                    actionStr.Invoke(value ?? string.Empty);
                    break;
                case Action<Point> actionPoint:
                    var point = JsonSerializer.Deserialize<Point>(eventArgsJson, WebRendererJsonContext.Default.Point);
                    actionPoint.Invoke(point);
                    break;
                case Action<MouseButton> actionMouse:
                    var button = JsonSerializer.Deserialize<int>(eventArgsJson, WebRendererJsonContext.Default.Int32);
                    actionMouse.Invoke(MapMouseButton(button));
                    break;
                case Action<int> actionInt:
                    var keyCode = JsonSerializer.Deserialize<int>(eventArgsJson, WebRendererJsonContext.Default.Int32);
                    actionInt.Invoke(keyCode);
                    break;
                case Action<TextCompositionEvent> actionComposition:
                    var compositionEvent = JsonSerializer.Deserialize(eventArgsJson, WebRendererJsonContext.Default.TextCompositionEvent);
                    if (compositionEvent != null)
                        actionComposition.Invoke(compositionEvent);
                    break;
            }
        }
    }

    /// <summary>
    /// A web-specific patch object that can be directly serialized to JSON for the JS interop layer.
    /// This is created by the WebRenderer from the generic PropertyPatch.
    /// </summary>
    internal class DomPropertyPatch
    {
        public Dictionary<string, string?>? Styles { get; set; }
        public Dictionary<string, object?>? Attributes { get; set; }
        public List<string>? AttributesToRemove { get; set; }
        public List<string>? EventsToAdd { get; set; }
        public List<string>? EventsToRemove { get; set; }
        public void SetStyle(string key, string? value) { Styles ??= new(); Styles[key] = value; }
        public void SetAttribute(string key, object? value) { Attributes ??= new(); Attributes[key] = value; }
        public void RemoveAttribute(string key) { AttributesToRemove ??= new(); if (!AttributesToRemove.Contains(key)) AttributesToRemove.Add(key); }
        public void UpdateEvent(string eventName, object? handler)
        {
            if (handler != null)
            {
                EventsToAdd ??= new();
                EventsToRemove?.Remove(eventName);
                if (!EventsToAdd.Contains(eventName)) EventsToAdd.Add(eventName);
            }
            else
            {
                EventsToRemove ??= new();
                EventsToAdd?.Remove(eventName);
                if (!EventsToRemove.Contains(eventName)) EventsToRemove.Add(eventName);
            }
        }
        public bool HasContent() => (Styles?.Count > 0) || (Attributes?.Count > 0) || (AttributesToRemove?.Count > 0) || (EventsToAdd?.Count > 0) || (EventsToRemove?.Count > 0);
    }


    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonSerializable(typeof(DomPropertyPatch))]
    [JsonSerializable(typeof(Point))]
    [JsonSerializable(typeof(int))]
    [JsonSerializable(typeof(string))]
    [JsonSerializable(typeof(TextCompositionEvent))]
    internal partial class WebRendererJsonContext : JsonSerializerContext
    {
    }

    internal static partial class DomInterop
    {
        [JSImport("dom.createElement", "dom")]
        internal static partial void CreateElement(string elementId, string type);

        [JSImport("dom.patchProperties", "dom")]
        internal static partial void PatchProperties(string elementId, string patchJson);

        [JSImport("dom.addChild", "dom")]
        internal static partial void AddChild(string parentId, string childId, int index);

        [JSImport("dom.removeChild", "dom")]
        internal static partial void RemoveChild(string parentId, string childId);

        [JSImport("dom.moveChild", "dom")]
        internal static partial void MoveChild(string parentId, string childId, int newIndex);

        [JSImport("dom.measureText", "dom")]
        internal static partial double MeasureText(string text, string? fontFamily, float fontSize, string? fontWeight);
    }

    public class WebUpdateScheduler : IUpdateScheduler
    {
        public void Schedule(Func<Task> updateAction)
        {
            _ = updateAction.Invoke();
        }
    }
}
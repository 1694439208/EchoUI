using static EchoUI.Core.Hooks;

namespace EchoUI.Core
{
    public record class TextInputProps : Props
    {
        public string Value { get; init; } = "";
        public string Placeholder { get; init; } = "";
        public Action<string>? OnValueChanged { get; init; }
        public Dimension? Width { get; init; }
        public Dimension? Height { get; init; }
        public Color? BackgroundColor { get; init; }
        public Color? TextColor { get; init; }
        public Color? PlaceholderColor { get; init; }
        public Color? BorderColor { get; init; }
        public Color? FocusedBorderColor { get; init; }
        public Color? CaretColor { get; init; }
        public Spacing? Padding { get; init; }
        public float? BorderRadius { get; init; }
        public string? FontFamily { get; init; }
        public float? FontSize { get; init; }
        public string? FontWeight { get; init; }
    }

    public partial class Elements
    {
        [Element(DefaultProperty = nameof(TextInputProps.Value))]
        public static Element TextInput(TextInputProps props)
        {
            var propValue = props.Value ?? string.Empty;
            var (isFocused, setIsFocused, _) = State(false);
            var (isCaretVisible, setIsCaretVisible, updateCaretVisible) = State(true);
            var (caretBlinkVersion, _, updateCaretBlinkVersion) = State(0);
            var (isComposing, setIsComposing, _) = State(false);
            var (compositionText, setCompositionText, _) = State(string.Empty);
            var (compositionStartIndex, setCompositionStartIndex, _) = State(propValue.Length);
            var (bufferValue, _, _) = State(propValue);
            var (lastPropValue, _, _) = State(propValue);
            var (caretIndex, setCaretIndex, _) = State(propValue.Length);
            var (lastPointerX, setLastPointerX, _) = State<float?>(null);

            if (lastPropValue.Value != propValue)
            {
                lastPropValue.Value = propValue;
                bufferValue.Value = propValue;
                caretIndex.Value = Math.Clamp(caretIndex.Value, 0, propValue.Length);
                isComposing.Value = false;
                compositionText.Value = string.Empty;
                compositionStartIndex.Value = Math.Clamp(caretIndex.Value, 0, propValue.Length);
            }

            void ResetCaretBlink()
            {
                setIsCaretVisible(true);
                updateCaretBlinkVersion(v => v + 1);
            }

            void SetCaret(int nextCaretIndex)
            {
                setCaretIndex(nextCaretIndex);
                ResetCaretBlink();
            }

            Effect(() =>
            {
                if (!isFocused.Value || isComposing.Value)
                    return null;

                var timer = new System.Threading.Timer(_ => updateCaretVisible(v => !v), null, 500, 500);
                return () => timer.Dispose();
            }, [isFocused.Value, isComposing.Value, caretBlinkVersion.Value]);

            var measureText = CreateTextMeasurer();
            var value = bufferValue.Value ?? string.Empty;
            var effectiveCaretIndex = Math.Clamp(caretIndex.Value, 0, value.Length);
            var effectiveCompositionStartIndex = Math.Clamp(compositionStartIndex.Value, 0, value.Length);
            var activeCompositionText = isComposing.Value ? compositionText.Value ?? string.Empty : string.Empty;
            var displayValue = isComposing.Value
                ? value.Insert(effectiveCompositionStartIndex, activeCompositionText)
                : value;
            var displayCaretIndex = isComposing.Value
                ? effectiveCompositionStartIndex + activeCompositionText.Length
                : effectiveCaretIndex;
            var visibleRange = GetVisibleTextRange(measureText, props, displayValue, displayCaretIndex, isFocused.Value);
            var imeAnchorPoint = GetInputMethodAnchorPoint(measureText, props, displayValue, visibleRange.Start, visibleRange.End, displayCaretIndex);
            var textColor = props.TextColor ?? Color.Black;
            var placeholderColor = props.PlaceholderColor ?? Color.Gray;
            var borderColor = isFocused.Value
                ? props.FocusedBorderColor ?? props.BorderColor ?? Color.FromHex("#2563eb")
                : props.BorderColor ?? Color.FromHex("#d1d5db");

            void UpdateValue(string nextValue, int nextCaretIndex)
            {
                bufferValue.Value = nextValue;
                props.OnValueChanged?.Invoke(nextValue);
                setCaretIndex(Math.Clamp(nextCaretIndex, 0, nextValue.Length));
                ResetCaretBlink();
            }

            void HandleTextInput(string text)
            {
                if (!isFocused.Value || isComposing.Value || string.IsNullOrEmpty(text))
                    return;

                for (var i = 0; i < text.Length; i++)
                {
                    if (char.IsControl(text[i]))
                        return;
                }

                var currentValue = bufferValue.Value ?? string.Empty;
                var currentCaretIndex = Math.Clamp(caretIndex.Value, 0, currentValue.Length);
                var nextValue = currentValue.Insert(currentCaretIndex, text);
                UpdateValue(nextValue, currentCaretIndex + text.Length);
            }

            void HandleTextComposition(TextCompositionEvent compositionEvent)
            {
                if (!isFocused.Value)
                    return;

                switch (compositionEvent.Phase)
                {
                    case TextCompositionPhase.Start:
                        setIsComposing(true);
                        setCompositionText(string.Empty);
                        setCompositionStartIndex(Math.Clamp(caretIndex.Value, 0, (bufferValue.Value ?? string.Empty).Length));
                        ResetCaretBlink();
                        break;
                    case TextCompositionPhase.Update:
                        if (!isComposing.Value)
                        {
                            setIsComposing(true);
                            setCompositionStartIndex(Math.Clamp(caretIndex.Value, 0, (bufferValue.Value ?? string.Empty).Length));
                        }
                        setCompositionText(compositionEvent.Text ?? string.Empty);
                        ResetCaretBlink();
                        break;
                    case TextCompositionPhase.Commit:
                        {
                            var currentValue = bufferValue.Value ?? string.Empty;
                            var committedText = compositionEvent.Text ?? string.Empty;
                            var insertIndex = isComposing.Value
                                ? Math.Clamp(compositionStartIndex.Value, 0, currentValue.Length)
                                : Math.Clamp(caretIndex.Value, 0, currentValue.Length);

                            setIsComposing(false);
                            setCompositionText(string.Empty);
                            setCompositionStartIndex(insertIndex);

                            if (!string.IsNullOrEmpty(committedText))
                            {
                                UpdateValue(currentValue.Insert(insertIndex, committedText), insertIndex + committedText.Length);
                            }
                            else
                            {
                                SetCaret(insertIndex);
                            }
                            break;
                        }
                    case TextCompositionPhase.End:
                        setIsComposing(false);
                        setCompositionText(string.Empty);
                        ResetCaretBlink();
                        break;
                }
            }

            void HandleKeyDown(int keyCode)
            {
                if (!isFocused.Value || isComposing.Value)
                    return;

                var currentValue = bufferValue.Value ?? string.Empty;
                var currentCaretIndex = Math.Clamp(caretIndex.Value, 0, currentValue.Length);

                switch (keyCode)
                {
                    case 8:
                        if (currentCaretIndex > 0)
                            UpdateValue(currentValue.Remove(currentCaretIndex - 1, 1), currentCaretIndex - 1);
                        break;
                    case 35:
                        SetCaret(currentValue.Length);
                        break;
                    case 36:
                        SetCaret(0);
                        break;
                    case 37:
                        SetCaret(Math.Max(0, currentCaretIndex - 1));
                        break;
                    case 39:
                        SetCaret(Math.Min(currentValue.Length, currentCaretIndex + 1));
                        break;
                    case 46:
                        if (currentCaretIndex < currentValue.Length)
                            UpdateValue(currentValue.Remove(currentCaretIndex, 1), currentCaretIndex);
                        break;
                }
            }

            return Container(new ContainerProps
            {
                Key = props.Key,
                Width = props.Width ?? Dimension.Pixels(200),
                Height = props.Height ?? Dimension.Pixels(36),
                Direction = LayoutDirection.Horizontal,
                JustifyContent = JustifyContent.Start,
                AlignItems = AlignItems.Center,
                Overflow = Overflow.Hidden,
                Padding = props.Padding ?? new Spacing(Dimension.Pixels(10), Dimension.Pixels(6)),
                BackgroundColor = props.BackgroundColor ?? Color.White,
                BorderWidth = 1,
                BorderStyle = BorderStyle.Solid,
                BorderColor = borderColor,
                BorderRadius = props.BorderRadius ?? 4,
                OnClick = _ =>
                {
                    setIsFocused(true);
                    ResetCaretBlink();

                    if (isComposing.Value)
                        return;

                    var currentValue = bufferValue.Value ?? string.Empty;
                    var nextCaretIndex = lastPointerX.Value.HasValue
                        ? ResolveCaretIndexFromPointer(measureText, props, currentValue, visibleRange.Start, visibleRange.End, lastPointerX.Value.Value)
                        : currentValue.Length;

                    SetCaret(nextCaretIndex);
                },
                OnMouseMove = point => setLastPointerX(point.X),
                OnFocus = () =>
                {
                    setIsFocused(true);
                    ResetCaretBlink();
                },
                OnBlur = () =>
                {
                    setIsFocused(false);
                    setIsCaretVisible(false);
                    setIsComposing(false);
                    setCompositionText(string.Empty);
                },
                OnKeyDown = HandleKeyDown,
                OnTextInput = HandleTextInput,
                OnTextComposition = HandleTextComposition,
                InputMethodAnchorPoint = imeAnchorPoint,
                Children =
                [
                    Container(new ContainerProps
                    {
                        Width = Dimension.Percent(100),
                        Direction = LayoutDirection.Horizontal,
                        AlignItems = AlignItems.Center,
                        Children = BuildTextInputChildren(
                            props,
                            displayValue,
                            visibleRange.Start,
                            visibleRange.End,
                            displayCaretIndex,
                            isFocused.Value,
                            isCaretVisible.Value || isComposing.Value,
                            textColor,
                            placeholderColor)
                    })
                ]
            });
        }

        private static List<Element> BuildTextInputChildren(TextInputProps props, string value, int visibleStart, int visibleEnd, int caretIndex, bool isFocused, bool showCaret, Color textColor, Color placeholderColor)
        {
            var children = new List<Element>();
            var clampedStart = Math.Clamp(visibleStart, 0, value.Length);
            var clampedEnd = Math.Clamp(visibleEnd, clampedStart, value.Length);
            var visibleValue = value[clampedStart..clampedEnd];
            var visibleCaretIndex = Math.Clamp(caretIndex - clampedStart, 0, visibleValue.Length);

            if (!isFocused)
            {
                if (visibleValue.Length == 0)
                {
                    if (!string.IsNullOrEmpty(props.Placeholder))
                        children.Add(CreateTextInputText(props.Placeholder, placeholderColor, props));

                    return children;
                }

                children.Add(CreateTextInputText(visibleValue, textColor, props));
                return children;
            }

            if (visibleValue.Length == 0)
            {
                if (showCaret)
                    children.Add(CreateTextInputCaret(props.CaretColor ?? textColor, props));

                if (!string.IsNullOrEmpty(props.Placeholder))
                    children.Add(CreateTextInputText(props.Placeholder, placeholderColor, props));

                return children;
            }

            if (!showCaret)
            {
                children.Add(CreateTextInputText(visibleValue, textColor, props));
                return children;
            }

            if (visibleCaretIndex > 0)
                children.Add(CreateTextInputText(visibleValue[..visibleCaretIndex], textColor, props));

            children.Add(CreateTextInputCaret(props.CaretColor ?? textColor, props));

            if (visibleCaretIndex < visibleValue.Length)
                children.Add(CreateTextInputText(visibleValue[visibleCaretIndex..], textColor, props));

            return children;
        }

        private static (int Start, int End) GetVisibleTextRange(Func<TextMeasurementRequest, TextMeasurementResult> measureText, TextInputProps props, string value, int caretIndex, bool isFocused)
        {
            if (string.IsNullOrEmpty(value))
                return (0, 0);

            var availableWidth = GetApproxAvailableTextWidth(props, isFocused);
            if (availableWidth <= 0)
                return (0, value.Length);

            var cache = new Dictionary<(int Start, int End), float>();

            float MeasureRangeWidth(int start, int end)
            {
                if (end <= start)
                    return 0;

                var key = (start, end);
                if (cache.TryGetValue(key, out var cachedWidth))
                    return cachedWidth;

                var width = measureText(new TextMeasurementRequest
                {
                    Text = value[start..end],
                    FontFamily = props.FontFamily,
                    FontSize = props.FontSize,
                    FontWeight = props.FontWeight
                }).Width;

                cache[key] = width;
                return width;
            }

            int FindStartForVisibleSuffix(int endExclusive, float maxWidth)
            {
                var low = 0;
                var high = endExclusive;

                while (low < high)
                {
                    var mid = (low + high) / 2;
                    if (MeasureRangeWidth(mid, endExclusive) <= maxWidth)
                        high = mid;
                    else
                        low = mid + 1;
                }

                return low;
            }

            int FindMaxVisibleEnd(int start, float maxWidth)
            {
                var low = start;
                var high = value.Length;

                while (low < high)
                {
                    var mid = (low + high + 1) / 2;
                    if (MeasureRangeWidth(start, mid) <= maxWidth)
                        low = mid;
                    else
                        high = mid - 1;
                }

                return low;
            }

            if (!isFocused)
            {
                var suffixStart = FindStartForVisibleSuffix(value.Length, availableWidth);
                return (suffixStart, value.Length);
            }

            var anchor = Math.Clamp(caretIndex, 0, value.Length);
            var start = FindStartForVisibleSuffix(anchor, availableWidth);
            var end = FindMaxVisibleEnd(start, availableWidth);

            if (start == end)
            {
                if (anchor < value.Length)
                    return (anchor, anchor + 1);

                if (anchor > 0)
                    return (anchor - 1, anchor);
            }

            return (start, Math.Max(anchor, end));
        }

        private static Point GetInputMethodAnchorPoint(Func<TextMeasurementRequest, TextMeasurementResult> measureText, TextInputProps props, string value, int visibleStart, int visibleEnd, int caretIndex)
        {
            var clampedStart = Math.Clamp(visibleStart, 0, value.Length);
            var clampedEnd = Math.Clamp(visibleEnd, clampedStart, value.Length);
            var clampedCaretIndex = Math.Clamp(caretIndex, clampedStart, clampedEnd);
            var prefix = value[clampedStart..clampedCaretIndex];
            var caretOffsetX = measureText(new TextMeasurementRequest
            {
                Text = prefix,
                FontFamily = props.FontFamily,
                FontSize = props.FontSize,
                FontWeight = props.FontWeight
            }).Width;

            return new Point(
                (int)Math.Round(GetTextLeftInset(props) + caretOffsetX),
                (int)Math.Round(GetInputMethodAnchorY(props)));
        }

        private static int ResolveCaretIndexFromPointer(Func<TextMeasurementRequest, TextMeasurementResult> measureText, TextInputProps props, string value, int visibleStart, int visibleEnd, float pointerX)
        {
            if (string.IsNullOrEmpty(value))
                return 0;

            var clampedStart = Math.Clamp(visibleStart, 0, value.Length);
            var clampedEnd = Math.Clamp(visibleEnd, clampedStart, value.Length);
            var visibleValue = value[clampedStart..clampedEnd];
            if (visibleValue.Length == 0)
                return clampedStart;

            var localX = Math.Max(0, pointerX - GetTextLeftInset(props));
            if (localX <= 0)
                return clampedStart;

            var fullVisibleWidth = measureText(new TextMeasurementRequest
            {
                Text = visibleValue,
                FontFamily = props.FontFamily,
                FontSize = props.FontSize,
                FontWeight = props.FontWeight
            }).Width;

            if (localX >= fullVisibleWidth)
                return clampedEnd;

            for (var i = 0; i < visibleValue.Length; i++)
            {
                var leftWidth = measureText(new TextMeasurementRequest
                {
                    Text = visibleValue[..i],
                    FontFamily = props.FontFamily,
                    FontSize = props.FontSize,
                    FontWeight = props.FontWeight
                }).Width;

                var charWidth = measureText(new TextMeasurementRequest
                {
                    Text = visibleValue[i].ToString(),
                    FontFamily = props.FontFamily,
                    FontSize = props.FontSize,
                    FontWeight = props.FontWeight
                }).Width;

                if (localX <= leftWidth + charWidth / 2f)
                    return clampedStart + i;
            }

            return clampedEnd;
        }

        private static float GetApproxAvailableTextWidth(TextInputProps props, bool isFocused)
        {
            var width = props.Width is { Unit: DimensionUnit.Pixels } explicitWidth
                ? explicitWidth.Value
                : 200f;

            var padding = props.Padding ?? new Spacing(Dimension.Pixels(10), Dimension.Pixels(6));
            var horizontalPadding = GetPixelValue(padding.Left) + GetPixelValue(padding.Right);
            var borderWidth = 2f;
            var caretWidth = isFocused ? GetCaretWidth() : 0f;
            return Math.Max(0, width - horizontalPadding - borderWidth - caretWidth);
        }

        private static float GetTextLeftInset(TextInputProps props)
        {
            var padding = props.Padding ?? new Spacing(Dimension.Pixels(10), Dimension.Pixels(6));
            return 1f + GetPixelValue(padding.Left);
        }

        private static float GetInputMethodAnchorY(TextInputProps props)
        {
            var height = props.Height is { Unit: DimensionUnit.Pixels } explicitHeight ? explicitHeight.Value : 36f;
            var padding = props.Padding ?? new Spacing(Dimension.Pixels(10), Dimension.Pixels(6));
            var fontHeight = Math.Max(14f, props.FontSize ?? 14f);
            var contentHeight = Math.Max(0, height - GetPixelValue(padding.Top) - GetPixelValue(padding.Bottom) - 2f);
            var textTop = 1f + GetPixelValue(padding.Top) + Math.Max(0, (contentHeight - fontHeight) / 2f);
            return textTop + fontHeight;
        }

        private static float GetPixelValue(Dimension dimension)
        {
            return dimension.Unit == DimensionUnit.Pixels ? dimension.Value : 0;
        }

        private static float GetCaretWidth()
        {
            return 1f;
        }

        private static Element CreateTextInputText(string text, Color color, TextInputProps props)
        {
            return Text(new TextProps
            {
                Text = text,
                Color = color,
                FontFamily = props.FontFamily,
                FontSize = props.FontSize,
                FontWeight = props.FontWeight,
                NoWrap = true
            });
        }

        private static Element CreateTextInputCaret(Color color, TextInputProps props)
        {
            return Container(new ContainerProps
            {
                Width = Dimension.Pixels(GetCaretWidth()),
                Height = Dimension.Pixels(Math.Max(14, props.FontSize ?? 14)),
                BackgroundColor = color,
                FlexShrink = 0
            });
        }
    }
}

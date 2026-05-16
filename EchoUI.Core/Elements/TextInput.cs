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

    internal record class TextInputContextMenuItemProps : Props
    {
        public string Text { get; init; } = string.Empty;
        public bool Enabled { get; init; } = true;
        public Action? OnActivate { get; init; }
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
            var (selectionAnchor, setSelectionAnchor, _) = State(propValue.Length);
            var (selectionFocus, setSelectionFocus, _) = State(propValue.Length);
            var (isSelecting, setIsSelecting, _) = State(false);
            var (isContextMenuVisible, setIsContextMenuVisible, _) = State(false);

            if (lastPropValue.Value != propValue)
            {
                lastPropValue.Value = propValue;
                bufferValue.Value = propValue;
                caretIndex.Value = Math.Clamp(caretIndex.Value, 0, propValue.Length);
                selectionAnchor.Value = Math.Clamp(selectionAnchor.Value, 0, propValue.Length);
                selectionFocus.Value = Math.Clamp(selectionFocus.Value, 0, propValue.Length);
                isComposing.Value = false;
                compositionText.Value = string.Empty;
                compositionStartIndex.Value = Math.Clamp(caretIndex.Value, 0, propValue.Length);
                isSelecting.Value = false;
            }

            void ResetCaretBlink()
            {
                setIsCaretVisible(true);
                updateCaretBlinkVersion(v => v + 1);
            }

            void SetSelectionCollapsed(int index)
            {
                var clamped = Math.Clamp(index, 0, (bufferValue.Value ?? string.Empty).Length);
                setSelectionAnchor(clamped);
                setSelectionFocus(clamped);
                setCaretIndex(clamped);
                ResetCaretBlink();
            }

            void SetSelectionRangeValue(int anchor, int focus)
            {
                var currentLength = (bufferValue.Value ?? string.Empty).Length;
                var clampedAnchor = Math.Clamp(anchor, 0, currentLength);
                var clampedFocus = Math.Clamp(focus, 0, currentLength);
                setSelectionAnchor(clampedAnchor);
                setSelectionFocus(clampedFocus);
                setCaretIndex(clampedFocus);
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
            var readClipboardTextAsync = CreateClipboardReaderAsync();
            var writeClipboardTextAsync = CreateClipboardWriterAsync();
            var value = bufferValue.Value ?? string.Empty;
            var effectiveSelectionAnchor = Math.Clamp(selectionAnchor.Value, 0, value.Length);
            var effectiveSelectionFocus = Math.Clamp(selectionFocus.Value, 0, value.Length);
            var (selectionStart, selectionEnd) = GetSelectionRange(effectiveSelectionAnchor, effectiveSelectionFocus);
            var hasSelection = selectionStart != selectionEnd;
            var effectiveCaretIndex = Math.Clamp(caretIndex.Value, 0, value.Length);
            var effectiveCompositionStartIndex = Math.Clamp(compositionStartIndex.Value, 0, value.Length);
            var activeCompositionText = isComposing.Value ? compositionText.Value ?? string.Empty : string.Empty;
            var displayValue = isComposing.Value
                ? value.Insert(effectiveCompositionStartIndex, activeCompositionText)
                : value;
            var displayCaretIndex = isComposing.Value
                ? effectiveCompositionStartIndex + activeCompositionText.Length
                : effectiveCaretIndex;
            var visibleRange = GetVisibleTextRange(measureText, props, displayValue, displayCaretIndex, isFocused.Value, !hasSelection || isComposing.Value);
            var imeAnchorPoint = GetInputMethodAnchorPoint(measureText, props, displayValue, visibleRange.Start, visibleRange.End, displayCaretIndex);
            var textColor = props.TextColor ?? Color.Black;
            var placeholderColor = props.PlaceholderColor ?? Color.Gray;
            var selectionBackgroundColor = Color.FromHex("#2563eb");
            var selectionTextColor = Color.White;
            var borderColor = isFocused.Value
                ? props.FocusedBorderColor ?? props.BorderColor ?? Color.FromHex("#2563eb")
                : props.BorderColor ?? Color.FromHex("#d1d5db");
            var inputBodyHeight = GetInputBodyHeight(props);

            void UpdateValue(string nextValue, int nextCaretIndex)
            {
                bufferValue.Value = nextValue;
                props.OnValueChanged?.Invoke(nextValue);
                var clampedCaret = Math.Clamp(nextCaretIndex, 0, nextValue.Length);
                setCaretIndex(clampedCaret);
                setSelectionAnchor(clampedCaret);
                setSelectionFocus(clampedCaret);
                ResetCaretBlink();
            }

            int DeleteSelectedRange(string currentValue)
            {
                var (start, end) = GetSelectionRange(selectionAnchor.Value, selectionFocus.Value);
                start = Math.Clamp(start, 0, currentValue.Length);
                end = Math.Clamp(end, start, currentValue.Length);
                if (start == end)
                    return start;

                UpdateValue(currentValue.Remove(start, end - start), start);
                return start;
            }

            string GetSelectedText(string currentValue)
            {
                var (start, end) = GetSelectionRange(selectionAnchor.Value, selectionFocus.Value);
                start = Math.Clamp(start, 0, currentValue.Length);
                end = Math.Clamp(end, start, currentValue.Length);
                return start == end ? string.Empty : currentValue[start..end];
            }

            void ReplaceSelectionOrInsertText(string text)
            {
                var normalizedText = NormalizeSingleLineText(text);
                if (string.IsNullOrEmpty(normalizedText))
                    return;

                var currentValue = bufferValue.Value ?? string.Empty;
                if (HasSelection(selectionAnchor.Value, selectionFocus.Value))
                {
                    var (start, end) = GetSelectionRange(selectionAnchor.Value, selectionFocus.Value);
                    start = Math.Clamp(start, 0, currentValue.Length);
                    end = Math.Clamp(end, start, currentValue.Length);
                    var nextValue = currentValue.Remove(start, end - start).Insert(start, normalizedText);
                    UpdateValue(nextValue, start + normalizedText.Length);
                    return;
                }

                var currentCaretIndex = Math.Clamp(caretIndex.Value, 0, currentValue.Length);
                UpdateValue(currentValue.Insert(currentCaretIndex, normalizedText), currentCaretIndex + normalizedText.Length);
            }

            void HandleTextInput(string text)
            {
                if (!isFocused.Value || isComposing.Value)
                    return;

                setIsContextMenuVisible(false);
                ReplaceSelectionOrInsertText(text);
            }

            void HandleTextComposition(TextCompositionEvent compositionEvent)
            {
                if (!isFocused.Value)
                    return;

                switch (compositionEvent.Phase)
                {
                    case TextCompositionPhase.Start:
                        if (HasSelection(selectionAnchor.Value, selectionFocus.Value))
                        {
                            var currentValue = bufferValue.Value ?? string.Empty;
                            var collapsedIndex = DeleteSelectedRange(currentValue);
                            setCompositionStartIndex(collapsedIndex);
                        }
                        else
                        {
                            setCompositionStartIndex(Math.Clamp(caretIndex.Value, 0, (bufferValue.Value ?? string.Empty).Length));
                        }
                        setIsComposing(true);
                        setCompositionText(string.Empty);
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
                            var committedText = NormalizeSingleLineText(compositionEvent.Text ?? string.Empty);
                            var currentValue = bufferValue.Value ?? string.Empty;
                            var insertIndex = Math.Clamp(compositionStartIndex.Value, 0, currentValue.Length);

                            setIsComposing(false);
                            setCompositionText(string.Empty);
                            setCompositionStartIndex(insertIndex);

                            if (!string.IsNullOrEmpty(committedText))
                            {
                                UpdateValue(currentValue.Insert(insertIndex, committedText), insertIndex + committedText.Length);
                            }
                            else
                            {
                                SetSelectionCollapsed(insertIndex);
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

                setIsContextMenuVisible(false);

                var currentValue = bufferValue.Value ?? string.Empty;
                var currentCaretIndex = Math.Clamp(caretIndex.Value, 0, currentValue.Length);
                var (currentSelectionStart, currentSelectionEnd) = GetSelectionRange(selectionAnchor.Value, selectionFocus.Value);
                var selectionExists = currentSelectionStart != currentSelectionEnd;

                switch (keyCode)
                {
                    case 8:
                        if (selectionExists)
                        {
                            DeleteSelectedRange(currentValue);
                        }
                        else if (currentCaretIndex > 0)
                        {
                            UpdateValue(currentValue.Remove(currentCaretIndex - 1, 1), currentCaretIndex - 1);
                        }
                        break;
                    case 35:
                        SetSelectionCollapsed(currentValue.Length);
                        break;
                    case 36:
                        SetSelectionCollapsed(0);
                        break;
                    case 37:
                        SetSelectionCollapsed(selectionExists ? currentSelectionStart : Math.Max(0, currentCaretIndex - 1));
                        break;
                    case 39:
                        SetSelectionCollapsed(selectionExists ? currentSelectionEnd : Math.Min(currentValue.Length, currentCaretIndex + 1));
                        break;
                    case 46:
                        if (selectionExists)
                        {
                            DeleteSelectedRange(currentValue);
                        }
                        else if (currentCaretIndex < currentValue.Length)
                        {
                            UpdateValue(currentValue.Remove(currentCaretIndex, 1), currentCaretIndex);
                        }
                        break;
                }
            }

            bool IsPointInInputBody(Point point)
            {
                return point.Y >= 0 && point.Y <= Math.Round(inputBodyHeight);
            }

            int ResolvePointerCaretIndex(Point point)
            {
                return ResolveCaretIndexFromPointer(measureText, props, displayValue, visibleRange.Start, visibleRange.End, point.X);
            }

            bool IsPointerInsideCurrentSelection(int pointerIndex)
            {
                var (start, end) = GetSelectionRange(selectionAnchor.Value, selectionFocus.Value);
                return start != end && pointerIndex >= start && pointerIndex <= end;
            }

            void HandlePointerDown(MouseEvent mouseEvent)
            {
                if (!IsPointInInputBody(mouseEvent.Position))
                    return;

                setIsFocused(true);
                setIsContextMenuVisible(false);
                ResetCaretBlink();

                if (isComposing.Value)
                    return;

                var pointerIndex = ResolvePointerCaretIndex(mouseEvent.Position);

                switch (mouseEvent.Button)
                {
                    case MouseButton.Left:
                        setIsSelecting(true);
                        SetSelectionRangeValue(pointerIndex, pointerIndex);
                        break;
                    case MouseButton.Right:
                        setIsSelecting(false);
                        if (!IsPointerInsideCurrentSelection(pointerIndex))
                        {
                            SetSelectionCollapsed(pointerIndex);
                        }
                        break;
                }
            }

            void HandlePointerMove(MouseEvent mouseEvent)
            {
                if (!isSelecting.Value || isComposing.Value)
                    return;

                var pointerIndex = ResolvePointerCaretIndex(mouseEvent.Position);
                setSelectionFocus(pointerIndex);
                setCaretIndex(pointerIndex);
                setIsCaretVisible(true);
            }

            void HandlePointerUp(MouseEvent mouseEvent)
            {
                if (!IsPointInInputBody(mouseEvent.Position))
                {
                    setIsSelecting(false);
                    return;
                }

                var pointerIndex = ResolvePointerCaretIndex(mouseEvent.Position);

                switch (mouseEvent.Button)
                {
                    case MouseButton.Left:
                        if (isSelecting.Value && !isComposing.Value)
                        {
                            setSelectionFocus(pointerIndex);
                            setCaretIndex(pointerIndex);
                            ResetCaretBlink();
                        }
                        setIsSelecting(false);
                        break;
                    case MouseButton.Right:
                        setIsSelecting(false);
                        if (!IsPointerInsideCurrentSelection(pointerIndex))
                        {
                            SetSelectionCollapsed(pointerIndex);
                        }
                        setIsContextMenuVisible(true);
                        break;
                }
            }

            async void CopySelectionAsync()
            {
                var selectedText = GetSelectedText(bufferValue.Value ?? string.Empty);
                if (!string.IsNullOrEmpty(selectedText))
                    await writeClipboardTextAsync(selectedText);
                setIsContextMenuVisible(false);
            }

            async void CutSelectionAsync()
            {
                if (isComposing.Value)
                {
                    setIsContextMenuVisible(false);
                    return;
                }

                var currentValue = bufferValue.Value ?? string.Empty;
                var selectedText = GetSelectedText(currentValue);
                if (!string.IsNullOrEmpty(selectedText))
                {
                    await writeClipboardTextAsync(selectedText);
                    DeleteSelectedRange(currentValue);
                }
                setIsContextMenuVisible(false);
            }

            async void PasteClipboardAsync()
            {
                if (isComposing.Value)
                {
                    setIsContextMenuVisible(false);
                    return;
                }

                var clipboardText = await readClipboardTextAsync();
                ReplaceSelectionOrInsertText(clipboardText);
                setIsContextMenuVisible(false);
            }

            void SelectAllText()
            {
                var currentLength = (bufferValue.Value ?? string.Empty).Length;
                SetSelectionRangeValue(0, currentLength);
                setIsContextMenuVisible(false);
            }

            return Container(new ContainerProps
            {
                Key = props.Key,
                Width = props.Width ?? Dimension.Pixels(200),
                Direction = LayoutDirection.Vertical,
                Overflow = Overflow.Visible,
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
                    setIsSelecting(false);
                    setIsContextMenuVisible(false);
                },
                OnKeyDown = HandleKeyDown,
                OnTextInput = HandleTextInput,
                OnTextComposition = HandleTextComposition,
                OnPointerDown = HandlePointerDown,
                OnPointerMove = HandlePointerMove,
                OnPointerUp = HandlePointerUp,
                InputMethodAnchorPoint = imeAnchorPoint,
                SuppressContextMenu = true,
                Children =
                [
                    Container(new ContainerProps
                    {
                        Width = Dimension.Percent(100),
                        Height = props.Height ?? Dimension.Pixels(36),
                        Direction = LayoutDirection.Horizontal,
                        JustifyContent = JustifyContent.Start,
                        AlignItems = AlignItems.Center,
                        Overflow = Overflow.Hidden,
                        Padding = props.Padding ?? new Spacing(Dimension.Pixels(10), Dimension.Pixels(6)),
                        BackgroundColor = props.BackgroundColor ?? DesignTokens.BgContent,
                        BorderWidth = 1,
                        BorderStyle = BorderStyle.Solid,
                        BorderColor = borderColor,
                        BorderRadius = props.BorderRadius ?? 4,
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
                                    selectionStart,
                                    selectionEnd,
                                    isFocused.Value,
                                    isCaretVisible.Value || isComposing.Value,
                                    textColor,
                                    placeholderColor,
                                    selectionBackgroundColor,
                                    selectionTextColor)
                            })
                        ]
                    }),
                    isContextMenuVisible.Value
                        ? CreateTextInputContextMenu(hasSelection, isComposing.Value, CopySelectionAsync, CutSelectionAsync, PasteClipboardAsync, SelectAllText)
                        : Empty()
                ]
            });
        }

        private static List<Element> BuildTextInputChildren(
            TextInputProps props,
            string value,
            int visibleStart,
            int visibleEnd,
            int caretIndex,
            int selectionStart,
            int selectionEnd,
            bool isFocused,
            bool showCaret,
            Color textColor,
            Color placeholderColor,
            Color selectionBackgroundColor,
            Color selectionTextColor)
        {
            var children = new List<Element>();
            var clampedStart = Math.Clamp(visibleStart, 0, value.Length);
            var clampedEnd = Math.Clamp(visibleEnd, clampedStart, value.Length);
            var visibleValue = value[clampedStart..clampedEnd];
            var visibleCaretIndex = Math.Clamp(caretIndex - clampedStart, 0, visibleValue.Length);
            var hasSelection = selectionStart != selectionEnd;
            var relativeSelectionStart = Math.Clamp(selectionStart - clampedStart, 0, visibleValue.Length);
            var relativeSelectionEnd = Math.Clamp(selectionEnd - clampedStart, 0, visibleValue.Length);
            var hasVisibleSelection = hasSelection && relativeSelectionEnd > relativeSelectionStart;

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
                if (showCaret && !hasSelection)
                    children.Add(CreateTextInputCaret(props.CaretColor ?? textColor, props));

                if (!string.IsNullOrEmpty(props.Placeholder))
                    children.Add(CreateTextInputText(props.Placeholder, placeholderColor, props));

                return children;
            }

            if (hasSelection)
            {
                if (!hasVisibleSelection)
                {
                    children.Add(CreateTextInputText(visibleValue, textColor, props));
                    return children;
                }

                if (relativeSelectionStart > 0)
                    children.Add(CreateTextInputText(visibleValue[..relativeSelectionStart], textColor, props));

                children.Add(CreateSelectedTextFragment(
                    visibleValue[relativeSelectionStart..relativeSelectionEnd],
                    selectionTextColor,
                    selectionBackgroundColor,
                    props));

                if (relativeSelectionEnd < visibleValue.Length)
                    children.Add(CreateTextInputText(visibleValue[relativeSelectionEnd..], textColor, props));

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

        private static (int Start, int End) GetVisibleTextRange(Func<TextMeasurementRequest, TextMeasurementResult> measureText, TextInputProps props, string value, int caretIndex, bool isFocused, bool reserveCaretWidth)
        {
            if (string.IsNullOrEmpty(value))
                return (0, 0);

            var availableWidth = GetApproxAvailableTextWidth(props, reserveCaretWidth && isFocused);
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

        private static float GetApproxAvailableTextWidth(TextInputProps props, bool reserveCaretWidth)
        {
            var width = props.Width is { Unit: DimensionUnit.Pixels } explicitWidth
                ? explicitWidth.Value
                : 200f;

            var padding = props.Padding ?? new Spacing(Dimension.Pixels(10), Dimension.Pixels(6));
            var horizontalPadding = GetPixelValue(padding.Left) + GetPixelValue(padding.Right);
            var borderWidth = 2f;
            var caretWidth = reserveCaretWidth ? GetCaretWidth() : 0f;
            return Math.Max(0, width - horizontalPadding - borderWidth - caretWidth);
        }

        private static float GetTextLeftInset(TextInputProps props)
        {
            var padding = props.Padding ?? new Spacing(Dimension.Pixels(10), Dimension.Pixels(6));
            return 1f + GetPixelValue(padding.Left);
        }

        private static float GetInputMethodAnchorY(TextInputProps props)
        {
            var height = GetInputBodyHeight(props);
            var padding = props.Padding ?? new Spacing(Dimension.Pixels(10), Dimension.Pixels(6));
            var fontHeight = Math.Max(14f, props.FontSize ?? 14f);
            var contentHeight = Math.Max(0, height - GetPixelValue(padding.Top) - GetPixelValue(padding.Bottom) - 2f);
            var textTop = 1f + GetPixelValue(padding.Top) + Math.Max(0, (contentHeight - fontHeight) / 2f);
            return textTop + fontHeight;
        }

        private static float GetInputBodyHeight(TextInputProps props)
        {
            return props.Height is { Unit: DimensionUnit.Pixels } explicitHeight ? explicitHeight.Value : 36f;
        }

        private static float GetPixelValue(Dimension dimension)
        {
            return dimension.Unit == DimensionUnit.Pixels ? dimension.Value : 0;
        }

        private static float GetCaretWidth()
        {
            return 1f;
        }

        private static (int Start, int End) GetSelectionRange(int anchor, int focus)
        {
            return anchor <= focus ? (anchor, focus) : (focus, anchor);
        }

        private static bool HasSelection(int anchor, int focus)
        {
            return anchor != focus;
        }

        private static string NormalizeSingleLineText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var builder = new System.Text.StringBuilder(text.Length);
            foreach (var c in text)
            {
                if (!char.IsControl(c))
                    builder.Append(c);
            }

            return builder.ToString();
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

        private static Element CreateSelectedTextFragment(string text, Color textColor, Color backgroundColor, TextInputProps props)
        {
            return Container(new ContainerProps
            {
                Direction = LayoutDirection.Horizontal,
                AlignItems = AlignItems.Center,
                BackgroundColor = backgroundColor,
                FlexShrink = 0,
                Children =
                [
                    CreateTextInputText(text, textColor, props)
                ]
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

        private static Element CreateTextInputContextMenu(bool hasSelection, bool isComposing, Action copyAction, Action cutAction, Action pasteAction, Action selectAllAction)
        {
            const float itemHeight = 32f;
            const float menuPadding = 4f;
            var menuHeight = itemHeight * 4 + menuPadding * 2 + 2f;

            return Container(new ContainerProps
            {
                Float = true,
                Width = Dimension.Pixels(128),
                Height = Dimension.Pixels(menuHeight),
                Margin = new Spacing(Dimension.ZeroPixels, Dimension.Pixels(4), Dimension.ZeroPixels, Dimension.ZeroPixels),
                Children =
                [
                    Container(new ContainerProps
                    {
                        Width = Dimension.Percent(100),
                        Height = Dimension.Percent(100),
                        Direction = LayoutDirection.Vertical,
                        Padding = new Spacing(Dimension.Pixels(menuPadding)),
                        BackgroundColor = Color.White,
                        BorderWidth = 1,
                        BorderStyle = BorderStyle.Solid,
                        BorderColor = Color.FromHex("#d1d5db"),
                        BorderRadius = 6,
                        Children =
                        [
                            CreateTextInputContextMenuItem("复制", hasSelection, copyAction),
                            CreateTextInputContextMenuItem("剪切", hasSelection && !isComposing, cutAction),
                            CreateTextInputContextMenuItem("粘贴", !isComposing, pasteAction),
                            CreateTextInputContextMenuItem("全选", true, selectAllAction)
                        ]
                    })
                ]
            });
        }

        private static Element CreateTextInputContextMenuItem(string text, bool enabled, Action onClick)
        {
            return new Element((Component)TextInputContextMenuItemComponent, new TextInputContextMenuItemProps
            {
                Text = text,
                Enabled = enabled,
                OnActivate = onClick
            });
        }

        private static Element? TextInputContextMenuItemComponent(Props props)
        {
            var itemProps = (TextInputContextMenuItemProps)props;
            var (isHovered, setIsHovered, _) = State(false);

            var backgroundColor = !itemProps.Enabled
                ? DesignTokens.BgDisabled
                : isHovered.Value
                    ? DesignTokens.PrimaryBg
                    : DesignTokens.BgContent;

            var textColor = itemProps.Enabled
                ? (isHovered.Value ? Color.FromHex("#1d4ed8") : Color.Black)
                : Color.Gray;

            return Container(new ContainerProps
            {
                Width = Dimension.Percent(100),
                Height = Dimension.Pixels(32),
                JustifyContent = JustifyContent.Center,
                Padding = new Spacing(Dimension.Pixels(10), Dimension.Pixels(6)),
                BackgroundColor = backgroundColor,
                BorderRadius = 4,
                OnMouseEnter = itemProps.Enabled ? () => setIsHovered(true) : null,
                OnMouseLeave = itemProps.Enabled ? () => setIsHovered(false) : null,
                OnClick = itemProps.Enabled ? _ => itemProps.OnActivate?.Invoke() : null,
                Children =
                [
                    Text(new TextProps
                    {
                        Text = itemProps.Text,
                        Color = textColor,
                        NoWrap = true,
                        MouseThrough = true
                    })
                ]
            });
        }
    }
}

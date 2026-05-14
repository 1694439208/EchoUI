using EchoUI.Core;

namespace EchoUI.Render.Win32
{
    /// <summary>
    /// 鼠标命中测试与事件分发。
    /// 从元素树中找到鼠标坐标命中的最深层元素，并分发事件。
    /// </summary>
    internal class HitTestManager
    {
        private Win32Element? _hoveredElement;
        private Win32Element? _pressedClickTarget;
        private Win32Element? _focusedElement;
        private MouseButton? _pressedButton;
        private int _suppressedCommittedCharCount;
        internal Win32Element? FocusedElement => _focusedElement;
        private readonly Win32Renderer _renderer;


        public HitTestManager(Win32Renderer renderer)
        {
            _renderer = renderer;
        }

        /// <summary>
        /// 从根元素递归查找命中的最深层元素
        /// </summary>
        public Win32Element? HitTest(Win32Element root, float x, float y)
        {
            var floats = _renderer.FloatingElements;
            if (floats != null)
            {
                for (int i = floats.Count - 1; i >= 0; i--)
                {
                    var hit = HitTestRecursive(floats[i], x, y);
                    if (hit != null)
                        return hit;
                }
            }

            var localHit = HitTestFromHoveredChain(x, y);
            if (localHit != null)
                return localHit;

            return HitTestRecursive(root, x, y);
        }

        private Win32Element? HitTestRecursive(Win32Element element, float x, float y)
        {
            var bounds = element.GetAbsoluteBounds();

            // Overflow.Visible / Float 元素的子节点可能超出自身边界
            bool isInBounds = x >= bounds.X && x <= bounds.Right && y >= bounds.Y && y <= bounds.Bottom;
            bool canHitOutsideBounds = element.Overflow == Overflow.Visible || element.Float;

            // 如果元素有 Overflow 裁剪，超出部分不可点击
            if (!isInBounds && !canHitOutsideBounds)
                return null;

            // 先检查 Float 子元素（它们在最上层，可能超出父容器边界）
            for (int i = element.Children.Count - 1; i >= 0; i--)
            {
                var child = element.Children[i];
                if (!child.Float) continue;

                var hit = HitTestRecursive(child, x, y);
                if (hit != null) return hit;
            }

            // 从后往前遍历非 Float 子元素
            for (int i = element.Children.Count - 1; i >= 0; i--)
            {
                var child = element.Children[i];
                if (child.Float) continue;

                if (child.ElementType == ElementCoreName.Text && child.MouseThrough)
                    continue;

                var hit = HitTestRecursive(child, x, y);
                if (hit != null) return hit;
            }

            if (!isInBounds)
                return null;

            if (HasEventHandler(element))
                return element;

            if (element.ElementType == ElementCoreName.Input)
                return element;

            if (element.ElementType == ElementCoreName.Container)
                return element;

            if (element.ElementType == ElementCoreName.Text && !element.MouseThrough)
                return element;

            return null;
        }

        private static bool HasEventHandler(Win32Element element)
        {
            return element.OnClick != null ||
                   element.OnMouseMove != null ||
                   element.OnMouseEnter != null ||
                   element.OnMouseLeave != null ||
                   element.OnMouseDown != null ||
                   element.OnMouseUp != null;
        }

        /// <summary>
        /// 处理鼠标移动事件
        /// </summary>
        public void HandleMouseMove(Win32Element root, float x, float y)
        {
            var hit = HitTest(root, x, y);

            if (hit != _hoveredElement)
            {
                var oldHovered = _hoveredElement;
                var commonAncestor = FindCommonAncestor(oldHovered, hit);

                if (oldHovered != null)
                {
                    FireMouseLeaveChain(oldHovered, commonAncestor);
                }

                if (hit != null)
                {
                    FireMouseEnterChain(hit, commonAncestor);
                }

                _hoveredElement = hit;
                _renderer.RequestRepaint(oldHovered, hit);
            }

            var moveTarget = FindMoveHandler(hit);
            if (moveTarget != null)
            {
                var localPoint = ToLocalPoint(moveTarget, x, y);
                moveTarget.OnMouseMove?.Invoke(localPoint);
                moveTarget.OnPointerMove?.Invoke(new MouseEvent(localPoint, _pressedButton ?? MouseButton.Left));
            }
        }

        /// <summary>
        /// 处理鼠标按下事件
        /// </summary>
        public void HandleMouseDown(Win32Element root, float x, float y, MouseButton button)
        {
            var hit = HitTest(root, x, y);
            _pressedButton = button;
            _pressedClickTarget = FindClickHandler(hit);

            SetFocusedElement(FindFocusableElement(hit));

            if (hit != null)
            {
                var downTarget = FindDownHandler(hit);
                if (downTarget != null)
                {
                    var localPoint = ToLocalPoint(downTarget, x, y);
                    downTarget.OnMouseDown?.Invoke();
                    downTarget.OnPointerDown?.Invoke(new MouseEvent(localPoint, button));
                }
                _renderer.RequestRepaint(hit);
            }
        }

        /// <summary>
        /// 处理鼠标释放事件
        /// </summary>
        public void HandleMouseUp(Win32Element root, float x, float y, MouseButton button)
        {
            var hit = HitTest(root, x, y);

            if (hit != null)
            {
                var upTarget = FindUpHandler(hit);
                if (upTarget != null)
                {
                    var localPoint = ToLocalPoint(upTarget, x, y);
                    upTarget.OnMouseUp?.Invoke();
                    upTarget.OnPointerUp?.Invoke(new MouseEvent(localPoint, button));
                }

                var releaseClickTarget = FindClickHandler(hit);
                if (releaseClickTarget != null && ReferenceEquals(releaseClickTarget, _pressedClickTarget))
                {
                    releaseClickTarget.OnClick?.Invoke(button);
                }

                _renderer.RequestRepaint(hit, _pressedClickTarget);
            }

            _pressedButton = null;
            _pressedClickTarget = null;
        }

        /// <summary>
        /// 处理鼠标滚轮事件
        /// </summary>
        public void HandleMouseWheel(Win32Element root, float x, float y, int delta, float vpW, float vpH)
        {
            var hit = HitTest(root, x, y);
            var scrollTarget = FindScrollTarget(hit);

            if (scrollTarget != null)
            {
                float contentWidth = FlexLayout.MeasureContentWidth(scrollTarget, vpW, vpH);
                float contentHeight = FlexLayout.MeasureContentHeight(scrollTarget, vpW, vpH);
                float maxScrollX = Math.Max(0, contentWidth - scrollTarget.LayoutWidth);
                float maxScrollY = Math.Max(0, contentHeight - scrollTarget.LayoutHeight);
                bool scrollHorizontal = (NativeInterop.GetKeyState(NativeInterop.VK_SHIFT) & 0x8000) != 0 || maxScrollY <= 0;
                float previousScrollX = scrollTarget.ScrollOffsetX;
                float previousScrollY = scrollTarget.ScrollOffsetY;

                if (scrollHorizontal && maxScrollX > 0)
                {
                    scrollTarget.ScrollOffsetX -= delta * 0.3f;
                    scrollTarget.ScrollOffsetX = Math.Clamp(scrollTarget.ScrollOffsetX, 0, maxScrollX);
                }
                else if (maxScrollY > 0)
                {
                    scrollTarget.ScrollOffsetY -= delta * 0.3f;
                    scrollTarget.ScrollOffsetY = Math.Clamp(scrollTarget.ScrollOffsetY, 0, maxScrollY);
                }

                if (!previousScrollX.Equals(scrollTarget.ScrollOffsetX) || !previousScrollY.Equals(scrollTarget.ScrollOffsetY))
                {
                    _renderer.RequestScrollReposition(scrollTarget);
                }
            }
        }

        /// <summary>
        /// 处理键盘按下事件
        /// </summary>
        public void HandleKeyDown(int keyCode)
        {
            _focusedElement?.OnKeyDown?.Invoke(keyCode);
        }

        /// <summary>
        /// 处理键盘释放事件
        /// </summary>
        public void HandleKeyUp(int keyCode)
        {
            _focusedElement?.OnKeyUp?.Invoke(keyCode);
        }

        /// <summary>
        /// 处理字符输入
        /// </summary>
        public void HandleTextInput(uint charCode)
        {
            if (_suppressedCommittedCharCount > 0 && charCode >= 32)
            {
                _suppressedCommittedCharCount--;
                return;
            }

            _focusedElement?.OnTextInput?.Invoke(new string((char)charCode, 1));
        }

        public void HandleTextComposition(TextCompositionEvent compositionEvent)
        {
            if (compositionEvent.Phase == TextCompositionPhase.Commit && !string.IsNullOrEmpty(compositionEvent.Text))
            {
                _suppressedCommittedCharCount += compositionEvent.Text.Length;
            }

            _focusedElement?.OnTextComposition?.Invoke(compositionEvent);
        }

        /// <summary>
        /// 处理鼠标离开窗口
        /// </summary>
        public void HandleMouseLeave()
        {
            if (_hoveredElement != null)
            {
                var oldHovered = _hoveredElement;
                FireMouseLeaveChain(oldHovered, null);
                _hoveredElement = null;
                _renderer.RequestRepaint(oldHovered);
            }
        }

        // --- 辅助方法 ---

        private void FireMouseLeaveChain(Win32Element from, Win32Element? stopAt)
        {
            var current = from;
            while (current != null && current != stopAt)
            {
                current.OnMouseLeave?.Invoke();
                current.IsHovered = false;
                current = current.Parent;
            }
        }

        private void FireMouseEnterChain(Win32Element to, Win32Element? stopAt)
        {
            var chain = new List<Win32Element>();
            var current = to;
            while (current != null && current != stopAt)
            {
                chain.Add(current);
                current = current.Parent;
            }

            for (int i = chain.Count - 1; i >= 0; i--)
            {
                chain[i].OnMouseEnter?.Invoke();
                chain[i].IsHovered = true;
            }
        }

        private Win32Element? HitTestFromHoveredChain(float x, float y)
        {
            var current = _hoveredElement;
            while (current != null)
            {
                if (!current.Float)
                {
                    var hit = HitTestRecursive(current, x, y);
                    if (hit != null)
                        return hit;
                }

                current = current.Parent;
            }

            return null;
        }

        public void DetachSubtree(Win32Element subtreeRoot)
        {
            if (_hoveredElement != null && IsInSubtree(subtreeRoot, _hoveredElement))
            {
                var oldHovered = _hoveredElement;
                FireMouseLeaveChain(oldHovered, null);
                _hoveredElement = null;
                _renderer.RequestRepaint(oldHovered);
            }

            if (_pressedClickTarget != null && IsInSubtree(subtreeRoot, _pressedClickTarget))
            {
                _pressedClickTarget = null;
                _pressedButton = null;
            }

            if (_focusedElement != null && IsInSubtree(subtreeRoot, _focusedElement))
            {
                SetFocusedElement(null);
            }
        }

        private static Win32Element? FindCommonAncestor(Win32Element? first, Win32Element? second)
        {
            var firstDepth = GetDepth(first);
            var secondDepth = GetDepth(second);

            while (firstDepth > secondDepth && first != null)
            {
                first = first.Parent;
                firstDepth--;
            }

            while (secondDepth > firstDepth && second != null)
            {
                second = second.Parent;
                secondDepth--;
            }

            while (first != second)
            {
                first = first?.Parent;
                second = second?.Parent;
            }

            return first;
        }

        private static int GetDepth(Win32Element? element)
        {
            int depth = 0;
            var current = element;
            while (current != null)
            {
                depth++;
                current = current.Parent;
            }

            return depth;
        }

        private static bool IsInSubtree(Win32Element subtreeRoot, Win32Element? element)
        {
            var current = element;
            while (current != null)
            {
                if (ReferenceEquals(current, subtreeRoot))
                    return true;
                current = current.Parent;
            }

            return false;
        }

        private static Win32Element? FindMoveHandler(Win32Element? element)
        {
            var current = element;
            while (current != null)
            {
                if (current.OnMouseMove != null || current.OnPointerMove != null)
                    return current;
                current = current.Parent;
            }

            return null;
        }

        private static Win32Element? FindClickHandler(Win32Element? element)
        {
            var current = element;
            while (current != null)
            {
                if (current.OnClick != null)
                    return current;
                current = current.Parent;
            }

            return null;
        }

        private static Win32Element? FindDownHandler(Win32Element? element)
        {
            var current = element;
            while (current != null)
            {
                if (current.OnMouseDown != null || current.OnPointerDown != null)
                    return current;
                current = current.Parent;
            }

            return null;
        }

        private static Win32Element? FindUpHandler(Win32Element? element)
        {
            var current = element;
            while (current != null)
            {
                if (current.OnMouseUp != null || current.OnPointerUp != null)
                    return current;
                current = current.Parent;
            }

            return null;
        }

        private static Win32Element? FindFocusableElement(Win32Element? element)
        {
            var current = element;
            while (current != null)
            {
                if (current.ElementType == ElementCoreName.Input ||
                    current.OnKeyDown != null ||
                    current.OnKeyUp != null ||
                    current.OnTextInput != null ||
                    current.OnTextComposition != null ||
                    current.OnFocus != null ||
                    current.OnBlur != null)
                {
                    return current;
                }

                current = current.Parent;
            }

            return null;
        }

        private void SetFocusedElement(Win32Element? element)
        {
            if (ReferenceEquals(_focusedElement, element))
            {
                if (element?.ElementType == ElementCoreName.Input && element.EditHwnd != 0 && NativeInterop.IsWindow(element.EditHwnd))
                {
                    NativeInterop.SetFocus(element.EditHwnd);
                }
                else if (element != null)
                {
                    _renderer.FocusWindow();
                }
                return;
            }

            var oldFocused = _focusedElement;
            if (oldFocused != null)
            {
                oldFocused.IsFocused = false;
                oldFocused.OnBlur?.Invoke();
            }

            _focusedElement = element;

            if (_focusedElement != null)
            {
                _focusedElement.IsFocused = true;

                if (_focusedElement.ElementType == ElementCoreName.Input && _focusedElement.EditHwnd != 0 && NativeInterop.IsWindow(_focusedElement.EditHwnd))
                {
                    NativeInterop.SetFocus(_focusedElement.EditHwnd);
                }
                else
                {
                    _renderer.FocusWindow();
                }

                _focusedElement.OnFocus?.Invoke();
            }

            _renderer.RequestRepaint(oldFocused, _focusedElement);
        }

        private static Core.Point ToLocalPoint(Win32Element element, float x, float y)
        {
            return new Core.Point(
                (int)Math.Round(x - element.AbsoluteX, MidpointRounding.AwayFromZero),
                (int)Math.Round(y - element.AbsoluteY, MidpointRounding.AwayFromZero));
        }

        private static Win32Element? FindScrollTarget(Win32Element? element)
        {
            var current = element;
            while (current != null)
            {
                if (current.Overflow == Overflow.Auto || current.Overflow == Overflow.Scroll)
                    return current;
                current = current.Parent;
            }
            return null;
        }

    }
}

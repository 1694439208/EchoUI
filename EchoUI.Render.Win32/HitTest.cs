using System.Drawing;
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
        private Win32Element? _pressedElement;
        private Win32Element? _focusedElement;
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
            // 1. 优先检查全局 Float 层（倒序，最新的在最上层）
            var floats = _renderer.FloatingElements;
            if (floats != null)
            {
                for (int i = floats.Count - 1; i >= 0; i--)
                {
                    // 注意：这里的 HitTestRecursive 内部也会检查它自己的 Float 子元素
                    // 但由于我们将所有 Float 元素都收集到了 FloatingElements，
                    // 其实这里只需检查 floats[i] 自身及其非 Float 子元素即可。
                    // 不过为了简单，复用 HitTestRecursive 也没问题，
                    // 因为嵌套的 Float 元素虽然也在 FloatingElements 列表里（如果被递归收集的话），
                    // 但我们的收集逻辑是 "遇到 Float 则停止递归 collecting children"，
                    // 所以 FloatingElements 只包含 "Root Floats"。
                    // 它可以包含嵌套的 Float 吗？看收集逻辑。
                    // 收集逻辑：if (child.Float) { add; } else { recurse; }
                    // 所以 FloatingElements 包含的是最外层的 Float 元素。
                    // 它们的内部 Float 元素没有被收集到顶层列表，而是保留在子树中。
                    // 所以 HitTestRecursive(floats[i]) 会正确处理内部结构。
                    
                    var hit = HitTestRecursive(floats[i], x, y);
                    if (hit != null) return hit;
                }
            }

            // 2. 检查常规树
            // 注意：如果鼠标在 Float 元素上，上面的循环应该已经返回了。
            // 但如果在常规树遍历中再次遇到了该 Float 元素（因为它是某个节点的 Child），
            // HitTestRecursive 会再次检查它。
            // 这虽然有重复，但如果是 "Return null check" (Line 49 in HitTestRecursive)，
            // 或者是正常的命中测试，结果应该一致。
            // 唯一的问题是：如果 Float 元素遮挡了后面的常规元素，
            // 这里的 step 1 已经捕获了。
            // 如果 Float 元素被后面的常规元素遮挡（通常不应该，Float 应在顶层），
            // 这里的 step 1 也会优先捕获，实现了 "TopMost" 效果。
            
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

            // 处理 Enter/Leave
            if (hit != _hoveredElement)
            {
                // 向上遍历旧元素链，触发 Leave
                if (_hoveredElement != null)
                {
                    FireMouseLeaveChain(_hoveredElement, hit);
                }

                // 向上遍历新元素链，触发 Enter
                if (hit != null)
                {
                    FireMouseEnterChain(hit, _hoveredElement);
                }

                _hoveredElement = hit;
                _renderer.RequestRepaint();
            }

            // 向上冒泡查找有 OnMouseMove 的元素
            var moveTarget = FindHandler(hit, e => e.OnMouseMove != null);
            if (moveTarget != null)
            {
                moveTarget.OnMouseMove?.Invoke(new Core.Point((int)x, (int)y));
            }
        }

        /// <summary>
        /// 处理鼠标按下事件
        /// </summary>
        public void HandleMouseDown(Win32Element root, float x, float y, MouseButton button)
        {
            var hit = HitTest(root, x, y);
            _pressedElement = hit;

            if (hit != null)
            {
                _focusedElement = hit;
                if (hit.ElementType == ElementCoreName.Input && hit.EditHwnd != 0 && NativeInterop.IsWindow(hit.EditHwnd))
                    NativeInterop.SetFocus(hit.EditHwnd);

                var downTarget = FindHandler(hit, e => e.OnMouseDown != null);
                downTarget?.OnMouseDown?.Invoke();
                _renderer.RequestRepaint();
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
                var upTarget = FindHandler(hit, e => e.OnMouseUp != null);
                upTarget?.OnMouseUp?.Invoke();

                // 如果按下和释放在同一个元素上（或其祖先），触发 Click
                if (hit == _pressedElement || IsAncestorOf(_pressedElement, hit) || IsAncestorOf(hit, _pressedElement))
                {
                    var clickTarget = FindHandler(hit, e => e.OnClick != null);
                    clickTarget?.OnClick?.Invoke(button);
                }

                _renderer.RequestRepaint();
            }

            _pressedElement = null;
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

                _renderer.RequestRelayout();
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
        /// 处理鼠标离开窗口
        /// </summary>
        public void HandleMouseLeave()
        {
            if (_hoveredElement != null)
            {
                FireMouseLeaveChain(_hoveredElement, null);
                _hoveredElement = null;
                _renderer.RequestRepaint();
            }
        }

        // --- 辅助方法 ---

        private void FireMouseLeaveChain(Win32Element from, Win32Element? to)
        {
            var current = from;
            while (current != null)
            {
                if (current == to) break;
                if (IsAncestorOf(current, to)) break;
                current.OnMouseLeave?.Invoke();
                current.IsHovered = false;
                current = current.Parent;
            }
        }

        private void FireMouseEnterChain(Win32Element to, Win32Element? from)
        {
            // 收集需要触发 Enter 的元素
            var chain = new List<Win32Element>();
            var current = to;
            while (current != null)
            {
                if (current == from) break;
                if (IsAncestorOf(current, from)) break;
                chain.Add(current);
                current = current.Parent;
            }

            // 从外到内触发 Enter
            for (int i = chain.Count - 1; i >= 0; i--)
            {
                chain[i].OnMouseEnter?.Invoke();
                chain[i].IsHovered = true;
            }
        }

        /// <summary>
        /// 向上冒泡查找满足条件的元素
        /// </summary>
        private static Win32Element? FindHandler(Win32Element? element, Func<Win32Element, bool> predicate)
        {
            var current = element;
            while (current != null)
            {
                if (predicate(current)) return current;
                current = current.Parent;
            }
            return null;
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

        private static bool IsAncestorOf(Win32Element? ancestor, Win32Element? descendant)
        {
            if (ancestor == null || descendant == null) return false;
            var current = descendant.Parent;
            while (current != null)
            {
                if (current == ancestor) return true;
                current = current.Parent;
            }
            return false;
        }
    }
}

using EchoUI.Core;

namespace EchoUI.Render.Win32
{
    /// <summary>
    /// 简化版 Flexbox 布局引擎。
    /// 支持 Direction, JustifyContent, AlignItems, FlexGrow, FlexShrink, Gap, Padding, Margin,
    /// Width/Height (Pixels, Percent, ViewportHeight), Min/Max 约束, Float, Overflow。
    /// 没有指定尺寸的容器会被内容撑开（auto sizing）。
    /// </summary>
    internal static class FlexLayout
    {
        /// <summary>
        /// 从根元素开始计算整棵树的布局
        /// </summary>
        public static void ComputeLayout(Win32Element root, float viewportWidth, float viewportHeight, int measureCacheGeneration)
        {
            root.LayoutX = 0;
            root.LayoutY = 0;
            root.LayoutWidth = viewportWidth;
            root.LayoutHeight = viewportHeight;
            root.AbsoluteX = 0;
            root.AbsoluteY = 0;
            root.UpdateAbsoluteBounds();

            LayoutChildren(root, viewportWidth, viewportHeight, measureCacheGeneration);

            if (ClampScrollOffsetsRecursive(root, viewportWidth, viewportHeight))
            {
                LayoutChildren(root, viewportWidth, viewportHeight, measureCacheGeneration);
            }
        }

        private static bool ClampScrollOffsetsRecursive(Win32Element element, float vpW, float vpH)
        {
            var changed = false;

            foreach (var child in element.Children)
            {
                changed |= ClampScrollOffsetsRecursive(child, vpW, vpH);
            }

            if (element.Overflow != Overflow.Auto && element.Overflow != Overflow.Scroll &&
                element.ScrollOffsetX == 0 && element.ScrollOffsetY == 0)
            {
                return changed;
            }

            float contentWidth = MeasureContentWidth(element, vpW, vpH);
            float contentHeight = MeasureContentHeight(element, vpW, vpH);
            float maxScrollX = Math.Max(0, contentWidth - element.LayoutWidth);
            float maxScrollY = Math.Max(0, contentHeight - element.LayoutHeight);

            float clampedX = maxScrollX <= 0 ? 0 : Math.Clamp(element.ScrollOffsetX, 0, maxScrollX);
            float clampedY = maxScrollY <= 0 ? 0 : Math.Clamp(element.ScrollOffsetY, 0, maxScrollY);

            if (!clampedX.Equals(element.ScrollOffsetX))
            {
                element.ScrollOffsetX = clampedX;
                changed = true;
            }

            if (!clampedY.Equals(element.ScrollOffsetY))
            {
                element.ScrollOffsetY = clampedY;
                changed = true;
            }

            return changed;
        }

        private static void LayoutChildren(Win32Element container, float vpW, float vpH, int measureCacheGeneration)
        {
            var padding = ResolveSpacing(container.Padding, container.LayoutWidth, vpW, vpH);
            float border = GetBorderInset(container);
            container.CachedContentWidth = padding.Left + padding.Right + border * 2;
            container.CachedContentHeight = padding.Top + padding.Bottom + border * 2;

            if (container.Children.Count == 0) return;

            float contentX = border + padding.Left;
            float contentY = border + padding.Top;
            float contentWidth = Math.Max(0, container.LayoutWidth - border * 2 - padding.Left - padding.Right);
            float contentHeight = Math.Max(0, container.LayoutHeight - border * 2 - padding.Top - padding.Bottom);

            bool isRow = container.Direction == LayoutDirection.Horizontal;
            float mainSize = isRow ? contentWidth : contentHeight;
            float crossSize = isRow ? contentHeight : contentWidth;
            float gap = container.Gap;
            float contentRight = border + padding.Left;
            float contentBottom = border + padding.Top;

            // --- 第一遍：计算每个子元素的基础尺寸 ---
            var items = new List<FlexItem>(container.Children.Count);
            foreach (var child in container.Children)
            {
                if (child.Float)
                {
                    items.Add(new FlexItem { Element = child, IsFloat = true });
                    continue;
                }

                var margin = ResolveSpacing(child.Margin, isRow ? contentWidth : contentHeight, vpW, vpH);

                // 主轴尺寸
                float? explicitMain = isRow
                    ? ResolveSize(child.Width, contentWidth, vpW, vpH)
                    : ResolveSize(child.Height, contentHeight, vpW, vpH);

                float mainBase;
                if (explicitMain.HasValue)
                {
                    mainBase = explicitMain.Value;
                }
                else
                {
                    // FlexGrow > 0 时，标准 Flexbox 行为应该是基于 intrinsic size (flex-basis: auto) 开始增长。
                    // 之前的实现强制为 0 (flex-basis: 0)，这虽然模仿了 flex: 1 的简写行为，但破坏了 flex-basis: auto。
                    // 如果用户想要 flex-basis: 0，应该明确设置 Width/Height 为 0。
                    
                    // 没有显式尺寸 → 测量内容固有尺寸
                    mainBase = isRow
                        ? MeasureIntrinsicWidth(child, contentHeight, vpW, vpH, measureCacheGeneration)
                        : MeasureIntrinsicHeight(child, contentWidth, vpW, vpH, measureCacheGeneration);
                }

                // 交叉轴尺寸
                float? explicitCross = isRow
                    ? ResolveSize(child.Height, contentHeight, vpW, vpH)
                    : ResolveSize(child.Width, contentWidth, vpW, vpH);

                float crossBase;
                if (explicitCross.HasValue)
                {
                    crossBase = explicitCross.Value;
                }
                else
                {
                    // 只有当 AlignItems 为 Stretch 时，且子元素没有显式尺寸，才拉伸。
                    // 如果 AlignItems 是 Start/Center/End，则使用固有尺寸。
                    bool isStretch = container.AlignItems == AlignItems.Stretch;
                    
                    if (isStretch)
                    {
                        float marginCross = isRow
                            ? margin.Top + margin.Bottom
                            : margin.Left + margin.Right;
                        crossBase = Math.Max(0, crossSize - marginCross);
                    }
                    else
                    {
                        // 非 Stretch，测量固有尺寸
                        crossBase = isRow
                            ? MeasureIntrinsicHeight(child, contentWidth, vpW, vpH, measureCacheGeneration)
                            : MeasureIntrinsicWidth(child, contentHeight, vpW, vpH, measureCacheGeneration);
                    }
                }

                if (crossBase < 0) crossBase = 0;
                if (mainBase < 0) mainBase = 0;

                items.Add(new FlexItem
                {
                    Element = child,
                    MainBase = mainBase,
                    CrossBase = crossBase,
                    Margin = margin,
                    Grow = child.FlexGrow,
                    Shrink = child.FlexShrink
                });
            }

            // --- 第二遍：分配 FlexGrow / FlexShrink ---
            int normalCount = 0;
            foreach (var item in items)
            {
                if (!item.IsFloat)
                    normalCount++;
            }

            float totalGaps = normalCount > 1 ? gap * (normalCount - 1) : 0;
            float usedMain = totalGaps;
            float totalGrow = 0;
            float totalShrink = 0;
            foreach (var item in items)
            {
                if (item.IsFloat) continue;

                float marginMain = isRow
                    ? item.Margin.Left + item.Margin.Right
                    : item.Margin.Top + item.Margin.Bottom;
                usedMain += item.MainBase + marginMain;
                totalGrow += item.Grow;
                totalShrink += item.Shrink * item.MainBase;
            }

            float freeSpace = mainSize - usedMain;

            if (freeSpace > 0 && totalGrow > 0)
            {
                foreach (var item in items)
                {
                    if (!item.IsFloat && item.Grow > 0)
                        item.MainBase += freeSpace * (item.Grow / totalGrow);
                }
            }
            else if (freeSpace < 0 && totalShrink > 0)
            {
                foreach (var item in items)
                {
                    if (!item.IsFloat && item.Shrink > 0)
                    {
                        float shrinkAmount = (-freeSpace) * (item.Shrink * item.MainBase / totalShrink);
                        item.MainBase = Math.Max(0, item.MainBase - shrinkAmount);
                    }
                }
            }

            // --- 应用 Min/Max 约束 ---
            foreach (var item in items)
            {
                if (item.IsFloat) continue;

                var child = item.Element;
                float? minMain, maxMain, minCross, maxCross;
                if (isRow)
                {
                    minMain = ResolveSize(child.MinWidth, contentWidth, vpW, vpH);
                    maxMain = ResolveSize(child.MaxWidth, contentWidth, vpW, vpH);
                    minCross = ResolveSize(child.MinHeight, contentHeight, vpW, vpH);
                    maxCross = ResolveSize(child.MaxHeight, contentHeight, vpW, vpH);
                }
                else
                {
                    minMain = ResolveSize(child.MinHeight, contentHeight, vpW, vpH);
                    maxMain = ResolveSize(child.MaxHeight, contentHeight, vpW, vpH);
                    minCross = ResolveSize(child.MinWidth, contentWidth, vpW, vpH);
                    maxCross = ResolveSize(child.MaxWidth, contentWidth, vpW, vpH);
                }
                if (minMain.HasValue) item.MainBase = Math.Max(item.MainBase, minMain.Value);
                if (maxMain.HasValue) item.MainBase = Math.Min(item.MainBase, maxMain.Value);
                if (minCross.HasValue) item.CrossBase = Math.Max(item.CrossBase, minCross.Value);
                if (maxCross.HasValue) item.CrossBase = Math.Min(item.CrossBase, maxCross.Value);
            }

            // --- 第三遍：JustifyContent 定位 ---
            float actualUsedMain = totalGaps;
            foreach (var item in items)
            {
                if (item.IsFloat) continue;

                float marginMain = isRow
                    ? item.Margin.Left + item.Margin.Right
                    : item.Margin.Top + item.Margin.Bottom;
                actualUsedMain += item.MainBase + marginMain;
            }

            float remainingSpace = Math.Max(0, mainSize - actualUsedMain);
            float mainOffset = 0;
            float spaceBetween = 0;

            switch (container.JustifyContent)
            {
                case JustifyContent.Start:
                    break;
                case JustifyContent.Center:
                    mainOffset = remainingSpace / 2;
                    break;
                case JustifyContent.End:
                    mainOffset = remainingSpace;
                    break;
                case JustifyContent.SpaceBetween:
                    if (normalCount > 1)
                        spaceBetween = remainingSpace / (normalCount - 1);
                    break;
                case JustifyContent.SpaceAround:
                    if (normalCount > 0)
                    {
                        float space = remainingSpace / normalCount;
                        mainOffset = space / 2;
                        spaceBetween = space;
                    }
                    break;
            }

            // --- 第四遍：放置子元素 ---
            float cursor = mainOffset;
            int normalIndex = 0;
            foreach (var item in items)
            {
                var child = item.Element;

                if (item.IsFloat)
                {
                    var floatMargin = ResolveSpacing(child.Margin, isRow ? contentWidth : contentHeight, vpW, vpH);
                    float floatWidth = ResolveSize(child.Width, contentWidth, vpW, vpH)
                        ?? Math.Max(0, contentWidth - floatMargin.Left - floatMargin.Right);
                    float floatHeight = ResolveSize(child.Height, contentHeight, vpW, vpH) ?? 0;

                    child.LayoutWidth = floatWidth;
                    child.LayoutHeight = floatHeight;

                    if (isRow)
                    {
                        child.LayoutX = contentX + cursor + floatMargin.Left;
                        child.LayoutY = contentY + floatMargin.Top;
                    }
                    else
                    {
                        child.LayoutX = contentX + floatMargin.Left;
                        child.LayoutY = contentY + cursor + floatMargin.Top;
                    }

                    child.AbsoluteX = container.AbsoluteX + child.LayoutX;
                    child.AbsoluteY = container.AbsoluteY + child.LayoutY;
                    if (container.ScrollOffsetX != 0)
                    {
                        child.AbsoluteX -= container.ScrollOffsetX;
                    }
                    if (container.ScrollOffsetY != 0)
                    {
                        child.AbsoluteY -= container.ScrollOffsetY;
                    }
                    child.UpdateAbsoluteBounds();
                    LayoutChildren(child, vpW, vpH, measureCacheGeneration);
                    continue;
                }

                float marginMainStart = isRow ? item.Margin.Left : item.Margin.Top;
                float marginMainEnd = isRow ? item.Margin.Right : item.Margin.Bottom;
                float marginCrossStart = isRow ? item.Margin.Top : item.Margin.Left;
                float marginCrossEnd = isRow ? item.Margin.Bottom : item.Margin.Right;

                float mainPos = cursor + marginMainStart;

                // AlignItems 交叉轴定位
                float availableCross = crossSize - marginCrossStart - marginCrossEnd;
                float childCross = item.CrossBase;

                float crossPos;
                switch (container.AlignItems)
                {
                    case AlignItems.Center:
                        crossPos = marginCrossStart + (availableCross - childCross) / 2;
                        break;
                    case AlignItems.End:
                        crossPos = marginCrossStart + availableCross - childCross;
                        break;
                    default: // Start or Stretch (already handled in Sizing)
                        crossPos = marginCrossStart;
                        break;
                }

                if (isRow)
                {
                    child.LayoutX = contentX + mainPos;
                    child.LayoutY = contentY + crossPos;
                    child.LayoutWidth = item.MainBase;
                    child.LayoutHeight = childCross;
                }
                else
                {
                    child.LayoutX = contentX + crossPos;
                    child.LayoutY = contentY + mainPos;
                    child.LayoutWidth = childCross;
                    child.LayoutHeight = item.MainBase;
                }

                child.AbsoluteX = container.AbsoluteX + child.LayoutX;
                child.AbsoluteY = container.AbsoluteY + child.LayoutY;

                if (container.ScrollOffsetX != 0)
                {
                    child.AbsoluteX -= container.ScrollOffsetX;
                }
                if (container.ScrollOffsetY != 0)
                {
                    child.AbsoluteY -= container.ScrollOffsetY;
                }

                child.UpdateAbsoluteBounds();
                contentRight = Math.Max(contentRight, child.LayoutX + child.LayoutWidth + item.Margin.Right);
                contentBottom = Math.Max(contentBottom, child.LayoutY + child.LayoutHeight + item.Margin.Bottom);

                cursor = mainPos + item.MainBase + marginMainEnd + gap;
                if (normalIndex < normalCount - 1 &&
                    (container.JustifyContent == JustifyContent.SpaceBetween ||
                     container.JustifyContent == JustifyContent.SpaceAround))
                {
                    cursor += spaceBetween;
                }
                normalIndex++;

                LayoutChildren(child, vpW, vpH, measureCacheGeneration);
            }

            container.CachedContentWidth = contentRight + padding.Right + border;
            container.CachedContentHeight = contentBottom + padding.Bottom + border;
        }

        /// <summary>
        /// 计算元素内容的总高度（用于滚动）
        /// </summary>
        public static float MeasureContentHeight(Win32Element container, float vpW, float vpH)
        {
            return container.CachedContentHeight;
        }

        /// <summary>
        /// 计算元素内容的总宽度（用于横向滚动）
        /// </summary>
        public static float MeasureContentWidth(Win32Element container, float vpW, float vpH)
        {
            return container.CachedContentWidth;
        }

        // --- 尺寸解析 ---

        private static float? ResolveFixedSize(Dimension? dim, float vpW, float vpH)
        {
            if (!dim.HasValue) return null;
            // 在固有尺寸测量阶段，忽略百分比和 Viewport 单位（因为父容器尺寸未知或未传递）
            if (dim.Value.Unit == DimensionUnit.Percent || dim.Value.Unit == DimensionUnit.ViewportHeight) return null;
            return dim.Value.Value;
        }

        private static float? ResolveSize(Dimension? dim, float parentSize, float vpW, float vpH)
        {
            if (!dim.HasValue) return null;
            return dim.Value.Unit switch
            {
                DimensionUnit.Pixels => dim.Value.Value,
                DimensionUnit.Percent => parentSize * dim.Value.Value / 100f,
                DimensionUnit.ViewportHeight => vpH * dim.Value.Value / 100f,
                _ => null
            };
        }

        // --- 固有尺寸测量（auto sizing） ---

        /// <summary>
        /// 测量元素的固有宽度。容器会递归测量子元素。
        /// </summary>
        private static float MeasureIntrinsicWidth(Win32Element element, float availableHeight, float vpW, float vpH, int measureCacheGeneration)
        {
            if (element.IntrinsicWidthCacheVersion == measureCacheGeneration && element.IntrinsicWidthCacheConstraint.Equals(availableHeight))
                return element.CachedIntrinsicWidth;

            float result;
            if (element.ElementType == ElementCoreName.Text)
            {
                result = MeasureTextWidth(element);
            }
            else if (element.ElementType == ElementCoreName.Input)
            {
                var inputPadding = ResolveSpacing(element.Padding, 0, vpW, vpH);
                result = 100 + inputPadding.Left + inputPadding.Right + GetBorderInset(element) * 2;
            }
            else if (element.Children.Count == 0)
            {
                result = GetBorderInset(element) * 2;
            }
            else
            {
                var padding = ResolveSpacing(element.Padding, 0, vpW, vpH);
                float border = GetBorderInset(element);
                bool isRow = element.Direction == LayoutDirection.Horizontal;
                float gap = element.Gap;
                result = 0;
                int count = 0;

                foreach (var child in element.Children)
                {
                    if (child.Float) continue;
                    float childW = ResolveFixedSize(child.Width, vpW, vpH)
                                   ?? MeasureIntrinsicWidth(child, availableHeight, vpW, vpH, measureCacheGeneration);
                    var margin = ResolveSpacing(child.Margin, 0, vpW, vpH);
                    float totalChild = childW + margin.Left + margin.Right;

                    if (isRow)
                    {
                        result += totalChild;
                        count++;
                    }
                    else
                    {
                        result = Math.Max(result, totalChild);
                        count++;
                    }
                }

                if (isRow && count > 1)
                    result += gap * (count - 1);

                result += padding.Left + padding.Right + border * 2;
            }

            element.IntrinsicWidthCacheVersion = measureCacheGeneration;
            element.IntrinsicWidthCacheConstraint = availableHeight;
            element.CachedIntrinsicWidth = result;
            return result;
        }

        /// <summary>
        /// 测量元素的固有高度。容器会递归测量子元素。
        /// </summary>
        private static float MeasureIntrinsicHeight(Win32Element element, float availableWidth, float vpW, float vpH, int measureCacheGeneration)
        {
            if (element.IntrinsicHeightCacheVersion == measureCacheGeneration && element.IntrinsicHeightCacheConstraint.Equals(availableWidth))
                return element.CachedIntrinsicHeight;

            float result;
            if (element.ElementType == ElementCoreName.Text)
            {
                result = MeasureTextHeight(element, availableWidth);
            }
            else if (element.ElementType == ElementCoreName.Input)
            {
                var inputPadding = ResolveSpacing(element.Padding, 0, vpW, vpH);
                result = 24 + inputPadding.Top + inputPadding.Bottom + GetBorderInset(element) * 2;
            }
            else if (element.Children.Count == 0)
            {
                result = GetBorderInset(element) * 2;
            }
            else
            {
                var padding = ResolveSpacing(element.Padding, 0, vpW, vpH);
                float border = GetBorderInset(element);
                bool isRow = element.Direction == LayoutDirection.Horizontal;
                float gap = element.Gap;
                result = 0;
                int count = 0;

                foreach (var child in element.Children)
                {
                    if (child.Float) continue;
                    float childH = ResolveFixedSize(child.Height, vpW, vpH)
                                   ?? MeasureIntrinsicHeight(child, availableWidth, vpW, vpH, measureCacheGeneration);
                    var margin = ResolveSpacing(child.Margin, 0, vpW, vpH);
                    float totalChild = childH + margin.Top + margin.Bottom;

                    if (isRow)
                    {
                        result = Math.Max(result, totalChild);
                        count++;
                    }
                    else
                    {
                        result += totalChild;
                        count++;
                    }
                }

                if (!isRow && count > 1)
                    result += gap * (count - 1);

                result += padding.Top + padding.Bottom + border * 2;
            }

            element.IntrinsicHeightCacheVersion = measureCacheGeneration;
            element.IntrinsicHeightCacheConstraint = availableWidth;
            element.CachedIntrinsicHeight = result;
            return result;
        }

        private static float MeasureTextWidth(Win32Element element)
        {
            if (string.IsNullOrEmpty(element.Text)) return 0;

            var fontSize = element.FontSize > 0 ? element.FontSize : 14;
            var size = GdiText.MeasureText(element.Text, element.FontFamily, fontSize, element.FontWeight, noWrap: true);
            return size.Width;
        }

        private static float MeasureTextHeight(Win32Element element, float widthConstraint)
        {
            var fontSize = element.FontSize > 0 ? element.FontSize : 14;
            if (string.IsNullOrEmpty(element.Text))
                return GdiText.GetPreferredLineHeight(element.FontFamily, fontSize, element.FontWeight);

            float? maxWidth = element.NoWrap ? null : widthConstraint > 0 ? widthConstraint : 100000f;
            var size = GdiText.MeasureText(element.Text, element.FontFamily, fontSize, element.FontWeight, maxWidth, element.NoWrap);
            return size.Height;
        }

        private static (float Left, float Top, float Right, float Bottom) ResolveSpacing(
            Spacing? spacing, float parentSize, float vpW, float vpH)
        {
            if (!spacing.HasValue) return (0, 0, 0, 0);
            var s = spacing.Value;
            return (
                ResolveSize(s.Left, parentSize, vpW, vpH) ?? 0,
                ResolveSize(s.Top, parentSize, vpW, vpH) ?? 0,
                ResolveSize(s.Right, parentSize, vpW, vpH) ?? 0,
                ResolveSize(s.Bottom, parentSize, vpW, vpH) ?? 0
            );
        }

        private static float GetBorderInset(Win32Element element)
        {
            return element.BorderStyle == BorderStyle.None ? 0 : Math.Max(0, element.BorderWidth);
        }

        private class FlexItem
        {
            public Win32Element Element { get; set; } = null!;
            public float MainBase { get; set; }
            public float CrossBase { get; set; }
            public (float Left, float Top, float Right, float Bottom) Margin { get; set; }
            public float Grow { get; set; }
            public float Shrink { get; set; }
            public bool IsFloat { get; set; }
        }
    }
}

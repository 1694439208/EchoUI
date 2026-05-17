namespace EchoUI.Core;

public interface ILayoutNode<TNode> where TNode : class, ILayoutNode<TNode>
{
    IReadOnlyList<TNode> LayoutChildren { get; }
    string ElementType { get; }

    Dimension? Width { get; set; }
    Dimension? Height { get; set; }
    Dimension? MinWidth { get; set; }
    Dimension? MinHeight { get; set; }
    Dimension? MaxWidth { get; set; }
    Dimension? MaxHeight { get; set; }
    Spacing? Margin { get; set; }
    Spacing? Padding { get; set; }

    LayoutDirection Direction { get; set; }
    JustifyContent JustifyContent { get; set; }
    AlignItems AlignItems { get; set; }
    float FlexGrow { get; set; }
    float FlexShrink { get; set; }
    float Gap { get; set; }
    bool Float { get; set; }
    Overflow Overflow { get; set; }

    BorderStyle BorderStyle { get; set; }
    float BorderWidth { get; set; }

    string? Text { get; set; }
    string? FontFamily { get; set; }
    float FontSize { get; set; }
    string? FontWeight { get; set; }
    bool NoWrap { get; set; }

    float LayoutX { get; set; }
    float LayoutY { get; set; }
    float LayoutWidth { get; set; }
    float LayoutHeight { get; set; }
    float AbsoluteX { get; set; }
    float AbsoluteY { get; set; }
    float ScrollOffsetX { get; set; }
    float ScrollOffsetY { get; set; }

    float CachedContentWidth { get; set; }
    float CachedContentHeight { get; set; }
    int IntrinsicWidthCacheVersion { get; set; }
    float IntrinsicWidthCacheConstraint { get; set; }
    float CachedIntrinsicWidth { get; set; }
    int IntrinsicHeightCacheVersion { get; set; }
    float IntrinsicHeightCacheConstraint { get; set; }
    float CachedIntrinsicHeight { get; set; }

    void CommitLayout();
}

public static class LayoutEngine
{
    public static void ComputeLayout<TNode>(
        TNode root,
        float viewportWidth,
        float viewportHeight,
        int measureCacheGeneration,
        Func<TNode, float?, bool, TextMeasurementResult> measureText)
        where TNode : class, ILayoutNode<TNode>
    {
        root.LayoutX = 0;
        root.LayoutY = 0;
        root.LayoutWidth = viewportWidth;
        root.LayoutHeight = viewportHeight;
        root.AbsoluteX = 0;
        root.AbsoluteY = 0;
        root.CommitLayout();

        LayoutChildren(root, viewportWidth, viewportHeight, measureCacheGeneration, measureText);

        if (ClampScrollOffsetsRecursive(root, viewportWidth, viewportHeight))
        {
            LayoutChildren(root, viewportWidth, viewportHeight, measureCacheGeneration, measureText);
        }
    }

    public static void UpdateAbsoluteLayout<TNode>(TNode root) where TNode : class, ILayoutNode<TNode>
    {
        root.CommitLayout();
        UpdateAbsoluteChildren(root);
    }

    public static float MeasureContentWidth<TNode>(TNode container) where TNode : class, ILayoutNode<TNode>
    {
        return container.CachedContentWidth;
    }

    public static float MeasureContentHeight<TNode>(TNode container) where TNode : class, ILayoutNode<TNode>
    {
        return container.CachedContentHeight;
    }

    private static void UpdateAbsoluteChildren<TNode>(TNode container) where TNode : class, ILayoutNode<TNode>
    {
        foreach (var child in container.LayoutChildren)
        {
            child.AbsoluteX = container.AbsoluteX + child.LayoutX - container.ScrollOffsetX;
            child.AbsoluteY = container.AbsoluteY + child.LayoutY - container.ScrollOffsetY;
            child.CommitLayout();
            UpdateAbsoluteChildren(child);
        }
    }

    private static bool ClampScrollOffsetsRecursive<TNode>(TNode element, float vpW, float vpH)
        where TNode : class, ILayoutNode<TNode>
    {
        var changed = false;

        foreach (var child in element.LayoutChildren)
        {
            changed |= ClampScrollOffsetsRecursive(child, vpW, vpH);
        }

        if (element.Overflow != Overflow.Auto && element.Overflow != Overflow.Scroll &&
            element.ScrollOffsetX == 0 && element.ScrollOffsetY == 0)
        {
            return changed;
        }

        float maxScrollX = Math.Max(0, element.CachedContentWidth - element.LayoutWidth);
        float maxScrollY = Math.Max(0, element.CachedContentHeight - element.LayoutHeight);
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

    private static void LayoutChildren<TNode>(
        TNode container,
        float vpW,
        float vpH,
        int measureCacheGeneration,
        Func<TNode, float?, bool, TextMeasurementResult> measureText)
        where TNode : class, ILayoutNode<TNode>
    {
        var padding = ResolveSpacing(container.Padding, container.LayoutWidth, vpW, vpH);
        float border = GetBorderInset(container);
        container.CachedContentWidth = padding.Left + padding.Right + border * 2;
        container.CachedContentHeight = padding.Top + padding.Bottom + border * 2;

        var children = container.LayoutChildren;
        if (children.Count == 0) return;

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

        var items = new List<FlexItem<TNode>>(children.Count);
        foreach (var child in children)
        {
            if (child.Float)
            {
                items.Add(new FlexItem<TNode> { Element = child, IsFloat = true });
                continue;
            }

            var margin = ResolveSpacing(child.Margin, isRow ? contentWidth : contentHeight, vpW, vpH);
            float? explicitMain = isRow
                ? ResolveSize(child.Width, contentWidth, vpW, vpH)
                : ResolveSize(child.Height, contentHeight, vpW, vpH);
            float mainBase = explicitMain ?? (isRow
                ? MeasureIntrinsicWidth(child, contentHeight, vpW, vpH, measureCacheGeneration, measureText)
                : MeasureIntrinsicHeight(child, contentWidth, vpW, vpH, measureCacheGeneration, measureText));

            float? explicitCross = isRow
                ? ResolveSize(child.Height, contentHeight, vpW, vpH)
                : ResolveSize(child.Width, contentWidth, vpW, vpH);
            float crossBase;
            if (explicitCross.HasValue)
            {
                crossBase = explicitCross.Value;
            }
            else if (container.AlignItems == AlignItems.Stretch)
            {
                float marginCross = isRow ? margin.Top + margin.Bottom : margin.Left + margin.Right;
                crossBase = Math.Max(0, crossSize - marginCross);
            }
            else
            {
                crossBase = isRow
                    ? MeasureIntrinsicHeight(child, contentWidth, vpW, vpH, measureCacheGeneration, measureText)
                    : MeasureIntrinsicWidth(child, contentHeight, vpW, vpH, measureCacheGeneration, measureText);
            }

            items.Add(new FlexItem<TNode>
            {
                Element = child,
                MainBase = Math.Max(0, mainBase),
                CrossBase = Math.Max(0, crossBase),
                Margin = margin,
                Grow = child.FlexGrow,
                Shrink = child.FlexShrink
            });
        }

        int normalCount = items.Count(i => !i.IsFloat);
        float totalGaps = normalCount > 1 ? gap * (normalCount - 1) : 0;
        float usedMain = totalGaps;
        float totalGrow = 0;
        float totalShrink = 0;
        foreach (var item in items)
        {
            if (item.IsFloat) continue;
            float marginMain = isRow ? item.Margin.Left + item.Margin.Right : item.Margin.Top + item.Margin.Bottom;
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

        ApplyConstraints(items, isRow, contentWidth, contentHeight, vpW, vpH);
        ArrangeItems(container, items, isRow, mainSize, crossSize, totalGaps, normalCount, gap, contentX, contentY, contentWidth, contentHeight, ref contentRight, ref contentBottom, vpW, vpH, measureCacheGeneration, measureText);
        container.CachedContentWidth = contentRight + padding.Right + border;
        container.CachedContentHeight = contentBottom + padding.Bottom + border;
    }

    private static void ApplyConstraints<TNode>(IEnumerable<FlexItem<TNode>> items, bool isRow, float contentWidth, float contentHeight, float vpW, float vpH)
        where TNode : class, ILayoutNode<TNode>
    {
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
    }

    private static void ArrangeItems<TNode>(
        TNode container,
        List<FlexItem<TNode>> items,
        bool isRow,
        float mainSize,
        float crossSize,
        float totalGaps,
        int normalCount,
        float gap,
        float contentX,
        float contentY,
        float contentWidth,
        float contentHeight,
        ref float contentRight,
        ref float contentBottom,
        float vpW,
        float vpH,
        int measureCacheGeneration,
        Func<TNode, float?, bool, TextMeasurementResult> measureText)
        where TNode : class, ILayoutNode<TNode>
    {
        float actualUsedMain = totalGaps;
        foreach (var item in items)
        {
            if (item.IsFloat) continue;
            float marginMain = isRow ? item.Margin.Left + item.Margin.Right : item.Margin.Top + item.Margin.Bottom;
            actualUsedMain += item.MainBase + marginMain;
        }

        float remainingSpace = Math.Max(0, mainSize - actualUsedMain);
        float mainOffset = container.JustifyContent switch
        {
            JustifyContent.Center => remainingSpace / 2,
            JustifyContent.End => remainingSpace,
            JustifyContent.SpaceAround when normalCount > 0 => remainingSpace / normalCount / 2,
            _ => 0
        };
        float spaceBetween = container.JustifyContent switch
        {
            JustifyContent.SpaceBetween when normalCount > 1 => remainingSpace / (normalCount - 1),
            JustifyContent.SpaceAround when normalCount > 0 => remainingSpace / normalCount,
            _ => 0
        };

        float cursor = mainOffset;
        int normalIndex = 0;
        foreach (var item in items)
        {
            var child = item.Element;
            if (item.IsFloat)
            {
                ArrangeFloat(container, child, isRow, cursor, contentX, contentY, contentWidth, contentHeight, vpW, vpH, measureCacheGeneration, measureText);
                continue;
            }

            float marginMainStart = isRow ? item.Margin.Left : item.Margin.Top;
            float marginMainEnd = isRow ? item.Margin.Right : item.Margin.Bottom;
            float marginCrossStart = isRow ? item.Margin.Top : item.Margin.Left;
            float marginCrossEnd = isRow ? item.Margin.Bottom : item.Margin.Right;
            float mainPos = cursor + marginMainStart;
            float availableCross = crossSize - marginCrossStart - marginCrossEnd;
            float crossPos = container.AlignItems switch
            {
                AlignItems.Center => marginCrossStart + (availableCross - item.CrossBase) / 2,
                AlignItems.End => marginCrossStart + availableCross - item.CrossBase,
                _ => marginCrossStart
            };

            if (isRow)
            {
                child.LayoutX = contentX + mainPos;
                child.LayoutY = contentY + crossPos;
                child.LayoutWidth = item.MainBase;
                child.LayoutHeight = item.CrossBase;
            }
            else
            {
                child.LayoutX = contentX + crossPos;
                child.LayoutY = contentY + mainPos;
                child.LayoutWidth = item.CrossBase;
                child.LayoutHeight = item.MainBase;
            }

            SetAbsoluteFromParent(container, child);
            contentRight = Math.Max(contentRight, child.LayoutX + child.LayoutWidth + item.Margin.Right);
            contentBottom = Math.Max(contentBottom, child.LayoutY + child.LayoutHeight + item.Margin.Bottom);
            cursor = mainPos + item.MainBase + marginMainEnd + gap;
            if (normalIndex < normalCount - 1 &&
                (container.JustifyContent == JustifyContent.SpaceBetween || container.JustifyContent == JustifyContent.SpaceAround))
            {
                cursor += spaceBetween;
            }
            normalIndex++;
            LayoutChildren(child, vpW, vpH, measureCacheGeneration, measureText);
        }
    }

    private static void ArrangeFloat<TNode>(TNode container, TNode child, bool isRow, float cursor, float contentX, float contentY, float contentWidth, float contentHeight, float vpW, float vpH, int measureCacheGeneration, Func<TNode, float?, bool, TextMeasurementResult> measureText)
        where TNode : class, ILayoutNode<TNode>
    {
        var margin = ResolveSpacing(child.Margin, isRow ? contentWidth : contentHeight, vpW, vpH);
        child.LayoutWidth = ResolveSize(child.Width, contentWidth, vpW, vpH) ?? Math.Max(0, contentWidth - margin.Left - margin.Right);
        child.LayoutHeight = ResolveSize(child.Height, contentHeight, vpW, vpH) ?? 0;
        child.LayoutX = isRow ? contentX + cursor + margin.Left : contentX + margin.Left;
        child.LayoutY = isRow ? contentY + margin.Top : contentY + cursor + margin.Top;
        SetAbsoluteFromParent(container, child);
        LayoutChildren(child, vpW, vpH, measureCacheGeneration, measureText);
    }

    private static void SetAbsoluteFromParent<TNode>(TNode parent, TNode child) where TNode : class, ILayoutNode<TNode>
    {
        child.AbsoluteX = parent.AbsoluteX + child.LayoutX - parent.ScrollOffsetX;
        child.AbsoluteY = parent.AbsoluteY + child.LayoutY - parent.ScrollOffsetY;
        child.CommitLayout();
    }

    private static float? ResolveFixedSize(Dimension? dim)
    {
        if (!dim.HasValue) return null;
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

    private static float MeasureIntrinsicWidth<TNode>(TNode element, float availableHeight, float vpW, float vpH, int measureCacheGeneration, Func<TNode, float?, bool, TextMeasurementResult> measureText)
        where TNode : class, ILayoutNode<TNode>
    {
        if (element.IntrinsicWidthCacheVersion == measureCacheGeneration && element.IntrinsicWidthCacheConstraint.Equals(availableHeight))
            return element.CachedIntrinsicWidth;

        float result;
        if (element.ElementType == ElementCoreName.Text)
            result = string.IsNullOrEmpty(element.Text) ? 0 : measureText(element, null, true).Width;
        else if (element.ElementType == ElementCoreName.Input)
            result = 100 + ResolveSpacing(element.Padding, 0, vpW, vpH).Left + ResolveSpacing(element.Padding, 0, vpW, vpH).Right + GetBorderInset(element) * 2;
        else if (element.LayoutChildren.Count == 0)
            result = GetBorderInset(element) * 2;
        else
            result = MeasureContainerIntrinsicWidth(element, availableHeight, vpW, vpH, measureCacheGeneration, measureText);

        element.IntrinsicWidthCacheVersion = measureCacheGeneration;
        element.IntrinsicWidthCacheConstraint = availableHeight;
        element.CachedIntrinsicWidth = result;
        return result;
    }

    private static float MeasureIntrinsicHeight<TNode>(TNode element, float availableWidth, float vpW, float vpH, int measureCacheGeneration, Func<TNode, float?, bool, TextMeasurementResult> measureText)
        where TNode : class, ILayoutNode<TNode>
    {
        if (element.IntrinsicHeightCacheVersion == measureCacheGeneration && element.IntrinsicHeightCacheConstraint.Equals(availableWidth))
            return element.CachedIntrinsicHeight;

        float result;
        if (element.ElementType == ElementCoreName.Text)
        {
            float? maxWidth = element.NoWrap ? null : availableWidth > 0 ? availableWidth : 100000f;
            result = measureText(element, maxWidth, element.NoWrap).Height;
        }
        else if (element.ElementType == ElementCoreName.Input)
        {
            var padding = ResolveSpacing(element.Padding, 0, vpW, vpH);
            result = 24 + padding.Top + padding.Bottom + GetBorderInset(element) * 2;
        }
        else if (element.LayoutChildren.Count == 0)
            result = GetBorderInset(element) * 2;
        else
            result = MeasureContainerIntrinsicHeight(element, availableWidth, vpW, vpH, measureCacheGeneration, measureText);

        element.IntrinsicHeightCacheVersion = measureCacheGeneration;
        element.IntrinsicHeightCacheConstraint = availableWidth;
        element.CachedIntrinsicHeight = result;
        return result;
    }

    private static float MeasureContainerIntrinsicWidth<TNode>(TNode element, float availableHeight, float vpW, float vpH, int measureCacheGeneration, Func<TNode, float?, bool, TextMeasurementResult> measureText)
        where TNode : class, ILayoutNode<TNode>
    {
        var padding = ResolveSpacing(element.Padding, 0, vpW, vpH);
        float border = GetBorderInset(element);
        bool isRow = element.Direction == LayoutDirection.Horizontal;
        float result = 0;
        int count = 0;
        foreach (var child in element.LayoutChildren)
        {
            if (child.Float) continue;
            float childW = ResolveFixedSize(child.Width) ?? MeasureIntrinsicWidth(child, availableHeight, vpW, vpH, measureCacheGeneration, measureText);
            var margin = ResolveSpacing(child.Margin, 0, vpW, vpH);
            float totalChild = childW + margin.Left + margin.Right;
            result = isRow ? result + totalChild : Math.Max(result, totalChild);
            count++;
        }
        if (isRow && count > 1) result += element.Gap * (count - 1);
        return result + padding.Left + padding.Right + border * 2;
    }

    private static float MeasureContainerIntrinsicHeight<TNode>(TNode element, float availableWidth, float vpW, float vpH, int measureCacheGeneration, Func<TNode, float?, bool, TextMeasurementResult> measureText)
        where TNode : class, ILayoutNode<TNode>
    {
        var padding = ResolveSpacing(element.Padding, 0, vpW, vpH);
        float border = GetBorderInset(element);
        bool isRow = element.Direction == LayoutDirection.Horizontal;
        float result = 0;
        int count = 0;
        foreach (var child in element.LayoutChildren)
        {
            if (child.Float) continue;
            float childH = ResolveFixedSize(child.Height) ?? MeasureIntrinsicHeight(child, availableWidth, vpW, vpH, measureCacheGeneration, measureText);
            var margin = ResolveSpacing(child.Margin, 0, vpW, vpH);
            float totalChild = childH + margin.Top + margin.Bottom;
            result = isRow ? Math.Max(result, totalChild) : result + totalChild;
            count++;
        }
        if (!isRow && count > 1) result += element.Gap * (count - 1);
        return result + padding.Top + padding.Bottom + border * 2;
    }

    private static (float Left, float Top, float Right, float Bottom) ResolveSpacing(Spacing? spacing, float parentSize, float vpW, float vpH)
    {
        if (!spacing.HasValue) return (0, 0, 0, 0);
        var s = spacing.Value;
        return (
            ResolveSize(s.Left, parentSize, vpW, vpH) ?? 0,
            ResolveSize(s.Top, parentSize, vpW, vpH) ?? 0,
            ResolveSize(s.Right, parentSize, vpW, vpH) ?? 0,
            ResolveSize(s.Bottom, parentSize, vpW, vpH) ?? 0);
    }

    private static float GetBorderInset<TNode>(TNode element) where TNode : class, ILayoutNode<TNode>
    {
        return element.BorderStyle == BorderStyle.None ? 0 : Math.Max(0, element.BorderWidth);
    }

    private sealed class FlexItem<TNode> where TNode : class, ILayoutNode<TNode>
    {
        public TNode Element { get; init; } = null!;
        public float MainBase { get; set; }
        public float CrossBase { get; set; }
        public (float Left, float Top, float Right, float Bottom) Margin { get; init; }
        public float Grow { get; init; }
        public float Shrink { get; init; }
        public bool IsFloat { get; init; }
    }
}

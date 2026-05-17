namespace EchoUI.Core;

/// <summary>绘制引擎：从 Element 的属性自动生成 RenderCommand 列表</summary>
public static class PaintEngine
{
    /// <summary>从单个元素生成绘制命令（不含子元素递归）</summary>
    public static List<RenderCommand> GenerateCommands(Element el, LayoutBox layout)
    {
        var list = new List<RenderCommand>();
        AddElementCommands(el, layout, list);
        return list;
    }

    /// <summary>从元素树生成绘制命令，布局由调用方提供。</summary>
    public static List<RenderCommand> GenerateCommands(Element root, Func<Element, LayoutBox?> resolveLayout)
    {
        var list = new List<RenderCommand>();
        AddTreeCommands(root, resolveLayout, list);
        return list;
    }

    /// <summary>从实例树生成绘制命令，布局直接取自实例。</summary>
    public static List<RenderCommand> GenerateCommands(ComponentInstance root)
    {
        var list = new List<RenderCommand>();
        AddInstanceTreeCommands(root, list);
        return list;
    }

    private static void AddTreeCommands(Element el, Func<Element, LayoutBox?> resolveLayout, List<RenderCommand> list)
    {
        var layout = resolveLayout(el);
        if (!layout.HasValue) return;

        AddElementCommands(el, layout.Value, list);

        var shouldClip = el.Props is ContainerProps cp && (cp.Overflow ?? Overflow.Visible) != Overflow.Visible;
        if (shouldClip)
        {
            list.Add(new PushClip(layout.Value));
        }

        foreach (var child in el.Props.Children)
        {
            AddTreeCommands(child, resolveLayout, list);
        }

        if (shouldClip)
        {
            list.Add(new PopClip());
        }
    }

    private static void AddInstanceTreeCommands(ComponentInstance instance, List<RenderCommand> list)
    {
        var isFloat = instance.Element.Type.IsNative
            && instance.Element.Props is ContainerProps { Float: true };
        if (isFloat)
        {
            return;
        }

        if (instance.Element.Type.IsNative && instance.Layout.HasValue)
        {
            var nativeType = instance.Element.Type.AsNativeType;
            if (nativeType == ElementCoreName.Container || nativeType == ElementCoreName.Text)
            {
                AddElementCommands(instance.Element, instance.Layout.Value, list);
            }
        }

        var shouldClip = instance.Element.Type.IsNative
            && instance.Layout.HasValue
            && instance.Element.Props is ContainerProps cp
            && (cp.Overflow ?? Overflow.Visible) != Overflow.Visible;

        if (shouldClip)
        {
            list.Add(new PushClip(instance.Layout!.Value));
        }

        foreach (var child in instance.Children)
        {
            AddInstanceTreeCommands(child, list);
        }

        if (shouldClip)
        {
            list.Add(new PopClip());
        }
    }

    private static void AddElementCommands(Element el, LayoutBox layout, List<RenderCommand> list)
    {
        if (el.Props is ContainerProps cp)
        {
            if (cp.Shadow.HasValue && cp.Shadow.Value.IsVisible)
            {
                var shadow = cp.Shadow.Value;
                list.Add(new DrawShadow(layout, shadow.Color, shadow.OffsetY, cp.BorderRadius ?? 0f, shadow.Blur));
            }

            if (cp.BackgroundColor is { A: > 0 } bg)
            {
                list.Add(new DrawRect(layout, bg, cp.BorderRadius ?? 0f));
            }

            if (cp.BorderStyle.HasValue && cp.BorderStyle.Value != BorderStyle.None
                && cp.BorderColor is { A: > 0 } bc && cp.BorderWidth.HasValue && cp.BorderWidth.Value > 0)
            {
                list.Add(new DrawBorder(layout, bc, cp.BorderWidth.Value, cp.BorderRadius ?? 0f, cp.BorderStyle.Value));
            }
        }

        if (el.Props is TextProps tp && !string.IsNullOrEmpty(tp.Text))
        {
            list.Add(new DrawText(layout, tp.Text,
                tp.Color ?? new Color(0, 0, 0),
                tp.FontFamily, tp.FontSize ?? 14f, tp.FontWeight, tp.NoWrap));
        }
    }
}

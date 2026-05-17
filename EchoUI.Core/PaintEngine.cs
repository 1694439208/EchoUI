namespace EchoUI.Core;

/// <summary>绘制引擎：从 Element 的属性自动生成 RenderCommand 列表</summary>
public static class PaintEngine
{
    /// <summary>从单个元素生成所有绘制命令（不含子元素递归）</summary>
    public static List<RenderCommand> GenerateCommands(Element el, LayoutBox layout)
    {
        var list = new List<RenderCommand>();

        if (el.Props is ContainerProps cp)
        {
            // Shadow: ShadowColor + BorderWidth → DrawShadow
            if (cp.ShadowColor is { A: > 0 } sc)
            {
                var sh = cp.BorderWidth ?? 4f;
                var r  = cp.BorderRadius ?? 0f;
                list.Add(new DrawShadow(layout, sc, sh, r));
            }
            // BackgroundColor → DrawRect
            if (cp.BackgroundColor is { A: > 0 } bg)
            {
                list.Add(new DrawRect(layout, bg, cp.BorderRadius ?? 0f));
            }
            // Border → DrawBorder
            if (cp.BorderStyle.HasValue && cp.BorderStyle.Value != BorderStyle.None
                && cp.BorderColor is { A: > 0 } bc && cp.BorderWidth.HasValue && cp.BorderWidth.Value > 0)
            {
                list.Add(new DrawBorder(layout, bc, cp.BorderWidth.Value, cp.BorderRadius ?? 0f, cp.BorderStyle.Value));
            }
        }
        // Text → DrawText
        if (el.Props is TextProps tp && !string.IsNullOrEmpty(tp.Text))
        {
            list.Add(new DrawText(layout, tp.Text,
                tp.Color ?? new Color(0, 0, 0),
                tp.FontFamily, tp.FontSize ?? 14f, tp.FontWeight));
        }

        return list;
    }
}

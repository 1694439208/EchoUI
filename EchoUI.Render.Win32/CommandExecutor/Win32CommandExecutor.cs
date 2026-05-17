using EchoUI.Core;

namespace EchoUI.Render.Win32;

/// <summary>Win32 绘制命令执行器：将 RenderCommand 翻译为 GDI+ 调用</summary>
internal static class Win32CommandExecutor
{
    [ThreadStatic]
    private static Stack<uint>? s_gdiPlusClipStates;

    public static void ExecuteSingle(nint hdc, RenderCommand cmd, Win32Element? element = null)
    {
        ExecuteOne(hdc, cmd, element);
    }

    public static void Execute(nint hdc, List<RenderCommand> commands, Win32Element? element = null)
    {
        foreach (var cmd in commands)
            ExecuteOne(hdc, cmd, element);
    }

    private static void ExecuteOne(nint hdc, RenderCommand cmd, Win32Element? element)
    {
        switch (cmd)
        {
            case DrawRect r:
                if (r.BackgroundColor is { A: > 0 } bg)
                {
                    GdiPlus.Flush();
                    GdiPainter.FillShape(hdc, element,
                        new RectF(r.Layout.X, r.Layout.Y, r.Layout.Width, r.Layout.Height),
                        bg, r.BorderRadius);
                }
                break;

            case DrawBorder b:
                if (b.Color.A > 0 && b.Width > 0)
                {
                    GdiPlus.Flush();
                    GdiPainter.DrawBorder(hdc, element,
                        new RectF(b.Layout.X, b.Layout.Y, b.Layout.Width, b.Layout.Height),
                        b.Color, b.Width, b.Radius, b.Style);
                }
                break;

            case DrawShadow s:
                if (s.Color.A > 0 && (s.OffsetY != 0 || s.Blur > 0))
                {
                    GdiPlus.Flush();
                    var blur = Math.Max(0, s.Blur);
                    var ext = new RectF(s.Layout.X - blur, s.Layout.Y,
                        s.Layout.Width + blur * 2, s.Layout.Height + s.OffsetY + blur);
                    GdiPainter.FillShape(hdc, null, ext, s.Color, s.BorderRadius + blur);
                }
                break;

            case DrawText d:
                GdiPlus.Flush();
                GdiText.DrawText(hdc, d.Text, d.FontFamily, d.FontSize, d.FontWeight, d.Color,
                    new RectF(d.Layout.X, d.Layout.Y, d.Layout.Width, d.Layout.Height), d.NoWrap);
                break;

            case PushClip c:
                NativeInterop.SaveDC(hdc);
                var clipRect = new RectF(c.Layout.X, c.Layout.Y, c.Layout.Width, c.Layout.Height);
                var clip = GdiPainter.ToRect(clipRect);
                NativeInterop.IntersectClipRect(hdc, clip.Left, clip.Top, clip.Right, clip.Bottom);
                s_gdiPlusClipStates ??= [];
                s_gdiPlusClipStates.Push(GdiPlus.SaveGraphics());
                GdiPlus.IntersectClip(clipRect);
                break;

            case PopClip:
                if (s_gdiPlusClipStates is { Count: > 0 })
                {
                    GdiPlus.RestoreGraphics(s_gdiPlusClipStates.Pop());
                }
                NativeInterop.RestoreDC(hdc, -1);
                break;
        }
    }
}

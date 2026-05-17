using EchoUI.Core;

namespace EchoUI.Render.Win32;

/// <summary>Win32 绘制命令执行器：将 RenderCommand 翻译为 GDI+ 调用</summary>
internal static class Win32CommandExecutor
{
    public static void ExecuteSingle(nint hdc, RenderCommand cmd)
    {
        ExecuteOne(hdc, cmd);
    }

    public static void Execute(nint hdc, List<RenderCommand> commands)
    {
        foreach (var cmd in commands)
            ExecuteOne(hdc, cmd);
    }

    private static void ExecuteOne(nint hdc, RenderCommand cmd)
    {
        switch (cmd)
        {
            case DrawRect r:
                if (r.BackgroundColor is { A: > 0 } bg)
                {
                    GdiPlus.Flush();
                    GdiPainter.FillShape(hdc, null,
                        new RectF(r.Layout.X, r.Layout.Y, r.Layout.Width, r.Layout.Height),
                        bg, r.BorderRadius);
                }
                break;

            case DrawBorder b:
                if (b.Color.A > 0 && b.Width > 0)
                {
                    GdiPlus.Flush();
                    GdiPainter.DrawBorder(hdc, null,
                        new RectF(b.Layout.X, b.Layout.Y, b.Layout.Width, b.Layout.Height),
                        b.Color, b.Width, b.Radius, b.Style);
                }
                break;

            case DrawShadow s:
                if (s.Color.A > 0 && s.OffsetY > 0)
                {
                    GdiPlus.Flush();
                    var ext = new RectF(s.Layout.X, s.Layout.Y,
                        s.Layout.Width, s.Layout.Height + s.OffsetY);
                    GdiPainter.FillShape(hdc, null, ext, s.Color, s.BorderRadius);
                }
                break;

            case DrawText d:
                GdiPlus.Flush();
                GdiText.DrawText(hdc, d.Text, d.FontFamily, d.FontSize, d.FontWeight, d.Color,
                    new RectF(d.Layout.X, d.Layout.Y, d.Layout.Width, d.Layout.Height), noWrap: true);
                break;

            case PushClip c:
                NativeInterop.SaveDC(hdc);
                var clip = GdiPainter.ToRect(new RectF(c.Layout.X, c.Layout.Y, c.Layout.Width, c.Layout.Height));
                NativeInterop.IntersectClipRect(hdc, clip.Left, clip.Top, clip.Right, clip.Bottom);
                break;

            case PopClip:
                NativeInterop.RestoreDC(hdc, -1);
                break;
        }
    }
}

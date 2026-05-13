using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using EchoUI.Core;

namespace EchoUI.Render.Win32
{
    /// <summary>
    /// GDI+ 绘制引擎，遍历 Win32Element 树并绘制到 Graphics 上。
    /// 支持双缓冲、圆角矩形、文本渲染、裁剪。
    /// </summary>
    internal static class GdiPainter
    {
        /// <summary>
        /// 绘制整棵元素树
        /// </summary>
        /// <summary>
        /// 绘制整棵元素树
        /// </summary>
        public static void Paint(Graphics g, Win32Element root, IReadOnlyCollection<Win32Element>? floatingElements, float viewportWidth, float viewportHeight)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(System.Drawing.Color.White);

            var viewportRect = new RectangleF(0, 0, viewportWidth, viewportHeight);

            // 第一遍：绘制非 Float 元素
            PaintElement(g, root, viewportRect, floatingElements);

            // 第二遍：绘制 Float 元素（作为顶层覆盖）
            if (floatingElements != null)
            {
                foreach (var floatElem in floatingElements)
                {
                    // Float 元素通常不受父级 overflow 裁剪（除非我们想那样），
                    // 这里我们只裁剪到视口
                    PaintElement(g, floatElem, viewportRect, null);
                }
            }
        }

        private static void PaintElement(Graphics g, Win32Element element, RectangleF clipRect, IReadOnlyCollection<Win32Element>? skippedElements)
        {
            // 如果该元素在待会要画的 Float 列表里，这一遍先跳过
            if (skippedElements != null && skippedElements.Contains(element))
                return;

            var bounds = element.GetAbsoluteBounds();

            // 如果元素完全在裁剪区域外且子元素不会外溢可见，跳过
            if (bounds.Right < clipRect.Left || bounds.Left > clipRect.Right ||
                bounds.Bottom < clipRect.Top || bounds.Top > clipRect.Bottom)
            {
                if (element.Overflow != Overflow.Visible || element.Children.Count == 0)
                    return;
            }

            switch (element.ElementType)
            {
                case ElementCoreName.Container:
                    PaintContainer(g, element, bounds, clipRect, skippedElements);
                    break;
                case ElementCoreName.Text:
                    PaintText(g, element, bounds, clipRect);
                    break;
                case ElementCoreName.Input:
                    // Input 由原生 Edit 控件绘制，这里只画边框/背景
                    PaintInputBackground(g, element, bounds);
                    break;
                case "img":
                    PaintImage(g, element, bounds);
                    break;
                default:
                    // 未知类型当作容器处理
                    PaintContainer(g, element, bounds, clipRect, skippedElements);
                    break;
            }
        }

        private static void PaintContainer(Graphics g, Win32Element element, RectangleF bounds, RectangleF clipRect, IReadOnlyCollection<Win32Element>? skippedElements)
        {
            bool hasDrawableBounds = bounds.Width > 0 && bounds.Height > 0;

            // 绘制背景
            if (hasDrawableBounds && element.BackgroundColor.HasValue && element.BackgroundColor.Value.A > 0)
            {
                var color = ToGdiColor(element.BackgroundColor.Value);
                using var brush = new SolidBrush(color);

                if (element.BorderRadius > 0)
                {
                    using var path = CreateRoundedRect(bounds, element.BorderRadius);
                    g.FillPath(brush, path);
                }
                else
                {
                    g.FillRectangle(brush, bounds);
                }
            }

            // 绘制边框
            if (hasDrawableBounds && element.BorderWidth > 0 && element.BorderStyle != Core.BorderStyle.None && element.BorderColor.HasValue)
            {
                var borderColor = ToGdiColor(element.BorderColor.Value);
                var dashStyle = element.BorderStyle switch
                {
                    Core.BorderStyle.Dashed => System.Drawing.Drawing2D.DashStyle.Dash,
                    Core.BorderStyle.Dotted => System.Drawing.Drawing2D.DashStyle.Dot,
                    _ => System.Drawing.Drawing2D.DashStyle.Solid
                };

                using var pen = new Pen(borderColor, element.BorderWidth) { DashStyle = dashStyle };

                if (element.BorderRadius > 0)
                {
                    using var path = CreateRoundedRect(bounds, element.BorderRadius);
                    g.DrawPath(pen, path);
                }
                else
                {
                    g.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width, bounds.Height);
                }
            }

            // 裁剪子元素（如果 Overflow != Visible）
            var childClip = clipRect;
            GraphicsState? savedState = null;

            if (element.Overflow != Overflow.Visible)
            {
                savedState = g.Save();
                var clipRegion = new RectangleF(
                    Math.Max(bounds.X, clipRect.X),
                    Math.Max(bounds.Y, clipRect.Y),
                    Math.Min(bounds.Right, clipRect.Right) - Math.Max(bounds.X, clipRect.X),
                    Math.Min(bounds.Bottom, clipRect.Bottom) - Math.Max(bounds.Y, clipRect.Y));

                if (clipRegion.Width > 0 && clipRegion.Height > 0)
                {
                    g.SetClip(clipRegion);
                    childClip = clipRegion;
                }
                else
                {
                    g.Restore(savedState);
                    return; // 完全被裁剪
                }
            }

            // 绘制子元素
            foreach (var child in element.Children)
            {
                PaintElement(g, child, childClip, skippedElements);
            }

            if (savedState != null)
            {
                g.Restore(savedState);
            }

            // 绘制滚动条（如果需要）
            if (element.Overflow == Overflow.Auto || element.Overflow == Overflow.Scroll)
            {
                PaintScrollbar(g, element, bounds);
            }
        }

        private static void PaintText(Graphics g, Win32Element element, RectangleF bounds, RectangleF clipRect)
        {
            if (string.IsNullOrEmpty(element.Text)) return;

            var fontSize = element.FontSize > 0 ? element.FontSize : 14f;
            var fontStyle = FontStyle.Regular;
            if (element.FontWeight != null)
            {
                var weight = element.FontWeight.ToLower();
                if (weight is "bold" or "semibold" or "500" or "600" or "700" or "800" or "900")
                    fontStyle = FontStyle.Bold;
            }

            var fontFamily = element.FontFamily;
            if (string.IsNullOrEmpty(fontFamily))
            {
                // 如果文本包含 Emoji 或特殊符号，优先使用 Segoe UI Emoji
                // 简单的启发式检查：代理对（常见 Emoji）或特定符号范围
                if (IsLikelyEmojiOrSymbol(element.Text))
                {
                    fontFamily = "Segoe UI Emoji";
                }
                else
                {
                    fontFamily = "Segoe UI";
                }
            }
            using var font = new Font(fontFamily, fontSize, fontStyle, GraphicsUnit.Pixel);

            var color = element.TextColor.HasValue
                ? ToGdiColor(element.TextColor.Value)
                : System.Drawing.Color.Black;
            using var brush = new SolidBrush(color);

            var format = new StringFormat
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Near,
                FormatFlags = element.NoWrap ? StringFormatFlags.NoWrap : 0
            };

            g.DrawString(element.Text, font, brush, bounds, format);
        }

        private static void PaintInputBackground(Graphics g, Win32Element element, RectangleF bounds)
        {
            // Input 的背景和边框由渲染器绘制，内容由原生 Edit 控件绘制
            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            // 绘制背景
            if (element.BackgroundColor.HasValue)
            {
                using var brush = new SolidBrush(ToGdiColor(element.BackgroundColor.Value));
                
                if (element.BorderRadius > 0)
                {
                    using var path = CreateRoundedRect(bounds, element.BorderRadius);
                    g.FillPath(brush, path);
                }
                else
                {
                    g.FillRectangle(brush, bounds);
                }
            }

            // 绘制边框
            var effectiveBorderColor = element.IsFocused && element.FocusedBorderColor.HasValue
                ? element.FocusedBorderColor
                : element.BorderColor;
            if (element.BorderWidth > 0 && element.BorderStyle != Core.BorderStyle.None && effectiveBorderColor.HasValue)
            {
                var borderColor = ToGdiColor(effectiveBorderColor.Value);
                var dashStyle = element.BorderStyle switch
                {
                    Core.BorderStyle.Dashed => System.Drawing.Drawing2D.DashStyle.Dash,
                    Core.BorderStyle.Dotted => System.Drawing.Drawing2D.DashStyle.Dot,
                    _ => System.Drawing.Drawing2D.DashStyle.Solid
                };

                using var pen = new Pen(borderColor, element.BorderWidth) { DashStyle = dashStyle };

                if (element.BorderRadius > 0)
                {
                    using var path = CreateRoundedRect(bounds, element.BorderRadius);
                    g.DrawPath(pen, path);
                }
                else
                {
                    g.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width, bounds.Height);
                }
            }
        }

        private static void PaintScrollbar(Graphics g, Win32Element element, RectangleF bounds)
        {
            float contentWidth = FlexLayout.MeasureContentWidth(element, bounds.Width, bounds.Height);
            float contentHeight = FlexLayout.MeasureContentHeight(element, bounds.Width, bounds.Height);
            bool alwaysShow = element.Overflow == Overflow.Scroll;
            bool showVertical = alwaysShow || contentHeight > element.LayoutHeight;
            bool showHorizontal = alwaysShow || contentWidth > element.LayoutWidth;

            if (!showVertical && !showHorizontal) return;

            const float scrollbarSize = 6;
            using var trackBrush = new SolidBrush(System.Drawing.Color.FromArgb(36, 128, 128, 128));
            using var thumbBrush = new SolidBrush(System.Drawing.Color.FromArgb(128, 128, 128, 128));

            if (showVertical)
            {
                float trackHeight = Math.Max(0, bounds.Height - (showHorizontal ? scrollbarSize + 2 : 0));
                var trackRect = new RectangleF(bounds.Right - scrollbarSize - 2, bounds.Y, scrollbarSize, trackHeight);
                if (alwaysShow && trackRect.Width > 0 && trackRect.Height > 0)
                {
                    using var trackPath = CreateRoundedRect(trackRect, scrollbarSize / 2);
                    g.FillPath(trackBrush, trackPath);
                }

                float maxScroll = Math.Max(0, contentHeight - element.LayoutHeight);
                float thumbHeight = maxScroll > 0 && contentHeight > 0
                    ? Math.Max(20, trackHeight * (element.LayoutHeight / contentHeight))
                    : trackHeight;
                float thumbY = maxScroll > 0
                    ? bounds.Y + (element.ScrollOffsetY / maxScroll) * Math.Max(0, trackHeight - thumbHeight)
                    : bounds.Y;
                var thumbRect = new RectangleF(bounds.Right - scrollbarSize - 2, thumbY, scrollbarSize, Math.Max(0, thumbHeight));

                if (thumbRect.Width > 0 && thumbRect.Height > 0)
                {
                    using var thumbPath = CreateRoundedRect(thumbRect, scrollbarSize / 2);
                    g.FillPath(thumbBrush, thumbPath);
                }
            }

            if (showHorizontal)
            {
                float trackWidth = Math.Max(0, bounds.Width - (showVertical ? scrollbarSize + 2 : 0));
                var trackRect = new RectangleF(bounds.X, bounds.Bottom - scrollbarSize - 2, trackWidth, scrollbarSize);
                if (alwaysShow && trackRect.Width > 0 && trackRect.Height > 0)
                {
                    using var trackPath = CreateRoundedRect(trackRect, scrollbarSize / 2);
                    g.FillPath(trackBrush, trackPath);
                }

                float maxScroll = Math.Max(0, contentWidth - element.LayoutWidth);
                float thumbWidth = maxScroll > 0 && contentWidth > 0
                    ? Math.Max(20, trackWidth * (element.LayoutWidth / contentWidth))
                    : trackWidth;
                float thumbX = maxScroll > 0
                    ? bounds.X + (element.ScrollOffsetX / maxScroll) * Math.Max(0, trackWidth - thumbWidth)
                    : bounds.X;
                var thumbRect = new RectangleF(thumbX, bounds.Bottom - scrollbarSize - 2, Math.Max(0, thumbWidth), scrollbarSize);

                if (thumbRect.Width > 0 && thumbRect.Height > 0)
                {
                    using var thumbPath = CreateRoundedRect(thumbRect, scrollbarSize / 2);
                    g.FillPath(thumbBrush, thumbPath);
                }
            }
        }

        private static void PaintImage(Graphics g, Win32Element element, RectangleF bounds)
        {
            if (element.NativeImage == null) return;
            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            // 如果有圆角，应用裁剪
            GraphicsContainer? container = null;
            if (element.BorderRadius > 0)
            {
                container = g.BeginContainer();
                using var path = CreateRoundedRect(bounds, element.BorderRadius);
                g.SetClip(path);
            }

            // 绘制图片 (缩放以填充)
            g.DrawImage(element.NativeImage, bounds);

            if (container != null)
            {
                g.EndContainer(container);
            }
        }

        // --- 辅助方法 ---

        private static System.Drawing.Color ToGdiColor(Core.Color c)
        {
            return System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B);
        }

        private static GraphicsPath CreateRoundedRect(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            float diameter = radius * 2;

            if (diameter > rect.Width) diameter = rect.Width;
            if (diameter > rect.Height) diameter = rect.Height;

            var arc = new RectangleF(rect.X, rect.Y, diameter, diameter);

            // 左上角
            path.AddArc(arc, 180, 90);
            // 右上角
            arc.X = rect.Right - diameter;
            path.AddArc(arc, 270, 90);
            // 右下角
            arc.Y = rect.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            // 左下角
            arc.X = rect.X;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return path;
        }

        private static bool IsLikelyEmojiOrSymbol(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (var c in text)
            {
                // 检查代理对（Emoji 通常在非基本多语言平面）
                if (char.IsSurrogate(c)) return true;

                // 检查特定符号区域（例如 Dingbats, Geometric Shapes, Misc Symbols 等）
                // 0x2000-0x33FF 涵盖了常见标点、符号、箭头、数学符号、CJK 符号等
                // 其中包含了常见 Emoji 所在的 Misc Symbols (2600-26FF), Dingbats (2700-27BF)
                if (c >= 0x2000 && c <= 0x33FF) return true;
            }
            return false;
        }
    }
}

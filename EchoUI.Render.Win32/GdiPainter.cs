using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using EchoUI.Core;

namespace EchoUI.Render.Win32
{
    internal readonly record struct CpuBitmapSurface(nint Bits, int Width, int Height, int Stride)
    {
        public bool IsValid => Bits != 0 && Width > 0 && Height > 0 && Stride >= Width * sizeof(uint);
    }

    /// <summary>
    /// GDI 绘制引擎，遍历 Win32Element 树并绘制到 HDC。
    /// </summary>
    internal static class GdiPainter
    {
        [ThreadStatic]
        private static CpuBitmapSurface? s_bitmapSurface;

        [ThreadStatic]
        private static RectF s_effectiveClipRect;

        public static void Paint(nint hdc, Win32Element root, IReadOnlyCollection<Win32Element>? floatingElements, float viewportWidth, float viewportHeight, RectF? dirtyRect = null, CpuBitmapSurface? bitmapSurface = null)
        {
            using var gdiPlusScope = GdiPlus.BeginDraw(hdc);
            var viewportRect = new RectF(0, 0, viewportWidth, viewportHeight);
            var paintRect = dirtyRect.HasValue ? RectF.Intersect(viewportRect, dirtyRect.Value) : viewportRect;
            if (paintRect.Width <= 0 || paintRect.Height <= 0)
                return;

            var savedState = NativeInterop.SaveDC(hdc);
            var gdiPlusState = GdiPlus.SaveGraphics();
            var previousSurface = s_bitmapSurface;
            var previousClipRect = s_effectiveClipRect;

            try
            {
                s_bitmapSurface = bitmapSurface.HasValue && bitmapSurface.Value.IsValid ? bitmapSurface : null;
                s_effectiveClipRect = paintRect;
                var clip = ToRect(paintRect);
                NativeInterop.IntersectClipRect(hdc, clip.Left, clip.Top, clip.Right, clip.Bottom);
                GdiPlus.IntersectClip(paintRect);

                FillSolidRect(hdc, paintRect, Core.Color.White);

                PaintElement(hdc, root, paintRect, floatingElements);

                if (floatingElements != null)
                {
                    foreach (var floatElem in floatingElements)
                    {
                        PaintElement(hdc, floatElem, paintRect, null);
                    }
                }
            }
            finally
            {
                s_bitmapSurface = previousSurface;
                s_effectiveClipRect = previousClipRect;
                GdiPlus.RestoreGraphics(gdiPlusState);
                if (savedState != 0)
                    NativeInterop.RestoreDC(hdc, savedState);
            }
        }

        public static void ReleaseCachedResources(Win32Element element)
        {
            GdiPlus.ReleaseElementPaths(element);
        }

        private static void PaintElement(nint hdc, Win32Element element, RectF clipRect, IReadOnlyCollection<Win32Element>? skippedElements)
        {
            if (skippedElements != null && skippedElements.Contains(element))
                return;

            var bounds = element.GetAbsoluteBounds();

            if (bounds.Right < clipRect.Left || bounds.Left > clipRect.Right ||
                bounds.Bottom < clipRect.Top || bounds.Top > clipRect.Bottom)
            {
                if (element.Overflow != Overflow.Visible || element.Children.Count == 0)
                    return;
            }

            switch (element.ElementType)
            {
                case ElementCoreName.Container:
                    PaintContainer(hdc, element, bounds, clipRect, skippedElements);
                    break;
                case ElementCoreName.Text:
                    PaintText(hdc, element, bounds);
                    break;
                case ElementCoreName.Input:
                    PaintInputBackground(hdc, element, bounds);
                    break;
                case "img":
                    PaintImage(hdc, element, bounds);
                    break;
                default:
                    PaintContainer(hdc, element, bounds, clipRect, skippedElements);
                    break;
            }
        }

        private static void PaintContainer(nint hdc, Win32Element element, RectF bounds, RectF clipRect, IReadOnlyCollection<Win32Element>? skippedElements)
        {
            var hasDrawableBounds = bounds.Width > 0 && bounds.Height > 0;

            if (hasDrawableBounds && element.BackgroundColor.HasValue && element.BackgroundColor.Value.A > 0)
            {
                FillShape(hdc, element, bounds, element.BackgroundColor.Value, element.BorderRadius);
            }

            if (hasDrawableBounds && element.BorderWidth > 0 && element.BorderStyle != Core.BorderStyle.None && element.BorderColor.HasValue)
            {
                DrawBorder(hdc, element, bounds, element.BorderColor.Value, element.BorderWidth, element.BorderRadius, element.BorderStyle);
            }

            var childClip = clipRect;
            var savedState = 0;
            uint gdiPlusState = 0;
            var previousClipRect = s_effectiveClipRect;
            var clipChanged = false;

            if (element.Overflow != Overflow.Visible)
            {
                savedState = NativeInterop.SaveDC(hdc);
                var clipRegion = RectF.Intersect(bounds, clipRect);

                if (clipRegion.Width > 0 && clipRegion.Height > 0)
                {
                    var clip = ToRect(clipRegion);
                    NativeInterop.IntersectClipRect(hdc, clip.Left, clip.Top, clip.Right, clip.Bottom);
                    gdiPlusState = GdiPlus.SaveGraphics();
                    GdiPlus.IntersectClip(clipRegion);
                    childClip = clipRegion;
                    s_effectiveClipRect = clipRegion;
                    clipChanged = true;
                }
                else
                {
                    if (savedState != 0)
                        NativeInterop.RestoreDC(hdc, savedState);
                    return;
                }
            }

            foreach (var child in element.Children)
            {
                PaintElement(hdc, child, childClip, skippedElements);
            }

            if (clipChanged)
            {
                s_effectiveClipRect = previousClipRect;
            }

            if (gdiPlusState != 0)
            {
                GdiPlus.RestoreGraphics(gdiPlusState);
            }

            if (savedState != 0)
            {
                NativeInterop.RestoreDC(hdc, savedState);
            }

            if (element.Overflow == Overflow.Auto || element.Overflow == Overflow.Scroll)
            {
                PaintScrollbar(hdc, element, bounds);
            }
        }

        private static void PaintText(nint hdc, Win32Element element, RectF bounds)
        {
            if (string.IsNullOrEmpty(element.Text)) return;

            var fontSize = element.FontSize > 0 ? element.FontSize : 14f;
            var color = element.TextColor ?? Core.Color.Black;
            GdiPlus.Flush();
            GdiText.DrawText(hdc, element.Text, element.FontFamily, fontSize, element.FontWeight, color, bounds, element.NoWrap);
        }

        private static void PaintInputBackground(nint hdc, Win32Element element, RectF bounds)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            if (element.BackgroundColor.HasValue)
            {
                FillShape(hdc, element, bounds, element.BackgroundColor.Value, element.BorderRadius);
            }

            var effectiveBorderColor = element.IsFocused && element.FocusedBorderColor.HasValue
                ? element.FocusedBorderColor
                : element.BorderColor;

            if (element.BorderWidth > 0 && element.BorderStyle != Core.BorderStyle.None && effectiveBorderColor.HasValue)
            {
                DrawBorder(hdc, element, bounds, effectiveBorderColor.Value, element.BorderWidth, element.BorderRadius, element.BorderStyle);
            }
        }

        private static void PaintScrollbar(nint hdc, Win32Element element, RectF bounds)
        {
            var contentWidth = FlexLayout.MeasureContentWidth(element, bounds.Width, bounds.Height);
            var contentHeight = FlexLayout.MeasureContentHeight(element, bounds.Width, bounds.Height);
            var alwaysShow = element.Overflow == Overflow.Scroll;
            var showVertical = alwaysShow || contentHeight > element.LayoutHeight;
            var showHorizontal = alwaysShow || contentWidth > element.LayoutWidth;

            if (!showVertical && !showHorizontal) return;

            const float scrollbarSize = 6;
            var trackColor = new Core.Color(237, 237, 237);
            var thumbColor = new Core.Color(128, 128, 128);

            if (showVertical)
            {
                var trackHeight = Math.Max(0, bounds.Height - (showHorizontal ? scrollbarSize + 2 : 0));
                var trackRect = new RectF(bounds.Right - scrollbarSize - 2, bounds.Y, scrollbarSize, trackHeight);
                if (alwaysShow && trackRect.Width > 0 && trackRect.Height > 0)
                {
                    FillShape(hdc, null, trackRect, trackColor, scrollbarSize / 2);
                }

                var maxScroll = Math.Max(0, contentHeight - element.LayoutHeight);
                var thumbHeight = maxScroll > 0 && contentHeight > 0
                    ? Math.Max(20, trackHeight * (element.LayoutHeight / contentHeight))
                    : trackHeight;
                var thumbY = maxScroll > 0
                    ? bounds.Y + (element.ScrollOffsetY / maxScroll) * Math.Max(0, trackHeight - thumbHeight)
                    : bounds.Y;
                var thumbRect = new RectF(bounds.Right - scrollbarSize - 2, thumbY, scrollbarSize, Math.Max(0, thumbHeight));

                if (thumbRect.Width > 0 && thumbRect.Height > 0)
                {
                    FillShape(hdc, null, thumbRect, thumbColor, scrollbarSize / 2);
                }
            }

            if (showHorizontal)
            {
                var trackWidth = Math.Max(0, bounds.Width - (showVertical ? scrollbarSize + 2 : 0));
                var trackRect = new RectF(bounds.X, bounds.Bottom - scrollbarSize - 2, trackWidth, scrollbarSize);
                if (alwaysShow && trackRect.Width > 0 && trackRect.Height > 0)
                {
                    FillShape(hdc, null, trackRect, trackColor, scrollbarSize / 2);
                }

                var maxScroll = Math.Max(0, contentWidth - element.LayoutWidth);
                var thumbWidth = maxScroll > 0 && contentWidth > 0
                    ? Math.Max(20, trackWidth * (element.LayoutWidth / contentWidth))
                    : trackWidth;
                var thumbX = maxScroll > 0
                    ? bounds.X + (element.ScrollOffsetX / maxScroll) * Math.Max(0, trackWidth - thumbWidth)
                    : bounds.X;
                var thumbRect = new RectF(thumbX, bounds.Bottom - scrollbarSize - 2, Math.Max(0, thumbWidth), scrollbarSize);

                if (thumbRect.Width > 0 && thumbRect.Height > 0)
                {
                    FillShape(hdc, null, thumbRect, thumbColor, scrollbarSize / 2);
                }
            }
        }

        private static void PaintImage(nint hdc, Win32Element element, RectF bounds)
        {
            if (element.NativeImageHandle == 0 || bounds.Width <= 0 || bounds.Height <= 0) return;

            GdiPlus.Flush();
            var imageDc = NativeInterop.CreateCompatibleDC(hdc);
            if (imageDc == 0) return;

            var oldBitmap = NativeInterop.SelectObject(imageDc, element.NativeImageHandle);
            try
            {
                var rect = ToRect(bounds);
                NativeInterop.SetStretchBltMode(hdc, NativeInterop.HALFTONE);
                NativeInterop.StretchBlt(
                    hdc,
                    rect.Left,
                    rect.Top,
                    Math.Max(1, rect.Right - rect.Left),
                    Math.Max(1, rect.Bottom - rect.Top),
                    imageDc,
                    0,
                    0,
                    element.NativeImageWidth,
                    element.NativeImageHeight,
                    NativeInterop.SRCCOPY);
            }
            finally
            {
                if (oldBitmap != 0)
                    NativeInterop.SelectObject(imageDc, oldBitmap);
                NativeInterop.DeleteDC(imageDc);
            }
        }

        private static void FillSolidRect(nint hdc, RectF rect, Core.Color color)
        {
            if (rect.Width <= 0 || rect.Height <= 0 || color.A == 0) return;

            GdiPlus.Flush();
            if (CpuRasterizer.TryFillRect(s_bitmapSurface, s_effectiveClipRect, rect, color))
                return;

            var brush = GdiResourceCache.GetSolidBrush(ToColorRef(color));
            if (brush == 0) return;

            var nativeRect = ToRect(rect);
            NativeInterop.FillRect(hdc, ref nativeRect, brush);
        }

        private static void FillShape(nint hdc, Win32Element? element, RectF rect, Core.Color color, float radius)
        {
            if (rect.Width <= 0 || rect.Height <= 0 || color.A == 0) return;

            if (radius <= 0)
            {
                FillSolidRect(hdc, rect, color);
                return;
            }

            if (GdiPlus.FillRoundedRectangle(element, rect, color, radius))
                return;

            GdiPlus.Flush();
            var brush = GdiResourceCache.GetSolidBrush(ToColorRef(color));
            if (brush == 0) return;

            var oldBrush = NativeInterop.SelectObject(hdc, brush);
            var oldPen = NativeInterop.SelectObject(hdc, NativeInterop.GetStockObject(NativeInterop.NULL_PEN));

            try
            {
                var nativeRect = ToRect(rect);
                var diameter = Math.Max(1, (int)Math.Round(Math.Min(radius * 2, Math.Min(rect.Width, rect.Height))));
                NativeInterop.RoundRect(hdc, nativeRect.Left, nativeRect.Top, nativeRect.Right, nativeRect.Bottom, diameter, diameter);
            }
            finally
            {
                if (oldBrush != 0)
                    NativeInterop.SelectObject(hdc, oldBrush);
                if (oldPen != 0)
                    NativeInterop.SelectObject(hdc, oldPen);
            }
        }

        private static void DrawBorder(nint hdc, Win32Element? element, RectF rect, Core.Color color, float width, float radius, Core.BorderStyle style)
        {
            if (rect.Width <= 0 || rect.Height <= 0 || color.A == 0) return;

            if (radius <= 0 && style == Core.BorderStyle.Solid && CpuRasterizer.TryDrawRectBorder(s_bitmapSurface, s_effectiveClipRect, rect, color, width))
                return;

            if (radius > 0 && GdiPlus.DrawRoundedRectangle(element, rect, color, width, radius, style))
                return;

            GdiPlus.Flush();
            var penStyle = style switch
            {
                Core.BorderStyle.Dashed => NativeInterop.PS_DASH,
                Core.BorderStyle.Dotted => NativeInterop.PS_DOT,
                _ => NativeInterop.PS_SOLID
            };
            var pen = NativeInterop.CreatePen(penStyle, Math.Max(1, (int)Math.Round(width)), ToColorRef(color));
            if (pen == 0) return;

            var oldPen = NativeInterop.SelectObject(hdc, pen);
            var oldBrush = NativeInterop.SelectObject(hdc, NativeInterop.GetStockObject(NativeInterop.NULL_BRUSH));

            try
            {
                var nativeRect = ToRect(rect);
                if (radius > 0)
                {
                    var diameter = Math.Max(1, (int)Math.Round(Math.Min(radius * 2, Math.Min(rect.Width, rect.Height))));
                    NativeInterop.RoundRect(hdc, nativeRect.Left, nativeRect.Top, nativeRect.Right, nativeRect.Bottom, diameter, diameter);
                }
                else
                {
                    NativeInterop.Rectangle(hdc, nativeRect.Left, nativeRect.Top, nativeRect.Right, nativeRect.Bottom);
                }
            }
            finally
            {
                if (oldPen != 0)
                    NativeInterop.SelectObject(hdc, oldPen);
                if (oldBrush != 0)
                    NativeInterop.SelectObject(hdc, oldBrush);
                NativeInterop.DeleteObject(pen);
            }
        }

        private static NativeInterop.RECT ToRect(RectF rect)
        {
            return new NativeInterop.RECT
            {
                Left = (int)Math.Floor(rect.Left),
                Top = (int)Math.Floor(rect.Top),
                Right = (int)Math.Ceiling(rect.Right),
                Bottom = (int)Math.Ceiling(rect.Bottom)
            };
        }

        private static int ToColorRef(Core.Color color)
        {
            if (color.A < 255)
            {
                color = BlendOverWhite(color);
            }

            return color.R | (color.G << 8) | (color.B << 16);
        }

        private static Core.Color BlendOverWhite(Core.Color color)
        {
            var alpha = color.A / 255f;
            return new Core.Color(
                (byte)Math.Round(color.R * alpha + 255 * (1 - alpha)),
                (byte)Math.Round(color.G * alpha + 255 * (1 - alpha)),
                (byte)Math.Round(color.B * alpha + 255 * (1 - alpha)));
        }
    }

    internal static class CpuRasterizer
    {
        public static unsafe bool TryFillRect(CpuBitmapSurface? surface, RectF clipRect, RectF rect, Core.Color color)
        {
            if (!surface.HasValue || !surface.Value.IsValid || color.A == 0)
                return false;

            var targetRect = RectF.Intersect(RectF.Intersect(new RectF(0, 0, surface.Value.Width, surface.Value.Height), clipRect), rect);
            if (targetRect.Width <= 0 || targetRect.Height <= 0)
                return false;

            int left = Math.Max(0, (int)Math.Floor(targetRect.Left));
            int top = Math.Max(0, (int)Math.Floor(targetRect.Top));
            int right = Math.Min(surface.Value.Width, (int)Math.Ceiling(targetRect.Right));
            int bottom = Math.Min(surface.Value.Height, (int)Math.Ceiling(targetRect.Bottom));
            int width = right - left;
            int height = bottom - top;

            if (width <= 0 || height <= 0)
                return false;

            var packedColor = ToOpaqueBgra(color);
            var rowStart = (byte*)surface.Value.Bits + top * surface.Value.Stride + left * sizeof(uint);
            FillRectCore(rowStart, surface.Value.Stride, width, height, packedColor);
            return true;
        }

        public static bool TryDrawRectBorder(CpuBitmapSurface? surface, RectF clipRect, RectF rect, Core.Color color, float width)
        {
            if (!surface.HasValue || !surface.Value.IsValid || color.A == 0 || width <= 0)
                return false;

            int thickness = Math.Max(1, (int)Math.Ceiling(width));
            if (thickness * 2 >= rect.Width || thickness * 2 >= rect.Height)
                return TryFillRect(surface, clipRect, rect, color);

            var top = new RectF(rect.X, rect.Y, rect.Width, thickness);
            var bottom = new RectF(rect.X, rect.Bottom - thickness, rect.Width, thickness);
            var sideHeight = Math.Max(0, rect.Height - thickness * 2);
            var left = new RectF(rect.X, rect.Y + thickness, thickness, sideHeight);
            var right = new RectF(rect.Right - thickness, rect.Y + thickness, thickness, sideHeight);

            var drawn = TryFillRect(surface, clipRect, top, color);
            drawn |= TryFillRect(surface, clipRect, bottom, color);
            if (sideHeight > 0)
            {
                drawn |= TryFillRect(surface, clipRect, left, color);
                drawn |= TryFillRect(surface, clipRect, right, color);
            }

            return drawn;
        }

        private static unsafe void FillRectCore(byte* rowStart, int stride, int width, int height, uint packedColor)
        {
            if (Avx2.IsSupported && width >= 8)
            {
                var vector = Vector256.Create(packedColor).AsSingle();
                for (int y = 0; y < height; y++)
                {
                    var row = (uint*)(rowStart + y * stride);
                    int x = 0;
                    int simdLimit = width - 8;
                    for (; x <= simdLimit; x += 8)
                    {
                        Avx.Store((float*)(row + x), vector);
                    }

                    for (; x < width; x++)
                    {
                        row[x] = packedColor;
                    }
                }

                return;
            }

            if (Sse2.IsSupported && width >= 4)
            {
                var vector = Vector128.Create(packedColor).AsInt32();
                for (int y = 0; y < height; y++)
                {
                    var row = (uint*)(rowStart + y * stride);
                    int x = 0;
                    int simdLimit = width - 4;
                    for (; x <= simdLimit; x += 4)
                    {
                        Sse2.Store((int*)(row + x), vector);
                    }

                    for (; x < width; x++)
                    {
                        row[x] = packedColor;
                    }
                }

                return;
            }

            for (int y = 0; y < height; y++)
            {
                var row = (uint*)(rowStart + y * stride);
                for (int x = 0; x < width; x++)
                {
                    row[x] = packedColor;
                }
            }
        }

        private static uint ToOpaqueBgra(Core.Color color)
        {
            if (color.A < 255)
            {
                color = BlendOverWhite(color);
            }

            return ((uint)255 << 24) | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B;
        }

        private static Core.Color BlendOverWhite(Core.Color color)
        {
            int alpha = color.A;
            int inverseAlpha = 255 - alpha;
            return new Core.Color(
                (byte)((color.R * alpha + 255 * inverseAlpha + 127) / 255),
                (byte)((color.G * alpha + 255 * inverseAlpha + 127) / 255),
                (byte)((color.B * alpha + 255 * inverseAlpha + 127) / 255));
        }
    }

    internal static class GdiResourceCache
    {
        private static readonly object Lock = new();
        private static readonly Dictionary<int, nint> SolidBrushes = [];

        static GdiResourceCache()
        {
            AppDomain.CurrentDomain.ProcessExit += static (_, _) => Clear();
        }

        public static nint GetSolidBrush(int colorRef)
        {
            lock (Lock)
            {
                if (SolidBrushes.TryGetValue(colorRef, out var brush))
                    return brush;

                brush = NativeInterop.CreateSolidBrush(colorRef);
                if (brush != 0)
                    SolidBrushes[colorRef] = brush;

                return brush;
            }
        }

        private static void Clear()
        {
            lock (Lock)
            {
                foreach (var brush in SolidBrushes.Values)
                    NativeInterop.DeleteObject(brush);

                SolidBrushes.Clear();
            }
        }
    }

    internal static class GdiPlus
    {
        [ThreadStatic]
        private static nint s_graphics;

        [ThreadStatic]
        private static int s_drawDepth;

        [ThreadStatic]
        private static bool s_needsFlush;

        private static readonly object StartupLock = new();
        private static readonly object ResourceLock = new();
        private static readonly Dictionary<uint, nint> SolidBrushes = [];
        private static readonly Dictionary<PenKey, nint> Pens = [];
        private static nint s_token;
        private static bool s_startupAttempted;

        public static IDisposable BeginDraw(nint hdc)
        {
            if (hdc == 0 || !EnsureStarted())
                return EmptyScope.Instance;

            s_drawDepth++;
            if (s_drawDepth == 1)
            {
                if (NativeInterop.GdipCreateFromHDC(hdc, out s_graphics) != NativeInterop.GdipOk)
                {
                    s_graphics = 0;
                }
                else
                {
                    NativeInterop.GdipSetSmoothingMode(s_graphics, NativeInterop.SmoothingModeAntiAlias);
                    NativeInterop.GdipSetPixelOffsetMode(s_graphics, NativeInterop.PixelOffsetModeHalf);
                }
            }

            return DrawScope.Instance;
        }

        public static bool FillRoundedRectangle(Win32Element? element, RectF rect, Core.Color color, float radius)
        {
            if (s_graphics == 0)
                return false;

            var ownsPath = element == null;
            var path = ownsPath
                ? CreateTransientRoundedPath(rect, radius)
                : GetRoundedFillPath(element!, rect, radius);
            if (path == 0)
                return false;

            try
            {
                var brush = GetSolidBrush(ToArgb(color));
                return brush != 0 && MarkDrawn(NativeInterop.GdipFillPath(s_graphics, brush, path));
            }
            finally
            {
                if (ownsPath)
                    NativeInterop.GdipDeletePath(path);
            }
        }

        public static bool DrawRoundedRectangle(Win32Element? element, RectF rect, Core.Color color, float width, float radius, Core.BorderStyle style)
        {
            if (s_graphics == 0 || style == Core.BorderStyle.None)
                return false;

            width = Math.Max(1, width);
            var inset = width / 2f;
            var strokeRect = new RectF(
                rect.X + inset,
                rect.Y + inset,
                Math.Max(0, rect.Width - width),
                Math.Max(0, rect.Height - width));

            if (strokeRect.Width <= 0 || strokeRect.Height <= 0)
                return false;

            var ownsPath = element == null;
            var path = ownsPath
                ? CreateTransientRoundedPath(strokeRect, Math.Max(0, radius - inset))
                : GetRoundedBorderPath(element!, strokeRect, Math.Max(0, radius - inset));
            if (path == 0)
                return false;

            try
            {
                var pen = GetPen(ToArgb(color), width, style);
                return pen != 0 && MarkDrawn(NativeInterop.GdipDrawPath(s_graphics, pen, path));
            }
            finally
            {
                if (ownsPath)
                    NativeInterop.GdipDeletePath(path);
            }
        }

        public static void ReleaseElementPaths(Win32Element element)
        {
            var fillPath = element.RoundedFillPath;
            DeletePath(ref fillPath);
            element.RoundedFillPath = fillPath;

            var borderPath = element.RoundedBorderPath;
            DeletePath(ref borderPath);
            element.RoundedBorderPath = borderPath;

            element.RoundedFillPathRadius = -1;
            element.RoundedBorderPathRadius = -1;
        }

        public static uint SaveGraphics()
        {
            return s_graphics != 0 && NativeInterop.GdipSaveGraphics(s_graphics, out var state) == NativeInterop.GdipOk
                ? state
                : 0;
        }

        public static void RestoreGraphics(uint state)
        {
            if (s_graphics != 0 && state != 0)
                NativeInterop.GdipRestoreGraphics(s_graphics, state);
        }

        public static void IntersectClip(RectF rect)
        {
            if (s_graphics != 0 && rect.Width > 0 && rect.Height > 0)
            {
                NativeInterop.GdipSetClipRect(
                    s_graphics,
                    rect.X,
                    rect.Y,
                    rect.Width,
                    rect.Height,
                    NativeInterop.CombineModeIntersect);
            }
        }

        public static void Flush()
        {
            if (s_graphics != 0 && s_needsFlush)
            {
                NativeInterop.GdipFlush(s_graphics, NativeInterop.FlushIntentionFlush);
                s_needsFlush = false;
            }
        }

        private static bool MarkDrawn(int status)
        {
            if (status == NativeInterop.GdipOk)
            {
                s_needsFlush = true;
                return true;
            }

            return false;
        }

        private static nint GetRoundedFillPath(Win32Element element, RectF rect, float radius)
        {
            if (element.RoundedFillPath != 0 && SameGeometry(element.RoundedFillPathBounds, rect) && element.RoundedFillPathRadius.Equals(radius))
                return element.RoundedFillPath;

            var cachedPath = element.RoundedFillPath;
            DeletePath(ref cachedPath);
            element.RoundedFillPath = cachedPath;
            if (!CreateRoundedRectanglePath(rect, radius, out var path))
                return 0;

            element.RoundedFillPath = path;
            element.RoundedFillPathBounds = rect;
            element.RoundedFillPathRadius = radius;
            return path;
        }

        private static nint GetRoundedBorderPath(Win32Element element, RectF rect, float radius)
        {
            if (element.RoundedBorderPath != 0 && SameGeometry(element.RoundedBorderPathBounds, rect) && element.RoundedBorderPathRadius.Equals(radius))
                return element.RoundedBorderPath;

            var cachedPath = element.RoundedBorderPath;
            DeletePath(ref cachedPath);
            element.RoundedBorderPath = cachedPath;
            if (!CreateRoundedRectanglePath(rect, radius, out var path))
                return 0;

            element.RoundedBorderPath = path;
            element.RoundedBorderPathBounds = rect;
            element.RoundedBorderPathRadius = radius;
            return path;
        }

        private static nint CreateTransientRoundedPath(RectF rect, float radius)
        {
            return CreateRoundedRectanglePath(rect, radius, out var path) ? path : 0;
        }

        private static bool SameGeometry(RectF a, RectF b)
        {
            return a.X.Equals(b.X) && a.Y.Equals(b.Y) && a.Width.Equals(b.Width) && a.Height.Equals(b.Height);
        }

        private static nint GetSolidBrush(uint argb)
        {
            lock (ResourceLock)
            {
                if (SolidBrushes.TryGetValue(argb, out var brush))
                    return brush;

                if (NativeInterop.GdipCreateSolidFill(argb, out brush) != NativeInterop.GdipOk)
                    return 0;

                SolidBrushes[argb] = brush;
                return brush;
            }
        }

        private static nint GetPen(uint argb, float width, Core.BorderStyle style)
        {
            var key = new PenKey(argb, width, style);

            lock (ResourceLock)
            {
                if (Pens.TryGetValue(key, out var pen))
                    return pen;

                if (NativeInterop.GdipCreatePen1(argb, key.Width, NativeInterop.UnitPixel, out pen) != NativeInterop.GdipOk)
                    return 0;

                NativeInterop.GdipSetPenDashStyle(pen, style switch
                {
                    Core.BorderStyle.Dashed => NativeInterop.DashStyleDash,
                    Core.BorderStyle.Dotted => NativeInterop.DashStyleDot,
                    _ => NativeInterop.DashStyleSolid
                });

                Pens[key] = pen;
                return pen;
            }
        }

        private static void DeletePath(ref nint path)
        {
            if (path != 0)
            {
                NativeInterop.GdipDeletePath(path);
                path = 0;
            }
        }

        private static bool CreateRoundedRectanglePath(RectF rect, float radius, out nint path)
        {
            path = 0;
            if (rect.Width <= 0 || rect.Height <= 0)
                return false;

            if (NativeInterop.GdipCreatePath(NativeInterop.FillModeAlternate, out path) != NativeInterop.GdipOk)
                return false;

            radius = Math.Clamp(radius, 0, Math.Min(rect.Width, rect.Height) / 2f);
            var status = NativeInterop.GdipStartPathFigure(path);

            if (radius <= 0)
            {
                status |= NativeInterop.GdipAddPathLine(path, rect.Left, rect.Top, rect.Right, rect.Top);
                status |= NativeInterop.GdipAddPathLine(path, rect.Right, rect.Top, rect.Right, rect.Bottom);
                status |= NativeInterop.GdipAddPathLine(path, rect.Right, rect.Bottom, rect.Left, rect.Bottom);
                status |= NativeInterop.GdipAddPathLine(path, rect.Left, rect.Bottom, rect.Left, rect.Top);
                status |= NativeInterop.GdipClosePathFigure(path);
                if (status == NativeInterop.GdipOk)
                    return true;

                NativeInterop.GdipDeletePath(path);
                path = 0;
                return false;
            }

            var diameter = radius * 2f;
            status |= NativeInterop.GdipAddPathArc(path, rect.Left, rect.Top, diameter, diameter, 180, 90);
            status |= NativeInterop.GdipAddPathLine(path, rect.Left + radius, rect.Top, rect.Right - radius, rect.Top);
            status |= NativeInterop.GdipAddPathArc(path, rect.Right - diameter, rect.Top, diameter, diameter, 270, 90);
            status |= NativeInterop.GdipAddPathLine(path, rect.Right, rect.Top + radius, rect.Right, rect.Bottom - radius);
            status |= NativeInterop.GdipAddPathArc(path, rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            status |= NativeInterop.GdipAddPathLine(path, rect.Right - radius, rect.Bottom, rect.Left + radius, rect.Bottom);
            status |= NativeInterop.GdipAddPathArc(path, rect.Left, rect.Bottom - diameter, diameter, diameter, 90, 90);
            status |= NativeInterop.GdipAddPathLine(path, rect.Left, rect.Bottom - radius, rect.Left, rect.Top + radius);
            status |= NativeInterop.GdipClosePathFigure(path);

            if (status == NativeInterop.GdipOk)
                return true;

            NativeInterop.GdipDeletePath(path);
            path = 0;
            return false;
        }

        private static bool EnsureStarted()
        {
            if (s_token != 0)
                return true;

            lock (StartupLock)
            {
                if (s_token != 0)
                    return true;

                if (s_startupAttempted)
                    return false;

                s_startupAttempted = true;
                var input = new NativeInterop.GdiplusStartupInput
                {
                    GdiplusVersion = 1
                };

                if (NativeInterop.GdiplusStartup(out s_token, ref input, 0) != NativeInterop.GdipOk)
                {
                    s_token = 0;
                    return false;
                }

                AppDomain.CurrentDomain.ProcessExit += static (_, _) => Shutdown();
                return true;
            }
        }

        private static void Shutdown()
        {
            lock (ResourceLock)
            {
                foreach (var brush in SolidBrushes.Values)
                    NativeInterop.GdipDeleteBrush(brush);
                SolidBrushes.Clear();

                foreach (var pen in Pens.Values)
                    NativeInterop.GdipDeletePen(pen);
                Pens.Clear();
            }

            lock (StartupLock)
            {
                if (s_token != 0)
                {
                    NativeInterop.GdiplusShutdown(s_token);
                    s_token = 0;
                }
            }
        }

        private static uint ToArgb(Core.Color color)
        {
            return ((uint)color.A << 24) | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B;
        }

        private readonly record struct PenKey(uint Argb, float Width, Core.BorderStyle Style);

        private sealed class DrawScope : IDisposable
        {
            public static readonly DrawScope Instance = new();

            public void Dispose()
            {
                if (s_drawDepth <= 0)
                    return;

                s_drawDepth--;
                if (s_drawDepth == 0 && s_graphics != 0)
                {
                    Flush();
                    NativeInterop.GdipDeleteGraphics(s_graphics);
                    s_graphics = 0;
                }
            }
        }

        private sealed class EmptyScope : IDisposable
        {
            public static readonly EmptyScope Instance = new();
            public void Dispose() { }
        }
    }

    internal static class GdiText
    {
        private const int MaxMeasureCacheEntries = 4096;
        private static readonly object CacheLock = new();
        private static readonly Dictionary<FontKey, nint> Fonts = [];
        private static readonly Dictionary<FontKey, float> LineHeights = [];
        private static readonly Dictionary<TextMeasureKey, TextMeasurementResult> MeasureCache = [];

        static GdiText()
        {
            AppDomain.CurrentDomain.ProcessExit += static (_, _) => ClearCaches();
        }

        public static TextMeasurementResult MeasureText(string? text, string? fontFamily, float fontSize, string? fontWeight, float? widthConstraint = null, bool noWrap = true)
        {
            text ??= string.Empty;
            fontSize = fontSize > 0 ? fontSize : 14f;
            var resolvedFamily = ResolveFontFamily(fontFamily, text);
            var fontKey = FontKey.Create(resolvedFamily, fontSize, fontWeight);
            var key = TextMeasureKey.Create(text, resolvedFamily, fontSize, fontWeight, widthConstraint, noWrap);

            lock (CacheLock)
            {
                if (MeasureCache.TryGetValue(key, out var cached))
                    return cached;
            }

            if (text.Length == 0)
            {
                var emptyResult = new TextMeasurementResult(0, GetOrCreateLineHeight(resolvedFamily, fontSize, fontWeight, fontKey));
                lock (CacheLock)
                {
                    if (MeasureCache.Count >= MaxMeasureCacheEntries)
                        MeasureCache.Clear();

                    MeasureCache[key] = emptyResult;
                }

                return emptyResult;
            }

            var hdc = NativeInterop.GetDC(0);
            if (hdc == 0)
                return new TextMeasurementResult(0, fontSize * 1.4f);

            var font = GetFontHandle(resolvedFamily, fontSize, fontWeight);
            var oldFont = font != 0 ? NativeInterop.SelectObject(hdc, font) : 0;

            try
            {
                var lineHeight = GetCachedLineHeight(fontKey);
                if (lineHeight <= 0)
                {
                    lineHeight = GetLineHeight(hdc, fontSize);
                    CacheLineHeight(fontKey, lineHeight);
                }

                TextMeasurementResult result;
                if (noWrap || widthConstraint == null || widthConstraint <= 0)
                {
                    result = NativeInterop.GetTextExtentPoint32(hdc, text, text.Length, out var size)
                        ? new TextMeasurementResult(size.cx, Math.Max(lineHeight, size.cy))
                        : new TextMeasurementResult(text.Length * fontSize * 0.6f, lineHeight);
                }
                else
                {
                    var rect = new NativeInterop.RECT
                    {
                        Left = 0,
                        Top = 0,
                        Right = Math.Max(1, (int)Math.Ceiling(widthConstraint.Value)),
                        Bottom = 0
                    };
                    NativeInterop.DrawText(hdc, text, text.Length, ref rect,
                        NativeInterop.DT_LEFT | NativeInterop.DT_TOP | NativeInterop.DT_WORDBREAK |
                        NativeInterop.DT_CALCRECT | NativeInterop.DT_NOPREFIX);

                    result = new TextMeasurementResult(Math.Max(0, rect.Width), Math.Max(lineHeight, rect.Height));
                }

                lock (CacheLock)
                {
                    if (MeasureCache.Count >= MaxMeasureCacheEntries)
                        MeasureCache.Clear();

                    MeasureCache[key] = result;
                }

                return result;
            }
            finally
            {
                if (oldFont != 0)
                    NativeInterop.SelectObject(hdc, oldFont);
                NativeInterop.ReleaseDC(0, hdc);
            }
        }

        public static void DrawText(nint hdc, string text, string? fontFamily, float fontSize, string? fontWeight, Core.Color color, RectF bounds, bool noWrap)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0 || string.IsNullOrEmpty(text)) return;

            fontSize = fontSize > 0 ? fontSize : 14f;
            var font = GetFontHandle(ResolveFontFamily(fontFamily, text), fontSize, fontWeight);
            var oldFont = font != 0 ? NativeInterop.SelectObject(hdc, font) : 0;
            var rect = new NativeInterop.RECT
            {
                Left = (int)Math.Floor(bounds.Left),
                Top = (int)Math.Floor(bounds.Top),
                Right = (int)Math.Ceiling(bounds.Right),
                Bottom = (int)Math.Ceiling(bounds.Bottom)
            };

            try
            {
                NativeInterop.SetBkMode(hdc, NativeInterop.TRANSPARENT);
                NativeInterop.SetTextColor(hdc, ToColorRef(color));
                var flags = NativeInterop.DT_LEFT | NativeInterop.DT_TOP | NativeInterop.DT_NOPREFIX |
                            (noWrap ? NativeInterop.DT_SINGLELINE : NativeInterop.DT_WORDBREAK);
                NativeInterop.DrawText(hdc, text, text.Length, ref rect, flags);
            }
            finally
            {
                if (oldFont != 0)
                    NativeInterop.SelectObject(hdc, oldFont);
            }
        }

        public static nint GetFontHandle(string? fontFamily, float fontSize, string? fontWeight)
        {
            var family = string.IsNullOrWhiteSpace(fontFamily) ? "Segoe UI" : fontFamily;
            var key = FontKey.Create(family, fontSize, fontWeight);

            lock (CacheLock)
            {
                if (Fonts.TryGetValue(key, out var font))
                    return font;

                var height = -Math.Max(1, (int)Math.Round(fontSize > 0 ? fontSize : 14f));
                font = NativeInterop.CreateFont(
                    height,
                    0,
                    0,
                    0,
                    key.Weight,
                    0,
                    0,
                    0,
                    NativeInterop.DEFAULT_CHARSET,
                    NativeInterop.OUT_DEFAULT_PRECIS,
                    NativeInterop.CLIP_DEFAULT_PRECIS,
                    NativeInterop.CLEARTYPE_QUALITY,
                    NativeInterop.DEFAULT_PITCH | NativeInterop.FF_DONTCARE,
                    key.Family);

                if (font != 0)
                    Fonts[key] = font;

                return font;
            }
        }

        public static string ResolveFontFamily(string? fontFamily, string? text)
        {
            if (!string.IsNullOrWhiteSpace(fontFamily))
                return fontFamily;

            return IsLikelyEmojiOrSymbol(text) ? "Segoe UI Emoji" : "Segoe UI";
        }

        public static float GetPreferredLineHeight(string? fontFamily, float fontSize, string? fontWeight)
        {
            fontSize = fontSize > 0 ? fontSize : 14f;
            var resolvedFamily = ResolveFontFamily(fontFamily, null);
            return GetOrCreateLineHeight(resolvedFamily, fontSize, fontWeight, FontKey.Create(resolvedFamily, fontSize, fontWeight));
        }

        private static void ClearCaches()
        {
            lock (CacheLock)
            {
                foreach (var font in Fonts.Values)
                    NativeInterop.DeleteObject(font);

                Fonts.Clear();
                LineHeights.Clear();
                MeasureCache.Clear();
            }
        }

        private static float GetCachedLineHeight(FontKey key)
        {
            lock (CacheLock)
            {
                return LineHeights.TryGetValue(key, out var lineHeight) ? lineHeight : 0;
            }
        }

        private static void CacheLineHeight(FontKey key, float lineHeight)
        {
            if (lineHeight <= 0)
                return;

            lock (CacheLock)
            {
                LineHeights[key] = lineHeight;
            }
        }

        private static float GetOrCreateLineHeight(string resolvedFamily, float fontSize, string? fontWeight, FontKey key)
        {
            var cached = GetCachedLineHeight(key);
            if (cached > 0)
                return cached;

            var hdc = NativeInterop.GetDC(0);
            if (hdc == 0)
                return fontSize + 3f;

            var font = GetFontHandle(resolvedFamily, fontSize, fontWeight);
            var oldFont = font != 0 ? NativeInterop.SelectObject(hdc, font) : 0;

            try
            {
                var lineHeight = GetLineHeight(hdc, fontSize);
                CacheLineHeight(key, lineHeight);
                return lineHeight;
            }
            finally
            {
                if (oldFont != 0)
                    NativeInterop.SelectObject(hdc, oldFont);
                NativeInterop.ReleaseDC(0, hdc);
            }
        }

        private readonly record struct FontKey(string Family, int Size, int Weight)
        {
            public static FontKey Create(string family, float fontSize, string? fontWeight)
            {
                var size = Math.Max(1, (int)Math.Round((fontSize > 0 ? fontSize : 14f) * 4f));
                return new FontKey(family, size, IsBold(fontWeight) ? NativeInterop.FW_BOLD : NativeInterop.FW_NORMAL);
            }
        }

        private readonly record struct TextMeasureKey(string Text, string Family, int Size, string Weight, int WidthConstraint, bool NoWrap)
        {
            public static TextMeasureKey Create(string text, string family, float fontSize, string? fontWeight, float? widthConstraint, bool noWrap)
            {
                var size = Math.Max(1, (int)Math.Round((fontSize > 0 ? fontSize : 14f) * 4f));
                var width = widthConstraint.HasValue && widthConstraint.Value > 0
                    ? Math.Max(1, (int)Math.Ceiling(widthConstraint.Value))
                    : 0;
                return new TextMeasureKey(text, family, size, fontWeight ?? string.Empty, width, noWrap);
            }
        }

        private static float GetLineHeight(nint hdc, float fallbackFontSize)
        {
            if (NativeInterop.GetTextMetrics(hdc, out var metrics) && metrics.tmHeight > 0)
                return metrics.tmHeight + Math.Max(0, metrics.tmExternalLeading);

            return fallbackFontSize + 3f;
        }

        private static bool IsBold(string? fontWeight)
        {
            if (string.IsNullOrEmpty(fontWeight)) return false;
            var weight = fontWeight.ToLowerInvariant();
            return weight is "bold" or "semibold" or "500" or "600" or "700" or "800" or "900";
        }

        private static bool IsLikelyEmojiOrSymbol(string? text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (var c in text)
            {
                if (char.IsSurrogate(c)) return true;
                if (c >= 0x2000 && c <= 0x33FF) return true;
            }
            return false;
        }

        private static int ToColorRef(Core.Color color)
        {
            if (color.A < 255)
            {
                var alpha = color.A / 255f;
                color = new Core.Color(
                    (byte)Math.Round(color.R * alpha + 255 * (1 - alpha)),
                    (byte)Math.Round(color.G * alpha + 255 * (1 - alpha)),
                    (byte)Math.Round(color.B * alpha + 255 * (1 - alpha)));
            }

            return color.R | (color.G << 8) | (color.B << 16);
        }
    }

    internal static class WicImageLoader
    {
        private static readonly Guid ClsidWicImagingFactory = new("cacaf262-9370-4615-a13b-9f5539da4c0a");
        private static readonly Guid PixelFormat32bppBgra = new("6fddc324-4e03-4bfe-b185-3d77768dc90e");

        public static bool TryLoadBitmap(string path, out nint hBitmap, out int width, out int height)
        {
            hBitmap = 0;
            width = 0;
            height = 0;

            var hr = NativeInterop.CoInitializeEx(0, NativeInterop.COINIT_APARTMENTTHREADED);
            var shouldUninitialize = hr is NativeInterop.S_OK or NativeInterop.S_FALSE;

            IWICImagingFactory? factory = null;
            IWICBitmapDecoder? decoder = null;
            IWICBitmapFrameDecode? frame = null;
            IWICFormatConverter? converter = null;

            try
            {
                var factoryType = Type.GetTypeFromCLSID(ClsidWicImagingFactory, throwOnError: true)!;
                factory = (IWICImagingFactory)Activator.CreateInstance(factoryType)!;

                factory.CreateDecoderFromFilename(path, 0, NativeInterop.GENERIC_READ, WICDecodeOptions.MetadataCacheOnDemand, out decoder);
                decoder.GetFrame(0, out frame);
                factory.CreateFormatConverter(out converter);

                var format = PixelFormat32bppBgra;
                converter.Initialize(frame, ref format, WICBitmapDitherType.None, 0, 0, WICBitmapPaletteType.Custom);
                converter.GetSize(out var w, out var h);

                if (w == 0 || h == 0 || w > int.MaxValue || h > int.MaxValue)
                    return false;

                width = (int)w;
                height = (int)h;
                var stride = checked(width * 4);
                var bufferSize = checked(stride * height);

                var bitmapInfo = new NativeInterop.BITMAPINFO
                {
                    bmiHeader = new NativeInterop.BITMAPINFOHEADER
                    {
                        biSize = (uint)Marshal.SizeOf<NativeInterop.BITMAPINFOHEADER>(),
                        biWidth = width,
                        biHeight = -height,
                        biPlanes = 1,
                        biBitCount = 32,
                        biCompression = NativeInterop.BI_RGB,
                        biSizeImage = (uint)bufferSize
                    }
                };

                var screenDc = NativeInterop.GetDC(0);
                try
                {
                    hBitmap = NativeInterop.CreateDIBSection(screenDc, ref bitmapInfo, NativeInterop.DIB_RGB_COLORS, out var bits, 0, 0);
                    if (hBitmap == 0 || bits == 0)
                    {
                        hBitmap = 0;
                        return false;
                    }

                    converter.CopyPixels(0, (uint)stride, (uint)bufferSize, bits);
                    return true;
                }
                finally
                {
                    if (screenDc != 0)
                        NativeInterop.ReleaseDC(0, screenDc);
                }
            }
            catch
            {
                if (hBitmap != 0)
                {
                    NativeInterop.DeleteObject(hBitmap);
                    hBitmap = 0;
                }

                width = 0;
                height = 0;
                return false;
            }
            finally
            {
                ReleaseComObject(converter);
                ReleaseComObject(frame);
                ReleaseComObject(decoder);
                ReleaseComObject(factory);

                if (shouldUninitialize)
                    NativeInterop.CoUninitialize();
            }
        }

        private static void ReleaseComObject(object? value)
        {
            if (value != null && Marshal.IsComObject(value))
                Marshal.ReleaseComObject(value);
        }

        private enum WICDecodeOptions : uint
        {
            MetadataCacheOnDemand = 0
        }

        private enum WICBitmapDitherType : uint
        {
            None = 0
        }

        private enum WICBitmapPaletteType : uint
        {
            Custom = 0
        }

        [ComImport]
        [Guid("ec5ec8a9-c395-4314-9c77-54d7a935ff70")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IWICImagingFactory
        {
            void CreateDecoderFromFilename([MarshalAs(UnmanagedType.LPWStr)] string wzFilename, nint pguidVendor, uint dwDesiredAccess, WICDecodeOptions metadataOptions, out IWICBitmapDecoder ppIDecoder);
            void CreateDecoderFromStream(nint pIStream, nint pguidVendor, WICDecodeOptions metadataOptions, out IWICBitmapDecoder ppIDecoder);
            void CreateDecoderFromFileHandle(nuint hFile, nint pguidVendor, WICDecodeOptions metadataOptions, out IWICBitmapDecoder ppIDecoder);
            void CreateComponentInfo(ref Guid clsidComponent, out nint ppIInfo);
            void CreateDecoder(ref Guid guidContainerFormat, nint pguidVendor, out IWICBitmapDecoder ppIDecoder);
            void CreateEncoder(ref Guid guidContainerFormat, nint pguidVendor, out nint ppIEncoder);
            void CreatePalette(out nint ppIPalette);
            void CreateFormatConverter(out IWICFormatConverter ppIFormatConverter);
        }

        [ComImport]
        [Guid("9edde9e7-8dee-47ea-99df-e6faf2ed44bf")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IWICBitmapDecoder
        {
            void QueryCapability(nint pIStream, out uint pdwCapability);
            void Initialize(nint pIStream, WICDecodeOptions cacheOptions);
            void GetContainerFormat(out Guid pguidContainerFormat);
            void GetDecoderInfo(out nint ppIDecoderInfo);
            void CopyPalette(nint pIPalette);
            void GetMetadataQueryReader(out nint ppIMetadataQueryReader);
            void GetPreview(out nint ppIBitmapSource);
            void GetColorContexts(uint cCount, nint ppIColorContexts, out uint pcActualCount);
            void GetThumbnail(out nint ppIThumbnail);
            void GetFrameCount(out uint pCount);
            void GetFrame(uint index, out IWICBitmapFrameDecode ppIBitmapFrame);
        }

        [ComImport]
        [Guid("00000120-a8f2-4877-ba0a-fd2b6645fb94")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IWICBitmapSource
        {
            void GetSize(out uint puiWidth, out uint puiHeight);
            void GetPixelFormat(out Guid pPixelFormat);
            void GetResolution(out double pDpiX, out double pDpiY);
            void CopyPalette(nint pIPalette);
            void CopyPixels(nint prc, uint cbStride, uint cbBufferSize, nint pbBuffer);
        }

        [ComImport]
        [Guid("3b16811b-6a43-4ec9-a813-3d930c13b940")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IWICBitmapFrameDecode : IWICBitmapSource
        {
            void GetMetadataQueryReader(out nint ppIMetadataQueryReader);
            void GetColorContexts(uint cCount, nint ppIColorContexts, out uint pcActualCount);
            void GetThumbnail(out nint ppIThumbnail);
        }

        [ComImport]
        [Guid("00000301-a8f2-4877-ba0a-fd2b6645fb94")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IWICFormatConverter : IWICBitmapSource
        {
            void Initialize(IWICBitmapSource pISource, ref Guid dstFormat, WICBitmapDitherType dither, nint pIPalette, double alphaThresholdPercent, WICBitmapPaletteType paletteTranslate);
            void CanConvert(ref Guid srcPixelFormat, ref Guid dstPixelFormat, out bool pfCanConvert);
        }
    }
}

using EchoUI.Core;

namespace EchoUI.Render.Win32
{
    internal static class FlexLayout
    {
        public static void ComputeLayout(Win32Element root, float viewportWidth, float viewportHeight, int measureCacheGeneration)
        {
            LayoutEngine.ComputeLayout(root, viewportWidth, viewportHeight, measureCacheGeneration, MeasureText);
        }

        public static void UpdateAbsoluteLayout(Win32Element root)
        {
            LayoutEngine.UpdateAbsoluteLayout(root);
        }

        public static float MeasureContentHeight(Win32Element container, float vpW, float vpH)
        {
            return LayoutEngine.MeasureContentHeight(container);
        }

        public static float MeasureContentWidth(Win32Element container, float vpW, float vpH)
        {
            return LayoutEngine.MeasureContentWidth(container);
        }

        private static TextMeasurementResult MeasureText(Win32Element element, float? widthConstraint, bool noWrap)
        {
            var fontSize = element.FontSize > 0 ? element.FontSize : 14f;
            return GdiText.MeasureText(element.Text, element.FontFamily, fontSize, element.FontWeight, widthConstraint, noWrap);
        }
    }
}

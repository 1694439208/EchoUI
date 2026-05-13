namespace EchoUI.Core
{
    /// <summary>
    /// 存储由 Reconciler 计算出的、与渲染器无关的通用属性变更集。
    /// 这是对“发生了什么变化”的抽象描述。
    /// </summary>
    public class PropertyPatch
    {
        /// <summary>
        /// 一个字典，包含已添加或更新的属性。
        /// key: C# 属性的名称 (例如, "Width", "BackgroundColor", "OnClick").
        /// value: 属性的新值 (例如, 一个 Dimension 结构体, 一个 Color 结构体, 或一个委托).
        /// 如果值为 null，表示该属性应被取消设置或恢复为默认值。
        /// </summary>
        public Dictionary<string, object?>? UpdatedProperties { get; set; }
    }

    public record class TextMeasurementRequest
    {
        public string Text { get; init; } = "";
        public string? FontFamily { get; init; }
        public float? FontSize { get; init; }
        public string? FontWeight { get; init; }
    }

    public readonly record struct TextMeasurementResult(float Width, float Height);

    public interface IRenderer
    {
        object CreateElement(string type);

        /// <summary>
        /// 将 Reconciler 计算出的属性变更应用到原生元素上。
        /// </summary>
        /// <param name="nativeElement">原生元素对象 (在 WebRenderer 中是 elementId)</param>
        /// <param name="newProps">完整的最新 Props，用于更新事件处理委托</param>
        /// <param name="patch">属性变更集</param>
        void PatchProperties(object nativeElement, Props newProps, PropertyPatch patch);

        void AddChild(object parent, object child, int index);
        void RemoveChild(object parent, object child);
        void MoveChild(object parent, object child, int newIndex);

        TextMeasurementResult MeasureText(TextMeasurementRequest request);

        IUpdateScheduler GetScheduler(object rootContainer);
    }

    public interface IUpdateScheduler
    {
        void Schedule(Func<Task> updateAction);
    }
}
using System.Xml.Linq;

namespace EchoUI.Core
{
    /// <summary>
    /// 存储一个组件实例的完整运行时信息
    /// </summary>
    public class ComponentInstance
    {
        public Reconciler Reconciler { get; }
        public Element Element { get; set; }
        public Delegate? ComponentDelegate => Element.Type.IsComponent || Element.Type.IsAsyncComponent ? Element.Type.AsComponentDelegate : null;
        public object? NativeElement { get; set; }
        public LayoutBox? Layout { get; set; }
        public ComponentInstance? Parent { get; set; }
        public List<ComponentInstance> Children { get; set; } = [];

        // Hooks support
        public int HookIndex { get; set; }
        public readonly List<object> HookStates = [];
        public readonly Dictionary<int, Action?> EffectCleanups = [];
        public bool HasCompletedInitialRender { get; set; }

        // Async component support
        public Task? RenderingTask { get; set; }
        public bool IsAsyncPlaceholder { get; set; }

        public ComponentInstance(Element element, ComponentInstance? parent, Reconciler reconciler)
        {
            Element = element;
            Parent = parent;
            Reconciler = reconciler;
        }
    }
}
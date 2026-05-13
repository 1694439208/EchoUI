using System.Diagnostics;

namespace EchoUI.Core
{
    /// <summary>
    /// 由Reconciler在渲染时提供的内部上下文
    /// </summary>
    internal class HookContext
    {
        public ComponentInstance Instance { get; init; } = null!;
        public Action<ComponentInstance> ScheduleUpdate { get; init; } = null!;
        public bool IsInitialRender => !Instance.HasCompletedInitialRender;
    }

    // 定义更强大的State Setter委托
    public delegate void ValueSetter<T>(T newValue);
    public delegate void StateUpdater<T>(Func<T, T> updater);
    public class Ref<T>
    {
        public T Value { get; set; }
        public Ref(T value) => Value = value;

        public static implicit operator T(Ref<T> value) => value.Value;
    }

    public static class Hooks
    {
        [ThreadStatic]
        internal static HookContext? Context;

        private static (ComponentInstance instance, int index) GetComponentWithHookIndex()
        {
            if (Context == null)
                throw new InvalidOperationException("Hooks can only be called inside a component render.");
            return (Context.Instance, Context.Instance.HookIndex++);
        }

        /// <summary>
        /// 检查是否是组件的首次渲染
        /// </summary>
        public static bool IsInitialRender()
        {
            if (Context == null)
                throw new InvalidOperationException("Hooks can only be called inside a component render.");
            return Context.IsInitialRender;
        }

        /// <summary>
        /// 通过当前渲染器测量文本尺寸。
        /// </summary>
        public static TextMeasurementResult MeasureText(TextMeasurementRequest request)
        {
            if (Context == null)
                throw new InvalidOperationException("Hooks can only be called inside a component render.");

            return Context.Instance.Reconciler.MeasureText(request);
        }

        /// <summary>
        /// 创建一个可在事件回调中继续使用的文本测量委托。
        /// </summary>
        public static Func<TextMeasurementRequest, TextMeasurementResult> CreateTextMeasurer()
        {
            if (Context == null)
                throw new InvalidOperationException("Hooks can only be called inside a component render.");

            var reconciler = Context.Instance.Reconciler;
            return request => reconciler.MeasureText(request);
        }

        public static Func<Task<string>> CreateClipboardReaderAsync()
        {
            if (Context == null)
                throw new InvalidOperationException("Hooks can only be called inside a component render.");

            var reconciler = Context.Instance.Reconciler;
            return () => reconciler.ReadClipboardTextAsync();
        }

        public static Func<string, Task> CreateClipboardWriterAsync()
        {
            if (Context == null)
                throw new InvalidOperationException("Hooks can only be called inside a component render.");

            var reconciler = Context.Instance.Reconciler;
            return reconciler.WriteClipboardTextAsync;
        }

        /// <summary>
        /// 提供组件状态，返回当前值和两种更新方式。
        /// </summary>
        public static (Ref<T> Value, ValueSetter<T> SetValue, StateUpdater<T> UpdateValue) State<T>(T initialValue)
        {
            var (instance, index) = GetComponentWithHookIndex();
            var scheduler = Context!.ScheduleUpdate;
            if (index >= instance.HookStates.Count)
            {
                instance.HookStates.Add(new Ref<T>(initialValue));
            }

            var stateRef = (Ref<T>)instance.HookStates[index];
            void SetValue(T newValue)
            {
                // 对于引用类型，总是触发更新（除非是完全相同的引用）
                // 对于值类型，比较值是否相等
                bool shouldUpdate = false;

                if (typeof(T).IsValueType)
                {
                    shouldUpdate = !EqualityComparer<T>.Default.Equals(stateRef.Value, newValue);
                }
                else
                {
                    // 对于引用类型，即使是新对象也要更新
                    shouldUpdate = !ReferenceEquals(stateRef.Value, newValue);
                }
                if (shouldUpdate)
                {
                    stateRef.Value = newValue;
                    scheduler(instance);
                }
            }
            void UpdateValue(Func<T, T> updater)
            {
                var oldValue = stateRef.Value;
                var newValue = updater(oldValue);

                bool shouldUpdate = false;

                if (typeof(T).IsValueType)
                {
                    shouldUpdate = !EqualityComparer<T>.Default.Equals(oldValue, newValue);
                }
                else
                {
                    // 对于引用类型，即使内容相同但是新对象，也要更新
                    shouldUpdate = !ReferenceEquals(oldValue, newValue);
                }
                if (shouldUpdate)
                {
                    stateRef.Value = newValue;
                    scheduler(instance);
                }
            }
            return (stateRef, SetValue, UpdateValue);
        }

        /// <summary>
        /// 处理副作用，支持清理函数和依赖项数组。
        /// </summary>
        public static void Effect(Func<Action?> effectAction, object[]? deps)
        {
            var (instance, index) = GetComponentWithHookIndex();

            object? oldState = (index < instance.HookStates.Count) ? instance.HookStates[index] : null;
            var oldDeps = oldState as object[];

            var shouldRun = oldDeps == null || deps == null || (deps.Length == 0 && oldDeps.Length > 0) || !oldDeps.SequenceEqual(deps);

            if (shouldRun)
            {
                if (instance.EffectCleanups.TryGetValue(index, out var oldCleanup) && oldCleanup != null)
                {
                    oldCleanup();
                }
                instance.EffectCleanups[index] = effectAction();

                if (index < instance.HookStates.Count)
                    instance.HookStates[index] = deps!;
                else
                    instance.HookStates.Add(deps!);
            }
        }

        /// <summary>
        /// 提供一个极简的、跨组件共享状态的方式。
        /// </summary>
        public static T Shared<T>() where T : class, new()
        {
            if (Context == null)
                throw new InvalidOperationException("Hooks can only be called inside a component render.");

            return Context.Instance.Reconciler.GetSharedState<T>();
        }

        /// <summary>
        /// 缓存一个昂贵的计算结果。
        /// 仅当依赖项(deps)发生变化时，才会重新调用 factory 函数计算新值。
        /// </summary>
        /// <typeparam name="T">要缓存的值的类型。</typeparam>
        /// <param name="factory">用于创建值的工厂函数。</param>
        /// <param name="deps">依赖项数组。如果数组中任何一项发生变化，值将重新计算。</param>
        /// <returns>缓存或新计算出的值。</returns>
        public static T Memo<T>(Func<T> factory, object[]? deps)
        {
            var (instance, index) = GetComponentWithHookIndex();

            // 尝试获取上一次的状态 (缓存的值和依赖项)
            object? oldState = (index < instance.HookStates.Count) ? instance.HookStates[index] : null;
            var oldMemo = oldState as Tuple<T, object[]?>;
            var oldDeps = oldMemo?.Item2;

            // 比较依赖项，判断是否需要重新计算
            // 规则：
            // 1. 如果是首次渲染 (oldDeps == null)，需要计算。
            // 2. 如果依赖项数组为 null，每次都重新计算（不推荐的用法，但需处理）。
            // 3. 如果新旧依赖项数组通过 SequenceEqual 比较不相等，需要重新计算。
            var needsRecomputation = oldDeps == null || deps == null || !oldDeps.SequenceEqual(deps);

            if (needsRecomputation)
            {
                // 计算新值
                var newValue = factory();
                var newMemo = Tuple.Create(newValue, deps);

                // 存储新值和新的依赖项
                if (index < instance.HookStates.Count)
                {
                    instance.HookStates[index] = newMemo!;
                }
                else
                {
                    instance.HookStates.Add(newMemo!);
                }

                return newValue;
            }

            // 依赖项未变，直接返回缓存的值
            return oldMemo!.Item1;
        }
    }
}
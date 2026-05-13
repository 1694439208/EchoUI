using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;

namespace EchoUI.Core
{
    public class Reconciler
    {
        private readonly IRenderer _renderer;
        private readonly object _rootContainer;
        private readonly IUpdateScheduler _scheduler;
        private ComponentInstance? _rootInstance;
        private readonly Dictionary<Type, object> _sharedStates = new();
        private readonly HashSet<ComponentInstance> _dirtyComponents = new();
        private bool _isUpdateQueued = false;

        public Reconciler(IRenderer renderer, object rootContainer)
        {
            _renderer = renderer;
            _rootContainer = rootContainer;
            _scheduler = renderer.GetScheduler(rootContainer);
        }

        public T GetSharedState<T>() where T : class, new()
        {
            if (!_sharedStates.ContainsKey(typeof(T)))
            {
                _sharedStates[typeof(T)] = new T();
            }
            return (T)_sharedStates[typeof(T)];
        }

        internal TextMeasurementResult MeasureText(TextMeasurementRequest request)
        {
            return _renderer.MeasureText(request);
        }

        public async Task Mount(Delegate rootComponentDelegate)
        {
            var methodInfo = rootComponentDelegate.Method;
            Element? rootElement;

            if (methodInfo.ReturnType.IsAssignableTo(typeof(Task)))
            {
                var asyncComponent = (AsyncComponent)Delegate.CreateDelegate(typeof(AsyncComponent), rootComponentDelegate.Target, methodInfo);
                rootElement = new Element(asyncComponent, new RootProps());
            }
            else
            {
                var component = (Component)Delegate.CreateDelegate(typeof(Component), rootComponentDelegate.Target, methodInfo);
                rootElement = new Element(component, new RootProps());
            }

            _rootInstance = new ComponentInstance(rootElement, null, this);

            var rendered = await RenderComponent(_rootInstance, _rootInstance.Element.Props);
            if (rendered != null)
            {
                var childInstance = new ComponentInstance(rendered, _rootInstance, this);
                _rootInstance.Children.Add(childInstance);
                await MountInstance(childInstance);
            }
        }

        private void ScheduleUpdate(ComponentInstance instance)
        {
            _dirtyComponents.Add(instance);
            if (!_isUpdateQueued)
            {
                _isUpdateQueued = true;
                _scheduler.Schedule(ProcessUpdates);
            }
        }

        private async Task ProcessUpdates()
        {
            var componentsToProcess = _dirtyComponents.ToHashSet();
            _dirtyComponents.Clear();
            _isUpdateQueued = false;

            foreach (var instance in componentsToProcess)
            {
                await UpdateInstance(instance);
            }

            if (_dirtyComponents.Count > 0 && !_isUpdateQueued)
            {
                ScheduleUpdate(_dirtyComponents.First());
            }
        }

        private async Task<Element?> RenderComponent(ComponentInstance instance, Props props)
        {
            var elementType = instance.Element.Type;

            var oldContext = Hooks.Context;
            Hooks.Context = new HookContext { Instance = instance, ScheduleUpdate = ScheduleUpdate };
            instance.HookIndex = 0;
            Element? resultElement = null;
            try
            {
                if (elementType.IsComponent)
                {
                    resultElement = ((Component)elementType.AsComponentDelegate)(props);
                }
                else if (elementType.IsAsyncComponent)
                {
                    var renderTask = ((AsyncComponent)elementType.AsComponentDelegate)(props);
                    if (renderTask.IsCompletedSuccessfully)
                    {
                        resultElement = renderTask.Result;
                        instance.HasCompletedInitialRender = true;
                    }
                    else
                    {
                        if (!instance.HasCompletedInitialRender)
                        {
                            instance.IsAsyncPlaceholder = true;
                            renderTask.ContinueWith(_ =>
                            {
                                instance.HasCompletedInitialRender = true;
                                ScheduleUpdate(instance);
                            }, TaskScheduler.FromCurrentSynchronizationContext());
                            resultElement = props.Fallback;
                        }
                        else
                        {
                            await renderTask;
                            resultElement = renderTask.Result;
                        }
                    }
                }
            }
            finally
            {
                Hooks.Context = oldContext;
            }
            return resultElement;
        }

        private async Task MountInstance(ComponentInstance instance)
        {
            var element = instance.Element;
            var elementType = element.Type;

            if (elementType.IsNative)
            {
                instance.NativeElement = _renderer.CreateElement(elementType.AsNativeType);
                var initialPatch = CreateInitialPatch(element.Props);
                if (initialPatch != null)
                {
                    _renderer.PatchProperties(instance.NativeElement, element.Props, initialPatch);
                }

                var parentContainer = GetParentContainer(instance);
                var index = instance.Parent?.Children.IndexOf(instance) ?? -1;
                _renderer.AddChild(parentContainer, instance.NativeElement, index);

                foreach (var childElement in element.Props.Children)
                {
                    var childInstance = new ComponentInstance(childElement, instance, this);
                    instance.Children.Add(childInstance);
                    await MountInstance(childInstance);
                }
            }
            else
            {
                var rendered = await RenderComponent(instance, element.Props);
                if (rendered != null)
                {
                    var childInstance = new ComponentInstance(rendered, instance, this);
                    instance.Children.Add(childInstance);
                    await MountInstance(childInstance);
                }
            }
        }

        private async Task UpdateInstance(ComponentInstance instance)
        {
            var element = instance.Element;
            var elementType = element.Type;

            if (elementType.IsNative)
            {
                await DiffChildren(instance, element.Props.Children);
            }
            else
            {
                var rendered = await RenderComponent(instance, element.Props);

                if (rendered == null)
                {
                    foreach (var child in instance.Children.ToList())
                    {
                        UnmountInstance(child);
                    }
                    instance.Children.Clear();
                }
                else
                {
                    if (instance.Children.Count == 0)
                    {
                        var childInstance = new ComponentInstance(rendered, instance, this);
                        instance.Children.Add(childInstance);
                        await MountInstance(childInstance);
                    }
                    else
                    {
                        var existingChild = instance.Children[0];
                        await DiffInstance(existingChild, rendered);
                    }
                }
            }
        }

        private async Task DiffInstance(ComponentInstance instance, Element newElement)
        {
            var oldElement = instance.Element;

            if (!ElementTypesMatch(oldElement.Type, newElement.Type))
            {
                var parent = instance.Parent;
                var index = parent?.Children.IndexOf(instance) ?? -1;

                UnmountInstance(instance);

                var newInstance = new ComponentInstance(newElement, parent, this);
                if (parent != null)
                {
                    if (index >= 0 && index < parent.Children.Count)
                        parent.Children.Insert(index, newInstance);
                    else
                        parent.Children.Add(newInstance);
                }

                await MountInstance(newInstance);
                return;
            }

            instance.Element = newElement;

            if (newElement.Type.IsNative)
            {
                var patch = DiffProps(oldElement.Props, newElement.Props);
                if (patch != null && patch.UpdatedProperties?.Count > 0)
                {
                    _renderer.PatchProperties(instance.NativeElement!, newElement.Props, patch);
                }

                await DiffChildren(instance, newElement.Props.Children);
            }
            else
            {
                await UpdateInstance(instance);
            }
        }

        #region Props Diffing Logic

        [UnconditionalSuppressMessage("AOT", "IL2067", Justification = "Props 属性类型在 AOT 编译时会被保留")]
        [UnconditionalSuppressMessage("AOT", "IL2072", Justification = "Props 属性类型在 AOT 编译时会被保留")]
        private PropertyPatch? CreateInitialPatch(Props props)
        {
            var patch = new PropertyPatch { UpdatedProperties = new Dictionary<string, object?>() };
            var hasContent = false;

            foreach (var propInfo in props.GetType().GetProperties())
            {
                if (propInfo.Name == nameof(Props.Children)) continue;

                // Handle NativeProps.Properties by unpacking its contents
                if (propInfo.Name == nameof(NativeProps.Properties) && props is NativeProps nativeProps && nativeProps.Properties != null)
                {
                    foreach (var kvp in nativeProps.Properties.Value.Data)
                    {
                        // We add all values, even if null, because they are explicitly set.
                        patch.UpdatedProperties[kvp.Key] = kvp.Value;
                        hasContent = true;
                    }
                    continue; // Skip adding the 'Properties' object itself to the patch.
                }

                var value = propInfo.GetValue(props);

                // For other properties, add them if they are not the default value.
                var defaultValue = propInfo.PropertyType.IsValueType ? Activator.CreateInstance(propInfo.PropertyType) : null;
                if (value != null && !value.Equals(defaultValue))
                {
                    patch.UpdatedProperties[propInfo.Name] = value;
                    hasContent = true;
                }
            }

            return hasContent ? patch : null;
        }

        private PropertyPatch? DiffProps(Props oldProps, Props newProps)
        {
            var patch = new PropertyPatch();
            var updatedProperties = new Dictionary<string, object?>();
            var hasChanges = false;

            var allPropNames = newProps.GetType().GetProperties().Select(p => p.Name)
                .Union(oldProps.GetType().GetProperties().Select(p => p.Name))
                .Distinct();

            foreach (var propName in allPropNames)
            {
                if (propName == nameof(Props.Children)) continue;

                // Special handling for NativeProps.Properties
                if (propName == nameof(NativeProps.Properties) && (oldProps is NativeProps || newProps is NativeProps))
                {
                    var oldNativeProps = oldProps as NativeProps;
                    var newNativeProps = newProps as NativeProps;

                    var oldDict = oldNativeProps?.Properties?.Data ?? new Dictionary<string, object?>();
                    var newDict = newNativeProps?.Properties?.Data ?? new Dictionary<string, object?>();
                    var allKeys = oldDict.Keys.Union(newDict.Keys).Distinct();

                    foreach (var key in allKeys)
                    {
                        oldDict.TryGetValue(key, out var oldPropValue);
                        newDict.TryGetValue(key, out var newPropValue);

                        bool propertyValueChanged;
                        var propValueType = newPropValue?.GetType() ?? oldPropValue?.GetType();

                        if (propValueType != null && typeof(Delegate).IsAssignableFrom(propValueType))
                        {
                            propertyValueChanged = (oldPropValue == null) != (newPropValue == null);
                        }
                        else
                        {
                            propertyValueChanged = !Equals(oldPropValue, newPropValue);
                        }

                        if (propertyValueChanged)
                        {
                            updatedProperties[key] = newPropValue; // newPropValue will be null if the key was removed
                            hasChanges = true;
                        }
                    }
                    continue; // Skip the generic diff for the 'Properties' property itself
                }

                var oldPropInfo = oldProps.GetType().GetProperty(propName);
                var newPropInfo = newProps.GetType().GetProperty(propName);

                var oldValue = oldPropInfo?.GetValue(oldProps);
                var newValue = newPropInfo?.GetValue(newProps);

                bool propertyChanged;
                var propType = newPropInfo?.PropertyType ?? oldPropInfo?.PropertyType;

                if (propType != null && typeof(Delegate).IsAssignableFrom(propType))
                {
                    propertyChanged = (oldValue == null) != (newValue == null);
                }
                else
                {
                    propertyChanged = !Equals(oldValue, newValue);
                }

                if (propertyChanged)
                {
                    updatedProperties[propName] = newValue;
                    hasChanges = true;
                }
            }

            if (hasChanges)
            {
                patch.UpdatedProperties = updatedProperties;
                return patch;
            }

            return null;
        }

        #endregion

        private async Task DiffChildren(ComponentInstance parent, IReadOnlyList<Element> newChildElements)
        {
            var oldChildren = parent.Children.ToList();
            var newChildren = new List<ComponentInstance>();
            var newInstancesCreated = new List<ComponentInstance>();

            var oldKeyedChildren = oldChildren
                .Where(c => c.Element.Props.Key != null)
                .ToDictionary(c => c.Element.Props.Key!);

            var processedOldChildren = new HashSet<ComponentInstance>();

            for (int i = 0; i < newChildElements.Count; i++)
            {
                var newChildElement = newChildElements[i];
                ComponentInstance? matchingChild = null;

                if (newChildElement.Props.Key != null &&
                    oldKeyedChildren.TryGetValue(newChildElement.Props.Key, out var keyedChild) &&
                    !processedOldChildren.Contains(keyedChild))
                {
                    matchingChild = keyedChild;
                }
                else if (newChildElement.Props.Key == null)
                {
                    // Fallback to index-based matching for non-keyed elements.
                    // This finds the next available old child at the current position that matches.
                    if (i < oldChildren.Count &&
                        oldChildren[i].Element.Props.Key == null &&
                        !processedOldChildren.Contains(oldChildren[i]) &&
                        ElementTypesMatch(oldChildren[i].Element.Type, newChildElement.Type))
                    {
                        matchingChild = oldChildren[i];
                    }
                }

                if (matchingChild != null)
                {
                    processedOldChildren.Add(matchingChild);
                    await DiffInstance(matchingChild, newChildElement);
                    newChildren.Add(matchingChild);
                }
                else
                {
                    var newInstance = new ComponentInstance(newChildElement, parent, this);
                    newInstancesCreated.Add(newInstance);
                    await MountInstance(newInstance); // Mount will add it to the DOM
                    newChildren.Add(newInstance);
                }
            }

            foreach (var oldChild in oldChildren)
            {
                if (!processedOldChildren.Contains(oldChild))
                {
                    UnmountInstance(oldChild);
                }
            }

            // Update the component tree structure first.
            parent.Children = newChildren;

            // If the parent is a native element, reorder the children in the DOM efficiently.
            if (parent.NativeElement != null)
            {
                // 1. Figure out the actual order of DOM nodes before we start moving them.
                var domStateBeforeReorder = oldChildren.Where(c => processedOldChildren.Contains(c)).ToList();
                domStateBeforeReorder.AddRange(newInstancesCreated);

                // 2. Iterate through the desired state and only move when necessary.
                for (int i = 0; i < newChildren.Count; i++)
                {
                    var instanceToPlace = newChildren[i];
                    object? nativeToPlace = GetFirstNativeElement(instanceToPlace);
                    if (nativeToPlace == null) continue;

                    object? currentNativeAtPosition = (i < domStateBeforeReorder.Count)
                        ? GetFirstNativeElement(domStateBeforeReorder[i])
                        : null;

                    // 3. If the node that should be at this position is not there, move it.
                    if (!object.ReferenceEquals(nativeToPlace, currentNativeAtPosition))
                    {
                        _renderer.MoveChild(parent.NativeElement, nativeToPlace, i);

                        // 4. Update our simulation of the DOM to reflect the move.
                        var instanceMovedInSim = domStateBeforeReorder.First(inst => GetFirstNativeElement(inst) == nativeToPlace);
                        domStateBeforeReorder.Remove(instanceMovedInSim);
                        domStateBeforeReorder.Insert(i, instanceMovedInSim);
                    }
                }
            }
        }

        private object GetParentContainer(ComponentInstance instance)
        {
            var parent = instance.Parent;
            while (parent != null)
            {
                if (parent.NativeElement != null)
                    return parent.NativeElement;
                parent = parent.Parent;
            }
            return _rootContainer;
        }

        private object? GetFirstNativeElement(ComponentInstance instance)
        {
            if (instance.NativeElement != null)
                return instance.NativeElement;

            foreach (var child in instance.Children)
            {
                var native = GetFirstNativeElement(child);
                if (native != null)
                    return native;
            }

            return null;
        }

        private bool ElementTypesMatch(ElementType type1, ElementType type2)
        {
            if (type1.IsNative && type2.IsNative)
                return type1.AsNativeType == type2.AsNativeType;

            return type1.AsComponentDelegate?.Method == type2.AsComponentDelegate?.Method;
        }

        private void UnmountInstance(ComponentInstance instance)
        {
            foreach (var cleanup in instance.EffectCleanups.Values)
                cleanup?.Invoke();

            foreach (var child in instance.Children.ToList())
            {
                UnmountInstance(child);
            }

            if (instance.NativeElement != null)
            {
                var container = GetParentContainer(instance);
                _renderer.RemoveChild(container, instance.NativeElement);
            }

            instance.Parent?.Children.Remove(instance);
        }

        public void HotSwapRootComponent(Delegate newRootComponent)
        {
            if (_rootInstance != null)
            {
                var methodInfo = newRootComponent.Method;
                ElementType newType;

                if (methodInfo.ReturnType.IsAssignableTo(typeof(Task)))
                {
                    newType = (AsyncComponent)Delegate.CreateDelegate(typeof(AsyncComponent), newRootComponent.Target, methodInfo);
                }
                else
                {
                    newType = (Component)Delegate.CreateDelegate(typeof(Component), newRootComponent.Target, methodInfo);
                }

                _rootInstance.Element = _rootInstance.Element with { Type = newType };
                ScheduleUpdate(_rootInstance);
            }
        }

        private record class RootProps : Props;
    }
}
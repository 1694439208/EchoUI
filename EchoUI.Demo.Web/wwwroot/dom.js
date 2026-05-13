const elementRegistry = new Map();
const pendingFloatLayoutRoots = new Set();
const echoUiDebug = globalThis.__ECHOUI_DEBUG === true;
let floatLayoutScheduled = false;

function logDebug(...args) {
    if (echoUiDebug) {
        console.debug('[EchoUI.dom]', ...args);
    }
}

function parsePx(value) {
    const parsed = Number.parseFloat(value);
    return Number.isFinite(parsed) ? parsed : 0;
}

function isFloatElement(element) {
    return element?.getAttribute('data-eui-float') === 'true';
}

function hasFloatAutoWidth(element) {
    return element?.getAttribute('data-eui-float-auto-width') === 'true';
}

function removeEventListeners(element) {
    const currentListeners = element?._eventListeners;
    if (!currentListeners) {
        return;
    }

    for (const [eventName, handler] of Object.entries(currentListeners)) {
        element.removeEventListener(eventName, handler);
    }

    element._eventListeners = {};
}

function removeManagedListeners(element) {
    const managedListeners = element?._managedListeners;
    if (!managedListeners) {
        return;
    }

    for (const { eventName, handler } of Object.values(managedListeners)) {
        element.removeEventListener(eventName, handler);
    }

    element._managedListeners = {};
}

function ensureManagedListener(element, key, eventName, handler) {
    element._managedListeners ??= {};
    if (element._managedListeners[key]) {
        return;
    }

    element.addEventListener(eventName, handler);
    element._managedListeners[key] = { eventName, handler };
}

function applyManagedInputBorder(element) {
    if (!element || element.tagName !== 'INPUT') {
        return;
    }

    const borderColor = element.getAttribute('data-eui-input-border-color');
    const focusedBorderColor = element.getAttribute('data-eui-input-focused-border-color');
    const hasManagedBorder = borderColor !== null || focusedBorderColor !== null;

    if (!hasManagedBorder) {
        element._echoUiSyncInputBorder = null;
        if (element._managedListeners?.echoUiInputFocus) {
            element.removeEventListener('focus', element._managedListeners.echoUiInputFocus.handler);
            delete element._managedListeners.echoUiInputFocus;
        }
        if (element._managedListeners?.echoUiInputBlur) {
            element.removeEventListener('blur', element._managedListeners.echoUiInputBlur.handler);
            delete element._managedListeners.echoUiInputBlur;
        }
        return;
    }

    const syncBorderColor = () => {
        const effectiveColor = document.activeElement === element
            ? (focusedBorderColor ?? borderColor ?? 'transparent')
            : (borderColor ?? 'transparent');
        element.style.borderColor = effectiveColor;
    };

    element._echoUiSyncInputBorder = syncBorderColor;
    ensureManagedListener(element, 'echoUiInputFocus', 'focus', () => element._echoUiSyncInputBorder?.());
    ensureManagedListener(element, 'echoUiInputBlur', 'blur', () => element._echoUiSyncInputBorder?.());
    syncBorderColor();
}

function cleanupSubtree(element) {
    if (!element) {
        return;
    }

    for (const child of Array.from(element.children)) {
        cleanupSubtree(child);
    }

    removeEventListeners(element);
    removeManagedListeners(element);

    if (element.__echoUiId) {
        elementRegistry.delete(element.__echoUiId);
    }
}

function scheduleFloatLayout(element) {
    if (!element) {
        return;
    }

    pendingFloatLayoutRoots.add(element);
    if (element.parentElement) {
        pendingFloatLayoutRoots.add(element.parentElement);
    }

    if (floatLayoutScheduled) {
        return;
    }

    floatLayoutScheduled = true;
    requestAnimationFrame(() => {
        floatLayoutScheduled = false;
        const roots = Array.from(pendingFloatLayoutRoots);
        pendingFloatLayoutRoots.clear();

        for (const root of roots) {
            syncFloatLayoutTree(root);
        }
    });
}

function syncFloatLayoutTree(root) {
    if (!root || !root.children) {
        return;
    }

    syncDirectFloatChildren(root);

    for (const child of Array.from(root.children)) {
        syncFloatLayoutTree(child);
    }
}

function getMainAxisStart(element, isRow) {
    const styles = window.getComputedStyle(element);
    return (isRow ? element.offsetLeft : element.offsetTop)
        - parsePx(isRow ? styles.marginLeft : styles.marginTop);
}

function getMainAxisEnd(element, isRow) {
    const styles = window.getComputedStyle(element);
    return isRow
        ? element.offsetLeft + element.offsetWidth + parsePx(styles.marginRight)
        : element.offsetTop + element.offsetHeight + parsePx(styles.marginBottom);
}

function syncDirectFloatChildren(parent) {
    const children = Array.from(parent.children || []);
    if (children.length === 0) {
        return;
    }

    const parentStyles = window.getComputedStyle(parent);
    const isRow = parentStyles.display === 'flex' || parentStyles.display === 'inline-flex'
        ? parentStyles.flexDirection.startsWith('row')
        : false;
    const mainGap = parsePx(isRow ? parentStyles.columnGap : parentStyles.rowGap) || parsePx(parentStyles.gap);

    for (let index = 0; index < children.length; index++) {
        const child = children[index];
        if (!isFloatElement(child)) {
            continue;
        }

        let mainPos = 0;
        let hasAnchor = false;

        for (let previousIndex = index - 1; previousIndex >= 0; previousIndex--) {
            const previousSibling = children[previousIndex];
            if (isFloatElement(previousSibling)) {
                continue;
            }

            mainPos = getMainAxisEnd(previousSibling, isRow) + mainGap;
            hasAnchor = true;
            break;
        }

        if (!hasAnchor) {
            for (let nextIndex = index + 1; nextIndex < children.length; nextIndex++) {
                const nextSibling = children[nextIndex];
                if (isFloatElement(nextSibling)) {
                    continue;
                }

                mainPos = getMainAxisStart(nextSibling, isRow);
                hasAnchor = true;
                break;
            }
        }

        if (isRow) {
            child.style.left = `${mainPos}px`;
            child.style.top = '0px';
        } else {
            child.style.left = '0px';
            child.style.top = `${mainPos}px`;
        }

        if (hasFloatAutoWidth(child)) {
            const parentPaddingLeft = parsePx(parentStyles.paddingLeft);
            const parentPaddingRight = parsePx(parentStyles.paddingRight);
            const childStyles = window.getComputedStyle(child);
            const availableWidth = Math.max(
                0,
                parent.clientWidth
                - parentPaddingLeft
                - parentPaddingRight
                - parsePx(childStyles.marginLeft)
                - parsePx(childStyles.marginRight));
            child.style.width = `${availableWidth}px`;
        }
    }
}

async function handleEvent(e, elementId) {
    let eventArgs = {};
    const eventType = e.type;
    const element = getElement(elementId);

    if ((eventType === 'mousemove' || eventType === 'mousedown' || eventType === 'mouseup') && element) {
        const rect = element.getBoundingClientRect();
        eventArgs = {
            X: Math.round(e.clientX - rect.left),
            Y: Math.round(e.clientY - rect.top),
            Button: e.button
        };
    } else if (eventType === 'click') {
        eventArgs = e.button;
    } else if (eventType === 'keydown' || eventType === 'keyup') {
        eventArgs = e.keyCode;
    } else if (eventType === 'input') {
        eventArgs = e.target.value;
    }

    try {
        await window.EchoUIHelper.RaiseEventAsync(elementId, eventType, JSON.stringify(eventArgs));
    } catch (error) {
        console.error(`Error invoking .NET method for event '${eventType}' on element '${elementId}':`, error);
    }
}

function getElement(elementId) {
    return elementRegistry.get(elementId) || document.getElementById(elementId);
}

export const dom = {
    createElement: (elementId, type) => {
        const el = document.createElement(type);
        el.__echoUiId = elementId;
        el._eventListeners = {};
        el._managedListeners = {};
        elementRegistry.set(elementId, el);
        return el;
    },

    patchProperties: (elementId, patchJson) => {
        const el = getElement(elementId);
        if (!el) {
            logDebug('patchProperties skipped for missing element', elementId);
            return;
        }

        const patch = JSON.parse(patchJson);

        if (patch.styles) {
            for (const [key, value] of Object.entries(patch.styles)) {
                el.style[key] = value ?? '';
            }
        }

        if (patch.attributes) {
            for (const [key, value] of Object.entries(patch.attributes)) {
                if (key in el && key !== 'style' && key !== 'class') {
                    el[key] = value;
                } else {
                    el.setAttribute(key, value);
                }
            }
        }

        if (patch.attributesToRemove) {
            for (const key of patch.attributesToRemove) {
                if (key === 'textContent') {
                    el.textContent = '';
                } else if (key === 'value') {
                    el.value = '';
                } else {
                    el.removeAttribute(key);
                }
            }
        }

        const currentListeners = el._eventListeners || {};
        let isChangeListeners = false;

        if (patch.eventsToRemove) {
            for (const eventName of patch.eventsToRemove) {
                if (currentListeners[eventName]) {
                    el.removeEventListener(eventName, currentListeners[eventName]);
                    delete currentListeners[eventName];
                }
            }
            isChangeListeners = true;
        }

        if (patch.eventsToAdd) {
            for (const eventName of patch.eventsToAdd) {
                if (!currentListeners[eventName]) {
                    const handler = (e) => handleEvent(e, elementId);
                    el.addEventListener(eventName, handler);
                    currentListeners[eventName] = handler;
                }
            }
            isChangeListeners = true;
        }

        if (isChangeListeners) {
            el._eventListeners = currentListeners;
        }

        applyManagedInputBorder(el);
        scheduleFloatLayout(el);
    },

    addChild: (parentId, childId, index) => {
        const parent = getElement(parentId);
        const child = getElement(childId);
        if (!parent || !child) return;
        const referenceNode = parent.children[index] || null;
        parent.insertBefore(child, referenceNode);
        scheduleFloatLayout(parent);
    },

    removeChild: (parentId, childId) => {
        const parent = getElement(parentId);
        const child = getElement(childId);
        if (!child) {
            return;
        }

        cleanupSubtree(child);

        if (parent && child.parentElement === parent) {
            parent.removeChild(child);
            scheduleFloatLayout(parent);
        }
    },

    moveChild: (parentId, childId, newIndex) => {
        const parent = getElement(parentId);
        const child = getElement(childId);
        if (parent && child) {
            const referenceNode = parent.children[newIndex] || null;
            parent.insertBefore(child, referenceNode);
            scheduleFloatLayout(parent);
        }
    }
};

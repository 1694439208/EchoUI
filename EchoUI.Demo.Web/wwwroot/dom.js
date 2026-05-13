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

function raiseLogicalEvent(elementId, eventName, eventArgs) {
    if (!elementId || !window.EchoUIHelper?.RaiseEventAsync) {
        return;
    }

    Promise.resolve(window.EchoUIHelper.RaiseEventAsync(elementId, eventName, JSON.stringify(eventArgs ?? {})))
        .catch(error => console.error(`Error invoking .NET method for logical event '${eventName}' on element '${elementId}':`, error));
}

let textInputProxy = null;
let activeTextInputTarget = null;
let suppressNextProxyInput = false;

function clearTextInputProxyValue() {
    if (!textInputProxy) {
        return;
    }

    textInputProxy.value = '';
    textInputProxy.setSelectionRange(0, 0);
}

function syncTextInputProxyPosition(target) {
    if (!textInputProxy || !target) {
        return;
    }

    const rect = target.getBoundingClientRect();
    const styles = window.getComputedStyle(target);
    const imeXRaw = target.getAttribute('data-eui-ime-x');
    const imeYRaw = target.getAttribute('data-eui-ime-y');
    const imeX = imeXRaw === null ? null : parsePx(imeXRaw);
    const imeY = imeYRaw === null ? null : parsePx(imeYRaw);
    const fallbackX = parsePx(styles.paddingLeft) + 1;
    const fallbackY = Math.max(0, rect.height / 2 - 1);
    const left = rect.left + (imeX ?? fallbackX);
    const top = rect.top + (imeY ?? fallbackY);

    textInputProxy.style.left = `${Math.round(left)}px`;
    textInputProxy.style.top = `${Math.round(top)}px`;
    textInputProxy.style.fontFamily = styles.fontFamily;
    textInputProxy.style.fontSize = styles.fontSize;
    textInputProxy.style.fontWeight = styles.fontWeight;
    textInputProxy.style.lineHeight = styles.lineHeight;
}

function deactivateTextInputProxy(shouldBlurTarget) {
    if (!textInputProxy) {
        return;
    }

    const previousTarget = activeTextInputTarget;
    activeTextInputTarget = null;
    suppressNextProxyInput = false;
    clearTextInputProxyValue();

    if (document.activeElement === textInputProxy) {
        textInputProxy.blur();
    }

    if (shouldBlurTarget && previousTarget?.__echoUiId) {
        raiseLogicalEvent(previousTarget.__echoUiId, 'blur', {});
    }
}

function ensureTextInputProxy() {
    if (textInputProxy) {
        return textInputProxy;
    }

    textInputProxy = document.createElement('textarea');
    textInputProxy.setAttribute('aria-hidden', 'true');
    textInputProxy.setAttribute('tabindex', '-1');
    textInputProxy.setAttribute('autocomplete', 'off');
    textInputProxy.setAttribute('autocorrect', 'off');
    textInputProxy.setAttribute('autocapitalize', 'off');
    textInputProxy.setAttribute('spellcheck', 'false');
    textInputProxy.style.position = 'fixed';
    textInputProxy.style.left = '-10000px';
    textInputProxy.style.top = '-10000px';
    textInputProxy.style.width = '1px';
    textInputProxy.style.height = '1px';
    textInputProxy.style.opacity = '0';
    textInputProxy.style.pointerEvents = 'none';
    textInputProxy.style.resize = 'none';
    textInputProxy.style.border = '0';
    textInputProxy.style.padding = '0';
    textInputProxy.style.margin = '0';
    textInputProxy.style.background = 'transparent';
    textInputProxy.style.color = 'transparent';
    textInputProxy.style.caretColor = 'transparent';
    textInputProxy.style.outline = 'none';
    textInputProxy.style.overflow = 'hidden';
    document.body.appendChild(textInputProxy);

    textInputProxy.addEventListener('keydown', (e) => {
        if (!activeTextInputTarget?.__echoUiId) {
            return;
        }

        raiseLogicalEvent(activeTextInputTarget.__echoUiId, 'keydown', e.keyCode);
    });

    textInputProxy.addEventListener('keyup', (e) => {
        if (!activeTextInputTarget?.__echoUiId) {
            return;
        }

        raiseLogicalEvent(activeTextInputTarget.__echoUiId, 'keyup', e.keyCode);
    });

    textInputProxy.addEventListener('compositionstart', () => {
        if (!activeTextInputTarget?.__echoUiId) {
            return;
        }

        suppressNextProxyInput = false;
        raiseLogicalEvent(activeTextInputTarget.__echoUiId, 'textcomposition', { phase: 0, text: '' });
    });

    textInputProxy.addEventListener('compositionupdate', (e) => {
        if (!activeTextInputTarget?.__echoUiId) {
            return;
        }

        raiseLogicalEvent(activeTextInputTarget.__echoUiId, 'textcomposition', { phase: 1, text: e.data ?? '' });
    });

    textInputProxy.addEventListener('compositionend', (e) => {
        if (!activeTextInputTarget?.__echoUiId) {
            return;
        }

        suppressNextProxyInput = true;
        const committedText = e.data ?? '';
        if (committedText.length > 0) {
            raiseLogicalEvent(activeTextInputTarget.__echoUiId, 'textcomposition', { phase: 2, text: committedText });
        }
        raiseLogicalEvent(activeTextInputTarget.__echoUiId, 'textcomposition', { phase: 3, text: '' });
        clearTextInputProxyValue();
    });

    textInputProxy.addEventListener('input', (e) => {
        if (!activeTextInputTarget?.__echoUiId) {
            clearTextInputProxyValue();
            return;
        }

        const inputText = e.data ?? textInputProxy.value ?? '';
        if (suppressNextProxyInput) {
            suppressNextProxyInput = false;
            clearTextInputProxyValue();
            return;
        }

        if (inputText.length > 0) {
            raiseLogicalEvent(activeTextInputTarget.__echoUiId, 'keypress', inputText);
        }

        clearTextInputProxyValue();
    });

    textInputProxy.addEventListener('blur', () => {
        if (!activeTextInputTarget?.__echoUiId) {
            return;
        }

        const previousTargetId = activeTextInputTarget.__echoUiId;
        activeTextInputTarget = null;
        suppressNextProxyInput = false;
        clearTextInputProxyValue();
        raiseLogicalEvent(previousTargetId, 'blur', {});
    });

    document.addEventListener('mousedown', (e) => {
        const imeTarget = e.target?.closest?.('[data-eui-ime-handler="true"]') ?? null;
        if (imeTarget || !activeTextInputTarget) {
            return;
        }

        deactivateTextInputProxy(true);
    }, true);

    window.addEventListener('resize', () => {
        if (activeTextInputTarget) {
            syncTextInputProxyPosition(activeTextInputTarget);
        }
    });

    return textInputProxy;
}

function activateTextInputProxy(target) {
    if (!target?.__echoUiId) {
        return;
    }

    const proxy = ensureTextInputProxy();
    const previousTarget = activeTextInputTarget;
    if (previousTarget && previousTarget !== target && previousTarget.__echoUiId) {
        raiseLogicalEvent(previousTarget.__echoUiId, 'blur', {});
    }

    activeTextInputTarget = target;
    suppressNextProxyInput = false;
    syncTextInputProxyPosition(target);
    clearTextInputProxyValue();
    proxy.focus({ preventScroll: true });

    if (previousTarget !== target) {
        raiseLogicalEvent(target.__echoUiId, 'focus', {});
    }
}

function applyManagedKeyboardFocus(element) {
    if (!element) {
        return;
    }

    const hasKeyboardHandler = element.getAttribute('data-eui-keyboard-handler') === 'true';
    const hasImeHandler = element.getAttribute('data-eui-ime-handler') === 'true';

    if (element._managedListeners?.echoUiKeyboardFocus && (!hasKeyboardHandler || hasImeHandler)) {
        element.removeEventListener('mousedown', element._managedListeners.echoUiKeyboardFocus.handler);
        delete element._managedListeners.echoUiKeyboardFocus;
    }

    if (element._managedListeners?.echoUiImeFocus && !hasImeHandler) {
        element.removeEventListener('mousedown', element._managedListeners.echoUiImeFocus.handler);
        delete element._managedListeners.echoUiImeFocus;
    }

    if (activeTextInputTarget === element && !hasImeHandler) {
        deactivateTextInputProxy(true);
    }

    if (hasImeHandler) {
        ensureManagedListener(element, 'echoUiImeFocus', 'mousedown', (e) => {
            e.preventDefault();
            activateTextInputProxy(element);
        });
        return;
    }

    if (!hasKeyboardHandler) {
        return;
    }

    ensureManagedListener(element, 'echoUiKeyboardFocus', 'mousedown', () => {
        if (document.activeElement !== element) {
            element.focus();
        }
    });
}

function cleanupSubtree(element) {
    if (!element) {
        return;
    }

    if (activeTextInputTarget && (element === activeTextInputTarget || element.contains(activeTextInputTarget))) {
        deactivateTextInputProxy(true);
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
    } else if (eventType === 'keypress') {
        eventArgs = e.key;
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

const textMeasureCanvas = document.createElement('canvas');
const textMeasureContext = textMeasureCanvas.getContext('2d');

function normalizeFontFamily(fontFamily) {
    if (!fontFamily || fontFamily.trim().length === 0) {
        return 'sans-serif';
    }

    return fontFamily
        .split(',')
        .map(part => {
            const trimmed = part.trim();
            if (trimmed.length === 0) {
                return trimmed;
            }

            if (trimmed.startsWith('"') || trimmed.startsWith("'")) {
                return trimmed;
            }

            return trimmed.includes(' ') ? `"${trimmed.replaceAll('"', '\\"')}"` : trimmed;
        })
        .join(', ');
}

function buildCanvasFont(fontFamily, fontSize, fontWeight) {
    const resolvedSize = Number.isFinite(fontSize) ? fontSize : 14;
    const resolvedWeight = fontWeight && fontWeight.trim().length > 0 ? fontWeight : '400';
    return `${resolvedWeight} ${resolvedSize}px ${normalizeFontFamily(fontFamily)}`;
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
        applyManagedKeyboardFocus(el);
        if (activeTextInputTarget === el) {
            syncTextInputProxyPosition(el);
        }
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
    },

    measureText: (text, fontFamily, fontSize, fontWeight) => {
        if (!textMeasureContext) {
            return 0;
        }

        textMeasureContext.font = buildCanvasFont(fontFamily, fontSize, fontWeight);
        return textMeasureContext.measureText(text ?? '').width;
    }
};

using System.Collections;

namespace EchoUI.Core
{
    /// <summary>
    /// 颜色
    /// </summary>
    public readonly record struct Color(byte R, byte G, byte B, byte A = 255)
    {
        public static Color White => new(255, 255, 255);
        public static Color Black => new(0, 0, 0);
        public static Color Red => new(255, 0, 0);
        public static Color Green => new(0, 255, 0);
        public static Color Blue => new(0, 0, 255);
        public static Color Gray => new(128, 128, 128);
        public static Color LightGray => new(211, 211, 211);
        public static Color Gainsboro => new(220, 220, 220);
        public static Color Transparent => new(0, 0, 0, 0);

        public static Color FromHex(string hex)
        {
            hex = hex.TrimStart('#');
            return new Color(
                Convert.ToByte(hex.Substring(0, 2), 16),
                Convert.ToByte(hex.Substring(2, 2), 16),
                Convert.ToByte(hex.Substring(4, 2), 16),
                hex.Length >= 8 ? Convert.ToByte(hex.Substring(6, 2), 16) : (byte)255
            );
        }

        public static Color FromRgb(byte r, byte g, byte b)
        {
            return new Color(
                r,
                g,
                b,
                255
            );
        }
        public Color WithAlpha(byte alpha)
        {
            return new Color(R, G, B, alpha);
        }
    }

    /// <summary>
    /// 代表一个尺寸值。它可以是固定的像素值、父容器的百分比，或是自动计算。
    /// </summary>
    public readonly record struct Dimension(float Value, DimensionUnit Unit)
    {
        /// <summary>
        /// 0像素
        /// </summary>
        public static Dimension ZeroPixels => new(0, DimensionUnit.Pixels);
        /// <summary>
        /// 创建一个以像素为单位的尺寸。
        /// </summary>
        public static Dimension Pixels(float value) => new(value, DimensionUnit.Pixels);
        /// <summary>
        /// 创建一个以百分比为单位的尺寸。
        /// </summary>
        public static Dimension Percent(float value) => new(value, DimensionUnit.Percent);
        public static Dimension ViewportHeight(float value) => new(value, DimensionUnit.ViewportHeight);
    }

    /// <summary>
    /// 尺寸单位的枚举。
    /// </summary>
    public enum DimensionUnit { Pixels, Percent, ViewportHeight }

    /// <summary>
    /// 内容溢出处理方式。
    /// </summary>
    public enum Overflow { Visible, Hidden, Scroll, Auto }

    /// <summary>
    /// 代表边距（Margin）或填充（Padding）的间距值，可以独立控制四个方向。
    /// </summary>
    public readonly record struct Spacing(Dimension Left, Dimension Top, Dimension Right, Dimension Bottom)
    {
        /// <summary>
        /// 为四个方向应用相同的间距值。
        /// </summary>
        public Spacing(Dimension all) : this(all, all, all, all) { }
        /// <summary>
        /// 分别为水平和垂直方向应用不同的间距值。
        /// </summary>
        public Spacing(Dimension horizontal, Dimension vertical) : this(horizontal, vertical, horizontal, vertical) { }
    }

    /// <summary>
    /// 代表一个二维坐标点。
    /// </summary>
    public readonly record struct Point(int X, int Y);

    /// <summary>
    /// 定义一个属性过渡动画。
    /// </summary>
    /// <param name="DurationMs">动画的持续时间（毫秒）。</param>
    /// <param name="Easing">动画使用的缓动函数。</param>
    public readonly record struct Transition(int DurationMs, Easing Easing = Easing.Linear);

    /// <summary>
    /// 动画缓动函数的通用枚举。
    /// </summary>
    public enum Easing
    {
        Linear,
        Ease,
        EaseIn,
        EaseOut,
        EaseInOut
    }

    /// <summary>
    /// 布局方向（用于容器）。
    /// </summary>
    public enum LayoutDirection { Vertical, Horizontal }
    /// <summary>
    /// 主轴对齐方式（决定子元素在主轴上的排列）。
    /// </summary>
    public enum JustifyContent { Start, Center, End, SpaceBetween, SpaceAround }
    /// <summary>
    /// 交叉轴对齐方式（决定子元素在交叉轴上的排列）。
    /// </summary>
    public enum AlignItems { Start, Center, End, Stretch }
    /// <summary>
    /// 边框样式。
    /// </summary>
    public enum BorderStyle { None, Solid, Dashed, Dotted }

    /// <summary>
    /// 定义鼠标按键的类型。
    /// </summary>
    public enum MouseButton { Left, Right, Middle }

    public readonly record struct MouseEvent(Point Position, MouseButton Button);

    public enum TextCompositionPhase
    {
        Start,
        Update,
        Commit,
        End
    }

    public record class TextCompositionEvent
    {
        public TextCompositionPhase Phase { get; init; }
        public string Text { get; init; } = string.Empty;
    }

    /// <summary>
    /// A value-type wrapper for a dictionary that exposes its data
    /// as a list of key-value pairs.
    /// </summary>
    public readonly record struct ValueDictionary<TKey, TValue> : IList<IList<object?>> where TKey : notnull
    {
        public IDictionary<TKey, TValue?> Data { get; }

        // --- Implemented Properties ---

        /// <summary>
        /// Gets the number of key/value pairs contained in the dictionary.
        /// </summary>
        public int Count => Data.Count;

        /// <summary>
        /// Gets a value indicating whether the dictionary is read-only.
        /// </summary>
        public bool IsReadOnly => Data.IsReadOnly;

        // --- Constructor ---

        public ValueDictionary(IDictionary<TKey, TValue?> data)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
        }


        public ValueDictionary()
        {
            Data = new Dictionary<TKey, TValue?>();
        }

        // --- Helper Method ---

        /// <summary>
        /// Safely parses an IList<object?> into a key and value.
        /// </summary>
        private static bool TryParseItem(IList<object?> item, out TKey key, out TValue? value)
        {
            key = default!;
            value = default;

            if (item == null || item.Count != 2 || item[0] is not TKey parsedKey)
            {
                return false;
            }

            key = parsedKey;
            // Allows value to be null if TValue is a reference type or Nullable<T>
            if (item[1] is TValue parsedValue)
            {
                value = parsedValue;
            }
            else if (item[1] == null)
            {
                // This is valid if TValue can be null
                value = default;
            }
            else
            {
                return false;
            }

            return true;
        }


        // --- IList<IList<object?>> Implementation ---

        /// <summary>
        /// Gets the key-value pair at the specified index.
        /// Setting a value by index is not supported.
        /// </summary>
        public IList<object?> this[int index]
        {
            get
            {
                if (index < 0 || index >= Data.Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }
                var kvp = Data.ElementAt(index);
                return new List<object?> { kvp.Key, kvp.Value };
            }
            set => throw new NotSupportedException("Replacing an item by index is not supported in a dictionary-backed list.");
        }

        /// <summary>
        /// Determines the index of a specific key-value pair in the dictionary.
        /// </summary>
        public int IndexOf(IList<object?> item)
        {
            if (!TryParseItem(item, out var key, out var value))
            {
                return -1;
            }

            if (Data.TryGetValue(key, out var existingValue) && EqualityComparer<TValue>.Default.Equals(value, existingValue))
            {
                int i = 0;
                foreach (var kvpKey in Data.Keys)
                {
                    if (kvpKey.Equals(key))
                    {
                        return i;
                    }
                    i++;
                }
            }

            return -1;
        }

        /// <summary>
        /// Inserting an element at a specific index is not supported.
        /// </summary>
        public void Insert(int index, IList<object?> item)
        {
            if (IsReadOnly) throw new NotSupportedException("Collection is read-only.");
            if (!TryParseItem(item, out var key, out var value))
            {
                throw new ArgumentException("Item must be an IList<object?> with two elements: a non-null key of type TKey and a value of type TValue.", nameof(item));
            }
            Data[key] = value;
        }

        /// <summary>
        /// Removes the key-value pair at the specified index.
        /// </summary>
        public void RemoveAt(int index)
        {
            if (IsReadOnly) throw new NotSupportedException("Collection is read-only.");
            if (index < 0 || index >= Data.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            var keyToRemove = Data.Keys.ElementAt(index);
            Data.Remove(keyToRemove);
        }

        /// <summary>
        /// Adds a key-value pair to the dictionary.
        /// </summary>
        public void Add(IList<object?> item)
        {
            if (IsReadOnly) throw new NotSupportedException("Collection is read-only.");
            if (!TryParseItem(item, out var key, out var value))
            {
                throw new ArgumentException("Item must be an IList<object?> with two elements: a non-null key of type TKey and a value of type TValue.", nameof(item));
            }
            Data[key] = value;
        }

        /// <summary>
        /// Removes all keys and values from the dictionary.
        /// </summary>
        public void Clear()
        {
            if (IsReadOnly) throw new NotSupportedException("Collection is read-only.");
            Data.Clear();
        }

        /// <summary>
        /// Determines whether the dictionary contains a specific key-value pair.
        /// </summary>
        public bool Contains(IList<object?> item)
        {
            if (!TryParseItem(item, out var key, out var value))
            {
                return false;
            }

            return Data.TryGetValue(key, out var existingValue) && EqualityComparer<TValue>.Default.Equals(value, existingValue);
        }

        /// <summary>
        /// Copies the key-value pairs of the dictionary to an Array, starting at a particular Array index.
        /// </summary>
        public void CopyTo(IList<object?>[] array, int arrayIndex)
        {
            if (array == null) throw new ArgumentNullException(nameof(array));
            if (arrayIndex < 0) throw new ArgumentOutOfRangeException(nameof(arrayIndex));
            if (array.Length - arrayIndex < Data.Count) throw new ArgumentException("The destination array has insufficient space.");

            int i = 0;
            foreach (var kvp in Data)
            {
                array[arrayIndex + i] = new List<object?> { kvp.Key, kvp.Value };
                i++;
            }
        }

        /// <summary>
        /// Removes the first occurrence of a specific key-value pair from the dictionary.
        /// </summary>
        public bool Remove(IList<object?> item)
        {
            if (IsReadOnly) throw new NotSupportedException("Collection is read-only.");
            if (!TryParseItem(item, out var key, out var value))
            {
                return false;
            }

            // Must check value equality before removing, per ICollection<KeyValuePair> contract
            if (Data.TryGetValue(key, out var existingValue) && EqualityComparer<TValue>.Default.Equals(value, existingValue))
            {
                return Data.Remove(key);
            }

            return false;
        }

        /// <summary>
        /// Returns an enumerator that iterates through the collection as key-value pair lists.
        /// </summary>
        public IEnumerator<IList<object?>> GetEnumerator()
        {
            foreach (var kvp in Data)
            {
                yield return new List<object?> { kvp.Key, kvp.Value };
            }
        }

        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        // --- Equality and HashCode (already implemented) ---

        public bool Equals(ValueDictionary<TKey, TValue> other) =>
            Data.Count == other.Data.Count &&
            Data.All(kvp =>
                other.Data.TryGetValue(kvp.Key, out var v) &&
                EqualityComparer<TValue>.Default.Equals(kvp.Value, v));

        public override int GetHashCode() =>
            Data.Aggregate(0, (hash, kvp) => hash ^ HashCode.Combine(kvp.Key, kvp.Value));

        // --- Implicit Operator (already implemented) ---

        public static implicit operator ValueDictionary<TKey, TValue>(Dictionary<TKey, TValue> dictionary) =>
            new(dictionary);
    }
}

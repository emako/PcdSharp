using System.Globalization;
using System.Reflection;

namespace PcdSharp.IO;

/// <summary>
/// 点类型工厂，用于动态创建和填充点对象
/// </summary>
/// <typeparam name="PointT">点类型</typeparam>
public class PointFactory<PointT> where PointT : new()
{
    private readonly List<FieldMapping> _fieldMappings;
    private readonly Func<PointT> _constructor;
    private readonly Dictionary<string, Action<PointContainer<PointT>, string>> _asciiSetters;
    private readonly Dictionary<string, Action<PointContainer<PointT>, byte[], int>> _binarySetters;

    public PointFactory(PCDHeader header)
    {
        _fieldMappings = CreateFieldMappings(header);
        _constructor = () => new PointT();
        _asciiSetters = CreateAsciiSetters();
        _binarySetters = CreateBinarySetters();
    }

    public List<FieldMapping> FieldMappings => _fieldMappings;

    /// <summary>
    /// 从ASCII数据创建点
    /// </summary>
    public PointT CreateFromAscii(string[] parts)
    {
        var point = _constructor();
        var container = new PointContainer<PointT>(point);

        // 按照原始字段顺序处理数据
        foreach (var mapping in _fieldMappings)
        {
            var fieldIndex = mapping.FieldIndex;

            if (fieldIndex < parts.Length && _asciiSetters.TryGetValue(mapping.FieldName, out var setter))
            {
                setter(container, parts[fieldIndex]);
            }
        }

        return container.Value;
    }

    /// <summary>
    /// 从Binary数据创建点
    /// </summary>
    public unsafe PointT CreateFromBinary(byte[] buffer)
    {
        var point = _constructor();
        var container = new PointContainer<PointT>(point);

        foreach (var mapping in _fieldMappings)
        {
            if (_binarySetters.TryGetValue(mapping.FieldName, out var setter))
            {
                setter(container, buffer, mapping.Offset);
            }
        }

        return container.Value;
    }

    private List<FieldMapping> CreateFieldMappings(PCDHeader header)
    {
        var mappings = new List<FieldMapping>();
        var pointType = typeof(PointT);
        var properties = pointType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var fields = pointType.GetFields(BindingFlags.Public | BindingFlags.Instance);

        int offset = 0;
        for (int i = 0; i < header.Fields.Count; i++)
        {
            var fieldName = header.Fields[i].ToLower();
            var size = header.Size[i];
            var dataType = header.Type[i];

            // 先尝试找到匹配的属性
            var property = FindMatchingProperty(properties, fieldName);
            if (property != null)
            {
                mappings.Add(new FieldMapping
                {
                    FieldName = fieldName,
                    PropertyName = property.Name,
                    PropertyType = property.PropertyType,
                    PropertyInfo = property,
                    FieldIndex = i,
                    Offset = offset,
                    Size = size,
                    DataType = dataType
                });
            }
            else
            {
                // 如果没有找到属性，尝试找字段
                var field = FindMatchingField(fields, fieldName);
                if (field != null)
                {
                    mappings.Add(new FieldMapping
                    {
                        FieldName = fieldName,
                        PropertyName = field.Name,
                        PropertyType = field.FieldType,
                        FieldInfo = field,
                        FieldIndex = i,
                        Offset = offset,
                        Size = size,
                        DataType = dataType
                    });
                }
            }

            offset += size;
        }

        return mappings;
    }

    private PropertyInfo? FindMatchingProperty(PropertyInfo[] properties, string fieldName)
    {
        // 直接匹配
        var direct = properties.FirstOrDefault(p =>
            string.Equals(p.Name, fieldName, StringComparison.OrdinalIgnoreCase));
        if (direct != null) return direct;

        // 常见字段映射
        var mappings = new Dictionary<string, string[]>
        {
            { "x", ["X", "x"] },
            { "y", ["Y", "y"] },
            { "z", ["Z", "z"] },
            { "rgb", ["RGB", "RGBA", "rgb", "rgba"] },
            { "rgba", ["RGBA", "RGB", "rgba", "rgb"] },
            { "r", ["R", "r"] },
            { "g", ["G", "g"] },
            { "b", ["B", "b"] },
            { "a", ["A", "a"] },
            { "normal_x", ["NormalX", "NX", "normal_x"] },
            { "normal_y", ["NormalY", "NY", "normal_y"] },
            { "normal_z", ["NormalZ", "NZ", "normal_z"] },
            { "curvature", ["Curvature", "curvature"] },
            { "label", ["Label", "label"] },
        };

        if (mappings.TryGetValue(fieldName, out var candidates))
        {
            foreach (var candidate in candidates)
            {
                var prop = properties.FirstOrDefault(p =>
                    string.Equals(p.Name, candidate, StringComparison.OrdinalIgnoreCase));
                if (prop != null) return prop;
            }
        }

        return null;
    }

    private FieldInfo? FindMatchingField(FieldInfo[] fields, string fieldName)
    {
        // 直接匹配
        var direct = fields.FirstOrDefault(f =>
            string.Equals(f.Name, fieldName, StringComparison.OrdinalIgnoreCase));
        if (direct != null) return direct;

        // 常见字段映射
        var mappings = new Dictionary<string, string[]>
        {
            { "x", ["X", "x"] },
            { "y", ["Y", "y"] },
            { "z", ["Z", "z"] },
            { "rgb", ["RGB", "RGBA", "rgb", "rgba"] },
            { "rgba", ["RGBA", "RGB", "rgba", "rgb"] },
            { "r", ["R", "r"] },
            { "g", ["G", "g"] },
            { "b", ["B", "b"] },
            { "a", ["A", "a"] },
            { "normal_x", ["NormalX", "NX", "normal_x"] },
            { "normal_y", ["NormalY", "NY", "normal_y"] },
            { "normal_z", ["NormalZ", "NZ", "normal_z"] },
            { "curvature", ["Curvature", "curvature"] },
            { "label", ["Label", "label"] },
        };

        if (mappings.TryGetValue(fieldName, out var candidates))
        {
            foreach (var candidate in candidates)
            {
                var field = fields.FirstOrDefault(f =>
                    string.Equals(f.Name, candidate, StringComparison.OrdinalIgnoreCase));
                if (field != null) return field;
            }
        }

        return null;
    }

    private Dictionary<string, Action<PointContainer<PointT>, string>> CreateAsciiSetters()
    {
        var setters = new Dictionary<string, Action<PointContainer<PointT>, string>>();

        foreach (var mapping in _fieldMappings)
        {
            Action<PointContainer<PointT>, string>? setter = null;

            if (mapping.IsProperty)
            {
                setter = CreateAsciiPropertySetter(mapping.PropertyInfo!);
            }
            else if (mapping.IsField)
            {
                setter = CreateAsciiFieldSetter(mapping.FieldInfo!);
            }

            if (setter != null)
            {
                setters[mapping.FieldName] = setter;
            }
        }

        return setters;
    }

    private Dictionary<string, Action<PointContainer<PointT>, byte[], int>> CreateBinarySetters()
    {
        var setters = new Dictionary<string, Action<PointContainer<PointT>, byte[], int>>();

        foreach (var mapping in _fieldMappings)
        {
            Action<PointContainer<PointT>, byte[], int>? setter = null;

            if (mapping.IsProperty)
            {
                setter = CreateBinaryPropertySetter(mapping.PropertyInfo!);
            }
            else if (mapping.IsField)
            {
                setter = CreateBinaryFieldSetter(mapping.FieldInfo!);
            }

            if (setter != null)
            {
                setters[mapping.FieldName] = setter;
            }
        }

        return setters;
    }

    private Action<PointContainer<PointT>, string>? CreateAsciiPropertySetter(PropertyInfo property)
    {
        return (container, value) =>
        {
            try
            {
                object parsedValue;

                if (property.PropertyType == typeof(float))
                {
                    parsedValue = float.Parse(value, CultureInfo.InvariantCulture);
                }
                else if (property.PropertyType == typeof(double))
                {
                    parsedValue = double.Parse(value, CultureInfo.InvariantCulture);
                }
                else if (property.PropertyType == typeof(int))
                {
                    parsedValue = int.Parse(value);
                }
                else if (property.PropertyType == typeof(uint))
                {
                    parsedValue = uint.Parse(value);
                }
                else if (property.PropertyType == typeof(byte))
                {
                    parsedValue = byte.Parse(value);
                }
                else
                {
                    return; // 不支持的类型
                }

                var boxed = (object?)container.Value;
                property.SetValue(boxed, parsedValue);
                container.Value = (PointT)boxed!;
            }
            catch
            {
                // 忽略解析错误
            }
        };
    }

    private Action<PointContainer<PointT>, string>? CreateAsciiFieldSetter(FieldInfo field)
    {
        return (container, value) =>
        {
            try
            {
                object parsedValue;

                if (field.FieldType == typeof(float))
                {
                    parsedValue = float.Parse(value, CultureInfo.InvariantCulture);
                }
                else if (field.FieldType == typeof(double))
                {
                    parsedValue = double.Parse(value, CultureInfo.InvariantCulture);
                }
                else if (field.FieldType == typeof(int))
                {
                    parsedValue = int.Parse(value);
                }
                else if (field.FieldType == typeof(uint))
                {
                    parsedValue = uint.Parse(value);
                }
                else if (field.FieldType == typeof(byte))
                {
                    parsedValue = byte.Parse(value);
                }
                else
                {
                    return; // 不支持的类型
                }

                var boxed = (object?)container.Value;
                field.SetValue(boxed, parsedValue);
                container.Value = (PointT)boxed!;
            }
            catch
            {
                // 忽略解析错误
            }
        };
    }

    private unsafe Action<PointContainer<PointT>, byte[], int>? CreateBinaryPropertySetter(PropertyInfo property)
    {
        return (container, buffer, offset) =>
        {
            fixed (byte* ptr = buffer)
            {
                if (property.PropertyType == typeof(float))
                {
                    var value = *(float*)(ptr + offset);
                    var boxed = (object?)container.Value;
                    property.SetValue(boxed, value);
                    container.Value = (PointT)boxed!;
                }
                else if (property.PropertyType == typeof(double))
                {
                    var value = *(double*)(ptr + offset);
                    var boxed = (object?)container.Value;
                    property.SetValue(boxed, value);
                    container.Value = (PointT)boxed!;
                }
                else if (property.PropertyType == typeof(int))
                {
                    var value = *(int*)(ptr + offset);
                    var boxed = (object?)container.Value;
                    property.SetValue(boxed, value);
                    container.Value = (PointT)boxed!;
                }
                else if (property.PropertyType == typeof(uint))
                {
                    var value = *(uint*)(ptr + offset);
                    var boxed = (object?)container.Value;
                    property.SetValue(boxed, value);
                    container.Value = (PointT)boxed!;
                }
                else if (property.PropertyType == typeof(byte))
                {
                    var value = *(ptr + offset);
                    var boxed = (object?)container.Value;
                    property.SetValue(boxed, value);
                    container.Value = (PointT)boxed!;
                }
            }
        };
    }

    private unsafe Action<PointContainer<PointT>, byte[], int>? CreateBinaryFieldSetter(FieldInfo field)
    {
        return (container, buffer, offset) =>
        {
            fixed (byte* ptr = buffer)
            {
                var boxed = (object?)container.Value;

                if (field.FieldType == typeof(float))
                {
                    var value = *(float*)(ptr + offset);
                    field.SetValue(boxed, value);
                }
                else if (field.FieldType == typeof(double))
                {
                    var value = *(double*)(ptr + offset);
                    field.SetValue(boxed, value);
                }
                else if (field.FieldType == typeof(int))
                {
                    var value = *(int*)(ptr + offset);
                    field.SetValue(boxed, value);
                }
                else if (field.FieldType == typeof(uint))
                {
                    var value = *(uint*)(ptr + offset);
                    field.SetValue(boxed, value);
                }
                else if (field.FieldType == typeof(byte))
                {
                    var value = *(ptr + offset);
                    field.SetValue(boxed, value);
                }

                container.Value = (PointT)boxed!;
            }
        };
    }
}

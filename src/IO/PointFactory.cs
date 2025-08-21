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
    private readonly CoordinateTransformOptions? _transformOptions;

    public PointFactory(PCDHeader header, CoordinateTransformOptions? transformOptions = null)
    {
        _fieldMappings = CreateFieldMappings(header);
        _constructor = () => new PointT();
        _transformOptions = transformOptions;
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
            // 只处理有对应属性的字段
            if (mapping.PropertyInfo != null || mapping.FieldInfo != null)
            {
                if (_binarySetters.TryGetValue(mapping.FieldName, out var setter))
                {
                    setter(container, buffer, mapping.Offset);
                }
            }
            // 跳过没有对应属性的字段（如 "_" 占位符）
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
            var count = header.Count[i];
            var dataType = header.Type[i];
            var totalFieldSize = size * count; // 考虑COUNT字段

            // 先尝试找到匹配的属性
            var property = FindMatchingProperty(properties, fieldName);
            FieldMapping mapping;

            if (property != null)
            {
                mapping = new FieldMapping
                {
                    FieldName = fieldName,
                    PropertyName = property.Name,
                    PropertyType = property.PropertyType,
                    PropertyInfo = property,
                    FieldIndex = i,
                    Offset = offset,
                    Size = size,
                    Count = count,
                    DataType = dataType
                };
            }
            else
            {
                // 如果没有找到属性，尝试找字段
                var field = FindMatchingField(fields, fieldName);
                if (field != null)
                {
                    mapping = new FieldMapping
                    {
                        FieldName = fieldName,
                        PropertyName = field.Name,
                        PropertyType = field.FieldType,
                        FieldInfo = field,
                        FieldIndex = i,
                        Offset = offset,
                        Size = size,
                        Count = count,
                        DataType = dataType
                    };
                }
                else
                {
                    // 即使没有匹配的属性，也要创建字段映射以正确计算偏移量
                    mapping = new FieldMapping
                    {
                        FieldName = fieldName,
                        PropertyName = null, // 标记为没有对应属性
                        PropertyType = null,
                        PropertyInfo = null,
                        FieldInfo = null,
                        FieldIndex = i,
                        Offset = offset,
                        Size = size,
                        Count = count,
                        DataType = dataType
                    };
                }
            }

            mappings.Add(mapping);

            // 无论是否找到匹配的属性，都要更新偏移量
            offset += totalFieldSize;
        }

        return mappings;
    }

    private PropertyInfo? FindMatchingProperty(PropertyInfo[] properties, string fieldName)
    {
        // 直接匹配
        var direct = properties.FirstOrDefault(p =>
            string.Equals(p.Name, fieldName, StringComparison.OrdinalIgnoreCase));
        if (direct != null) return direct;

        if (FieldMapping.Mappings.TryGetValue(fieldName, out var candidates))
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

        if (FieldMapping.Mappings.TryGetValue(fieldName, out var candidates))
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
                    var floatValue = float.Parse(value, CultureInfo.InvariantCulture);
                    
                    // 应用坐标变换
                    if (_transformOptions?.NeedsTransformation == true)
                    {
                        var propertyName = property.Name.ToLower();
                        if (propertyName == "x")
                            floatValue *= _transformOptions.ScaleX;
                        else if (propertyName == "y")
                            floatValue *= _transformOptions.ScaleY;
                        else if (propertyName == "z")
                            floatValue *= _transformOptions.ScaleZ;
                        else if (propertyName == "normalx")
                            floatValue *= Math.Sign(_transformOptions.ScaleX);
                        else if (propertyName == "normaly")
                            floatValue *= Math.Sign(_transformOptions.ScaleY);
                        else if (propertyName == "normalz")
                            floatValue *= Math.Sign(_transformOptions.ScaleZ);
                    }
                    
                    parsedValue = floatValue;
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
                    var floatValue = float.Parse(value, CultureInfo.InvariantCulture);
                    
                    // 应用坐标变换
                    if (_transformOptions?.NeedsTransformation == true)
                    {
                        var fieldName = field.Name.ToLower();
                        if (fieldName == "x")
                            floatValue *= _transformOptions.ScaleX;
                        else if (fieldName == "y")
                            floatValue *= _transformOptions.ScaleY;
                        else if (fieldName == "z")
                            floatValue *= _transformOptions.ScaleZ;
                        else if (fieldName == "normalx")
                            floatValue *= Math.Sign(_transformOptions.ScaleX);
                        else if (fieldName == "normaly")
                            floatValue *= Math.Sign(_transformOptions.ScaleY);
                        else if (fieldName == "normalz")
                            floatValue *= Math.Sign(_transformOptions.ScaleZ);
                    }
                    
                    parsedValue = floatValue;
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
                    
                    // 应用坐标变换
                    if (_transformOptions?.NeedsTransformation == true)
                    {
                        var propertyName = property.Name.ToLower();
                        if (propertyName == "x")
                            value *= _transformOptions.ScaleX;
                        else if (propertyName == "y")
                            value *= _transformOptions.ScaleY;
                        else if (propertyName == "z")
                            value *= _transformOptions.ScaleZ;
                        else if (propertyName == "normalx")
                            value *= Math.Sign(_transformOptions.ScaleX);
                        else if (propertyName == "normaly")
                            value *= Math.Sign(_transformOptions.ScaleY);
                        else if (propertyName == "normalz")
                            value *= Math.Sign(_transformOptions.ScaleZ);
                    }
                    
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
                    
                    // 应用坐标变换
                    if (_transformOptions?.NeedsTransformation == true)
                    {
                        var fieldName = field.Name.ToLower();
                        if (fieldName == "x")
                            value *= _transformOptions.ScaleX;
                        else if (fieldName == "y")
                            value *= _transformOptions.ScaleY;
                        else if (fieldName == "z")
                            value *= _transformOptions.ScaleZ;
                        else if (fieldName == "normalx")
                            value *= Math.Sign(_transformOptions.ScaleX);
                        else if (fieldName == "normaly")
                            value *= Math.Sign(_transformOptions.ScaleY);
                        else if (fieldName == "normalz")
                            value *= Math.Sign(_transformOptions.ScaleZ);
                    }
                    
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

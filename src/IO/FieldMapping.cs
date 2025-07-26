using System.Reflection;

namespace PcdSharp.IO;

/// <summary>
/// 字段映射信息
/// </summary>
public class FieldMapping
{
    public string FieldName { get; set; } = string.Empty;

    public string PropertyName { get; set; } = string.Empty;

    public Type PropertyType { get; set; } = typeof(object);

    public int FieldIndex { get; set; } // 在PCD文件中的字段索引

    public int Offset { get; set; }

    public int Size { get; set; }

    public string DataType { get; set; } = string.Empty;

    public PropertyInfo? PropertyInfo { get; set; }

    public FieldInfo? FieldInfo { get; set; }

    public bool IsProperty => PropertyInfo != null;

    public bool IsField => FieldInfo != null;
}

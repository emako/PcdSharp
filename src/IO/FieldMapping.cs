using System.Reflection;

namespace PcdSharp.IO;

/// <summary>
/// 字段映射信息
/// </summary>
public class FieldMapping
{
    public string FieldName { get; set; } = string.Empty;

    public string? PropertyName { get; set; }

    public Type? PropertyType { get; set; }

    public int FieldIndex { get; set; } // 在PCD文件中的字段索引

    public int Offset { get; set; }

    public int Size { get; set; } // 单个元素的大小

    public int Count { get; set; } = 1; // 元素数量

    public int TotalSize => Size * Count; // 总大小

    public string DataType { get; set; } = string.Empty;

    public PropertyInfo? PropertyInfo { get; set; }

    public FieldInfo? FieldInfo { get; set; }

    public bool IsProperty => PropertyInfo != null;

    public bool IsField => FieldInfo != null;

    public bool HasTarget => PropertyInfo != null || FieldInfo != null;

    public static Dictionary<string, string[]> Mappings => new()
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
        { "intensity", ["Intensity", "intensity"] },
        { "label", ["Label", "label"] },
    };
}

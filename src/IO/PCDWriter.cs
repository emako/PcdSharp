using System.Globalization;
using System.Reflection;
using System.Text;

namespace PcdSharp.IO;

public class PCDWriter
{
    /// <summary>
    /// 将点云数据写入PCD文件
    /// </summary>
    /// <typeparam name="PointT">点类型</typeparam>
    /// <param name="filePath">输出文件路径</param>
    /// <param name="pointCloud">点云对象</param>
    /// <param name="encoding">数据编码格式</param>
    public static void Write<PointT>(string filePath, PointCloud<PointT> pointCloud, DataEncoding encoding = DataEncoding.ASCII)
    {
        using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        Write(stream, pointCloud, encoding);
    }

    /// <summary>
    /// 将点云数据写入流
    /// </summary>
    /// <typeparam name="PointT">点类型</typeparam>
    /// <param name="stream">输出流</param>
    /// <param name="pointCloud">点云对象</param>
    /// <param name="encoding">数据编码格式</param>
    public static void Write<PointT>(Stream stream, PointCloud<PointT> pointCloud, DataEncoding encoding = DataEncoding.ASCII)
    {
        if (encoding == DataEncoding.BinaryCompressed)
            throw new NotSupportedException("BinaryCompressed encoding is not supported for writing");

        var header = CreateHeader(pointCloud, encoding);
        var fieldWriters = CreateFieldWriters<PointT>(header);

        // 写入头部
        WriteHeader(stream, header);

        // 写入数据
        switch (encoding)
        {
            case DataEncoding.ASCII:
                WritePointsAscii(stream, pointCloud, fieldWriters);
                break;

            case DataEncoding.Binary:
                WritePointsBinary(stream, pointCloud, fieldWriters);
                break;

            default:
                throw new ArgumentException($"Unsupported data encoding: {encoding}");
        }
    }

    /// <summary>
    /// 根据点云数据创建PCD头部
    /// </summary>
    private static PCDHeader CreateHeader<PointT>(PointCloud<PointT> pointCloud, DataEncoding encoding)
    {
        var pointType = typeof(PointT);
        var fields = new List<string>();
        var sizes = new List<int>();
        var types = new List<string>();
        var counts = new List<int>();

        // 获取点类型的所有公共属性和字段
        var properties = pointType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && IsWritableType(p.PropertyType))
            .ToArray();
        var classFields = pointType.GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Where(f => IsWritableType(f.FieldType))
            .ToArray();

        // 按照PCD常见字段顺序排序
        var orderedMembers = new List<MemberData>();

        // 添加属性
        foreach (var prop in properties)
        {
            orderedMembers.Add(new MemberData(prop.Name.ToLower(), prop.PropertyType, prop));
        }

        // 添加字段
        foreach (var field in classFields)
        {
            if (!orderedMembers.Any(m => string.Equals(m.Name, field.Name, StringComparison.OrdinalIgnoreCase)))
            {
                orderedMembers.Add(new MemberData(field.Name.ToLower(), field.FieldType, field));
            }
        }

        // 按照PCD字段的标准顺序排序
        orderedMembers.Sort((a, b) => GetFieldOrder(a.Name).CompareTo(GetFieldOrder(b.Name)));

        foreach (var memberData in orderedMembers)
        {
            fields.Add(GetPcdFieldName(memberData.Name));
            sizes.Add(GetTypeSize(memberData.Type));
            types.Add(GetPcdTypeName(memberData.Type));
            counts.Add(1);
        }

        return new PCDHeader
        {
            Version = "0.7",
            Fields = fields,
            Size = sizes,
            Type = types,
            Count = counts,
            Width = pointCloud.Width > 0 ? pointCloud.Width : pointCloud.Count,
            Height = pointCloud.Height > 0 ? pointCloud.Height : 1,
            ViewPoint = null,
            Points = pointCloud.Count,
            Data = encoding,
            IsDense = pointCloud.IsDense
        };
    }

    /// <summary>
    /// 获取字段的排序优先级
    /// </summary>
    private static int GetFieldOrder(string fieldName)
    {
        return fieldName.ToLower() switch
        {
            "x" => 0,
            "y" => 1,
            "z" => 2,
            "normal_x" or "normalx" => 3,
            "normal_y" or "normaly" => 4,
            "normal_z" or "normalz" => 5,
            "curvature" => 6,
            "rgb" => 7,
            "rgba" => 8,
            "r" => 9,
            "g" => 10,
            "b" => 11,
            "a" => 12,
            "intensity" => 13,
            "label" => 14,
            _ => 1000 // 其他字段放在最后
        };
    }

    /// <summary>
    /// 获取PCD字段名称
    /// </summary>
    private static string GetPcdFieldName(string memberName)
    {
        return memberName.ToLower() switch
        {
            "normalx" => "normal_x",
            "normaly" => "normal_y",
            "normalz" => "normal_z",
            _ => memberName.ToLower()
        };
    }

    /// <summary>
    /// 判断类型是否可写入PCD
    /// </summary>
    private static bool IsWritableType(Type type)
    {
        return type == typeof(float) ||
               type == typeof(double) ||
               type == typeof(int) ||
               type == typeof(uint) ||
               type == typeof(short) ||
               type == typeof(ushort) ||
               type == typeof(byte) ||
               type == typeof(sbyte);
    }

    /// <summary>
    /// 获取类型在PCD中的大小
    /// </summary>
    private static int GetTypeSize(Type type)
    {
        return type switch
        {
            Type t when t == typeof(float) => 4,
            Type t when t == typeof(double) => 8,
            Type t when t == typeof(int) => 4,
            Type t when t == typeof(uint) => 4,
            Type t when t == typeof(short) => 2,
            Type t when t == typeof(ushort) => 2,
            Type t when t == typeof(byte) => 1,
            Type t when t == typeof(sbyte) => 1,
            _ => throw new ArgumentException($"Unsupported type: {type}")
        };
    }

    /// <summary>
    /// 获取PCD类型名称
    /// </summary>
    private static string GetPcdTypeName(Type type)
    {
        return type switch
        {
            Type t when t == typeof(float) => "F",
            Type t when t == typeof(double) => "F",
            Type t when t == typeof(int) => "I",
            Type t when t == typeof(uint) => "U",
            Type t when t == typeof(short) => "I",
            Type t when t == typeof(ushort) => "U",
            Type t when t == typeof(byte) => "U",
            Type t when t == typeof(sbyte) => "I",
            _ => "F" // 默认为浮点
        };
    }

    /// <summary>
    /// 创建字段写入器
    /// </summary>
    private static List<FieldWriter> CreateFieldWriters<PointT>(PCDHeader header)
    {
        var pointType = typeof(PointT);
        var writers = new List<FieldWriter>();

        for (int i = 0; i < header.Fields.Count; i++)
        {
            var fieldName = header.Fields[i];
            var fieldType = header.Type[i];
            var fieldSize = header.Size[i];

            // 查找对应的属性或字段
            var property = FindMatchingProperty(pointType, fieldName);
            var field = property == null ? FindMatchingField(pointType, fieldName) : null;

            if (property != null || field != null)
            {
                writers.Add(new FieldWriter
                {
                    FieldName = fieldName,
                    PropertyInfo = property,
                    FieldInfo = field,
                    PcdType = fieldType,
                    Size = fieldSize
                });
            }
        }

        return writers;
    }

    /// <summary>
    /// 查找匹配的属性
    /// </summary>
    private static PropertyInfo? FindMatchingProperty(Type pointType, string fieldName)
    {
        var properties = pointType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

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

    /// <summary>
    /// 查找匹配的字段
    /// </summary>
    private static FieldInfo? FindMatchingField(Type pointType, string fieldName)
    {
        var fields = pointType.GetFields(BindingFlags.Public | BindingFlags.Instance);

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

    /// <summary>
    /// 写入PCD头部
    /// </summary>
    private static void WriteHeader(Stream stream, PCDHeader header)
    {
        using var writer = new StreamWriter(stream, Encoding.UTF8, 1024, leaveOpen: true);

        writer.WriteLine($"# .PCD v{header.Version} - Point Cloud Data file format");
        writer.WriteLine($"VERSION {header.Version}");
        writer.WriteLine($"FIELDS {string.Join(" ", header.Fields)}");
        writer.WriteLine($"SIZE {string.Join(" ", header.Size)}");
        writer.WriteLine($"TYPE {string.Join(" ", header.Type)}");
        writer.WriteLine($"COUNT {string.Join(" ", header.Count)}");
        writer.WriteLine($"WIDTH {header.Width}");
        writer.WriteLine($"HEIGHT {header.Height}");

        if (header.ViewPoint != null && header.ViewPoint.Count >= 7)
        {
            var viewpoint = string.Join(" ", header.ViewPoint.Select(v => v.ToString("G", CultureInfo.InvariantCulture)));
            writer.WriteLine($"VIEWPOINT {viewpoint}");
        }

        writer.WriteLine($"POINTS {header.Points}");

        var dataStr = header.Data switch
        {
            DataEncoding.ASCII => "ascii",
            DataEncoding.Binary => "binary",
            DataEncoding.BinaryCompressed => "binary_compressed",
            _ => "ascii"
        };

        writer.WriteLine($"DATA {dataStr}");
        writer.Flush();
    }

    /// <summary>
    /// 以ASCII格式写入点数据
    /// </summary>
    private static void WritePointsAscii<PointT>(Stream stream, PointCloud<PointT> pointCloud, List<FieldWriter> fieldWriters)
    {
        using var writer = new StreamWriter(stream, Encoding.UTF8, 1024, leaveOpen: true);

        foreach (var point in pointCloud.Points)
        {
            var values = new List<string>();

            foreach (var fieldWriter in fieldWriters)
            {
                var value = GetFieldValue(point, fieldWriter);
                values.Add(FormatValueForAscii(value));
            }

            writer.WriteLine(string.Join(" ", values));
        }

        writer.Flush();
    }

    /// <summary>
    /// 以Binary格式写入点数据
    /// </summary>
    private static void WritePointsBinary<PointT>(Stream stream, PointCloud<PointT> pointCloud, List<FieldWriter> fieldWriters)
    {
        foreach (var point in pointCloud.Points)
        {
            foreach (var fieldWriter in fieldWriters)
            {
                var value = GetFieldValue(point, fieldWriter);
                var bytes = ConvertValueToBytes(value, fieldWriter.Size);
                stream.Write(bytes, 0, bytes.Length);
            }
        }

        stream.Flush();
    }

    /// <summary>
    /// 获取字段值
    /// </summary>
    private static object? GetFieldValue(object? point, FieldWriter fieldWriter)
    {
        if (point == null) return null;

        if (fieldWriter.PropertyInfo != null)
        {
            return fieldWriter.PropertyInfo.GetValue(point);
        }
        else if (fieldWriter.FieldInfo != null)
        {
            return fieldWriter.FieldInfo.GetValue(point);
        }

        return null;
    }

    /// <summary>
    /// 格式化值为ASCII字符串
    /// </summary>
    private static string FormatValueForAscii(object? value)
    {
        if (value == null) return "0";

        return value switch
        {
            float f => f.ToString("G", CultureInfo.InvariantCulture),
            double d => d.ToString("G", CultureInfo.InvariantCulture),
            int i => i.ToString(CultureInfo.InvariantCulture),
            uint ui => ui.ToString(CultureInfo.InvariantCulture),
            short s => s.ToString(CultureInfo.InvariantCulture),
            ushort us => us.ToString(CultureInfo.InvariantCulture),
            byte b => b.ToString(CultureInfo.InvariantCulture),
            sbyte sb => sb.ToString(CultureInfo.InvariantCulture),
            _ => value.ToString() ?? "0"
        };
    }

    /// <summary>
    /// 将值转换为字节数组
    /// </summary>
    private static byte[] ConvertValueToBytes(object? value, int size)
    {
        if (value == null) return new byte[size];

        return value switch
        {
            float f => BitConverter.GetBytes(f),
            double d => BitConverter.GetBytes(d),
            int i => BitConverter.GetBytes(i),
            uint ui => BitConverter.GetBytes(ui),
            short s => BitConverter.GetBytes(s),
            ushort us => BitConverter.GetBytes(us),
            byte b => [b],
            sbyte sb => [(byte)sb],
            _ => new byte[size]
        };
    }

    /// <summary>
    /// 字段写入器
    /// </summary>
    private class FieldWriter
    {
        public string FieldName { get; set; } = string.Empty;
        public PropertyInfo? PropertyInfo { get; set; }
        public FieldInfo? FieldInfo { get; set; }
        public string PcdType { get; set; } = string.Empty;
        public int Size { get; set; }
    }

    /// <summary>
    /// 成员数据结构
    /// </summary>
    protected readonly struct MemberData(string name, Type type, MemberInfo member)
    {
        public string Name { get; } = name;
        public Type Type { get; } = type;
        public MemberInfo Member { get; } = member;
    }
}

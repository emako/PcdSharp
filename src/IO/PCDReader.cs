using PcdSharp.Exceptions;
using System.Globalization;
using System.Text;

namespace PcdSharp.IO;

public class PCDReader
{
    /// <summary>
    /// 读取PCD文件头部信息
    /// </summary>
    /// <param name="filePath">PCD文件路径</param>
    /// <returns>解析后的头部信息</returns>
    public static PCDHeader ReadHeader(string filePath)
    {
        using var reader = new StreamReader(filePath, Encoding.UTF8);
        return ReadHeader(reader);
    }

    /// <summary>
    /// 从流中读取PCD文件头部信息
    /// </summary>
    /// <param name="reader">文本读取器</param>
    /// <returns>解析后的头部信息</returns>
    public static PCDHeader ReadHeader(StreamReader reader)
    {
        var header = new PCDHeader();
        string? line;

        while ((line = reader.ReadLine()) != null)
        {
            line = line.Trim();

            // 跳过注释和空行
            if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                continue;

            // 解析头部字段
            var parts = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;

            switch (parts[0].ToUpper())
            {
                case "VERSION":
                    header.Version = parts.Length > 1 ? parts[1] : string.Empty;
                    break;

                case "FIELDS":
                    header.Fields = [.. parts.Skip(1)];
                    break;

                case "SIZE":
                    header.Size = [.. parts.Skip(1).Select(int.Parse)];
                    break;

                case "TYPE":
                    header.Type = [.. parts.Skip(1)];
                    break;

                case "COUNT":
                    header.Count = [.. parts.Skip(1).Select(int.Parse)];
                    break;

                case "WIDTH":
                    header.Width = int.Parse(parts[1]);
                    break;

                case "HEIGHT":
                    header.Height = int.Parse(parts[1]);
                    break;

                case "VIEWPOINT":
                    if (parts.Length >= 8)
                    {
                        // 7个float: x y z qw qx qy qz
                        var values = new List<float>(7);
                        for (int i = 1; i <= 7; i++)
                        {
                            values.Add(float.Parse(parts[i], CultureInfo.InvariantCulture));
                        }
                        header.ViewPoint = values;
                    }
                    break;

                case "POINTS":
                    header.Points = long.Parse(parts[1]);
                    break;

                case "DATA":
                    header.Data = parts[1].ToUpper() switch
                    {
                        "ASCII" => DataEncoding.ASCII,
                        "BINARY" => DataEncoding.Binary,
                        "BINARY_COMPRESSED" => DataEncoding.BinaryCompressed,
                        _ => DataEncoding.ASCII
                    };
                    // DATA标记是头部的最后一行
                    return header;
            }
        }

        throw new PcdException("Invalid PCD file: missing DATA field");
    }

    /// <summary>
    /// 读取任意类型的点云数据
    /// </summary>
    /// <typeparam name="PointT">点类型</typeparam>
    /// <param name="stream">PCD文件流</param>
    /// <returns>点云对象</returns>
    public static PointCloud<PointT> Read<PointT>(Stream stream) where PointT : new()
    {
        return Read<PointT>(stream, null);
    }

    /// <summary>
    /// 从流中读取任意类型的点云数据，支持坐标系变换 (内部实现方法)
    /// </summary>
    /// <typeparam name="PointT">点类型</typeparam>
    /// <param name="stream">PCD文件流</param>
    /// <param name="transformOptions">坐标变换选项</param>
    /// <returns>点云对象</returns>
    private static PointCloud<PointT> ReadInternal<PointT>(Stream stream, CoordinateTransformOptions? transformOptions) where PointT : new()
    {
        // 读取所有字节以便处理二进制格式
        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        var fileBytes = memoryStream.ToArray();

        var headerText = Encoding.UTF8.GetString(fileBytes);

        // 找到DATA行
        var lines = headerText.Split('\n');
        int dataLineIndex = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.StartsWith("DATA ", StringComparison.OrdinalIgnoreCase))
            {
                dataLineIndex = i;
                break;
            }
        }

        if (dataLineIndex == -1)
            throw new PcdException("Invalid PCD file: missing DATA field");

        // 计算数据开始位置
        var headerLines = lines.Take(dataLineIndex + 1);
        var headerBytes = Encoding.UTF8.GetBytes(string.Join("\n", headerLines) + "\n");
        var dataStartPosition = headerBytes.Length;

        // 使用内存流读取头部
        using var headerStream = new MemoryStream(headerBytes);
        using var reader = new StreamReader(headerStream, Encoding.UTF8);
        var header = ReadHeader(reader);

        var pointCloud = new PointCloudImpl<PointT>((int)header.Points)
        {
            Header = header
        };

        var factory = new PointFactory<PointT>(header, transformOptions);

        switch (header.Data)
        {
            case DataEncoding.ASCII:
                // 对于ASCII格式，重新用内存流读取
                using (var dataStream = new MemoryStream(fileBytes, dataStartPosition, fileBytes.Length - dataStartPosition))
                using (var fileReader = new StreamReader(dataStream, Encoding.UTF8))
                {
                    ReadPointsAscii(fileReader, pointCloud, factory);
                }
                break;

            case DataEncoding.Binary:
                // 从计算出的位置开始读取二进制数据
                using (var dataStream = new MemoryStream(fileBytes, dataStartPosition, fileBytes.Length - dataStartPosition))
                {
                    ReadPointsBinary(dataStream, pointCloud, factory, header);
                }
                break;

            case DataEncoding.BinaryCompressed:
                // 从计算出的位置开始读取压缩的二进制数据
                using (var dataStream = new MemoryStream(fileBytes, dataStartPosition, fileBytes.Length - dataStartPosition))
                {
                    ReadPointsBinaryCompressed(dataStream, pointCloud, factory, header);
                }
                break;

            default:
                throw new ArgumentException($"Unsupported data encoding: {header.Data}");
        }

        return pointCloud;
    }

    /// <summary>
    /// 读取任意类型的点云数据
    /// </summary>
    /// <typeparam name="PointT">点类型</typeparam>
    /// <param name="filePath">PCD文件路径</param>
    /// <returns>点云对象</returns>
    public static PointCloud<PointT> Read<PointT>(string filePath) where PointT : new()
    {
        using FileStream fileStream = new(filePath, FileMode.Open, FileAccess.Read);
        return Read<PointT>(fileStream);
    }

    /// <summary>
    /// 读取任意类型的点云数据，支持坐标系变换
    /// </summary>
    /// <typeparam name="PointT">点类型</typeparam>
    /// <param name="filePath">PCD文件路径</param>
    /// <param name="transformOptions">坐标变换选项</param>
    /// <returns>点云对象</returns>
    public static PointCloud<PointT> Read<PointT>(string filePath, CoordinateTransformOptions transformOptions) where PointT : new()
    {
        using FileStream fileStream = new(filePath, FileMode.Open, FileAccess.Read);
        return Read<PointT>(fileStream, transformOptions);
    }

    /// <summary>
    /// 从流中读取任意类型的点云数据，支持坐标系变换
    /// </summary>
    /// <typeparam name="PointT">点类型</typeparam>
    /// <param name="stream">PCD文件流</param>
    /// <param name="transformOptions">坐标变换选项</param>
    /// <returns>点云对象</returns>
    public static PointCloud<PointT> Read<PointT>(Stream stream, CoordinateTransformOptions? transformOptions = null) where PointT : new()
    {
        return ReadInternal<PointT>(stream, transformOptions);
    }

    /// <summary>
    /// 读取PCD文件并使用用户提供的回调函数创建任意类型的对象
    /// </summary>
    /// <typeparam name="TResult">结果对象类型</typeparam>
    /// <param name="filePath">PCD文件路径</param>
    /// <param name="pointConstructor">点构造器回调函数，接收字段名称和值的字典，返回构造的对象</param>
    /// <returns>构造的对象列表</returns>
    public static List<TResult> ReadWithCallback<TResult>(string filePath, Func<Dictionary<string, object>, TResult> pointConstructor)
    {
        using FileStream fileStream = new(filePath, FileMode.Open, FileAccess.Read);
        return ReadWithCallback(fileStream, pointConstructor);
    }

    /// <summary>
    /// 从流中读取PCD文件并使用用户提供的回调函数创建任意类型的对象
    /// </summary>
    /// <typeparam name="TResult">结果对象类型</typeparam>
    /// <param name="stream">PCD文件流</param>
    /// <param name="pointConstructor">点构造器回调函数，接收字段名称和值的字典，返回构造的对象</param>
    /// <param name="transformOptions">可选的坐标变换选项</param>
    /// <returns>构造的对象列表</returns>
    public static List<TResult> ReadWithCallback<TResult>(Stream stream, Func<Dictionary<string, object>, TResult> pointConstructor, CoordinateTransformOptions? transformOptions = null)
    {
        // 读取所有字节以便处理二进制格式
        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        var fileBytes = memoryStream.ToArray();

        var headerText = Encoding.UTF8.GetString(fileBytes);

        // 找到DATA行
        var lines = headerText.Split('\n');
        int dataLineIndex = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.StartsWith("DATA ", StringComparison.OrdinalIgnoreCase))
            {
                dataLineIndex = i;
                break;
            }
        }

        if (dataLineIndex == -1)
            throw new PcdException("Invalid PCD file: missing DATA field");

        // 计算数据开始位置
        var headerLines = lines.Take(dataLineIndex + 1);
        var headerBytes = Encoding.UTF8.GetBytes(string.Join("\n", headerLines) + "\n");
        var dataStartPosition = headerBytes.Length;

        // 使用内存流读取头部
        using var headerStream = new MemoryStream(headerBytes);
        using var reader = new StreamReader(headerStream, Encoding.UTF8);
        var header = ReadHeader(reader);

        var results = new List<TResult>();

        switch (header.Data)
        {
            case DataEncoding.ASCII:
                // 对于ASCII格式，重新用内存流读取
                using (var dataStream = new MemoryStream(fileBytes, dataStartPosition, fileBytes.Length - dataStartPosition))
                using (var fileReader = new StreamReader(dataStream, Encoding.UTF8))
                {
                    ReadPointsWithCallbackAscii(fileReader, header, results, pointConstructor, transformOptions);
                }
                break;

            case DataEncoding.Binary:
                // 从计算出的位置开始读取二进制数据
                using (var dataStream = new MemoryStream(fileBytes, dataStartPosition, fileBytes.Length - dataStartPosition))
                {
                    ReadPointsWithCallbackBinary(dataStream, header, results, pointConstructor, transformOptions);
                }
                break;

            case DataEncoding.BinaryCompressed:
                // 从计算出的位置开始读取压缩的二进制数据
                using (var dataStream = new MemoryStream(fileBytes, dataStartPosition, fileBytes.Length - dataStartPosition))
                {
                    ReadPointsWithCallbackBinaryCompressed(dataStream, header, results, pointConstructor, transformOptions);
                }
                break;

            default:
                throw new ArgumentException($"Unsupported data encoding: {header.Data}");
        }

        return results;
    }
    }

    private static void ReadPointsAscii<PointT>(StreamReader reader, PointCloudImpl<PointT> pointCloud, PointFactory<PointT> factory) where PointT : new()
    {
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            line = line.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            var parts = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < factory.FieldMappings.Count) continue;

            var point = factory.CreateFromAscii(parts);
            pointCloud.Add(point);
        }
    }

    private static void ReadPointsBinary<PointT>(Stream stream, PointCloudImpl<PointT> pointCloud, PointFactory<PointT> factory, PCDHeader header) where PointT : new()
    {
        var pointSize = header.Size.Zip(header.Count, (s, c) => s * c).Sum();
        var buffer = new byte[pointSize];

        for (int i = 0; i < header.Points; i++)
        {
            var bytesRead = stream.Read(buffer, 0, pointSize);
            if (bytesRead != pointSize) break;

            var point = factory.CreateFromBinary(buffer);
            pointCloud.Add(point);
        }
    }

    private static void ReadPointsBinaryCompressed<PointT>(Stream stream, PointCloudImpl<PointT> pointCloud, PointFactory<PointT> factory, PCDHeader header) where PointT : new()
    {
        // 读取压缩信息：4字节压缩后大小 + 4字节解压后大小
        var compressionInfo = new byte[8];
        var bytesRead = stream.Read(compressionInfo, 0, 8);
        if (bytesRead != 8)
            throw new PcdException("Failed to read compression info from binary compressed data");

        int compressedSize = BitConverter.ToInt32(compressionInfo, 0);
        int decompressedSize = BitConverter.ToInt32(compressionInfo, 4);

        // 读取压缩数据
        var compressedData = new byte[compressedSize];
        bytesRead = stream.Read(compressedData, 0, compressedSize);
        if (bytesRead != compressedSize)
            throw new PcdException("Failed to read compressed data from binary compressed data");

        // 使用LZF算法解压缩
        var decompressedData = LzfDecompress(compressedData, decompressedSize);
        if (decompressedData == null)
            throw new PcdException("Failed to decompress binary compressed data");

        // 解析解压后的数据
        // 在binary_compressed格式中，数据按字段分组存储（所有x值，然后所有y值，然后所有z值等）
        ParseDecompressedData(decompressedData, pointCloud, factory, header);
    }

    private static void ParseDecompressedData<PointT>(byte[] data, PointCloudImpl<PointT> pointCloud, PointFactory<PointT> factory, PCDHeader header) where PointT : new()
    {
        int pointCount = (int)header.Points;
        int fieldCount = header.Fields.Count;

        // 为每个点创建字节数组
        var pointSize = header.Size.Sum();
        var points = new List<byte[]>();

        for (int i = 0; i < pointCount; i++)
        {
            points.Add(new byte[pointSize]);
        }

        int dataOffset = 0;
        int pointOffset = 0;

        // 按字段顺序重组数据
        for (int fieldIdx = 0; fieldIdx < fieldCount; fieldIdx++)
        {
            int fieldSize = header.Size[fieldIdx];
            int fieldCount_per_point = header.Count[fieldIdx];

            for (int countIdx = 0; countIdx < fieldCount_per_point; countIdx++)
            {
                for (int pointIdx = 0; pointIdx < pointCount; pointIdx++)
                {
                    if (dataOffset + fieldSize <= data.Length)
                    {
                        Array.Copy(data, dataOffset, points[pointIdx], pointOffset, fieldSize);
                        dataOffset += fieldSize;
                    }
                }
                pointOffset += fieldSize;
            }
        }

        // 使用PointFactory创建点对象
        foreach (var pointData in points)
        {
            var point = factory.CreateFromBinary(pointData);
            pointCloud.Add(point);
        }
    }

    /// <summary>
    /// 使用LZF算法解压缩数据
    /// </summary>
    /// <param name="input">要解压的数据</param>
    /// <param name="outputLength">解压之后的长度</param>
    /// <returns>返回解压缩之后的内容</returns>
    private static byte[]? LzfDecompress(byte[] input, int outputLength)
    {
        uint iidx = 0;
        uint oidx = 0;
        int inputLength = input.Length;
        byte[] output = new byte[outputLength];

        do
        {
            uint ctrl = input[iidx++];

            if (ctrl < (1 << 5))
            {
                ctrl++;

                if (oidx + ctrl > outputLength)
                {
                    return null;
                }

                do
                    output[oidx++] = input[iidx++];
                while ((--ctrl) != 0);
            }
            else
            {
                var len = ctrl >> 5;
                var reference = (int)(oidx - ((ctrl & 0x1f) << 8) - 1);

                if (len == 7)
                    len += input[iidx++];

                reference -= input[iidx++];

                if (oidx + len + 2 > outputLength)
                {
                    return null;
                }

                if (reference < 0)
                {
                    return null;
                }

                output[oidx++] = output[reference++];
                output[oidx++] = output[reference++];

                do
                    output[oidx++] = output[reference++];
                while ((--len) != 0);
            }
        }
        while (iidx < inputLength);

        return output;
    }

    /// <summary>
    /// 使用回调函数读取ASCII格式的点数据
    /// </summary>
    private static void ReadPointsWithCallbackAscii<TResult>(StreamReader reader, PCDHeader header, List<TResult> results, Func<Dictionary<string, object>, TResult> pointConstructor, CoordinateTransformOptions? transformOptions)
    {
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            line = line.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            var parts = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < header.Fields.Count) continue;

            var fieldData = new Dictionary<string, object>();
            for (int i = 0; i < header.Fields.Count && i < parts.Length; i++)
            {
                var fieldName = header.Fields[i];
                var fieldValue = ParseFieldValue(parts[i], header.Type[i]);
                
                // 应用坐标变换
                if (transformOptions?.NeedsTransformation == true && fieldValue is float floatValue)
                {
                    var lowerFieldName = fieldName.ToLower();
                    if (lowerFieldName == "x")
                        fieldValue = floatValue * transformOptions.ScaleX;
                    else if (lowerFieldName == "y")
                        fieldValue = floatValue * transformOptions.ScaleY;
                    else if (lowerFieldName == "z")
                        fieldValue = floatValue * transformOptions.ScaleZ;
                    else if (lowerFieldName == "normal_x")
                        fieldValue = floatValue * Math.Sign(transformOptions.ScaleX);
                    else if (lowerFieldName == "normal_y")
                        fieldValue = floatValue * Math.Sign(transformOptions.ScaleY);
                    else if (lowerFieldName == "normal_z")
                        fieldValue = floatValue * Math.Sign(transformOptions.ScaleZ);
                }

                fieldData[fieldName] = fieldValue;
            }

            var point = pointConstructor(fieldData);
            results.Add(point);
        }
    }

    /// <summary>
    /// 使用回调函数读取Binary格式的点数据
    /// </summary>
    private static void ReadPointsWithCallbackBinary<TResult>(Stream stream, PCDHeader header, List<TResult> results, Func<Dictionary<string, object>, TResult> pointConstructor, CoordinateTransformOptions? transformOptions)
    {
        var pointSize = header.Size.Zip(header.Count, (s, c) => s * c).Sum();
        var buffer = new byte[pointSize];

        for (int i = 0; i < header.Points; i++)
        {
            var bytesRead = stream.Read(buffer, 0, pointSize);
            if (bytesRead != pointSize) break;

            var fieldData = ParseBinaryPoint(buffer, header, transformOptions);
            var point = pointConstructor(fieldData);
            results.Add(point);
        }
    }

    /// <summary>
    /// 使用回调函数读取Binary Compressed格式的点数据
    /// </summary>
    private static void ReadPointsWithCallbackBinaryCompressed<TResult>(Stream stream, PCDHeader header, List<TResult> results, Func<Dictionary<string, object>, TResult> pointConstructor, CoordinateTransformOptions? transformOptions)
    {
        // 读取压缩信息
        var compressionInfo = new byte[8];
        var bytesRead = stream.Read(compressionInfo, 0, 8);
        if (bytesRead != 8)
            throw new PcdException("Failed to read compression info from binary compressed data");

        int compressedSize = BitConverter.ToInt32(compressionInfo, 0);
        int decompressedSize = BitConverter.ToInt32(compressionInfo, 4);

        // 读取压缩数据
        var compressedData = new byte[compressedSize];
        bytesRead = stream.Read(compressedData, 0, compressedSize);
        if (bytesRead != compressedSize)
            throw new PcdException("Failed to read compressed data from binary compressed data");

        // 解压缩数据
        var decompressedData = LzfDecompress(compressedData, decompressedSize);
        if (decompressedData == null)
            throw new PcdException("Failed to decompress binary compressed data");

        // 解析解压后的数据
        ParseDecompressedDataWithCallback(decompressedData, header, results, pointConstructor, transformOptions);
    }

    /// <summary>
    /// 解析压缩数据并使用回调函数创建对象
    /// </summary>
    private static void ParseDecompressedDataWithCallback<TResult>(byte[] data, PCDHeader header, List<TResult> results, Func<Dictionary<string, object>, TResult> pointConstructor, CoordinateTransformOptions? transformOptions)
    {
        int pointCount = (int)header.Points;
        int fieldCount = header.Fields.Count;

        // 为每个点创建字节数组
        var pointSize = header.Size.Sum();
        var points = new List<byte[]>();

        for (int i = 0; i < pointCount; i++)
        {
            points.Add(new byte[pointSize]);
        }

        int dataOffset = 0;
        int pointOffset = 0;

        // 按字段顺序重组数据
        for (int fieldIdx = 0; fieldIdx < fieldCount; fieldIdx++)
        {
            int fieldSize = header.Size[fieldIdx];
            int fieldCount_per_point = header.Count[fieldIdx];

            for (int countIdx = 0; countIdx < fieldCount_per_point; countIdx++)
            {
                for (int pointIdx = 0; pointIdx < pointCount; pointIdx++)
                {
                    if (dataOffset + fieldSize <= data.Length)
                    {
                        Array.Copy(data, dataOffset, points[pointIdx], pointOffset, fieldSize);
                        dataOffset += fieldSize;
                    }
                }
                pointOffset += fieldSize;
            }
        }

        // 使用回调函数创建对象
        foreach (var pointData in points)
        {
            var fieldData = ParseBinaryPoint(pointData, header, transformOptions);
            var point = pointConstructor(fieldData);
            results.Add(point);
        }
    }

    /// <summary>
    /// 解析字段值
    /// </summary>
    private static object ParseFieldValue(string value, string dataType)
    {
        return dataType.ToUpper() switch
        {
            "I" => int.Parse(value),
            "U" => uint.Parse(value),
            "F" => float.Parse(value, CultureInfo.InvariantCulture),
            _ => value
        };
    }

    /// <summary>
    /// 解析二进制点数据
    /// </summary>
    private static unsafe Dictionary<string, object> ParseBinaryPoint(byte[] buffer, PCDHeader header, CoordinateTransformOptions? transformOptions)
    {
        var fieldData = new Dictionary<string, object>();
        int offset = 0;

        fixed (byte* ptr = buffer)
        {
            for (int i = 0; i < header.Fields.Count; i++)
            {
                var fieldName = header.Fields[i];
                var fieldSize = header.Size[i];
                var dataType = header.Type[i];
                var count = header.Count[i];

                for (int c = 0; c < count; c++)
                {
                    object value = dataType.ToUpper() switch
                    {
                        "I" when fieldSize == 1 => *(sbyte*)(ptr + offset),
                        "I" when fieldSize == 2 => *(short*)(ptr + offset),
                        "I" when fieldSize == 4 => *(int*)(ptr + offset),
                        "U" when fieldSize == 1 => *(ptr + offset),
                        "U" when fieldSize == 2 => *(ushort*)(ptr + offset),
                        "U" when fieldSize == 4 => *(uint*)(ptr + offset),
                        "F" when fieldSize == 4 => *(float*)(ptr + offset),
                        "F" when fieldSize == 8 => *(double*)(ptr + offset),
                        _ => 0
                    };

                    // 应用坐标变换
                    if (transformOptions?.NeedsTransformation == true && value is float floatValue)
                    {
                        var lowerFieldName = fieldName.ToLower();
                        if (lowerFieldName == "x")
                            value = floatValue * transformOptions.ScaleX;
                        else if (lowerFieldName == "y")
                            value = floatValue * transformOptions.ScaleY;
                        else if (lowerFieldName == "z")
                            value = floatValue * transformOptions.ScaleZ;
                        else if (lowerFieldName == "normal_x")
                            value = floatValue * Math.Sign(transformOptions.ScaleX);
                        else if (lowerFieldName == "normal_y")
                            value = floatValue * Math.Sign(transformOptions.ScaleY);
                        else if (lowerFieldName == "normal_z")
                            value = floatValue * Math.Sign(transformOptions.ScaleZ);
                    }

                    var key = count > 1 ? $"{fieldName}_{c}" : fieldName;
                    fieldData[key] = value;
                    offset += fieldSize;
                }
            }
        }

        return fieldData;
    }
}

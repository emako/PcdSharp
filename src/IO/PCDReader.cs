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

        var factory = new PointFactory<PointT>(header);

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
}

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
            var parts = line.Split([' '], StringSplitOptions.RemoveEmptyEntries);
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
        using var reader = new StreamReader(stream, Encoding.UTF8);

        return Read<PointT>(reader, stream);
    }

    /// <summary>
    /// 读取任意类型的点云数据
    /// </summary>
    /// <typeparam name="PointT">点类型</typeparam>
    /// <param name="filePath">PCD文件路径</param>
    /// <returns>点云对象</returns>
    public static PointCloud<PointT> Read<PointT>(string filePath) where PointT : new()
    {
        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var reader = new StreamReader(fileStream, Encoding.UTF8);

        return Read<PointT>(reader, fileStream);
    }

    /// <summary>
    /// 从流中读取任意类型的点云数据
    /// </summary>
    /// <typeparam name="PointT">点类型</typeparam>
    /// <param name="reader">文本读取器</param>
    /// <param name="stream">文件流（用于二进制读取）</param>
    /// <returns>点云对象</returns>
    public static PointCloud<PointT> Read<PointT>(StreamReader reader, Stream? stream = null) where PointT : new()
    {
        var header = ReadHeader(reader);
        var pointCloud = new PointCloudImpl<PointT>((int)header.Points)
        {
            Width = header.Width,
            Height = header.Height,
            IsDense = header.IsDense
        };

        var factory = new PointFactory<PointT>(header);

        switch (header.Data)
        {
            case DataEncoding.ASCII:
                ReadPointsAscii(reader, pointCloud, factory);
                break;

            case DataEncoding.Binary:
                if (stream == null)
                    throw new ArgumentException("FileStream is required for binary data reading");
                ReadPointsBinary(stream, pointCloud, factory, header);
                break;

            case DataEncoding.BinaryCompressed:
                throw new NotImplementedException("Binary compressed format is not supported yet");
            default:
                throw new ArgumentException($"Unsupported data encoding: {header.Data}");
        }

        return pointCloud;
    }

    /// <summary>
    /// 读取点云数据并返回点列表（向后兼容）
    /// </summary>
    /// <typeparam name="PointT">点类型</typeparam>
    /// <param name="filePath">PCD文件路径</param>
    /// <returns>点列表</returns>
    public static List<PointT> ReadPoints<PointT>(string filePath) where PointT : new()
    {
        var pointCloud = Read<PointT>(filePath);
        return pointCloud.Points;
    }

    private static void ReadPointsAscii<PointT>(StreamReader reader, PointCloudImpl<PointT> pointCloud, PointFactory<PointT> factory) where PointT : new()
    {
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            line = line.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            var parts = line.Split([' '], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < factory.FieldMappings.Count) continue;

            var point = factory.CreateFromAscii(parts);
            pointCloud.Add(point);
        }
    }

    private static void ReadPointsBinary<PointT>(Stream stream, PointCloudImpl<PointT> pointCloud, PointFactory<PointT> factory, PCDHeader header) where PointT : new()
    {
        var pointSize = header.Size.Sum();
        var buffer = new byte[pointSize];

        for (int i = 0; i < header.Points; i++)
        {
            var bytesRead = stream.Read(buffer, 0, pointSize);
            if (bytesRead != pointSize) break;

            var point = factory.CreateFromBinary(buffer);
            pointCloud.Add(point);
        }
    }
}

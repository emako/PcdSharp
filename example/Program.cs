using PcdSharp.IO;
using PcdSharp.Struct;

namespace PcdSharp.Examples;

internal sealed class Program
{
    private static void Main(string[] args)
    {
        // 演示如何使用PCDReader
        Console.WriteLine("PCD Reader for PCD format v0.7 示例程序 - 支持泛型");
        Console.WriteLine("=====================================");

        // 如果用户提供了文件路径参数
        if (args.Length > 0 && File.Exists(args[0]))
        {
            var filePath = args[0];
            Console.WriteLine($"正在读取文件: {filePath}");

            try
            {
                // 首先读取头部信息
                var header = PCDReader.ReadHeader(filePath);
                PrintHeaderInfo(header);

                // 只针对已经准备好的PCD文件做测试
                string fileName = Path.GetFileName(filePath);

                if (fileName.Contains("color"))
                {
                    var pointCloud = PCDReader.Read<PointXYZRGBA>(filePath);
                    Console.WriteLine($"成功读取点云 - 宽度: {pointCloud.Width}, 高度: {pointCloud.Height}");
                    Console.WriteLine($"总点数: {pointCloud.Count}, 是否密集: {pointCloud.IsDense}");

                    // 显示前几个点的信息
                    ShowSamplePointCloud(pointCloud, 5);
                }
                else if (fileName.Contains("simple"))
                {
                    Console.WriteLine("\n正在使用泛型方法读取 PointXYZ 点云数据...");
                    var pointCloud = PCDReader.Read<PointXYZ>(filePath);
                    Console.WriteLine($"成功读取点云 - 宽度: {pointCloud.Width}, 高度: {pointCloud.Height}");
                    Console.WriteLine($"总点数: {pointCloud.Count}, 是否密集: {pointCloud.IsDense}");

                    // 显示前几个点的信息
                    ShowSamplePointCloud(pointCloud, 5);
                }
                else if (fileName.Contains("intensity"))
                {
                    Console.WriteLine("\n正在使用泛型方法读取 PointXYZ 点云数据...");
                    var pointCloud = PCDReader.Read<IntensityXYZ>(filePath);
                    Console.WriteLine($"成功读取点云 - 宽度: {pointCloud.Width}, 高度: {pointCloud.Height}");
                    Console.WriteLine($"总点数: {pointCloud.Count}, 是否密集: {pointCloud.IsDense}");

                    // 显示前几个点的信息
                    ShowSamplePointCloud(pointCloud, 5);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"读取文件时出错: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine("用法: dotnet run <pcd文件路径>");
            Console.WriteLine("\n支持的功能:");
            Console.WriteLine("- PCD v0.7 格式支持");
            Console.WriteLine("- ASCII 和 Binary 数据格式");
            Console.WriteLine("- 泛型点云读取 PointCloud<T>");
            Console.WriteLine("- 支持任意点类型 (PointXYZ, PointXYZRGBA, 自定义点类型)");
            Console.WriteLine("- 完整的头部信息解析");
            Console.WriteLine("- 向后兼容的 API");
        }

        Console.WriteLine("\n按任意键退出...");
        Console.ReadKey();
    }

    static void PrintHeaderInfo(PCDHeader header)
    {
        Console.WriteLine("\n== PCD 文件头部信息 ==");
        Console.WriteLine($"版本: {header.Version}");
        Console.WriteLine($"字段: {string.Join(", ", header.Fields)}");
        Console.WriteLine($"大小: {string.Join(", ", header.Size)}");
        Console.WriteLine($"类型: {string.Join(", ", header.Type)}");
        Console.WriteLine($"计数: {string.Join(", ", header.Count)}");
        Console.WriteLine($"宽度: {header.Width}");
        Console.WriteLine($"高度: {header.Height}");
        Console.WriteLine($"点数: {header.Points}");
        Console.WriteLine($"数据格式: {header.Data}");
        Console.WriteLine($"是否密集: {header.IsDense}");

        if (header.ViewPoint != null && header.ViewPoint.Count == 7)
        {
            var vp = header.ViewPoint;
            Console.WriteLine($"视点: ({vp[0]}, {vp[1]}, {vp[2]}, {vp[3]}, {vp[4]}, {vp[5]}, {vp[6]})");
        }
    }

    static void ShowSamplePointCloud<T>(PointCloud<T> pointCloud, int maxCount) where T : struct
    {
        Console.WriteLine($"\n== 样本点云数据 ({typeof(T).Name}) ==");
        var count = Math.Min(maxCount, pointCloud.Count);

        for (int i = 0; i < count; i++)
        {
            var point = pointCloud.Points[i];
            Console.WriteLine($"[{i}] {FormatPoint(point)}");
        }

        if (pointCloud.Count > maxCount)
        {
            Console.WriteLine($"... 还有 {pointCloud.Count - maxCount} 个点");
        }
    }

    private static string FormatPoint<T>(T point) where T : struct
    {
        return point switch
        {
            PointXYZ xyz => $"({xyz.X:F3}, {xyz.Y:F3}, {xyz.Z:F3})",
            PointXYZRGBA xyzrgba => $"({xyzrgba.X:F3}, {xyzrgba.Y:F3}, {xyzrgba.Z:F3}) RGBA: 0x{xyzrgba.RGBA:X8}",
            PointNormal normal => $"({normal.X:F3}, {normal.Y:F3}, {normal.Z:F3}) N:({normal.NormalX:F2}, {normal.NormalY:F2}, {normal.NormalZ:F2}) C:{normal.Curvature:F3}",
            IntensityXYZ intensityxyz => $"{intensityxyz.Intensity} ({intensityxyz.X:F3}, {intensityxyz.Y:F3}, {intensityxyz.Z:F3})",
            _ => point.ToString() ?? "Unknown"
        };
    }
}

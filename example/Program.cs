using PcdSharp.IO;
using PcdSharp.Struct;

namespace PcdSharp.Examples;

internal sealed class Program
{
    private static void Main(string[] args)
    {
        // 演示如何使用PCDReader和PCDWriter
        Console.WriteLine("PCD Reader/Writer for PCD format v0.7 示例程序 - 支持泛型");
        Console.WriteLine("=========================================");

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

                    // 演示写入功能
                    DemoWritePointCloud(pointCloud, "output_color");
                }
                else if (fileName.Contains("simple"))
                {
                    Console.WriteLine("\n正在使用泛型方法读取 PointXYZ 点云数据...");
                    var pointCloud = PCDReader.Read<PointXYZ>(filePath);
                    Console.WriteLine($"成功读取点云 - 宽度: {pointCloud.Width}, 高度: {pointCloud.Height}");
                    Console.WriteLine($"总点数: {pointCloud.Count}, 是否密集: {pointCloud.IsDense}");

                    // 显示前几个点的信息
                    ShowSamplePointCloud(pointCloud, 5);

                    // 演示写入功能
                    DemoWritePointCloud(pointCloud, "output_simple");
                }
                else if (fileName.Contains("intensity"))
                {
                    Console.WriteLine("\n正在使用泛型方法读取 IntensityXYZ 点云数据...");
                    var pointCloud = PCDReader.Read<IntensityXYZ>(filePath);
                    Console.WriteLine($"成功读取点云 - 宽度: {pointCloud.Width}, 高度: {pointCloud.Height}");
                    Console.WriteLine($"总点数: {pointCloud.Count}, 是否密集: {pointCloud.IsDense}");

                    // 显示前几个点的信息
                    ShowSamplePointCloud(pointCloud, 5);

                    // 演示写入功能
                    DemoWritePointCloud(pointCloud, "output_intensity");
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
            Console.WriteLine("- ASCII 和 Binary 数据格式读取");
            Console.WriteLine("- ASCII 和 Binary 数据格式写入");
            Console.WriteLine("- 泛型点云读取/写入 PointCloud<T>");
            Console.WriteLine("- 支持任意点类型 (PointXYZ, PointXYZRGBA, IntensityXYZ, 自定义点类型)");
            Console.WriteLine("- 完整的头部信息解析");
            Console.WriteLine("- 向后兼容的 API");

            // 如果没有提供文件参数，演示创建和写入点云
            Console.WriteLine("\n演示创建和写入点云:");
            DemoCreateAndWritePointCloud();
        }
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

    /// <summary>
    /// 演示写入点云功能
    /// </summary>
    private static void DemoWritePointCloud<T>(PointCloud<T> pointCloud, string baseName)
    {
        Console.WriteLine($"\n== 演示PCDWriter写入功能 ==");

        try
        {
            // 写入ASCII格式
            var asciiPath = $"{baseName}_ascii.pcd";
            PCDWriter.Write(asciiPath, pointCloud, DataEncoding.ASCII);
            Console.WriteLine($"✓ 成功写入ASCII格式文件: {asciiPath}");

            // 写入Binary格式
            var binaryPath = $"{baseName}_binary.pcd";
            PCDWriter.Write(binaryPath, pointCloud, DataEncoding.Binary);
            Console.WriteLine($"✓ 成功写入Binary格式文件: {binaryPath}");

            // 验证写入的文件
            VerifyWrittenFile(asciiPath);
            VerifyWrittenFile(binaryPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"写入文件时出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 演示创建和写入点云
    /// </summary>
    private static void DemoCreateAndWritePointCloud()
    {
        Console.WriteLine("创建示例点云数据并写入文件...");

        // 创建PointXYZ点云
        var xyzPointCloud = CreateSampleXYZPointCloud();
        DemoWritePointCloud(xyzPointCloud, "created_xyz");

        // 创建PointXYZRGBA点云
        var rgbaPointCloud = CreateSampleRGBAPointCloud();
        DemoWritePointCloud(rgbaPointCloud, "created_rgba");

        // 创建IntensityXYZ点云
        var intensityPointCloud = CreateSampleIntensityPointCloud();
        DemoWritePointCloud(intensityPointCloud, "created_intensity");
    }

    /// <summary>
    /// 创建示例XYZ点云
    /// </summary>
    private static PointCloud<PointXYZ> CreateSampleXYZPointCloud()
    {
        var pointCloud = new PointCloudImpl<PointXYZ>
        {
            Width = 10,
            Height = 1,
            IsDense = true,
        };

        // 创建一个简单的立方体点云
        for (int i = 0; i < 10; i++)
        {
            var point = new PointXYZ
            {
                X = (float)(i * 0.1),
                Y = (float)(Math.Sin(i * 0.5) * 0.5),
                Z = (float)(Math.Cos(i * 0.5) * 0.5)
            };
            pointCloud.Add(point);
        }

        Console.WriteLine($"创建XYZ点云 - 点数: {pointCloud.Count}");
        return pointCloud;
    }

    /// <summary>
    /// 创建示例RGBA点云
    /// </summary>
    private static PointCloud<PointXYZRGBA> CreateSampleRGBAPointCloud()
    {
        var pointCloud = new PointCloudImpl<PointXYZRGBA>
        {
            Width = 8,
            Height = 1,
            IsDense = true,
        };

        // 创建一个彩色的螺旋线
        for (int i = 0; i < 8; i++)
        {
            var angle = i * Math.PI / 4;
            var point = new PointXYZRGBA
            {
                X = (float)(Math.Cos(angle) * 0.5),
                Y = (float)(Math.Sin(angle) * 0.5),
                Z = (float)(i * 0.1),
                RGBA = GenerateColor(i)
            };
            pointCloud.Add(point);
        }

        Console.WriteLine($"创建RGBA点云 - 点数: {pointCloud.Count}");
        return pointCloud;
    }

    /// <summary>
    /// 创建示例Intensity点云
    /// </summary>
    private static PointCloud<IntensityXYZ> CreateSampleIntensityPointCloud()
    {
        var pointCloud = new PointCloudImpl<IntensityXYZ>
        {
            Width = 6,
            Height = 1,
            IsDense = true,
        };

        // 创建一个带强度信息的直线
        for (int i = 0; i < 6; i++)
        {
            var point = new IntensityXYZ
            {
                Intensity = (float)(100 + i * 50), // 强度从100到350
                X = (float)(i * 0.2),
                Y = 0.0f,
                Z = (float)(i * 0.1)
            };
            pointCloud.Add(point);
        }

        Console.WriteLine($"创建Intensity点云 - 点数: {pointCloud.Count}");
        return pointCloud;
    }

    /// <summary>
    /// 生成颜色值
    /// </summary>
    private static uint GenerateColor(int index)
    {
        // 生成不同的RGBA颜色
        var colors = new uint[]
        {
            0xFF0000FF, // 红色
            0x00FF00FF, // 绿色
            0x0000FFFF, // 蓝色
            0xFFFF00FF, // 黄色
            0xFF00FFFF, // 洋红
            0x00FFFFFF, // 青色
            0xFFFFFFFF, // 白色
            0x808080FF  // 灰色
        };
        return colors[index % colors.Length];
    }

    /// <summary>
    /// 验证写入的文件
    /// </summary>
    private static void VerifyWrittenFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"✗ 文件不存在: {filePath}");
            return;
        }

        var fileInfo = new FileInfo(filePath);
        Console.WriteLine($"  - 文件大小: {fileInfo.Length} 字节");

        try
        {
            var header = PCDReader.ReadHeader(filePath);
            Console.WriteLine($"  - 格式: {header.Data}, 点数: {header.Points}, 字段: {string.Join(",", header.Fields)}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  - 验证失败: {ex.Message}");
        }
    }
}

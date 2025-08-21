using PcdSharp.IO;
using PcdSharp.Struct;
using System.Numerics;

namespace PcdSharp.Examples;

/// <summary>
/// 演示坐标系变换功能的示例
/// </summary>
public static class CoordinateTransformExample
{
    /// <summary>
    /// 演示如何使用坐标系变换
    /// </summary>
    public static void DemoCoordinateTransform()
    {
        Console.WriteLine("=== 坐标系变换功能演示 ===");
        
        // 创建测试点云数据
        var testPoints = CreateTestPointCloud();
        Console.WriteLine("创建了测试点云，包含以下点:");
        PrintPoints(testPoints);
        
        // 保存为PCD文件
        string testFile = "test_transform.pcd";
        PCDWriter.Write(testFile, testPoints, DataEncoding.ASCII);
        Console.WriteLine($"\n已保存为: {testFile}");
        
        // 1. 正常读取（无变换）
        Console.WriteLine("\n1. 正常读取（无变换）:");
        var normalPoints = PCDReader.Read<PointXYZ>(testFile);
        PrintPoints(normalPoints);
        
        // 2. 左手坐标系到右手坐标系变换（翻转Y轴）
        Console.WriteLine("\n2. 左手到右手坐标系变换（翻转Y轴）:");
        var lhsToRhsTransform = CoordinateTransformOptions.LeftToRightHanded();
        var transformedPoints = PCDReader.Read<PointXYZ>(testFile, lhsToRhsTransform);
        PrintPoints(transformedPoints);
        
        // 3. 自定义缩放变换
        Console.WriteLine("\n3. 自定义缩放变换（X*2, Y*0.5, Z*1.5）:");
        var customTransform = new CoordinateTransformOptions
        {
            ScaleX = 2.0f,
            ScaleY = 0.5f,
            ScaleZ = 1.5f
        };
        var scaledPoints = PCDReader.Read<PointXYZ>(testFile, customTransform);
        PrintPoints(scaledPoints);
    }
    
    private static PointCloud<PointXYZ> CreateTestPointCloud()
    {
        var pointCloud = new PointCloudImpl<PointXYZ>
        {
            Header = new PCDHeader
            {
                Width = 4,
                Height = 1,
                IsDense = true,
                Version = "0.7"
            }
        };
        
        // 添加一些测试点
        pointCloud.Add(new PointXYZ { X = 1.0f, Y = 2.0f, Z = 3.0f });
        pointCloud.Add(new PointXYZ { X = -1.0f, Y = -2.0f, Z = 0.0f });
        pointCloud.Add(new PointXYZ { X = 0.0f, Y = 1.0f, Z = -1.0f });
        pointCloud.Add(new PointXYZ { X = 2.5f, Y = -0.5f, Z = 1.5f });
        
        return pointCloud;
    }
    
    private static void PrintPoints<T>(PointCloud<T> pointCloud) where T : struct
    {
        for (int i = 0; i < Math.Min(pointCloud.Count, 10); i++)
        {
            var point = pointCloud.Points[i];
            if (point is PointXYZ xyz)
            {
                Console.WriteLine($"  [{i}] X:{xyz.X:F2}, Y:{xyz.Y:F2}, Z:{xyz.Z:F2}");
            }
            else
            {
                Console.WriteLine($"  [{i}] {point}");
            }
        }
    }
}
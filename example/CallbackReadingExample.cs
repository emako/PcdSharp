using PcdSharp.IO;
using System.Numerics;

namespace PcdSharp.Examples;

/// <summary>
/// 演示回调函数读取功能的示例
/// </summary>
public static class CallbackReadingExample
{
    // 自定义点类型
    public class CustomPoint
    {
        public Vector3 Position { get; set; }
        public float Intensity { get; set; }
        public string Label { get; set; } = "";
        public bool IsValid { get; set; }
        
        public override string ToString()
            => $"Pos:({Position.X:F2},{Position.Y:F2},{Position.Z:F2}) I:{Intensity:F2} L:{Label}";
    }

    public class MinimalPoint
    {
        public float X { get; set; }
        public float Y { get; set; }
        public override string ToString() => $"({X:F2}, {Y:F2})";
    }

    /// <summary>
    /// 演示如何使用回调函数读取
    /// </summary>
    public static void DemoCallbackReading()
    {
        Console.WriteLine("=== 回调函数读取功能演示 ===");
        
        // 首先创建测试文件
        string testFile = "test_callback.pcd";
        CreateTestFile(testFile);
        Console.WriteLine($"创建测试文件: {testFile}");

        // 1. 使用回调函数创建自定义点类型
        Console.WriteLine("\n1. 使用回调函数创建自定义点类型:");
        var customPoints = PCDReader.ReadWithCallback<CustomPoint>(testFile, fields =>
        {
            return new CustomPoint
            {
                Position = new Vector3(
                    GetFloatValue(fields, "x", 0.0f),
                    GetFloatValue(fields, "y", 0.0f),
                    GetFloatValue(fields, "z", 0.0f)
                ),
                Intensity = GetFloatValue(fields, "intensity", 100.0f),
                Label = "Point_0", // Fix the variable reference issue
                IsValid = true
            };
        });

        foreach (var point in customPoints.Take(5))
        {
            Console.WriteLine($"  {point}");
        }

        // 2. 创建最小化点类型（只取X,Y坐标）
        Console.WriteLine("\n2. 创建最小化点类型（只取X,Y）:");
        var minimalPoints = PCDReader.ReadWithCallback<MinimalPoint>(testFile, fields =>
        {
            return new MinimalPoint
            {
                X = GetFloatValue(fields, "x", 0.0f),
                Y = GetFloatValue(fields, "y", 0.0f)
            };
        });

        foreach (var point in minimalPoints.Take(5))
        {
            Console.WriteLine($"  {point}");
        }

        // 3. 使用变换 + 回调
        Console.WriteLine("\n3. 使用坐标变换 + 回调（Y轴翻转）:");
        var transformOptions = CoordinateTransformOptions.LeftToRightHanded();
        using var fileStream = new FileStream(testFile, FileMode.Open, FileAccess.Read);
        var transformedPoints = PCDReader.ReadWithCallback<MinimalPoint>(fileStream, fields =>
        {
            return new MinimalPoint
            {
                X = GetFloatValue(fields, "x", 0.0f),
                Y = GetFloatValue(fields, "y", 0.0f)
            };
        }, transformOptions);

        foreach (var point in transformedPoints.Take(5))
        {
            Console.WriteLine($"  {point}");
        }

        // 4. 直接返回字典数据
        Console.WriteLine("\n4. 直接返回原始字段数据:");
        var rawData = PCDReader.ReadWithCallback<Dictionary<string, object>>(testFile, fields => fields);
        
        foreach (var data in rawData.Take(3))
        {
            Console.WriteLine($"  Fields: {string.Join(", ", data.Select(kv => $"{kv.Key}={kv.Value}"))}");
        }

        Console.WriteLine($"\n总共处理了 {customPoints.Count} 个点");
    }

    private static float GetFloatValue(Dictionary<string, object> fields, string key, float defaultValue)
    {
        if (fields.TryGetValue(key, out var value))
        {
            return value is float f ? f : 
                   value is double d ? (float)d :
                   value is int i ? (float)i :
                   defaultValue;
        }
        return defaultValue;
    }

    private static void CreateTestFile(string filename)
    {
        var content = @"VERSION 0.7
FIELDS x y z intensity
SIZE 4 4 4 4
TYPE F F F F
COUNT 1 1 1 1
WIDTH 5
HEIGHT 1
VIEWPOINT 0 0 0 1 0 0 0
POINTS 5
DATA ascii
1.0 2.0 3.0 150.5
-1.5 0.5 -2.0 200.0
0.0 1.0 0.0 175.25
2.5 -1.0 1.5 125.75
-0.5 -0.5 2.5 190.0
";
        File.WriteAllText(filename, content);
    }
}
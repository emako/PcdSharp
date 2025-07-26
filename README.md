# <img src="assets/logo.png" alt="logo" style="zoom: 25%;" />

# PcdSharp - PCD v0.7 Reader/Writer Library

PcdSharp 是一个用于读取和写入 Point Cloud Data (PCD) v0.7 格式文件的高性能 C# 库。

## 功能特性

- ✅ **完整的 PCD v0.7 格式支持**
- ✅ **读取功能**: 支持 ASCII, Binary, Binary Compressed 格式
- ✅ **写入功能**: 支持 ASCII 和 Binary 格式
- ✅ **泛型点云支持**: `PointCloud<T>` 支持任意点类型
- ✅ **多种内置点类型**: PointXYZ, PointXYZRGBA, PointNormal, IntensityXYZ 等
- ✅ **自定义点类型**: 支持用户定义的点结构
- ✅ **完整的头部信息解析和生成**
- ✅ **多目标框架支持**: .NET Framework 4.6.2+ 到 .NET 9.0
- ✅ **高性能**: unsafe 代码实现，零拷贝优化

## 支持的点类型

### 内置点类型

| 点类型 | 描述 | 字段 |
|--------|------|------|
| `PointXYZ` | 基本3D点 | X, Y, Z |
| `PointXYZRGBA` | 带颜色的3D点 | X, Y, Z, RGBA |
| `PointNormal` | 带法向量的3D点 | X, Y, Z, NormalX, NormalY, NormalZ, Curvature |
| `IntensityXYZ` | 带强度信息的3D点 | Intensity, X, Y, Z |

### 自定义点类型
PCDSharp 支持任何包含公共字段或属性的自定义点类型：

```csharp
public struct MyCustomPoint
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float Temperature { get; set; }
    public uint Label { get; set; }
}
```

## 快速开始

### 安装

```bash
# 通过 NuGet 安装 (待发布)
dotnet add package PcdSharp

# 或者克隆源代码
git clone https://github.com/emako/PcdSharp.git
```

### 基本用法

#### 读取点云数据

```csharp
using PcdSharp.IO;
using PcdSharp.Struct;

// 读取 PointXYZ 点云
var xyzCloud = PCDReader.Read<PointXYZ>("points.pcd");
Console.WriteLine($"读取了 {xyzCloud.Count} 个点");

// 读取 PointXYZRGBA 点云
var rgbaCloud = PCDReader.Read<PointXYZRGBA>("colored_points.pcd");

// 读取自定义点类型
var customCloud = PCDReader.Read<MyCustomPoint>("custom_points.pcd");

// 访问点数据
foreach (var point in xyzCloud.Points)
{
    Console.WriteLine($"点坐标: ({point.X:F3}, {point.Y:F3}, {point.Z:F3})");
}
```

#### 写入点云数据

```csharp
using PcdSharp;
using PcdSharp.IO;
using PcdSharp.Struct;

// 创建点云
var pointCloud = new PointCloudImpl<PointXYZ>();
pointCloud.Width = 10;
pointCloud.Height = 1;

// 添加点数据
for (int i = 0; i < 10; i++)
{
    pointCloud.Add(new PointXYZ 
    { 
        X = i * 0.1f, 
        Y = (float)Math.Sin(i), 
        Z = (float)Math.Cos(i)
    });
}

// 写入 ASCII 格式
PCDWriter.Write("output_ascii.pcd", pointCloud, DataEncoding.ASCII);

// 写入 Binary 格式
PCDWriter.Write("output_binary.pcd", pointCloud, DataEncoding.Binary);
```

#### 读取头部信息

```csharp
// 只读取头部信息，不加载点数据
var header = PCDReader.ReadHeader("points.pcd");

Console.WriteLine($"版本: {header.Version}");
Console.WriteLine($"字段: {string.Join(", ", header.Fields)}");
Console.WriteLine($"点数: {header.Points}");
Console.WriteLine($"数据格式: {header.Data}");
Console.WriteLine($"尺寸: {header.Width} x {header.Height}");
```

## 高级用法

### 流式读写

```csharp
// 从流中读取
using var fileStream = new FileStream("points.pcd", FileMode.Open);
var pointCloud = PCDReader.Read<PointXYZ>(fileStream);

// 写入到流
using var outputStream = new FileStream("output.pcd", FileMode.Create);
PCDWriter.Write(outputStream, pointCloud, DataEncoding.Binary);
```

### 组织化点云

```csharp
// 创建组织化点云 (类似图像的行列结构)
var organizedCloud = new PointCloudImpl<PointXYZ>();
organizedCloud.Width = 640;  // 列数
organizedCloud.Height = 480; // 行数

// 访问特定位置的点 (需要 .NET 5.0+)
if (organizedCloud.IsOrganized)
{
    ref var point = ref organizedCloud.At(x: 100, y: 200);
    Console.WriteLine($"位置 (100,200) 的点: ({point.X}, {point.Y}, {point.Z})");
}
```

### 自定义点类型映射

PCDSharp 自动处理字段映射，支持以下映射规则：

```csharp
public struct FlexiblePoint
{
    // 支持标准命名
    public float X, Y, Z;
    
    // 支持大小写变化
    public float intensity;  // 映射到 PCD 的 "intensity" 字段
    
    // 支持常见别名
    public float NormalX;    // 映射到 PCD 的 "normal_x" 字段
    public uint RGBA;        // 映射到 PCD 的 "rgba" 或 "rgb" 字段
}
```

## 性能特性

- **零拷贝读取**: 直接从内存映射读取二进制数据
- **Unsafe 代码**: 在支持的平台上使用 unsafe 代码提升性能
- **内存友好**: 支持大文件的流式处理
- **多框架优化**: 针对不同 .NET 版本进行优化

## 项目结构

```
PcdSharp/
├── src/                          # 主库源码
│   ├── PcdSharp.csproj          # 主库项目文件
│   ├── PointCloud.cs            # 点云抽象基类
│   ├── PointCloudImpl.cs        # 点云具体实现
│   ├── IO/
│   │   ├── PCDReader.cs         # PCD 读取器
│   │   ├── PCDWriter.cs         # PCD 写入器
│   │   ├── PCDHeader.cs         # PCD 头部定义
│   │   ├── PointFactory.cs      # 点对象工厂
│   │   ├── FieldMapping.cs      # 字段映射
│   │   └── PointContainer.cs    # 点容器
│   ├── Struct/
│   │   └── PointTypes.cs        # 内置点类型定义
│   ├── Exceptions/
│   │   └── PcdException.cs      # 异常定义
│   └── Polyfills/
│       └── Vector3.cs           # .NET Framework 兼容性
├── example/                      # 示例程序
│   ├── PcdSharp.Examples.csproj # 示例项目文件
│   ├── Program.cs               # 示例程序主文件
│   └── *.pcd                    # 测试用 PCD 文件
└── README.md                    # 本文档
```

## PCD 文件格式示例

### 简单 ASCII 格式
```
# .PCD v0.7 - Point Cloud Data file format
VERSION 0.7
FIELDS x y z
SIZE 4 4 4
TYPE F F F
COUNT 1 1 1
WIDTH 5
HEIGHT 1
VIEWPOINT 0 0 0 1 0 0 0
POINTS 5
DATA ascii
0.0 0.0 0.0
1.0 0.0 0.0
0.0 1.0 0.0
0.0 0.0 1.0
1.0 1.0 1.0
```

### 带颜色的 ASCII 格式
```
# .PCD v0.7 - Point Cloud Data file format
VERSION 0.7
FIELDS x y z rgba
SIZE 4 4 4 4
TYPE F F F U
COUNT 1 1 1 1
WIDTH 3
HEIGHT 1
POINTS 3
DATA ascii
0.0 0.0 0.0 4278190335
1.0 0.0 0.0 16711935
0.0 1.0 0.0 65535
```

## 编译和运行

### 编译库
```bash
# 编译所有目标框架
dotnet build

# 编译特定框架
dotnet build -f net9.0
dotnet build -f net462
```

### 运行示例程序
```bash
# 运行默认示例（创建示例点云）
dotnet run --project example

# 读取指定 PCD 文件
dotnet run --project example -- "path/to/your.pcd"

# 在 example 目录中直接运行
cd example
dotnet run test_simple_ascii.pcd
```

## API 参考

### PCDReader 类

#### 静态方法

| 方法 | 描述 |
|------|------|
| `ReadHeader(string filePath)` | 从文件读取 PCD 头部信息 |
| `ReadHeader(StreamReader reader)` | 从流读取 PCD 头部信息 |  
| `Read<T>(string filePath)` | 读取指定类型的点云数据 |
| `Read<T>(Stream stream)` | 从流读取指定类型的点云数据 |
| `ReadPoints<T>(string filePath)` | 读取点列表（向后兼容） |

### PCDWriter 类

#### 静态方法

| 方法 | 描述 |
|------|------|
| `Write<T>(string filePath, PointCloud<T> pointCloud, DataEncoding encoding)` | 写入点云到文件 |
| `Write<T>(Stream stream, PointCloud<T> pointCloud, DataEncoding encoding)` | 写入点云到流 |

### DataEncoding 枚举

| 值 | 描述 | 读取支持 | 写入支持 |
|----|------|----------|----------|
| `ASCII` | ASCII 文本格式 | ✅ | ✅ |
| `Binary` | 二进制格式 | ✅ | ✅ |
| `BinaryCompressed` | LZF 压缩二进制格式 | ✅ | ❌ |

### PCDHeader 类

| 属性 | 类型 | 描述 |
|------|------|------|
| `Version` | `string` | PCD 版本号 |
| `Fields` | `List<string>` | 字段名列表 |
| `Size` | `List<int>` | 每个字段的字节大小 |
| `Type` | `List<string>` | 每个字段的数据类型 (F/I/U) |
| `Count` | `List<int>` | 每个字段的元素数量 |
| `Width` | `int` | 点云宽度 |
| `Height` | `int` | 点云高度 |  
| `Points` | `long` | 总点数 |
| `Data` | `DataEncoding` | 数据编码格式 |
| `IsDense` | `bool` | 是否为密集点云 |
| `ViewPoint` | `List<float>?` | 视点坐标 (7 个 float) |

### PointCloud<T> 抽象类

| 属性/方法 | 描述 |
|-----------|------|
| `Points` | 点数据列表 |
| `Count` | 点数量 |
| `Width`, `Height` | 点云尺寸 |
| `IsDense` | 是否密集 |
| `IsOrganized` | 是否组织化 |
| `At(int col, int row)` | 访问组织化点云中的特定点 |
| `Add(T point)` | 添加点 |

## 支持的 .NET 版本

- .NET Framework 4.6.2, 4.7.2, 4.8
- .NET Standard 2.0, 2.1  
- .NET 5.0, 6.0, 7.0, 8.0, 9.0

## 限制和已知问题

- **写入限制**: 目前不支持 Binary Compressed 格式的写入
- **内存使用**: 大文件会占用大量内存，建议对超大文件进行分块处理
- **.NET Framework**: 在 .NET Framework 4.6.2 中，组织化点云的 `At()` 方法不可用

## 许可证

本项目采用 MIT 许可证。详见 [LICENSE](LICENSE) 文件。

## 贡献

欢迎提交 Issue 和 Pull Request！

### 开发指南

1. Fork 本仓库
2. 创建特性分支 (`git checkout -b feature/AmazingFeature`)
3. 提交更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 创建 Pull Request

## 致谢

感谢 Point Cloud Library (PCL) 项目提供的 PCD 格式规范和参考实现。

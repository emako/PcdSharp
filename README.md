# PcdSharp - PCD v0.7 Reader

PcdSharp 是一个用于读取 Point Cloud Data (PCD) v0.7 格式文件的 C# 库。

## 功能特性

- ✅ 支持 PCD v0.7 格式
- ✅ 支持 ASCII 和 Binary 数据编码
- ✅ 支持多种点类型：PointXYZ, PointXYZRGBA
- ✅ 完整的头部信息解析
- ✅ 多目标框架支持 (.NET Framework 4.6.2 和 .NET 9.0)
- ✅ 高性能 unsafe 代码实现

## 支持的点类型

### PointXYZ
基本的3D点，包含X、Y、Z坐标信息。

### PointXYZRGBA  
带颜色信息的3D点，包含X、Y、Z坐标和RGBA颜色信息。

## 使用方法

### 读取头部信息

```csharp
using PcdSharp.IO;

// 读取PCD文件头部信息
var header = PCDReader.ReadHeader("path/to/your.pcd");

Console.WriteLine($"版本: {header.Version}");
Console.WriteLine($"点数: {header.Points}");
Console.WriteLine($"数据格式: {header.Data}");
```

### 读取PointXYZ数据

```csharp
using PcdSharp.IO;
using PcdSharp.Struct;

// 读取XYZ点云数据
var points = PCDReader.ReadPointXYZ("path/to/your.pcd");

foreach (var point in points)
{
    Console.WriteLine($"({point.X}, {point.Y}, {point.Z})");
}
```

### 读取PointXYZRGBA数据

```csharp
using PcdSharp.IO;
using PcdSharp.Struct;

// 读取XYZRGBA点云数据
var points = PCDReader.ReadPointXYZRGBA("path/to/your.pcd");

foreach (var point in points)
{
    Console.WriteLine($"({point.X}, {point.Y}, {point.Z}) Color: 0x{point.RGBA:X8}");
}
```

## PCD 文件格式示例

### 简单XYZ格式
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

### 带颜色信息的格式
```
# .PCD v0.7 - Point Cloud Data file format with RGB
VERSION 0.7
FIELDS x y z rgb
SIZE 4 4 4 4
TYPE F F F I
COUNT 1 1 1 1
WIDTH 3
HEIGHT 1
VIEWPOINT 0 0 0 1 0 0 0
POINTS 3
DATA ascii
0.0 0.0 0.0 4278190080
1.0 0.0 0.0 4278255360
0.0 1.0 0.0 4294901760
```

## 项目结构

```
PcdSharp/
├── src/
│   ├── PcdSharp.csproj          # 主库项目
│   ├── PointCloud.cs            # 点云基础类
│   ├── IO/
│   │   ├── PCDReader.cs         # PCD读取器实现
│   │   └── PCDWriter.cs         # PCD写入器（待实现）
│   └── Struct/
│       └── PointTypes.cs        # 点类型定义
├── Examples/
│   ├── Examples.csproj          # 示例程序项目
│   └── Program.cs               # 示例程序
└── README.md                    # 本文档
```

## 编译和运行

### 编译库
```bash
dotnet build src/
```

### 运行示例程序
```bash
dotnet run --project Examples -- "path/to/your.pcd"
```

## API 参考

### PCDReader 类

#### 静态方法

- `ReadHeader(string filePath)` - 从文件读取PCD头部信息
- `ReadHeader(StreamReader reader)` - 从流读取PCD头部信息  
- `ReadPointXYZ(string filePath)` - 读取PointXYZ类型的点云数据
- `ReadPointXYZRGBA(string filePath)` - 读取PointXYZRGBA类型的点云数据

#### PCDHeader 类

包含PCD文件的头部信息：

- `Version` - PCD版本
- `Fields` - 字段名列表
- `Size` - 每个字段的字节大小
- `Type` - 每个字段的数据类型
- `Count` - 每个字段的元素数量
- `Width` - 点云宽度
- `Height` - 点云高度  
- `Points` - 总点数
- `Data` - 数据编码格式（ASCII/Binary/BinaryCompressed）
- `IsDense` - 是否为密集点云
- `ViewPoint` - 视点坐标（可选）

## 限制

当前版本的限制：

- ❌ 尚不支持 Binary Compressed 格式
- ❌ 仅支持常见的点类型（XYZ, XYZRGBA）
- ❌ 尚未实现 PCDWriter

## 许可证

本项目采用 MIT 许可证。详见 LICENSE 文件。

## 贡献

欢迎提交 Issue 和 Pull Request！

## 更新日志

### v1.0.0 (当前版本)
- ✅ 实现 PCD v0.7 Reader
- ✅ 支持 ASCII 和 Binary 格式
- ✅ 支持 PointXYZ 和 PointXYZRGBA 点类型
- ✅ 完整的头部信息解析
- ✅ 示例程序和文档

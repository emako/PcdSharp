using PcdSharp.IO;

namespace PcdSharp;

/// <summary>
/// PointCloud的具体实现类
/// </summary>
/// <typeparam name="PointT">点类型</typeparam>
public class PointCloudImpl<PointT> : PointCloud<PointT>
{
    private List<PointT> _points;

    /// <summary>
    /// 点云头部信息，包含版本、字段定义、视点等元数据
    /// </summary>
    public override PCDHeader? Header { get; set; }

    public override List<PointT> Points
    {
        get => _points;
        set => _points = value ?? [];
    }

    public override int Count => _points.Count;

    public override bool IsOrganized => Height > 1;

    public PointCloudImpl()
    {
        _points = [];
    }

    public PointCloudImpl(int capacity)
    {
        _points = new List<PointT>(capacity);
    }

    public override ref PointT At(int col, int row)
    {
        if (!IsOrganized)
            throw new InvalidOperationException("Point cloud is not organized");

        var index = row * Width + col;
        if (index >= _points.Count)
            throw new IndexOutOfRangeException();

#if NET5_0_OR_GREATER
        return ref System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_points)[index];
#else
        // .NET Framework 不支持 CollectionsMarshal，使用索引器
        // 注意：这不会返回 ref，但是在旧框架中这是最佳选择
        throw new NotSupportedException("Ref return is not supported in .NET Framework. Use indexer instead.");
#endif
    }

    /// <summary>
    /// 获取指定位置的点（不返回引用，兼容旧框架）
    /// </summary>
    /// <param name="col">列索引</param>
    /// <param name="row">行索引</param>
    /// <returns>点对象</returns>
    public PointT GetAt(int col, int row)
    {
        if (!IsOrganized)
            throw new InvalidOperationException("Point cloud is not organized");

        var index = row * Width + col;
        if (index >= _points.Count)
            throw new IndexOutOfRangeException();

        return _points[index];
    }

    /// <summary>
    /// 设置指定位置的点
    /// </summary>
    /// <param name="col">列索引</param>
    /// <param name="row">行索引</param>
    /// <param name="point">点对象</param>
    public void SetAt(int col, int row, PointT point)
    {
        if (!IsOrganized)
            throw new InvalidOperationException("Point cloud is not organized");

        var index = row * Width + col;
        if (index >= _points.Count)
            throw new IndexOutOfRangeException();

        _points[index] = point;
    }

    public override void Add(PointT value)
    {
        _points.Add(value);
    }

    /// <summary>
    /// 批量添加点
    /// </summary>
    /// <param name="points">要添加的点集合</param>
    public override void AddRange(IEnumerable<PointT> points)
    {
        _points.AddRange(points);
    }

    /// <summary>
    /// 清空点云
    /// </summary>
    public override void Clear()
    {
        _points.Clear();
        _points.Capacity = 0;
        
        // 如果有 Header，更新 Header 中的信息
        if (Header != null)
        {
            Header.Width = 0;
            Header.Height = 1;
            Header.Points = 0;
        }
    }

    /// <summary>
    /// 预留容量
    /// </summary>
    /// <param name="capacity">容量</param>
    public override void Reserve(int capacity)
    {
        if (_points.Capacity < capacity)
        {
            _points.Capacity = capacity;
        }
    }
}

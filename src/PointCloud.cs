using PcdSharp.IO;

namespace PcdSharp;

public abstract class PointCloud<PointT>
{
    /// <summary>
    /// 点云头部信息，包含版本、字段定义、视点等元数据
    /// </summary>
    public abstract PCDHeader? Header { get; set; }

    /// <summary>
    /// 点云宽度，从Header中获取，如果Header不存在则使用点数
    /// </summary>
    public int Width => Header?.Width ?? Count;

    /// <summary>
    /// 点云高度，从Header中获取，如果Header不存在则默认为1
    /// </summary>
    public int Height => Header?.Height ?? 1;

    /// <summary>
    /// 是否为密集点云，从Header中获取，如果Header不存在则默认为true
    /// </summary>
    public bool IsDense => Header?.IsDense ?? true;

    public abstract List<PointT> Points { get; set; }

    public abstract int Count { get; }

    public abstract bool IsOrganized { get; }

    public abstract ref PointT At(int col, int row);

    public abstract void Add(PointT value);

    public abstract void AddRange(IEnumerable<PointT> points);

    public abstract void Clear();

    public abstract void Reserve(int capacity);
}

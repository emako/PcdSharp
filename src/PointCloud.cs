namespace PcdSharp;

public abstract class PointCloud<PointT>
{
    public abstract int Width { get; set; }

    public abstract int Height { get; set; }

    public abstract bool IsDense { get; set; }

    public abstract List<PointT> Points { get; set; }

    public abstract int Count { get; }

    public abstract bool IsOrganized { get; }

    public abstract ref PointT At(int col, int row);

    public abstract void Add(PointT value);

    public abstract void AddRange(IEnumerable<PointT> points);

    public abstract void Clear();

    public abstract void Reserve(int capacity);
}

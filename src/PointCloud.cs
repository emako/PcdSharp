namespace PcdSharp;

public abstract class PointCloud<PointT>
{
    public abstract int Width { get; set; }

    public abstract int Height { get; set; }

    public abstract bool IsDense { get; set; }

    public abstract List<PointT> Points { get; }

    public abstract int Count { get; }

    public abstract bool IsOrganized { get; }

    public abstract ref PointT At(int col, int row);

    public abstract void Add(PointT value);
}

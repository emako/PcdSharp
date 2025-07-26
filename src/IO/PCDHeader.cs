namespace PcdSharp.IO;

public class PCDHeader
{
    public string Version { get; set; } = string.Empty;

    public List<string> Fields { get; set; } = [];

    public List<int> Size { get; set; } = [];

    public List<string> Type { get; set; } = [];

    public List<int> Count { get; set; } = [];

    public int Width { get; set; }

    public int Height { get; set; }

    public List<float>? ViewPoint { get; set; }

    public long Points { get; set; }

    public DataEncoding Data { get; set; }

    public bool IsDense { get; set; } = true;
}

public enum DataEncoding
{
    ASCII,
    Binary,
    BinaryCompressed,
}

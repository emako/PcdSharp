namespace PcdSharp.IO;

/// <summary>
/// 用于包装点的容器，支持引用传递
/// </summary>
/// <typeparam name="T"></typeparam>
public class PointContainer<T>(T value)
{
    public T Value = value;
}

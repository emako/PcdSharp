namespace PcdSharp.IO;

/// <summary>
/// 坐标系类型
/// </summary>
public enum CoordinateSystem
{
    /// <summary>
    /// 右手坐标系 (默认)
    /// </summary>
    RightHanded,
    
    /// <summary>
    /// 左手坐标系
    /// </summary>
    LeftHanded
}

/// <summary>
/// 坐标变换选项
/// </summary>
public class CoordinateTransformOptions
{
    /// <summary>
    /// 源坐标系
    /// </summary>
    public CoordinateSystem SourceSystem { get; set; } = CoordinateSystem.RightHanded;
    
    /// <summary>
    /// 目标坐标系
    /// </summary>
    public CoordinateSystem TargetSystem { get; set; } = CoordinateSystem.RightHanded;
    
    /// <summary>
    /// X轴缩放因子
    /// </summary>
    public float ScaleX { get; set; } = 1.0f;
    
    /// <summary>
    /// Y轴缩放因子
    /// </summary>
    public float ScaleY { get; set; } = 1.0f;
    
    /// <summary>
    /// Z轴缩放因子
    /// </summary>
    public float ScaleZ { get; set; } = 1.0f;
    
    /// <summary>
    /// 检查是否需要坐标变换
    /// </summary>
    public bool NeedsTransformation => SourceSystem != TargetSystem || ScaleX != 1.0f || ScaleY != 1.0f || ScaleZ != 1.0f;
    
    /// <summary>
    /// 创建左手到右手坐标系的默认变换选项 (翻转Y轴)
    /// </summary>
    public static CoordinateTransformOptions LeftToRightHanded() => new()
    {
        SourceSystem = CoordinateSystem.LeftHanded,
        TargetSystem = CoordinateSystem.RightHanded,
        ScaleY = -1.0f
    };
    
    /// <summary>
    /// 创建右手到左手坐标系的默认变换选项 (翻转Y轴)
    /// </summary>
    public static CoordinateTransformOptions RightToLeftHanded() => new()
    {
        SourceSystem = CoordinateSystem.RightHanded,
        TargetSystem = CoordinateSystem.LeftHanded,
        ScaleY = -1.0f
    };
}
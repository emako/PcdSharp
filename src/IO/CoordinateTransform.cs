using System.Numerics;

namespace PcdSharp.IO;

/// <summary>
/// 坐标变换工具类
/// </summary>
public static class CoordinateTransform
{
    /// <summary>
    /// 对坐标点应用变换
    /// </summary>
    /// <param name="x">X坐标</param>
    /// <param name="y">Y坐标</param>
    /// <param name="z">Z坐标</param>
    /// <param name="options">变换选项</param>
    /// <param name="transformedX">变换后的X坐标</param>
    /// <param name="transformedY">变换后的Y坐标</param>
    /// <param name="transformedZ">变换后的Z坐标</param>
    public static void Transform(float x, float y, float z, CoordinateTransformOptions options, out float transformedX, out float transformedY, out float transformedZ)
    {
        if (!options.NeedsTransformation)
        {
            transformedX = x;
            transformedY = y;
            transformedZ = z;
            return;
        }

        transformedX = x * options.ScaleX;
        transformedY = y * options.ScaleY;
        transformedZ = z * options.ScaleZ;
    }

    /// <summary>
    /// 对Vector3应用变换
    /// </summary>
    /// <param name="vector">输入向量</param>
    /// <param name="options">变换选项</param>
    /// <returns>变换后的向量</returns>
    public static Vector3 Transform(Vector3 vector, CoordinateTransformOptions options)
    {
        if (!options.NeedsTransformation)
            return vector;

        return new Vector3(
            vector.X * options.ScaleX,
            vector.Y * options.ScaleY,
            vector.Z * options.ScaleZ);
    }

    /// <summary>
    /// 对法向量应用变换 (法向量需要特殊处理)
    /// </summary>
    /// <param name="normalX">法向量X分量</param>
    /// <param name="normalY">法向量Y分量</param>
    /// <param name="normalZ">法向量Z分量</param>
    /// <param name="options">变换选项</param>
    /// <param name="transformedNx">变换后的法向量X分量</param>
    /// <param name="transformedNy">变换后的法向量Y分量</param>
    /// <param name="transformedNz">变换后的法向量Z分量</param>
    public static void TransformNormal(float normalX, float normalY, float normalZ, CoordinateTransformOptions options, out float transformedNx, out float transformedNy, out float transformedNz)
    {
        if (!options.NeedsTransformation)
        {
            transformedNx = normalX;
            transformedNy = normalY;
            transformedNz = normalZ;
            return;
        }

        // 法向量变换需要考虑缩放的逆变换，但这里简化处理
        transformedNx = normalX * Math.Sign(options.ScaleX);
        transformedNy = normalY * Math.Sign(options.ScaleY);
        transformedNz = normalZ * Math.Sign(options.ScaleZ);
    }
}
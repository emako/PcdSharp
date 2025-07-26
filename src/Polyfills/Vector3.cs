#if NETSTANDARD2_0

using System.Runtime.InteropServices;

#pragma warning disable IDE0130 // Namespace does not match folder structure

namespace System.Numerics;

#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Vector3 polyfill for older .NET frameworks
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct Vector3 : IEquatable<Vector3>
{
    public float X;
    public float Y;
    public float Z;

    public Vector3(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public Vector3(float value)
    {
        X = Y = Z = value;
    }

    public static Vector3 Zero => new(0, 0, 0);
    public static Vector3 One => new(1, 1, 1);
    public static Vector3 UnitX => new(1, 0, 0);
    public static Vector3 UnitY => new(0, 1, 0);
    public static Vector3 UnitZ => new(0, 0, 1);

    public static Vector3 operator +(Vector3 left, Vector3 right)
    {
        return new Vector3(left.X + right.X, left.Y + right.Y, left.Z + right.Z);
    }

    public static Vector3 operator -(Vector3 left, Vector3 right)
    {
        return new Vector3(left.X - right.X, left.Y - right.Y, left.Z - right.Z);
    }

    public static Vector3 operator *(Vector3 left, float right)
    {
        return new Vector3(left.X * right, left.Y * right, left.Z * right);
    }

    public static Vector3 operator *(float left, Vector3 right)
    {
        return new Vector3(left * right.X, left * right.Y, left * right.Z);
    }

    public static Vector3 operator /(Vector3 left, float right)
    {
        return new Vector3(left.X / right, left.Y / right, left.Z / right);
    }

    public static bool operator ==(Vector3 left, Vector3 right)
    {
        return left.X == right.X && left.Y == right.Y && left.Z == right.Z;
    }

    public static bool operator !=(Vector3 left, Vector3 right)
    {
        return !(left == right);
    }

    public readonly bool Equals(Vector3 other)
    {
        return X == other.X && Y == other.Y && Z == other.Z;
    }

    public override readonly bool Equals(object? obj)
    {
        return obj is Vector3 other && Equals(other);
    }

    public override int GetHashCode()
    {
#if NETSTANDARD2_0
        // Simple hash combine for older frameworks
        unchecked
        {
            int hash = 17;
            hash = hash * 23 + X.GetHashCode();
            hash = hash * 23 + Y.GetHashCode();
            hash = hash * 23 + Z.GetHashCode();
            return hash;
        }
#else
        return HashCode.Combine(X, Y, Z);
#endif
    }

    public override readonly string ToString()
    {
        return $"<{X}, {Y}, {Z}>";
    }

    public readonly float Length()
    {
        return (float)Math.Sqrt(X * X + Y * Y + Z * Z);
    }

    public readonly float LengthSquared()
    {
        return X * X + Y * Y + Z * Z;
    }

    public static float Distance(Vector3 value1, Vector3 value2)
    {
        return (value1 - value2).Length();
    }

    public static float DistanceSquared(Vector3 value1, Vector3 value2)
    {
        return (value1 - value2).LengthSquared();
    }

    public static Vector3 Normalize(Vector3 value)
    {
        var length = value.Length();
        if (length == 0) return Zero;
        return value / length;
    }

    public static float Dot(Vector3 vector1, Vector3 vector2)
    {
        return vector1.X * vector2.X + vector1.Y * vector2.Y + vector1.Z * vector2.Z;
    }

    public static Vector3 Cross(Vector3 vector1, Vector3 vector2)
    {
        return new Vector3(
            vector1.Y * vector2.Z - vector1.Z * vector2.Y,
            vector1.Z * vector2.X - vector1.X * vector2.Z,
            vector1.X * vector2.Y - vector1.Y * vector2.X);
    }
}

#endif

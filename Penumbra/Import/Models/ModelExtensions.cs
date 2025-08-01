namespace Penumbra.Import.Models;

public static class ModelExtensions
{
    // https://github.com/vpenades/SharpGLTF/blob/2073cf3cd671f8ecca9667f9a8c7f04ed865d3ac/src/Shared/_Extensions.cs#L158
    private const float UnitLengthThresholdVec3 = 0.00674f;
    private const float UnitLengthThresholdVec4 = 0.00769f;
    
    internal static bool _IsFinite(this float value)
    {
        return float.IsFinite(value);
    }
    
    internal static bool _IsFinite(this Vector2 v)
    {
        return v.X._IsFinite() && v.Y._IsFinite();
    }

    internal static bool _IsFinite(this Vector3 v)
    {
        return v.X._IsFinite() && v.Y._IsFinite() && v.Z._IsFinite();
    }

    internal static bool _IsFinite(this in Vector4 v)
    {
        return v.X._IsFinite() && v.Y._IsFinite() && v.Z._IsFinite() && v.W._IsFinite();
    }
    
    internal static Boolean IsNormalized(this Vector3 normal)
    {
        if (!normal._IsFinite()) return false;

        return Math.Abs(normal.Length() - 1) <= UnitLengthThresholdVec3;
    }
    
    internal static void ValidateNormal(this Vector3 normal, string msg)
    {
        if (!normal._IsFinite()) throw new NotFiniteNumberException($"{msg} is invalid.");

        if (!normal.IsNormalized()) throw new ArithmeticException($"{msg} is not unit length.");
    }

    internal static void ValidateTangent(this Vector4 tangent, string msg)
    {
        if (tangent.W != 1 && tangent.W != -1) throw new ArithmeticException(msg);

        new Vector3(tangent.X, tangent.Y, tangent.Z).ValidateNormal(msg);
    }

    internal static Vector3 SanitizeNormal(this Vector3 normal)
    {
        if (normal == Vector3.Zero) return Vector3.UnitX;
        return normal.IsNormalized() ? normal : Vector3.Normalize(normal);
    }

    internal static bool IsValidTangent(this Vector4 tangent)
    {
        if (tangent.W != 1 && tangent.W != -1) return false;

        return new Vector3(tangent.X, tangent.Y, tangent.Z).IsNormalized();
    }

    internal static Vector4 SanitizeTangent(this Vector4 tangent)
    {
        var n = new Vector3(tangent.X, tangent.Y, tangent.Z).SanitizeNormal();
        var s = float.IsNaN(tangent.W) ? 1 : tangent.W;
        return new Vector4(n, s > 0 ? 1 : -1);
    }
}

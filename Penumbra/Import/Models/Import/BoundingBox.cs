using Lumina.Data.Parsing;

namespace Penumbra.Import.Models.Import;

/// <summary> Mutable representation of the bounding box surrounding a collection of vertices. </summary>
public class BoundingBox
{
    private Vector3 _minimum = new(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
    private Vector3 _maximum = new(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

    /// <summary> Use the specified position to update this bounding box, expanding it if necessary. </summary>
    public void Merge(Vector3 position)
    {
        _minimum = Vector3.Min(_minimum, position);
        _maximum = Vector3.Max(_maximum, position);
    }

    /// <summary> Merge the provided bounding box into this one, expanding it if necessary. </summary>
    /// <param name="other"></param>
    public void Merge(BoundingBox other)
    {
        _minimum = Vector3.Min(_minimum, other._minimum);
        _maximum = Vector3.Max(_maximum, other._maximum);
    }

    /// <summary> Convert this bounding box to the struct format used in .mdl data structures. </summary>
    public MdlStructs.BoundingBoxStruct ToStruct()
        => new()
        {
            Min = [_minimum.X, _minimum.Y, _minimum.Z, 1],
            Max = [_maximum.X, _maximum.Y, _maximum.Z, 1],
        };
}

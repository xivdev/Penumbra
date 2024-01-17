using Lumina.Data.Parsing;

namespace Penumbra.Import.Models.Import;

/// <summary> Mutable representation of the bounding box surrouding a collection of vertices. </summary>
public class BoundingBox
{
    private Vector3 _minimum = Vector3.Zero;
    private Vector3 _maximum = Vector3.Zero;

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
        => new MdlStructs.BoundingBoxStruct
        {
            Min = [_minimum.X, _minimum.Y, _minimum.Z, 1],
            Max = [_maximum.X, _maximum.Y, _maximum.Z, 1],
        };
}

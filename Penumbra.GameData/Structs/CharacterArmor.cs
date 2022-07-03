using System.Runtime.InteropServices;

namespace Penumbra.GameData.Structs;

[StructLayout( LayoutKind.Explicit, Pack = 1 )]
public readonly struct CharacterArmor
{
    [FieldOffset( 0 )]
    public readonly SetId Set;

    [FieldOffset( 2 )]
    public readonly byte Variant;

    [FieldOffset( 3 )]
    public readonly StainId Stain;

    [FieldOffset( 0 )]
    public readonly uint Value;

    public override string ToString()
        => $"{Set},{Variant},{Stain}";
}
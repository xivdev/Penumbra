using System.Runtime.InteropServices;

namespace Penumbra.GameData.Structs
{
    [StructLayout( LayoutKind.Sequential, Pack = 1 )]
    public readonly struct CharacterWeapon
    {
        public readonly SetId      Set;
        public readonly WeaponType Type;
        public readonly ushort     Variant;
        public readonly StainId    Stain;

        public override string ToString()
            => $"{Set},{Type},{Variant},{Stain}";
    }
}
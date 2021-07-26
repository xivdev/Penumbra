using System.Runtime.InteropServices;

namespace Penumbra.GameData.Structs
{
    [StructLayout( LayoutKind.Sequential, Pack = 1 )]
    public readonly struct ActorArmor
    {
        public readonly SetId   Set;
        public readonly byte    Variant;
        public readonly StainId Stain;

        public override string ToString()
            => $"{Set},{Variant},{Stain}";
    }
}
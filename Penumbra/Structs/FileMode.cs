namespace Penumbra.Structs
{
    public enum FileMode : uint
    {
        LoadUnpackedResource = 0,
        LoadFileResource = 1, // Shit in My Games uses this
        LoadSqpackResource = 0x0B
    }
}
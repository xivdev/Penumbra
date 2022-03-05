namespace Penumbra.Interop.Structs;

public enum FileMode : uint
{
    LoadUnpackedResource = 0,
    LoadFileResource     = 1, // Shit in My Games uses this

    // some shit here, the game does some jump if its < 0xA for other files for some reason but there's no impl, probs debug?
    LoadIndexResource  = 0xA, // load index/index2
    LoadSqPackResource = 0xB,
}
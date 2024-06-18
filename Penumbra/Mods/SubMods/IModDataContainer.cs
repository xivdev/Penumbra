using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Editor;
using Penumbra.Mods.Groups;
using Penumbra.String.Classes;

namespace Penumbra.Mods.SubMods;

public interface IModDataContainer
{
    public IMod       Mod   { get; }
    public IModGroup? Group { get; }

    public Dictionary<Utf8GamePath, FullPath> Files         { get; set; }
    public Dictionary<Utf8GamePath, FullPath> FileSwaps     { get; set; }
    public MetaDictionary                     Manipulations { get; set; }

    public string                          GetName();
    public string                          GetFullName();
    public (int GroupIndex, int DataIndex) GetDataIndices();
}

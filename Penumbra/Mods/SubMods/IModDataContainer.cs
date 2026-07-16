using Luna;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Editor;
using Penumbra.Mods.Groups;
using Penumbra.String.Classes;

namespace Penumbra.Mods.SubMods;

public interface IModDataContainer : IIndexed
{
    public IMod       Mod   { get; }
    public IModGroup? Group { get; }

    public int GroupIndex
        => Group?.Index ?? -1;

    public Dictionary<Utf8GamePath, FullPath> Files         { get; set; }
    public Dictionary<Utf8GamePath, FullPath> FileSwaps     { get; set; }
    public MetaDictionary                     Manipulations { get; set; }

    public string GetName();
    public string GetDirectoryName();
    public string GetFullName();
}

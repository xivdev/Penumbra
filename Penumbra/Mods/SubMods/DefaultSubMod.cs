using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Editor;
using Penumbra.Mods.Groups;
using Penumbra.String.Classes;

namespace Penumbra.Mods.SubMods;

public class DefaultSubMod(IMod mod) : IModDataContainer
{
    public const string FullName = "Default Option";

    internal readonly IMod Mod = mod;

    public Dictionary<Utf8GamePath, FullPath> Files         { get; set; } = [];
    public Dictionary<Utf8GamePath, FullPath> FileSwaps     { get; set; } = [];
    public MetaDictionary                     Manipulations { get; set; } = new();

    IMod IModDataContainer.Mod
        => Mod;

    IModGroup? IModDataContainer.Group
        => null;

    public void AddTo(Dictionary<Utf8GamePath, FullPath> redirections, MetaDictionary manipulations)
        => SubMod.AddContainerTo(this, redirections, manipulations);

    public string GetName()
        => FullName;

    public string GetFullName()
        => FullName;

    public (int GroupIndex, int DataIndex) GetDataIndices()
        => (-1, 0);
}

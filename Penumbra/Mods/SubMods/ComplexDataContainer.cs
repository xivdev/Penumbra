using Luna;
using Newtonsoft.Json.Linq;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Editor;
using Penumbra.Mods.Groups;
using Penumbra.String.Classes;

namespace Penumbra.Mods.SubMods;

public sealed class ComplexDataContainer(ComplexModGroup group) : IModDataContainer
{
    public IMod Mod
        => Group.Mod;

    public IModGroup Group { get; } = group;

    public Dictionary<Utf8GamePath, FullPath> Files         { get; set; } = [];
    public Dictionary<Utf8GamePath, FullPath> FileSwaps     { get; set; } = [];
    public MetaDictionary                     Manipulations { get; set; } = new();

    public MaskedSetting Association = MaskedSetting.Zero;

    public string Name { get; set; } = string.Empty;

    public string GetName()
        => Name.Length > 0 ? Name : $"Container {Group.DataContainers.IndexOf(this)}";

    public string GetDirectoryName()
        => Name.Length > 0 ? Name : $"{Group.DataContainers.IndexOf(this)}";

    public string GetFullName()
        => $"{Group.Name}: {GetName()}";

    public (int GroupIndex, int DataIndex) GetDataIndices()
        => (Group.GetIndex(), Group.DataContainers.IndexOf(this));

    public ComplexDataContainer(ComplexModGroup group, JToken json)
        : this(group)
    {
        SubMod.LoadDataContainer(json, this, group.Mod.ModPath);
        var mask  = json["AssociationMask"]?.ToObject<ulong>() ?? 0;
        var value = json["AssociationMask"]?.ToObject<ulong>() ?? 0;
        Association = new MaskedSetting(mask, value);
        Name        = json["Name"]?.ToObject<string>() ?? string.Empty;
    }
}

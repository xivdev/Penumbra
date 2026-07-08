using Luna;
using Newtonsoft.Json;
using Penumbra.Api.Enums;
using Penumbra.GameData.Data;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Settings;
using Penumbra.Mods.SubMods;
using Penumbra.String.Classes;
using Penumbra.UI.ModsTab.Groups;

namespace Penumbra.Mods.Groups;

public interface ITexToolsGroup
{
    public IReadOnlyList<OptionSubMod> OptionData { get; }
}

public enum GroupDrawBehaviour
{
    SingleSelection,
    MultiSelection,
}

public interface IModGroup : IModObject, IIndexed
{
    public const int MaxMultiOptions     = 32;
    public const int MaxCombiningOptions = 8;

    IModGroup IModObject.Group
        => this;

    /// <summary> Unused in Penumbra but for better TexTools interop. </summary>
    public string Image { get; set; }

    public GroupType          Type      { get; }
    public GroupDrawBehaviour Behaviour { get; }
    public ModPriority        Priority  { get; set; }

    /// <summary> Unused in Penumbra but for better TexTools interop. </summary>
    public int Page { get; set; }

    public Setting DefaultSettings { get; set; }

    public IModObject? ParentSetting { get; set; }

    IModObject? CycleChecker.IHasParent<IModObject>.Parent
        => ParentSetting;

    bool CycleChecker.IHasParent<IModObject>.CausesCycle(IModObject parent)
        => ReferenceEquals(this, parent.Group);

    public FullPath?   FindBestMatch(Utf8GamePath gamePath);
    public IModOption? AddOption(string name, string description = "");

    public IReadOnlyList<IModOption>        Options        { get; }
    public IReadOnlyList<IModDataContainer> DataContainers { get; }
    public bool                             IsOption       { get; }

    public IModGroupEditDrawer EditDrawer(ModGroupEditDrawer editDrawer);

    public void AddData(ModSettings settings, Setting setting, Dictionary<Utf8GamePath, FullPath> redirections, MetaDictionary manipulations);
    public void AddChangedItems(ObjectIdentification identifier, IDictionary<string, IIdentifiedObjectData> changedItems);

    /// <summary> Ensure that a value is valid for a group. </summary>
    public Setting FixSetting(Setting setting);

    public void WriteJson(JsonTextWriter jWriter, JsonSerializer serializer, DirectoryInfo? basePath = null);

    public (int Redirections, int Swaps, int Manips) GetCounts();
}

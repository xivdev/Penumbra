using Dalamud.Interface.ImGuiNotification;
using Luna;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Penumbra.Api.Enums;
using Penumbra.GameData.Data;
using Penumbra.GameData.Structs;
using Penumbra.Meta;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Settings;
using Penumbra.Mods.SubMods;
using Penumbra.String.Classes;
using Penumbra.UI.ModsTab.Groups;

namespace Penumbra.Mods.Groups;

public class ImcModGroup(Mod mod) : IModGroup
{
    public int Index { get; private set; } = -1;

    public void SetIndex(int index)
        => Index = index;

    public Mod    Mod         { get; }      = mod;
    public Guid   Id          { get; set; } = Guid.NewGuid();
    public string Name        { get; set; } = "Option";
    public string Description { get; set; } = string.Empty;
    public string Image       { get; set; } = string.Empty;

    public GroupType Type
        => GroupType.Imc;

    public GroupDrawBehaviour Behaviour
        => GroupDrawBehaviour.MultiSelection;

    public ModPriority                    Priority        { get; set; } = ModPriority.Default;
    public int                            Page            { get; set; }
    public Setting                        DefaultSettings { get; set; } = Setting.Zero;
    public ModSettingsLayout              Layout          { get; set; }
    public IModObject?                    ParentSetting   { get; set; }
    public ICondition<ModSettingContext>? Condition       { get; set; }

    public ImcIdentifier Identifier;
    public ImcEntry      DefaultEntry;
    public bool          AllVariants;
    public bool          OnlyAttributes;


    public FullPath? FindBestMatch(Utf8GamePath gamePath)
        => null;

    private bool _canBeDisabled;

    public bool CanBeDisabled
    {
        get => _canBeDisabled;
        set
        {
            _canBeDisabled = value;
            if (!value)
            {
                OptionData.RemoveAll(m => m.IsDisableSubMod);
                DefaultSettings = FixSetting(DefaultSettings);
            }
            else
            {
                if (!OptionData.Any(m => m.IsDisableSubMod))
                    OptionData.Add(ImcSubMod.DisableSubMod(this));
            }
        }
    }

    public IModOption AddOption(string name, string description = "")
    {
        var subMod = new ImcSubMod(this)
        {
            Name          = name,
            Description   = description,
            AttributeMask = 0,
        };
        OptionData.Add(subMod);
        return subMod;
    }

    public readonly IndexList<ImcSubMod> OptionData = [];

    public IReadOnlyList<IModOption> Options
        => OptionData;

    public IReadOnlyList<IModDataContainer> DataContainers
        => [];

    public bool IsOption
        => OptionData.Count > 0;

    public int GetIndex()
        => ModGroup.GetIndex(this);

    public IModGroupEditDrawer EditDrawer(ModGroupEditDrawer editDrawer)
        => new ImcModGroupEditDrawer(editDrawer, this);

    private ImcEntry GetEntry(Variant variant, ushort mask)
    {
        if (!OnlyAttributes)
            return DefaultEntry with { AttributeMask = mask };

        var defaultEntry = ImcChecker.GetDefaultEntry(Identifier with { Variant = variant }, true);
        if (defaultEntry.VariantExists)
            return defaultEntry.Entry with { AttributeMask = mask };

        return DefaultEntry with { AttributeMask = mask };
    }

    public void AddData(ModSettings settings, Setting setting, Dictionary<Utf8GamePath, FullPath> redirections, MetaDictionary manipulations)
    {
        var context = new ModSettingContext(Mod, settings);
        if (IsDisabled(context, setting))
            return;

        if (Condition is not null && !Condition.Evaluate(context))
            return;

        var mask = GetCurrentMask(context, setting);
        if (AllVariants)
        {
            var count = ImcChecker.GetVariantCount(Identifier);
            if (count is 0)
                manipulations.TryAdd(Identifier, GetEntry(Identifier.Variant, mask));
            else
                for (var i = 0; i <= count; ++i)
                    manipulations.TryAdd(Identifier with { Variant = (Variant)i }, GetEntry((Variant)i, mask));
        }
        else
        {
            manipulations.TryAdd(Identifier, GetEntry(Identifier.Variant, mask));
        }
    }

    public void AddChangedItems(ObjectIdentification identifier, IDictionary<string, IIdentifiedObjectData> changedItems)
        => Identifier.AddChangedItems(identifier, changedItems, AllVariants);

    public Setting FixSetting(Setting setting)
        => new(setting.Value & ((1ul << OptionData.Count) - 1));

    public (int Redirections, int Swaps, int Manips) GetCounts()
        => (0, 0, 1);

    private bool IsDisabled(in ModSettingContext context, Setting setting)
    {
        if (!CanBeDisabled)
            return false;

        var idx = OptionData.IndexOf(m => m.IsDisableSubMod);
        if (idx >= 0)
        {
            if (OptionData[idx].Condition is not { } condition || condition.Evaluate(context))
                return setting.HasFlag(idx);

            return false;
        }

        Penumbra.Log.Warning("A IMC Group should be able to be disabled, but does not contain a disable option.");
        return false;
    }

    private ushort GetCurrentMask(in ModSettingContext context, Setting setting)
    {
        var mask = DefaultEntry.AttributeMask;
        for (var i = 0; i < OptionData.Count; ++i)
        {
            if (!setting.HasFlag(i))
                continue;

            var option = OptionData[i];
            if (option.Condition is null || option.Condition.Evaluate(context))
                mask ^= option.AttributeMask;
        }

        return mask;
    }
}

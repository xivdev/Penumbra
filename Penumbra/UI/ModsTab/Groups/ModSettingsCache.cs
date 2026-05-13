using ImSharp;
using Luna;
using Penumbra.Api.Enums;
using Penumbra.Communication;
using Penumbra.Mods;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Settings;
using Penumbra.Mods.SubMods;
using Penumbra.Services;

namespace Penumbra.UI.ModsTab.Groups;

public sealed class ModSettingsCache : BasicCache
{
    public sealed class ModGroupCache
    {
        public GroupDrawBehaviour Behaviour
            => Group.Behaviour;

        public          IModGroup                                                                                Group = null!;
        public          int                                                                                      Index;
        public          bool                                                                                     IsCombo;
        public          StringU8                                                                                 Name;
        public          StringU8                                                                                 Description;
        public          float                                                                                    NameWidth;
        public          float                                                                                    ComboWidth;
        public readonly List<(StringU8 Option, StringU8 Description, Setting Value, float Width, bool Disabled)> Options = [];
        public          bool                                                                                     Disabled;
    }

    public           int                 Count         = 0;
    public           bool                AnyConditions = false;
    public readonly  List<ModGroupCache> SingleGroups  = [];
    public readonly  List<ModGroupCache> MultiGroups   = [];
    private readonly ModSelection        _selection;
    private readonly Configuration       _config;
    private readonly CommunicatorService _communicator;

    public ModSettingsCache(ModSelection selection, Configuration config, CommunicatorService communicator)
    {
        _selection    = selection;
        _config       = config;
        _communicator = communicator;
        _selection.Subscribe(OnSelectionChanged, ModSelection.Priority.ModPanel);
        _communicator.ModOptionChanged.Subscribe(OnModOptionChanged, ModOptionChanged.Priority.ModCacheManager);
        _communicator.ModSettingChanged.Subscribe(OnModSettingChanged, ModSettingChanged.Priority.ModGroupCache);
    }

    private void OnModSettingChanged(in ModSettingChanged.Arguments arguments)
    {
        if (!AnyConditions)
            return;

        if (arguments.Type is ModSettingChange.EnableState or ModSettingChange.Priority)
            return;

        Dirty |= IManagedCache.DirtyFlags.Custom;
    }

    private void OnModOptionChanged(in ModOptionChanged.Arguments arguments)
    {
        if (arguments.Mod == _selection.Mod)
            Dirty |= IManagedCache.DirtyFlags.Custom;
    }

    private void OnSelectionChanged(in ModSelection.Arguments arguments)
        => Dirty |= IManagedCache.DirtyFlags.Custom;

    protected override void Dispose(bool disposing)
    {
        _selection.Unsubscribe(OnSelectionChanged);
        _communicator.ModOptionChanged.Unsubscribe(OnModOptionChanged);
        _communicator.ModSettingChanged.Unsubscribe(OnModSettingChanged);
    }

    public override void Update()
    {
        if (!AnyDirty)
            return;

        AnyConditions = false;
        Dirty         = IManagedCache.DirtyFlags.Clean;
        SingleGroups.Clear();
        MultiGroups.Clear();
        if (_selection.Mod is not null)
        {
            var context = new ModSettingContext(_selection.Mod, _selection.Settings);
            SingleGroups.EnsureCapacity(_selection.Mod.Groups.Count);
            MultiGroups.EnsureCapacity(_selection.Mod.Groups.Count);
            foreach (var (index, group) in _selection.Mod.Groups.Index())
            {
                if (Create(context, group, index) is not { } cache)
                    continue;

                if (cache.Behaviour is GroupDrawBehaviour.SingleSelection)
                    SingleGroups.Add(cache);
                else
                    MultiGroups.Add(cache);
            }
        }

        Count = SingleGroups.Count + MultiGroups.Count;
    }

    public ModGroupCache? Create(in ModSettingContext context, IModGroup group, int groupIndex)
    {
        if (!group.IsOption)
            return null;

        AnyConditions |= group.Condition is not null;
        var condition = group.Condition is null || group.Condition.Evaluate(context);
        if (!condition && !group.Layout.HasFlag(ModSettingsLayout.Disable))
            return null;

        var ret = new ModGroupCache
        {
            Group       = group,
            Index       = groupIndex,
            Name        = new StringU8(group.Name),
            Description = new StringU8(group.Description),
            IsCombo     = group.Behaviour is GroupDrawBehaviour.SingleSelection && group.Options.Count > _config.SingleGroupRadioMax,
            Disabled    = !condition,
        };
        ret.NameWidth = ret.Name.CalculateSize().X;
        if (!ret.Description.IsEmpty && ret.IsCombo)
            ret.NameWidth += Im.Style.ItemInnerSpacing.X + LunaStyle.HelpMarker.CalculateSize().X;

        ret.Options.EnsureCapacity(group.Options.Count);
        foreach (var (index, option) in group.Options.Index())
        {
            AnyConditions |= option.Condition is not null;
            condition     =  option.Condition is null || option.Condition.Evaluate(context);
            if (!condition && !option.Layout.HasFlag(ModSettingsLayout.Disable))
                continue;

            var name        = new StringU8(option.Name);
            var description = new StringU8(option.Description);
            var width       = name.CalculateSize().X;
            if (!description.IsEmpty)
                width += Im.Style.ItemInnerSpacing.X + LunaStyle.HelpMarker.CalculateSize().X;
            ret.Options.Add((name, description, group.Type is GroupType.Single ? Setting.Single(index) : Setting.Multi(index), width,
                !condition));
            if (width > ret.ComboWidth)
                ret.ComboWidth = width;
        }

        if (ret.Options.Count is 0 || ret.Options.Count is 1 && group.Type is GroupType.Single)
            return null;

        return ret;
    }
}

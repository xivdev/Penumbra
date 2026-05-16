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
        public record Option(
            IModOption Data,
            StringU8 Name,
            StringU8 Description,
            Setting Value,
            float Width,
            bool Disabled,
            List<ModGroupCache> Children);

        public GroupDrawBehaviour Behaviour
            => Group.Behaviour;

        public          IModGroup           Group = null!;
        public          StringU8            Name;
        public          StringU8            Description;
        public readonly List<Option>        Options  = [];
        public readonly List<ModGroupCache> Children = [];
        public          bool                Indented;
        public          int                 Index;
        public          float               NameWidth;
        public          float               ComboWidth;
        public          bool                IsCombo;
        public          bool                HideHeader;
        public          bool                Disabled;
    }

    public readonly  List<ModGroupCache>                   Groups    = [];
    private readonly Dictionary<Guid, List<ModGroupCache>> _children = [];
    private readonly ModSelection                          _selection;
    private readonly Configuration                         _config;
    private readonly CommunicatorService                   _communicator;
    public           int                                   Count;
    public           bool                                  AnyConditions;

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
        _children.Clear();
        Groups.Clear();
        if (_selection.Mod is not null)
        {
            var context = new ModSettingContext(_selection.Mod, _selection.Settings);
            Groups.EnsureCapacity(_selection.Mod.Groups.Count);
            foreach (var (index, group) in _selection.Mod.Groups.Index())
            {
                if (Create(context, group, index) is { } cache)
                {
                    Groups.Add(cache);
                    _children.TryAdd(cache.Group.Id, cache.Children);
                    if (cache.IsCombo)
                        foreach (var option in cache.Options)
                            _children.TryAdd(option.Data.Id, cache.Children);
                    else
                        foreach (var option in cache.Options)
                            _children.TryAdd(option.Data.Id, option.Children);
                }
            }
        }

        Count = Groups.Count;
        for (var i = 0; i < Groups.Count; ++i)
        {
            var group = Groups[i];
            if (group.Group.ParentSetting == Guid.Empty)
                continue;

            if (_children.TryGetValue(group.Group.ParentSetting, out var obj))
            {
                obj.Add(group);
                Groups.RemoveAt(i--);
            }
        }
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
            HideHeader  = group.Group.Layout.HasFlag(ModSettingsLayout.ParentHeader),
            Indented    = group.Group.Layout.HasFlag(ModSettingsLayout.Indent),
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
            ret.Options.Add(new ModGroupCache.Option(option, name, description,
                group.Type is GroupType.Single ? Setting.Single(index) : Setting.Multi(index), width,
                !condition, []));
            if (width > ret.ComboWidth)
                ret.ComboWidth = width;
        }

        if (ret.Options.Count is 0 || ret.Options.Count is 1 && group.Type is GroupType.Single)
            return null;

        return ret;
    }
}

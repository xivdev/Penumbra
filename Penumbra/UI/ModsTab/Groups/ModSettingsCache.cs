using ImSharp;
using Luna;
using Penumbra.Api.Enums;
using Penumbra.Communication;
using Penumbra.Mods;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Settings;
using Penumbra.Mods.SubMods;
using Penumbra.Services;
using Penumbra.UI.Classes;

namespace Penumbra.UI.ModsTab.Groups;

public sealed class ModSettingsCache : BasicCache
{
    public sealed class Page(string name)
    {
        public readonly StringU8            Name   = new(name);
        public readonly List<ModGroupCache> Groups = [];
        public          float               WidestLabel;
        public          float               WidestCombo;
    }

    public record Node(StringU8 Name, StringU8 Description)
    {
        public readonly List<ModGroupCache> Children = [];
    }

    public sealed record Option(
        IModOption Data,
        StringU8 Name,
        StringU8 Description,
        Vector4 Color,
        Setting Value,
        float Width,
        bool Disabled,
        bool Separator) : Node(Name, Description);

    public sealed record ModGroupCache(IModGroup Group, StringU8 Name, StringU8 Description) : Node(Name, Description)
    {
        public GroupDrawBehaviour Behaviour
            => Group.Behaviour;

        public readonly List<Option> Options = [];
        public          float        NameWidth;
        public          float        ComboWidth;
        public          bool         Collapsible;
        public          bool         Indented;
        public          bool         IsCombo;
        public          bool         HideHeader;
        public          bool         Disabled;
    }


    public readonly SortedList<int, Page> Pages = [];

    private readonly Dictionary<Guid, List<ModGroupCache>> _children = [];
    private readonly ModSelection                          _selection;
    private readonly Configuration                         _config;
    private readonly CommunicatorService                   _communicator;
    public           Rgba32                                LineColorExpanded;
    public           Rgba32                                LineColorCollapsed;
    public           Rgba32                                TextColorExpanded;
    public           Rgba32                                TextColorCollapsed;
    public           Rgba32                                FrameColor;
    public           float                                 LeftSpacing;
    public           float                                 RightSpacing;
    public           float                                 CenterSpacing;
    public           float                                 Indentation;
    public           int                                   Count;
    public           bool                                  AnyConditions;
    public           int                                   ActivePages;

    public ModSettingsCache(ModSelection selection, Configuration config, CommunicatorService communicator)
    {
        _selection    = selection;
        _config       = config;
        _communicator = communicator;
        _selection.Subscribe(OnSelectionChanged, ModSelection.Priority.ModPanel);
        _communicator.ModOptionChanged.Subscribe(OnModOptionChanged, ModOptionChanged.Priority.ModCacheManager);
        _communicator.ModSettingChanged.Subscribe(OnModSettingChanged, ModSettingChanged.Priority.ModGroupCache);
    }

    private void Reset()
    {
        AnyConditions = false;
        Dirty         = IManagedCache.DirtyFlags.Clean;
        _children.Clear();
        Pages.Clear();
        Count              = 0;
        ActivePages        = 0;
        LeftSpacing        = 30 * Im.Style.GlobalScale;
        RightSpacing       = LeftSpacing;
        CenterSpacing      = 2 * Im.Style.ItemSpacing.X;
        Indentation        = Im.Style.FrameHeight + Im.Style.ItemInnerSpacing.X;
        LineColorExpanded  = ColorId.GroupSeparatorExpanded.Value();
        LineColorCollapsed = ColorId.GroupSeparatorCollapsed.Value();
        TextColorExpanded  = ColorId.GroupLabelTextExpanded.Value();
        TextColorCollapsed = ColorId.GroupLabelTextCollapsed.Value();
        FrameColor         = ColorId.OptionFrameBackGround.Value();
    }

    public override void Update()
    {
        if (!AnyDirty)
            return;

        Reset();

        if (_selection.Mod is not { } mod)
            return;

        var context = new ModSettingContext(_selection.Mod, _selection.Settings);
        foreach (var group in mod.Groups)
        {
            if (Create(context, group) is { } cache)
            {
                ++Count;
                var page = SetupPage(mod, group.Page);
                page.Groups.Add(cache);
                _children.TryAdd(cache.Group.Id, cache.Children);
                if (cache.IsCombo)
                    foreach (var option in cache.Options)
                        _children.TryAdd(option.Data.Id, cache.Children);
                else
                    foreach (var option in cache.Options)
                        _children.TryAdd(option.Data.Id, option.Children);
            }
        }

        NormalizePages();
        NormalizeCollapsible();
        ComputeExtent();
    }

    private Page SetupPage(Mod mod, int pageNumber)
    {
        if (Pages.TryGetValue(pageNumber, out var page))
            return page;

        page = new Page(mod.PageNames.TryGetValue(pageNumber, out var name) ? name : $"Page {pageNumber + 1}");
        Pages.Add(pageNumber, page);
        return page;
    }

    private void NormalizePages()
    {
        ActivePages = Pages.Count;
        foreach (var page in Pages.Values)
        {
            for (var i = 0; i < page.Groups.Count; ++i)
            {
                var group = page.Groups[i];
                if (group.Group.ParentSetting is null)
                    continue;

                if (_children.TryGetValue(group.Group.ParentSetting.Id, out var obj))
                {
                    obj.Add(group);
                    page.Groups.RemoveAt(i--);
                }
            }

            if (page.Groups.Count is 0)
                --ActivePages;
        }
    }

    private void NormalizeCollapsible()
    {
        foreach (var group in Pages.Values.SelectMany(p => p.Groups))
        {
            if (group is { IsCombo: true, Children.Count: 0 })
                group.Collapsible = false;
        }

        foreach (var group in _children.Values.SelectMany(p => p))
        {
            if (group is { IsCombo: true, Children.Count: 0 })
                group.Collapsible = false;
        }
    }

    private void ComputeExtent()
    {
        foreach (var page in Pages.Values)
        {
            page.WidestCombo = 100 * Im.Style.GlobalScale;
            page.WidestLabel = 0;
            foreach (var group in page.Groups)
            {
                if (group.IsCombo && group.ComboWidth > page.WidestCombo)
                    page.WidestCombo = group.ComboWidth;

                CheckExtend(group, 0);
            }

            continue;

            void CheckExtend(ModGroupCache cache, int indentation)
            {
                if (cache.Indented)
                    ++indentation;

                if (!cache.HideHeader)
                {
                    var extend = cache.NameWidth + indentation * Indentation + 2 * Im.Style.FramePadding.X;
                    if (cache.Collapsible)
                        extend += Im.Style.TextHeight + Im.Style.ItemInnerSpacing.X;
                    if (extend > page.WidestLabel)
                        page.WidestLabel = extend;
                }

                foreach (var child in cache.Children.Concat(cache.Options.SelectMany(o => o.Children)))
                    CheckExtend(child, indentation);
            }
        }
    }

    public ModGroupCache? Create(in ModSettingContext context, IModGroup group)
    {
        if (!group.IsOption)
            return null;

        AnyConditions |= group.Condition is not null;
        var condition = group.Condition is null || group.Condition.Evaluate(context);
        if (!condition && !group.Layout.HasFlag(ModSettingsLayout.Disable))
            return null;

        var ret = new ModGroupCache(group, new StringU8(group.Name), new StringU8(group.Description))
        {
            Collapsible = true,
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
            ret.Options.Add(new Option(option, name, description, option.ColorValue,
                group.Type is GroupType.Single ? Setting.Single(index) : Setting.Multi(index), width,
                !condition, option.Layout.HasFlag(ModSettingsLayout.Separator)));

            if (width > ret.ComboWidth)
                ret.ComboWidth = width;
        }

        if (ret.Options.Count is 0 || ret.Options.Count is 1 && group.Type is GroupType.Single)
            return null;

        ret.ComboWidth += Im.Style.FrameHeight + 2 * Im.Style.FramePadding.X;

        return ret;
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
}

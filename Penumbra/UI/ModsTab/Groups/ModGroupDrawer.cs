using ImSharp;
using Luna;
using Penumbra.Api.Enums;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
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

        public          IModGroup                                                                 Group = null!;
        public          int                                                                       Index;
        public          bool                                                                      IsCombo;
        public          StringU8                                                                  Name;
        public          StringU8                                                                  Description;
        public          float                                                                     NameWidth;
        public          float                                                                     ComboWidth;
        public readonly List<(StringU8 Option, StringU8 Description, Setting Value, float Width)> Options = [];
    }

    public           int                 Count        = 0;
    public readonly  List<ModGroupCache> SingleGroups = [];
    public readonly  List<ModGroupCache> MultiGroups  = [];
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
    }

    public override void Update()
    {
        if (!AnyDirty)
            return;

        Dirty = IManagedCache.DirtyFlags.Clean;
        SingleGroups.Clear();
        MultiGroups.Clear();
        if (_selection.Mod is not null)
        {
            SingleGroups.EnsureCapacity(_selection.Mod.Groups.Count);
            MultiGroups.EnsureCapacity(_selection.Mod.Groups.Count);
            foreach (var (index, group) in _selection.Mod.Groups.Index())
            {
                if (Create(group, index) is not { } cache)
                    continue;

                if (cache.Behaviour is GroupDrawBehaviour.SingleSelection)
                    SingleGroups.Add(cache);
                else
                    MultiGroups.Add(cache);
            }
        }

        Count = SingleGroups.Count + MultiGroups.Count;
    }

    public ModGroupCache? Create(IModGroup group, int groupIndex)
    {
        if (!group.IsOption)
            return null;

        var ret = new ModGroupCache
        {
            Group       = group,
            Index       = groupIndex,
            Name        = new StringU8(group.Name),
            Description = new StringU8(group.Description),
            IsCombo     = group.Behaviour is GroupDrawBehaviour.SingleSelection && group.Options.Count > _config.SingleGroupRadioMax,
        };
        ret.NameWidth = ret.Name.CalculateSize().X;
        if (!ret.Description.IsEmpty && ret.IsCombo)
            ret.NameWidth += Im.Style.ItemInnerSpacing.X + LunaStyle.HelpMarker.CalculateSize().X;

        ret.Options.EnsureCapacity(group.Options.Count);
        foreach (var (index, option) in group.Options.Index())
        {
            var name        = new StringU8(option.Name);
            var description = new StringU8(option.Description);
            var width       = name.CalculateSize().X;
            if (!description.IsEmpty)
                width += Im.Style.ItemInnerSpacing.X + LunaStyle.HelpMarker.CalculateSize().X;
            ret.Options.Add((name, description, group.Type is GroupType.Single ? Setting.Single(index) : Setting.Multi(index), width));
            if (width > ret.ComboWidth)
                ret.ComboWidth = width;
        }

        return ret;
    }
}

public sealed class ModGroupDrawer(
    Configuration config,
    CollectionManager collectionManager,
    SingleGroupCombo combo,
    ModSelection selection,
    CommunicatorService communicator)
    : IUiService
{
    private bool                  _temporary;
    private bool                  _locked;
    private TemporaryModSettings? _tempSettings;
    private ModSettings?          _settings;

    public void Draw(Mod mod, ModSettings settings, TemporaryModSettings? tempSettings)
    {
        var cache = CacheManager.Instance.GetOrCreateCache(Im.Id.Current, () => new ModSettingsCache(selection, config, communicator));
        if (cache.Count is 0)
            return;

        _settings     = settings;
        _tempSettings = tempSettings;
        _temporary    = tempSettings is not null;
        _locked       = (tempSettings?.Lock ?? 0) > 0;

        Im.Dummy(UiHelpers.DefaultSpace);
        foreach (var single in cache.SingleGroups)
            DrawSingleGroupCombo(single.Group, single.Index, settings.IsEmpty ? single.Group.DefaultSettings : settings.Settings[single.Index]);

        if (cache.MultiGroups.Count > 0)
            Im.Dummy(UiHelpers.DefaultSpace);
        foreach (var multi in cache.MultiGroups)
        {
            var option = settings.IsEmpty ? multi.Group.DefaultSettings : settings.Settings[multi.Index];
            if (multi.Behaviour is GroupDrawBehaviour.MultiSelection)
                DrawMultiGroup(multi.Group, multi.Index, option);
            else
                DrawSingleGroupRadio(multi.Group, multi.Index, option);
        }
    }

    /// <summary>
    /// Draw a single group selector as a combo box.
    /// If a description is provided, add a help marker besides it.
    /// </summary>
    private void DrawSingleGroupCombo(IModGroup group, int groupIdx, Setting setting)
    {
        using var id       = Im.Id.Push(groupIdx);
        using var disabled = Im.Disabled(_locked);
        combo.Draw(this, (SingleModGroup)group, groupIdx, setting);
        if (group.Description.Length > 0)
        {
            LunaStyle.DrawHelpMarkerLabel(group.Name, group.Description);
        }
        else
        {
            Im.Line.SameInner();
            Im.Text(group.Name);
        }
    }

    /// <summary>
    /// Draw a single group selector as a set of radio buttons.
    /// If a description is provided, add a help marker besides it.
    /// </summary>
    private void DrawSingleGroupRadio(IModGroup group, int groupIdx, Setting setting)
    {
        using var id             = Im.Id.Push(groupIdx);
        var       options        = group.Options;
        var       selectedOption = setting.AsIndex;
        using var g              = ImEx.FramedGroup(group.Name, LunaStyle.HelpMarker, group.Description);
        DrawCollapseHandling(options, g.MinimumWidth, DrawOptions);

        return;

        void DrawOptions()
        {
            using var disabled = Im.Disabled(_locked);
            for (var idx = 0; idx < group.Options.Count; ++idx)
            {
                using var i      = Im.Id.Push(idx);
                var       option = options[idx];
                if (Im.RadioButton(option.Name, selectedOption == idx))
                    SetModSetting(group, groupIdx, Setting.Single(idx));

                if (option.Description.Length is 0)
                    continue;

                Im.Line.SameInner();
                LunaStyle.DrawAlignedHelpMarker(option.Description, treatAsHovered: Im.Item.Hovered());
            }
        }
    }

    /// <summary>
    /// Draw a multi group selector as a bordered set of checkboxes.
    /// If a description is provided, add a help marker in the title.
    /// </summary>
    private void DrawMultiGroup(IModGroup group, int groupIdx, Setting setting)
    {
        using var id      = Im.Id.Push(groupIdx);
        var       options = group.Options;
        using (var g = ImEx.FramedGroup(group.Name, LunaStyle.HelpMarker, group.Description))
        {
            DrawCollapseHandling(options, g.MinimumWidth, DrawOptions);
        }

        var label = new StringU8($"##multi{groupIdx}");
        if (Im.Item.RightClicked())
            Im.Popup.Open(label);

        DrawMultiPopup(group, groupIdx, label);
        return;

        void DrawOptions()
        {
            using var disabled = Im.Disabled(_locked);
            for (var idx = 0; idx < options.Count; ++idx)
            {
                using var i       = Im.Id.Push(idx);
                var       option  = options[idx];
                var       enabled = setting.HasFlag(idx);

                if (Im.Checkbox(option.Name, ref enabled))
                    SetModSetting(group, groupIdx, setting.SetBit(idx, enabled));

                if (option.Description.Length > 0)
                {
                    Im.Line.SameInner();
                    LunaStyle.DrawAlignedHelpMarker(option.Description, treatAsHovered: Im.Item.Hovered());
                }
            }
        }
    }

    private void DrawMultiPopup(IModGroup group, int groupIdx, StringU8 label)
    {
        using var style = ImStyleSingle.PopupBorderThickness.Push(Im.Style.GlobalScale);
        using var popup = Im.Popup.Begin(label);
        if (!popup)
            return;

        Im.Text(group.Name);
        using var disabled = Im.Disabled(_locked);
        Im.Separator();
        if (Im.Selectable("Enable All"u8))
            SetModSetting(group, groupIdx, Setting.AllBits(group.Options.Count));

        if (Im.Selectable("Disable All"u8))
            SetModSetting(group, groupIdx, Setting.Zero);
    }

    private void DrawCollapseHandling(IReadOnlyList<IModOption> options, float minWidth, Action draw)
    {
        if (options.Count <= config.OptionGroupCollapsibleMin)
        {
            draw();
        }
        else
        {
            var collapseId     = Im.Id.Get("Collapse"u8);
            var shown          = Im.State.Storage.GetBool(collapseId, true);
            var buttonTextShow = new StringU8($"Show {options.Count} Options");
            var buttonTextHide = new StringU8($"Hide {options.Count} Options");
            var buttonWidth = Math.Max(Im.Font.CalculateSize(buttonTextShow).X, Im.Font.CalculateSize(buttonTextHide).X)
              + 2 * Im.Style.FramePadding.X;
            minWidth = Math.Max(buttonWidth, minWidth);
            if (shown)
            {
                var pos = Im.Cursor.Position;
                Im.FrameDummy();
                using (Im.Group())
                {
                    draw();
                }

                var width  = Math.Max(Im.Item.Size.X, minWidth);
                var endPos = Im.Cursor.Position;
                Im.Cursor.Position = pos;
                if (Im.Button(buttonTextHide, new Vector2(width, 0)))
                    Im.State.Storage.SetBool(collapseId, !shown);

                Im.Cursor.Position = endPos;
            }
            else
            {
                var optionWidth = options.Max(o => Im.Font.CalculateSize(o.Name).X)
                  + Im.Style.ItemInnerSpacing.X
                  + Im.Style.FrameHeight
                  + Im.Style.FramePadding.X;
                var width = Math.Max(optionWidth, minWidth);
                if (Im.Button(buttonTextShow, new Vector2(width, 0)))
                    Im.State.Storage.SetBool(collapseId, !shown);
            }
        }
    }

    private ModCollection Current
        => collectionManager.Active.Current;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal void SetModSetting(IModGroup group, int groupIdx, Setting setting)
    {
        if (_temporary || config.DefaultTemporaryMode)
        {
            _tempSettings                     ??= new TemporaryModSettings(group.Mod, _settings);
            _tempSettings!.ForceInherit       =   false;
            _tempSettings!.Settings[groupIdx] =   setting;
            collectionManager.Editor.SetTemporarySettings(Current, group.Mod, _tempSettings);
        }
        else
        {
            collectionManager.Editor.SetModSetting(Current, group.Mod, groupIdx, setting);
        }
    }
}

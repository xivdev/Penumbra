using ImSharp;
using Luna;
using OtterGui.Widgets;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.Mods;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Settings;
using Penumbra.Mods.SubMods;
using MouseWheelType = OtterGui.Widgets.MouseWheelType;

namespace Penumbra.UI.ModsTab.Groups;

public sealed class ModGroupDrawer : IUiService
{
    private readonly List<(IModGroup, int)> _blockGroupCache = [];
    private          bool                   _temporary;
    private          bool                   _locked;
    private          TemporaryModSettings?  _tempSettings;
    private          ModSettings?           _settings;
    private readonly SingleGroupCombo       _combo;
    private readonly Configuration          _config;
    private readonly CollectionManager      _collectionManager;

    public ModGroupDrawer(Configuration config, CollectionManager collectionManager)
    {
        _config            = config;
        _collectionManager = collectionManager;
        _combo             = new SingleGroupCombo(this);
    }

    private sealed class SingleGroupCombo(ModGroupDrawer parent)
        : FilterComboCache<IModOption>(() => _group!.Options, MouseWheelType.Control, Penumbra.Log)
    {
        private static IModGroup? _group;
        private static int        _groupIdx;

        protected override bool DrawSelectable(int globalIdx, bool selected)
        {
            var option = _group!.Options[globalIdx];
            var ret    = Im.Selectable(option.Name, globalIdx == CurrentSelectionIdx);

            if (option.Description.Length > 0)
                LunaStyle.DrawHelpMarker(option.Description, treatAsHovered: Im.Item.Hovered());

            return ret;
        }

        protected override string ToString(IModOption obj)
            => obj.Name;

        public void Draw(IModGroup group, int groupIndex, int currentOption)
        {
            _group              = group;
            _groupIdx           = groupIndex;
            CurrentSelectionIdx = currentOption;
            CurrentSelection    = _group.Options[CurrentSelectionIdx];
            if (Draw(string.Empty, CurrentSelection.Name, string.Empty, ref CurrentSelectionIdx, UiHelpers.InputTextWidth.X * 3 / 4,
                    Im.Style.TextHeightWithSpacing))
                parent.SetModSetting(_group, _groupIdx, Setting.Single(CurrentSelectionIdx));
        }
    }

    public void Draw(Mod mod, ModSettings settings, TemporaryModSettings? tempSettings)
    {
        if (mod.Groups.Count <= 0)
            return;

        _blockGroupCache.Clear();
        _settings     = settings;
        _tempSettings = tempSettings;
        _temporary    = tempSettings != null;
        _locked       = (tempSettings?.Lock ?? 0) > 0;
        var useDummy = true;
        foreach (var (idx, group) in mod.Groups.Index())
        {
            if (!group.IsOption)
                continue;

            switch (group.Behaviour)
            {
                case GroupDrawBehaviour.SingleSelection when group.Options.Count <= _config.SingleGroupRadioMax:
                case GroupDrawBehaviour.MultiSelection:
                    _blockGroupCache.Add((group, idx));
                    break;

                case GroupDrawBehaviour.SingleSelection:
                    if (useDummy)
                    {
                        Im.Dummy(UiHelpers.DefaultSpace);
                        useDummy = false;
                    }

                    DrawSingleGroupCombo(group, idx, settings.IsEmpty ? group.DefaultSettings : settings.Settings[idx]);
                    break;
            }
        }

        useDummy = true;
        foreach (var (group, idx) in _blockGroupCache)
        {
            if (useDummy)
            {
                Im.Dummy(UiHelpers.DefaultSpace);
                useDummy = false;
            }

            var option = settings.IsEmpty ? group.DefaultSettings : settings.Settings[idx];
            if (group.Behaviour is GroupDrawBehaviour.MultiSelection)
                DrawMultiGroup(group, idx, option);
            else
                DrawSingleGroupRadio(group, idx, option);
        }
    }

    /// <summary>
    /// Draw a single group selector as a combo box.
    /// If a description is provided, add a help marker besides it.
    /// </summary>
    private void DrawSingleGroupCombo(IModGroup group, int groupIdx, Setting setting)
    {
        using var id             = Im.Id.Push(groupIdx);
        var       selectedOption = setting.AsIndex;
        using var disabled       = Im.Disabled(_locked);
        _combo.Draw(group, groupIdx, selectedOption);
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
        var       selectedOption = setting.AsIndex;
        var       minWidth       = Widget.BeginFramedGroup(group.Name, group.Description);
        var       options        = group.Options;
        DrawCollapseHandling(options, minWidth, DrawOptions);

        Widget.EndFramedGroup();
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
        using var id       = Im.Id.Push(groupIdx);
        var       minWidth = Widget.BeginFramedGroup(group.Name, group.Description);
        var       options  = group.Options;
        DrawCollapseHandling(options, minWidth, DrawOptions);

        Widget.EndFramedGroup();
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
        if (options.Count <= _config.OptionGroupCollapsibleMin)
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
        => _collectionManager.Active.Current;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void SetModSetting(IModGroup group, int groupIdx, Setting setting)
    {
        if (_temporary || _config.DefaultTemporaryMode)
        {
            _tempSettings                     ??= new TemporaryModSettings(group.Mod, _settings);
            _tempSettings!.ForceInherit       =   false;
            _tempSettings!.Settings[groupIdx] =   setting;
            _collectionManager.Editor.SetTemporarySettings(Current, group.Mod, _tempSettings);
        }
        else
        {
            _collectionManager.Editor.SetModSetting(Current, group.Mod, groupIdx, setting);
        }
    }
}

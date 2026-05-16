using ImSharp;
using Luna;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.Mods;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Settings;
using Penumbra.Services;

namespace Penumbra.UI.ModsTab.Groups;

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
    private ModSettingContext     _context;

    public void Draw(Mod mod, ModSettings settings, TemporaryModSettings? tempSettings)
    {
        var cache = CacheManager.Instance.GetOrCreateCache(Im.Id.Current, () => new ModSettingsCache(selection, config, communicator));
        if (cache.Count is 0)
            return;

        _context      = new ModSettingContext(mod, tempSettings ?? settings);
        _tempSettings = tempSettings;
        _temporary    = tempSettings is not null;
        _locked       = (tempSettings?.Lock ?? 0) > 0;

        Im.Dummy(UiHelpers.DefaultSpace);
        foreach (var group in cache.Groups)
            DrawGroup(group);
    }

    private void DrawGroup(ModSettingsCache.ModGroupCache group)
    {
        using var indent  = IndentGroup(group.Indented);
        var       setting = _context.Settings.IsEmpty ? group.Group.DefaultSettings : _context.Settings.Settings[group.Index];
        if (group.IsCombo)
            DrawSingleGroupCombo(group, setting);
        else if (group.Behaviour is GroupDrawBehaviour.MultiSelection)
            DrawMultiGroup(group, setting);
        else
            DrawSingleGroupRadio(group, setting);

        foreach (var child in group.Children)
            DrawGroup(child);
    }

    /// <summary>
    /// Draw a single group selector as a combo box.
    /// If a description is provided, add a help marker besides it.
    /// </summary>
    private void DrawSingleGroupCombo(ModSettingsCache.ModGroupCache group, Setting setting)
    {
        using var id       = Im.Id.Push(group.Index);
        using var disabled = Im.Disabled(_locked);
        combo.Draw(this, (SingleModGroup)group.Group, group.Index, setting);
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
    private void DrawSingleGroupRadio(ModSettingsCache.ModGroupCache group, Setting setting)
    {
        using var id             = Im.Id.Push(group.Index);
        var       options        = group.Options;
        var       selectedOption = setting.AsIndex;
        if (group.HideHeader)
        {
            DrawOptions();
        }
        else
        {
            using var g = ImEx.FramedGroup(group.Name, LunaStyle.HelpMarker, group.Description);
            DrawCollapseHandling(options, g.MinimumWidth, DrawOptions);
        }

        return;

        void DrawOptions()
        {
            using var disabled = Im.Disabled(_locked);
            for (var idx = 0; idx < group.Options.Count; ++idx)
            {
                using var i      = Im.Id.Push(idx);
                var       option = options[idx];
                if (Im.RadioButton(option.Name, selectedOption == idx))
                    SetModSetting(group.Group, group.Index, Setting.Single(idx));

                if (option.Description.Length is 0)
                    continue;

                Im.Line.SameInner();
                LunaStyle.DrawAlignedHelpMarker(option.Description, treatAsHovered: Im.Item.Hovered());

                foreach (var childGroup in option.Children)
                    DrawGroup(childGroup);
            }
        }
    }

    /// <summary>
    /// Draw a multi group selector as a bordered set of checkboxes.
    /// If a description is provided, add a help marker in the title.
    /// </summary>
    private void DrawMultiGroup(ModSettingsCache.ModGroupCache group, Setting setting)
    {
        using var id      = Im.Id.Push(group.Index);
        var       options = group.Options;
        if (group.HideHeader)
        {
            DrawOptions();
        }
        else
        {
            using var g = ImEx.FramedGroup(group.Name, LunaStyle.HelpMarker, group.Description);
            DrawCollapseHandling(options, g.MinimumWidth, DrawOptions);
        }

        var label = new InlineStringU8<ulong>($"##m{group.Index:D4}");
        if (Im.Item.RightClicked())
            Im.Popup.Open(label);

        DrawMultiPopup(group, group.Index, label.GetBytes());
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
                    SetModSetting(group.Group, group.Index, setting.SetBit(idx, enabled));

                if (option.Description.Length > 0)
                {
                    Im.Line.SameInner();
                    LunaStyle.DrawAlignedHelpMarker(option.Description, treatAsHovered: Im.Item.Hovered());
                }

                foreach (var childGroup in option.Children)
                    DrawGroup(childGroup);
            }
        }
    }

    private void DrawMultiPopup(ModSettingsCache.ModGroupCache group, int groupIdx, ReadOnlySpan<byte> label)
    {
        using var style = ImStyleSingle.PopupBorderThickness.Push(Im.Style.GlobalScale);
        using var popup = Im.Popup.Begin(label);
        if (!popup)
            return;

        Im.Text(group.Name);
        using var disabled = Im.Disabled(_locked);
        Im.Separator();
        if (Im.Selectable("Enable All"u8))
            SetModSetting(group.Group, groupIdx, Setting.AllBits(group.Options.Count));

        if (Im.Selectable("Disable All"u8))
            SetModSetting(group.Group, groupIdx, Setting.Zero);
    }

    private void DrawCollapseHandling(IReadOnlyList<ModSettingsCache.ModGroupCache.Option> options, float minWidth, Action draw)
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
            _tempSettings                     ??= new TemporaryModSettings(group.Mod, _context.Settings);
            _tempSettings!.ForceInherit       =   false;
            _tempSettings!.Settings[groupIdx] =   setting;
            collectionManager.Editor.SetTemporarySettings(Current, group.Mod, _tempSettings);
        }
        else
        {
            collectionManager.Editor.SetModSetting(Current, group.Mod, groupIdx, setting);
        }
    }

    private static Im.IndentDisposable? IndentGroup(bool indent)
        => indent ? Im.Indent(Im.Style.FrameHeight + Im.Style.ItemInnerSpacing.X) : null;
}

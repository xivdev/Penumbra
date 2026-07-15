using ImSharp;
using Luna;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.Communication;
using Penumbra.Mods;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Settings;
using Penumbra.Mods.SubMods;
using Penumbra.Services;

namespace Penumbra.UI.ModsTab.Groups;

public sealed class ModGroupDrawer(
    Configuration config,
    CollectionManager collectionManager,
    SingleGroupCombo combo,
    CommunicatorService communicator)
    : IUiService
{
    private bool                  _temporary;
    private bool                  _locked;
    private TemporaryModSettings? _tempSettings;
    private ModSettingContext     _context;

    public void Draw(ModSettingsCache cache, Mod mod, ModSettings settings, TemporaryModSettings? tempSettings)
    {
        if (cache.Count is 0 || cache.ActivePages is 0)
            return;

        _context      = new ModSettingContext(mod, tempSettings ?? settings);
        _tempSettings = tempSettings;
        _temporary    = tempSettings is not null;
        _locked       = (tempSettings?.Lock ?? 0) > 0;

        if (cache.ActivePages > 1)
        {
            Im.Dummy(UiHelpers.DefaultSpace);
            using var tabBar = Im.TabBar.Begin("##pages"u8, TabBarFlags.FittingPolicyScroll);
            if (!tabBar)
                return;

            foreach (var (id, page) in cache.Pages)
            {
                using var _       = Im.Id.Push(id);
                using var tabItem = tabBar.Item(page.Name, TabItemFlags.NoPushId);
                if (!tabItem)
                    continue;

                using var child = Im.Child.Begin("##child"u8, false, WindowFlags.NoSavedSettings);
                if (!child)
                    continue;

                Im.Dummy(UiHelpers.DefaultSpace);
                foreach (var group in page.Groups)
                    DrawGroup(group);

                UiHelpers.DefaultLineSpace();
                communicator.PostSettingsPanelDraw.Invoke(new PostSettingsPanelDraw.Arguments(mod));
            }
        }
        else
        {
            var (id, page) = cache.Pages.First();
            using var _ = Im.Id.Push(id);
            Im.Dummy(UiHelpers.DefaultSpace);
            foreach (var group in page.Groups)
                DrawGroup(group);

            UiHelpers.DefaultLineSpace();
            communicator.PostSettingsPanelDraw.Invoke(new PostSettingsPanelDraw.Arguments(mod));
        }
    }

    private void DrawGroup(ModSettingsCache.ModGroupCache group)
    {
        using var indent  = IndentGroup(group.Indented);
        var       setting = _context.Settings.IsEmpty ? group.Group.DefaultSettings : _context.Settings.Settings[group.Group.Index];
        bool      drawChildren;
        if (group.IsCombo)
            drawChildren = DrawSingleGroupComboNew(group, setting);
        else if (group.Behaviour is GroupDrawBehaviour.MultiSelection)
            drawChildren = DrawMultiGroupNew(group, setting);
        else
            drawChildren = DrawSingleGroupRadioNew(group, setting);

        if (drawChildren)
            foreach (var child in group.Children)
                DrawGroup(child);
    }

    private bool DrawSingleGroupComboNew(ModSettingsCache.ModGroupCache group, Setting setting)
    {
        using var id = Im.Id.Push(group.Group.Index);
        var line = new HeaderLine
        {
            Collapsible   = group.Children.Count > 0,
            LeftDistance  = 30 * Im.Style.GlobalScale,
            RightDistance = 30 * Im.Style.GlobalScale,
            ComboDistance = Im.Style.ItemSpacing.X * 2,
            ComboDisabled = group.Disabled,
        };
        var (drawChildren, popupId, popupBox) = line.Combo(group.Name, group.Description, group.Group.Options[setting.AsIndex].Name);
        using var popup = Im.Combo.DrawPopup(popupId, popupBox);
        if (!popup)
            return drawChildren;

        foreach (var option in group.Options)
        {
            id.Push(option.Data.Index);
            if (Im.Selectable(option.Name, option.Data.Index == setting.AsIndex))
                SetModSetting(group.Group, group.Group.Index, Setting.Single(option.Data.Index));
            id.Pop();
        }

        return drawChildren;
    }

    private bool DrawSingleGroupRadioNew(ModSettingsCache.ModGroupCache group, Setting setting)
    {
        using var id = Im.Id.Push(group.Group.Index);
        var line = new HeaderLine
        {
            Collapsible   = true,
            DefaultClosed = group.Group.Layout.HasFlag(ModSettingsLayout.DefaultClosed),
            LeftDistance  = 30 * Im.Style.GlobalScale,
        };
        var options      = group.Options;
        var drawChildren = group.HideHeader || line.Basic(group.Name, group.Description);
        if (drawChildren)
            DrawOptions();

        return drawChildren;

        void DrawOptions()
        {
            using var disabled = Im.Disabled(_locked || group.Disabled);
            using var color    = Im.Color.Empty();
            for (var idx = 0; idx < options.Count; ++idx)
            {
                using var i      = Im.Id.Push(idx);
                var       option = options[idx];

                disabled.Push(option.Disabled);
                color.Push(ImGuiColor.Text, option.Color);
                if (Im.RadioButton(option.Name, idx == setting.AsIndex))
                    SetModSetting(group.Group, group.Group.Index, Setting.Single(idx));
                color.Pop();
                disabled.Pop();

                if (option.Description.Length > 0)
                {
                    Im.Line.SameInner();
                    LunaStyle.DrawAlignedHelpMarker(option.Description, treatAsHovered: Im.Item.Hovered(HoveredFlags.AllowWhenDisabled));
                }

                if (option.Separator)
                    Im.Separator();

                using var _ = Im.Enabled();
                foreach (var childGroup in option.Children)
                    DrawGroup(childGroup);
            }
        }
    }

    private bool DrawMultiGroupNew(ModSettingsCache.ModGroupCache group, Setting setting)
    {
        using var id = Im.Id.Push(group.Group.Index);
        var line = new HeaderLine
        {
            Collapsible   = true,
            DefaultClosed = group.Group.Layout.HasFlag(ModSettingsLayout.DefaultClosed),
            LeftDistance  = 30 * Im.Style.GlobalScale,
        };
        var options      = group.Options;
        var drawChildren = group.HideHeader || line.Basic(group.Name, group.Description);
        if (drawChildren)
            DrawOptions();

        return drawChildren;

        void DrawOptions()
        {
            using var disabled = Im.Disabled(_locked || group.Disabled);
            using var color    = Im.Color.Empty();
            for (var idx = 0; idx < options.Count; ++idx)
            {
                using var i       = Im.Id.Push(idx);
                var       option  = options[idx];
                var       enabled = setting.HasFlag(idx);

                disabled.Push(option.Disabled);
                color.Push(ImGuiColor.Text, option.Color);
                if (Im.Checkbox(option.Name, ref enabled))
                    SetModSetting(group.Group, group.Group.Index, setting.SetBit(idx, enabled));
                color.Pop();
                disabled.Pop();

                if (option.Description.Length > 0)
                {
                    Im.Line.SameInner();
                    LunaStyle.DrawAlignedHelpMarker(option.Description, treatAsHovered: Im.Item.Hovered(HoveredFlags.AllowWhenDisabled));
                }

                if (option.Separator)
                    Im.Separator();

                using var _ = Im.Enabled();
                foreach (var childGroup in option.Children)
                    DrawGroup(childGroup);
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

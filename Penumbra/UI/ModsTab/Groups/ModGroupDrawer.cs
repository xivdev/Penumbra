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
    private float                 _currentIndent;
    private float                 _labelExtend;
    private float                 _comboWidth;
    private bool                  _temporary;
    private bool                  _locked;
    private TemporaryModSettings? _tempSettings;
    private ModSettingContext     _context;

    public void Draw(ModSettingsCache cache, Mod mod, ModSettings settings, TemporaryModSettings? tempSettings)
    {
        if (cache.Count is 0 || cache.ActivePages is 0)
            return;

        _context       = new ModSettingContext(mod, tempSettings ?? settings);
        _tempSettings  = tempSettings;
        _temporary     = tempSettings is not null;
        _locked        = (tempSettings?.Lock ?? 0) > 0;
        _currentIndent = 0;

        if (cache.ActivePages > 1 && config.DisplayPages)
        {
            Im.Dummy(UiHelpers.DefaultSpace);
            using var tabBar = Im.TabBar.Begin("##pages"u8, TabBarFlags.FittingPolicyScroll);
            if (!tabBar)
                return;

            foreach (var (id, page) in cache.Pages)
            {
                if (page.Groups.Count is 0)
                    continue;

                using var _       = Im.Id.Push(id);
                using var tabItem = tabBar.Item(page.Name, TabItemFlags.NoPushId);
                if (!tabItem)
                    continue;

                using var child = Im.Child.Begin("##child"u8, false, WindowFlags.NoSavedSettings);
                if (!child)
                    continue;

                _labelExtend = page.WidestLabel;
                _comboWidth  = page.WidestCombo;
                Im.Dummy(UiHelpers.DefaultSpace);
                foreach (var group in page.Groups)
                    DrawGroup(cache, group, false);

                UiHelpers.DefaultLineSpace();
                communicator.PostSettingsPanelDraw.Invoke(new PostSettingsPanelDraw.Arguments(mod));
            }
        }
        else
        {
            Im.Dummy(UiHelpers.DefaultSpace);
            foreach (var (id, page) in cache.Pages)
            {
                if (page.Groups.Count is 0)
                    continue;

                using var _ = Im.Id.Push(id);
                if (cache.Pages.Count > 1 && !Im.Tree.Header(page.Name, TreeNodeFlags.DefaultOpen))
                    continue;

                _labelExtend = page.WidestLabel;
                _comboWidth  = page.WidestCombo;
                foreach (var group in page.Groups)
                    DrawGroup(cache, group, false);
            }

            UiHelpers.DefaultLineSpace();
            communicator.PostSettingsPanelDraw.Invoke(new PostSettingsPanelDraw.Arguments(mod));
        }
    }

    private void DrawGroup(ModSettingsCache cache, ModSettingsCache.ModGroupCache group, bool hasParent)
    {
        using var indent  = IndentGroup(cache, group.Indented);
        var       setting = _context.Settings.IsEmpty ? group.Group.DefaultSettings : _context.Settings.Settings[group.Group.Index];

        if (DoDrawGroup(cache, group, setting, hasParent))
            foreach (var child in group.Children)
                DrawGroup(cache, child, true);

        if (indent is not null)
            _currentIndent -= indent.CurrentIndent;
    }

    private bool DoDrawGroup(ModSettingsCache cache, ModSettingsCache.ModGroupCache group, Setting setting, bool hasParent)
    {
        using var id = Im.Id.Push(group.Group.Index);
        if (group.IsCombo)
            return DrawSingleGroupComboNew(cache, group, setting, hasParent);

        if (group.IsSameLineOption)
            return DrawToggleGroup(cache, group, setting, hasParent);

        if (group.Behaviour is GroupDrawBehaviour.MultiSelection)
            return DrawMultiGroupNew(cache, group, setting, hasParent);

        return DrawSingleGroupRadioNew(cache, group, setting, hasParent);
    }

    private HeaderLine HeaderLineBase(ModSettingsCache cache, ModSettingsCache.ModGroupCache group)
        => new()
        {
            LeftDistance              = cache.LeftSpacing,
            RightDistance             = -1f,
            ComboDistance             = cache.CenterSpacing,
            FixedComboWidth           = _comboWidth,
            FixedButtonWidth          = _labelExtend - _currentIndent,
            LineColorExpanded         = cache.LineColorExpanded,
            LineColorCollapsed        = cache.LineColorCollapsed,
            TextColorExpanded         = cache.TextColorExpanded,
            TextColorCollapsed        = cache.TextColorCollapsed,
            ButtonBackgroundExpanded  = cache.FrameColorExpanded,
            ButtonBackgroundCollapsed = cache.FrameColorCollapsed,
            DefaultClosed             = group.Group.Layout.HasFlag(ModSettingsLayout.DefaultClosed),
            ComboDisabled             = group.Disabled,
            TooltipIcon               = LunaStyle.HelpMarker,
            HideRightLine             = config.HideRightOptionGroupLine,
        };

    private bool DrawSingleGroupComboNew(ModSettingsCache cache, ModSettingsCache.ModGroupCache group, Setting setting, bool hasParent)
    {
        var line = HeaderLineBase(cache, group);
        line.Collapsible = group.Children.Count > 0;
        line.NoLabel     = group.HideHeader && hasParent;
        return line.Combo(w => combo.Draw(this, group, setting, w), group.ComboWidth, group.Name, group.Description);
    }

    private bool DrawToggleGroup(ModSettingsCache cache, ModSettingsCache.ModGroupCache group, Setting setting, bool hasParent)
    {
        var line = HeaderLineBase(cache, group);
        line.Collapsible = group.Children.Count > 0;
        line.NoLabel     = group.HideHeader && hasParent;
        if (group.IsCheckbox)
            line.FixedComboWidth = group.ComboWidth;
        return line.Combo(DrawCheckbox, group.ComboWidth, group.Name, group.Description);

        void DrawCheckbox(float width)
        {
            using var i       = Im.Id.Push(0);
            var       option  = group.Options[0];
            var       enabled = setting.HasFlag(option.Data.Index);
            if (!group.IsCheckbox)
            {
                ImEx.TextFramed(StringU8.Empty, new Vector2(width, 0), Rgba32.Transparent);
                Im.Line.NoSpacing();
                Im.Cursor.X -= width;
            }

            using (Im.Disabled(option.Disabled))
            {
                using var c = ImGuiColor.Text.Push(option.Color);
                if (Im.Checkbox(option.HideLabel ? "##check"u8 : option.Name, ref enabled))
                    SetModSetting(group.Group, group.Group.Index, setting.SetBit(option.Data.Index, enabled));
            }

            if (option.Description.Length <= 0)
                return;

            if (option.HideLabel)
            {
                Im.Tooltip.OnHover(option.Description);
            }
            else
            {
                Im.Line.SameInner();
                LunaStyle.DrawAlignedHelpMarker(option.Description, treatAsHovered: Im.Item.Hovered(HoveredFlags.AllowWhenDisabled));
            }
        }
    }

    private bool DrawSingleGroupRadioNew(ModSettingsCache cache, ModSettingsCache.ModGroupCache group, Setting setting, bool hasParent)
    {
        var line = HeaderLineBase(cache, group);
        line.Collapsible = true;
        var options      = group.Options;
        var drawChildren = hasParent && group.HideHeader || line.Basic(group.Name, group.Description);
        if (drawChildren)
            DrawOptions();

        return drawChildren;

        void DrawOptions()
        {
            using var indent   = Im.Indent(cache.Indentation);
            using var disabled = Im.Disabled(_locked || group.Disabled);
            using var color    = Im.Color.Empty();
            for (var idx = 0; idx < options.Count; ++idx)
            {
                using var i      = Im.Id.Push(idx);
                var       option = options[idx];

                disabled.Push(option.Disabled);
                color.Push(ImGuiColor.Text, option.Color);
                Im.Cursor.X += cache.LeftSpacing;
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
                    DrawGroup(cache, childGroup, true);
            }
        }
    }

    private bool DrawMultiGroupNew(ModSettingsCache cache, ModSettingsCache.ModGroupCache group, Setting setting, bool hasParent)
    {
        var line = HeaderLineBase(cache, group);
        line.Collapsible = true;
        var options      = group.Options;
        var drawChildren = hasParent && group.HideHeader || line.Basic(group.Name, group.Description);
        if (drawChildren)
            DrawOptions();

        return drawChildren;

        void DrawOptions()
        {
            using var indent   = Im.Indent(cache.Indentation);
            using var disabled = Im.Disabled(_locked || group.Disabled);
            using var color    = Im.Color.Empty();
            for (var idx = 0; idx < options.Count; ++idx)
            {
                using var i       = Im.Id.Push(idx);
                var       option  = options[idx];
                var       enabled = setting.HasFlag(option.Data.Index);

                disabled.Push(option.Disabled);
                color.Push(ImGuiColor.Text, option.Color);
                Im.Cursor.X += cache.LeftSpacing;
                if (Im.Checkbox(option.Name, ref enabled))
                    SetModSetting(group.Group, group.Group.Index, setting.SetBit(option.Data.Index, enabled));
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
                    DrawGroup(cache, childGroup, true);
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

    private Im.IndentDisposable? IndentGroup(ModSettingsCache cache, bool indent)
    {
        if (!indent)
            return null;

        _currentIndent += cache.Indentation;
        return Im.Indent(cache.Indentation);
    }
}

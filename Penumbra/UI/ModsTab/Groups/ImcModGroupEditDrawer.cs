using Dalamud.Interface;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Text;
using OtterGui.Text.Widget;
using OtterGuiInternal.Utility;
using Penumbra.GameData.Structs;
using Penumbra.Meta;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Manager.OptionEditor;
using Penumbra.Mods.SubMods;
using Penumbra.UI.AdvancedWindow.Meta;

namespace Penumbra.UI.ModsTab.Groups;

public readonly struct ImcModGroupEditDrawer(ModGroupEditDrawer editor, ImcModGroup group) : IModGroupEditDrawer
{
    public void Draw()
    {
        var identifier   = group.Identifier;
        var defaultEntry = ImcChecker.GetDefaultEntry(identifier, true).Entry;
        var entry        = group.DefaultEntry;
        var changes      = false;

        var width = editor.AvailableWidth.X - 3 * ImUtf8.ItemInnerSpacing.X - ImUtf8.ItemSpacing.X - ImUtf8.CalcTextSize("All Variants"u8).X - ImUtf8.CalcTextSize("Only Attributes"u8).X - 2 * ImUtf8.FrameHeight;
        ImUtf8.TextFramed(identifier.ToString(), 0, new Vector2(width, 0), borderColor: ImGui.GetColorU32(ImGuiCol.Border));

        ImUtf8.SameLineInner();
        var allVariants = group.AllVariants;
        if (ImUtf8.Checkbox("All Variants"u8, ref allVariants))
            editor.ModManager.OptionEditor.ImcEditor.ChangeAllVariants(group, allVariants);
        ImUtf8.HoverTooltip("Make this group overwrite all corresponding variants for this identifier, not just the one specified."u8);

        ImGui.SameLine();
        var onlyAttributes = group.OnlyAttributes;
        if (ImUtf8.Checkbox("Only Attributes"u8, ref onlyAttributes))
            editor.ModManager.OptionEditor.ImcEditor.ChangeOnlyAttributes(group, onlyAttributes);
        ImUtf8.HoverTooltip("Only overwrite the attribute flags and take all the other values from the game's default entry instead of the one configured here.\n\nMainly useful if used with All Variants to keep the material IDs for each variant."u8);

        using (ImUtf8.Group())
        {
            ImUtf8.TextFrameAligned("Material ID"u8);
            ImUtf8.TextFrameAligned("VFX ID"u8);
            ImUtf8.TextFrameAligned("Decal ID"u8);
        }

        ImGui.SameLine();
        using (ImUtf8.Group())
        {
            changes |= ImcMetaDrawer.DrawMaterialId(defaultEntry, ref entry, true);
            changes |= ImcMetaDrawer.DrawVfxId(defaultEntry, ref entry, true);
            changes |= ImcMetaDrawer.DrawDecalId(defaultEntry, ref entry, true);
        }

        ImGui.SameLine(0, editor.PriorityWidth);
        using (ImUtf8.Group())
        {
            ImUtf8.TextFrameAligned("Material Animation ID"u8);
            ImUtf8.TextFrameAligned("Sound ID"u8);
            ImUtf8.TextFrameAligned("Can Be Disabled"u8);
        }

        ImGui.SameLine();

        using (ImUtf8.Group())
        {
            changes |= ImcMetaDrawer.DrawMaterialAnimationId(defaultEntry, ref entry, true);
            changes |= ImcMetaDrawer.DrawSoundId(defaultEntry, ref entry, true);
            var canBeDisabled = group.CanBeDisabled;
            if (ImUtf8.Checkbox("##disabled"u8, ref canBeDisabled))
                editor.ModManager.OptionEditor.ImcEditor.ChangeCanBeDisabled(group, canBeDisabled);
        }

        if (changes)
            editor.ModManager.OptionEditor.ImcEditor.ChangeDefaultEntry(group, entry);

        ImGui.Dummy(Vector2.Zero);
        DrawOptions();
        var attributeCache = new ImcAttributeCache(group);
        DrawNewOption(attributeCache);
        ImGui.Dummy(Vector2.Zero);


        using (ImUtf8.Group())
        {
            ImUtf8.TextFrameAligned("Default Attributes"u8);
            foreach (var option in group.OptionData.Where(o => !o.IsDisableSubMod))
                ImUtf8.TextFrameAligned(option.Name);
        }

        ImUtf8.SameLineInner();
        using (ImUtf8.Group())
        {
            DrawAttributes(editor.ModManager.OptionEditor.ImcEditor, attributeCache, group.DefaultEntry.AttributeMask, group);
            foreach (var (option, idx) in group.OptionData.WithIndex().Where(o => !o.Value.IsDisableSubMod))
            {
                using var id = ImUtf8.PushId(idx);
                DrawAttributes(editor.ModManager.OptionEditor.ImcEditor, attributeCache, option.AttributeMask, option,
                    group.DefaultEntry.AttributeMask);
            }
        }
    }

    private void DrawOptions()
    {
        foreach (var (option, optionIdx) in group.OptionData.WithIndex())
        {
            using var id = ImRaii.PushId(optionIdx);
            editor.DrawOptionPosition(group, option, optionIdx);

            ImUtf8.SameLineInner();
            editor.DrawOptionDefaultMultiBehaviour(group, option, optionIdx);

            ImUtf8.SameLineInner();
            editor.DrawOptionName(option);

            ImUtf8.SameLineInner();
            editor.DrawOptionDescription(option);

            if (!option.IsDisableSubMod)
            {
                ImUtf8.SameLineInner();
                editor.DrawOptionDelete(option);
            }
        }
    }

    private void DrawNewOption(in ImcAttributeCache cache)
    {
        var dis       = cache.LowestUnsetMask == 0;
        var name      = editor.DrawNewOptionBase(group, group.Options.Count);
        var validName = name.Length > 0;
        var tt = dis
            ? "No Free Attribute Slots for New Options..."u8
            : validName
                ? "Add a new option to this group."u8
                : "Please enter a name for the new option."u8;
        if (ImUtf8.IconButton(FontAwesomeIcon.Plus, tt, default, !validName || dis))
        {
            editor.ModManager.OptionEditor.ImcEditor.AddOption(group, cache, name);
            editor.NewOptionName = null;
        }
    }

    private static void DrawAttributes(ImcModGroupEditor editor, in ImcAttributeCache cache, ushort mask, object data,
        ushort? defaultMask = null)
    {
        for (var i = 0; i < ImcEntry.NumAttributes; ++i)
        {
            using var id        = ImRaii.PushId(i);
            var       flag      = 1 << i;
            var       value     = (mask & flag) != 0;
            var       inDefault = defaultMask.HasValue && (defaultMask & flag) != 0;
            using (ImRaii.Disabled(defaultMask != null && !cache.CanChange(i)))
            {
                if (inDefault ? NegativeCheckbox.Instance.Draw(""u8, ref value) : ImUtf8.Checkbox(""u8, ref value))
                {
                    if (data is ImcModGroup g)
                        editor.ChangeDefaultAttribute(g, cache, i, value);
                    else
                        editor.ChangeOptionAttribute((ImcSubMod)data, cache, i, value);
                }
            }

            ImUtf8.HoverTooltip(ImGuiHoveredFlags.AllowWhenDisabled, "ABCDEFGHIJ"u8.Slice(i, 1));
            if (i != 9)
                ImUtf8.SameLineInner();
        }
    }

    private sealed class NegativeCheckbox : MultiStateCheckbox<bool>
    {
        public static readonly NegativeCheckbox Instance = new();

        protected override void RenderSymbol(bool value, Vector2 position, float size)
        {
            if (value)
                SymbolHelpers.RenderCross(ImGui.GetWindowDrawList(), position, ImGui.GetColorU32(ImGuiCol.CheckMark), size);
        }

        protected override bool NextValue(bool value)
            => !value;

        protected override bool PreviousValue(bool value)
            => !value;
    }
}

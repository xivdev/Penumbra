using Dalamud.Interface;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Text;
using Penumbra.GameData.Structs;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Manager.OptionEditor;
using Penumbra.Mods.SubMods;

namespace Penumbra.UI.ModsTab.Groups;

public readonly struct ImcModGroupEditDrawer(ModGroupEditDrawer editor, ImcModGroup group) : IModGroupEditDrawer
{
    public void Draw()
    {
        var identifier   = group.Identifier;
        var defaultEntry = editor.ImcChecker.GetDefaultEntry(identifier, true).Entry;
        var entry        = group.DefaultEntry;
        var changes      = false;

        ImUtf8.TextFramed(identifier.ToString(), 0, editor.AvailableWidth, borderColor: ImGui.GetColorU32(ImGuiCol.Border));

        using (ImUtf8.Group())
        {
            ImUtf8.TextFrameAligned("Material ID"u8);
            ImUtf8.TextFrameAligned("VFX ID"u8);
            ImUtf8.TextFrameAligned("Decal ID"u8);
        }

        ImGui.SameLine();
        using (ImUtf8.Group())
        {
            changes |= ImcManipulationDrawer.DrawMaterialId(defaultEntry, ref entry, true);
            changes |= ImcManipulationDrawer.DrawVfxId(defaultEntry, ref entry, true);
            changes |= ImcManipulationDrawer.DrawDecalId(defaultEntry, ref entry, true);
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
            changes |= ImcManipulationDrawer.DrawMaterialAnimationId(defaultEntry, ref entry, true);
            changes |= ImcManipulationDrawer.DrawSoundId(defaultEntry, ref entry, true);
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
                DrawAttributes(editor.ModManager.OptionEditor.ImcEditor, attributeCache, option.AttributeMask, option);
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

    private static void DrawAttributes(ImcModGroupEditor editor, in ImcAttributeCache cache, ushort mask, object data)
    {
        for (var i = 0; i < ImcEntry.NumAttributes; ++i)
        {
            using var id    = ImRaii.PushId(i);
            var       value = (mask & (1 << i)) != 0;
            using (ImRaii.Disabled(!cache.CanChange(i)))
            {
                if (ImUtf8.Checkbox(TerminatedByteString.Empty, ref value))
                {
                    if (data is ImcModGroup g)
                        editor.ChangeDefaultAttribute(g, cache, i, value);
                    else
                        editor.ChangeOptionAttribute((ImcSubMod)data, cache, i, value);
                }
            }

            ImUtf8.HoverTooltip("ABCDEFGHIJ"u8.Slice(i, 1));
            if (i != 9)
                ImUtf8.SameLineInner();
        }
    }
}

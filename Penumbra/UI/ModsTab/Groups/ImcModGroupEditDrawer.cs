using Dalamud.Interface;
using ImGuiNET;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Raii;
using OtterGui.Text;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Manager.OptionEditor;
using Penumbra.Mods.SubMods;

namespace Penumbra.UI.ModsTab.Groups;

public readonly struct ImcModGroupEditDrawer(ModGroupEditDrawer editor, ImcModGroup group) : IModGroupEditDrawer
{
    public void Draw()
    {
        using (ImUtf8.Group())
        {
            ImUtf8.Text("Object Type"u8);
            if (group.ObjectType is ObjectType.Equipment or ObjectType.Accessory or ObjectType.DemiHuman)
                ImUtf8.Text("Slot"u8);
            ImUtf8.Text("Primary ID");
            if (group.ObjectType is not ObjectType.Equipment and not ObjectType.Accessory)
                ImUtf8.Text("Secondary ID");
            ImUtf8.Text("Variant"u8);

            ImUtf8.TextFrameAligned("Material ID"u8);
            ImUtf8.TextFrameAligned("Material Animation ID"u8);
            ImUtf8.TextFrameAligned("Decal ID"u8);
            ImUtf8.TextFrameAligned("VFX ID"u8);
            ImUtf8.TextFrameAligned("Sound ID"u8);
            ImUtf8.TextFrameAligned("Can Be Disabled"u8);
            ImUtf8.TextFrameAligned("Default Attributes"u8);
        }

        ImGui.SameLine();

        var attributeCache = new ImcAttributeCache(group);

        using (ImUtf8.Group())
        {
            ImUtf8.Text(group.ObjectType.ToName());
            if (group.ObjectType is ObjectType.Equipment or ObjectType.Accessory or ObjectType.DemiHuman)
                ImUtf8.Text(group.EquipSlot.ToName());
            ImUtf8.Text($"{group.PrimaryId.Id}");
            if (group.ObjectType is not ObjectType.Equipment and not ObjectType.Accessory)
                ImUtf8.Text($"{group.SecondaryId.Id}");
            ImUtf8.Text($"{group.Variant.Id}");

            ImUtf8.TextFrameAligned($"{group.DefaultEntry.MaterialId}");
            ImUtf8.TextFrameAligned($"{group.DefaultEntry.MaterialAnimationId}");
            ImUtf8.TextFrameAligned($"{group.DefaultEntry.DecalId}");
            ImUtf8.TextFrameAligned($"{group.DefaultEntry.VfxId}");
            ImUtf8.TextFrameAligned($"{group.DefaultEntry.SoundId}");

            var canBeDisabled = group.CanBeDisabled;
            if (ImUtf8.Checkbox("##disabled"u8, ref canBeDisabled))
                editor.ModManager.OptionEditor.ImcEditor.ChangeCanBeDisabled(group, canBeDisabled, SaveType.Queue);

            var defaultDisabled = group.DefaultDisabled;
            ImUtf8.SameLineInner();
            if (ImUtf8.Checkbox("##defaultDisabled"u8, ref defaultDisabled))
                editor.ModManager.OptionEditor.ChangeModGroupDefaultOption(group,
                    group.DefaultSettings.SetBit(ImcModGroup.DisabledIndex, defaultDisabled));

            DrawAttributes(editor.ModManager.OptionEditor.ImcEditor, attributeCache, group.DefaultEntry.AttributeMask, group);
        }


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

            ImUtf8.SameLineInner();
            editor.DrawOptionDelete(option);

            ImUtf8.SameLineInner();
            ImGui.Dummy(new Vector2(editor.PriorityWidth, 0));

            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + editor.OptionIdxSelectable.X + ImUtf8.ItemInnerSpacing.X * 2 + ImUtf8.FrameHeight);
            DrawAttributes(editor.ModManager.OptionEditor.ImcEditor, attributeCache, option.AttributeMask, option);
        }

        DrawNewOption(attributeCache);
        return;

        static void DrawAttributes(ImcModGroupEditor editor, in ImcAttributeCache cache, ushort mask, object data)
        {
            for (var i = 0; i < ImcEntry.NumAttributes; ++i)
            {
                using var id = ImRaii.PushId(i);
                var value = (mask & 1 << i) != 0;
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

                ImUtf8.HoverTooltip($"{(char)('A' + i)}");
                if (i != 9)
                    ImUtf8.SameLineInner();
            }
        }
    }

    private void DrawNewOption(in ImcAttributeCache cache)
    {
        if (cache.LowestUnsetMask == 0)
            return;

        var name = editor.DrawNewOptionBase(group, group.Options.Count);
        var validName = name.Length > 0;
        if (ImUtf8.IconButton(FontAwesomeIcon.Plus, validName
                ? "Add a new option to this group."u8
                : "Please enter a name for the new option."u8, !validName))
        {
            editor.ModManager.OptionEditor.ImcEditor.AddOption(group, cache, name);
            editor.NewOptionName = null;
        }
    }
}

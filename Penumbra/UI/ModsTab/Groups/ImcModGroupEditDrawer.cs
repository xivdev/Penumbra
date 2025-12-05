using ImSharp;
using Luna;
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

        var width = editor.AvailableWidth.X
          - 3 * Im.Style.ItemInnerSpacing.X
          - Im.Style.ItemSpacing.X
          - Im.Font.CalculateSize("All Variants"u8).X
          - Im.Font.CalculateSize("Only Attributes"u8).X
          - 2 * Im.Style.FrameHeight;
        ImEx.TextFramed(identifier.ToString(), new Vector2(width, 0), Rgba32.Transparent);

        Im.Line.SameInner();
        var allVariants = group.AllVariants;
        if (Im.Checkbox("All Variants"u8, ref allVariants))
            editor.ModManager.OptionEditor.ImcEditor.ChangeAllVariants(group, allVariants);
        Im.Tooltip.OnHover("Make this group overwrite all corresponding variants for this identifier, not just the one specified."u8);

        Im.Line.Same();
        var onlyAttributes = group.OnlyAttributes;
        if (Im.Checkbox("Only Attributes"u8, ref onlyAttributes))
            editor.ModManager.OptionEditor.ImcEditor.ChangeOnlyAttributes(group, onlyAttributes);
        Im.Tooltip.OnHover(
            "Only overwrite the attribute flags and take all the other values from the game's default entry instead of the one configured here.\n\nMainly useful if used with All Variants to keep the material IDs for each variant."u8);

        using (Im.Group())
        {
            ImEx.TextFrameAligned("Material ID"u8);
            ImEx.TextFrameAligned("VFX ID"u8);
            ImEx.TextFrameAligned("Decal ID"u8);
        }

        Im.Line.Same();
        using (Im.Group())
        {
            changes |= ImcMetaDrawer.DrawMaterialId(defaultEntry, ref entry, true);
            changes |= ImcMetaDrawer.DrawVfxId(defaultEntry, ref entry, true);
            changes |= ImcMetaDrawer.DrawDecalId(defaultEntry, ref entry, true);
        }

        Im.Line.Same(0, editor.PriorityWidth);
        using (Im.Group())
        {
            ImEx.TextFrameAligned("Material Animation ID"u8);
            ImEx.TextFrameAligned("Sound ID"u8);
            ImEx.TextFrameAligned("Can Be Disabled"u8);
        }

        Im.Line.Same();

        using (Im.Group())
        {
            changes |= ImcMetaDrawer.DrawMaterialAnimationId(defaultEntry, ref entry, true);
            changes |= ImcMetaDrawer.DrawSoundId(defaultEntry, ref entry, true);
            var canBeDisabled = group.CanBeDisabled;
            if (Im.Checkbox("##disabled"u8, ref canBeDisabled))
                editor.ModManager.OptionEditor.ImcEditor.ChangeCanBeDisabled(group, canBeDisabled);
        }

        if (changes)
            editor.ModManager.OptionEditor.ImcEditor.ChangeDefaultEntry(group, entry);

        Im.Dummy(Vector2.Zero);
        DrawOptions();
        var attributeCache = new ImcAttributeCache(group);
        DrawNewOption(attributeCache);
        Im.Dummy(Vector2.Zero);


        using (Im.Group())
        {
            ImEx.TextFrameAligned("Default Attributes"u8);
            foreach (var option in group.OptionData.Where(o => !o.IsDisableSubMod))
                ImEx.TextFrameAligned(option.Name);
        }

        Im.Line.SameInner();
        using (Im.Group())
        {
            DrawAttributes(editor.ModManager.OptionEditor.ImcEditor, attributeCache, group.DefaultEntry.AttributeMask, group);
            foreach (var (idx, option) in group.OptionData.Index().Where(o => !o.Item.IsDisableSubMod))
            {
                using var id = Im.Id.Push(idx);
                DrawAttributes(editor.ModManager.OptionEditor.ImcEditor, attributeCache, option.AttributeMask, option,
                    group.DefaultEntry.AttributeMask);
            }
        }
    }

    private void DrawOptions()
    {
        foreach (var (optionIdx, option) in group.OptionData.Index())
        {
            using var id = Im.Id.Push(optionIdx);
            editor.DrawOptionPosition(group, option, optionIdx);

            Im.Line.SameInner();
            editor.DrawOptionDefaultMultiBehaviour(group, option, optionIdx);

            Im.Line.SameInner();
            editor.DrawOptionName(option);

            Im.Line.SameInner();
            editor.DrawOptionDescription(option);

            if (!option.IsDisableSubMod)
            {
                Im.Line.SameInner();
                editor.DrawOptionDelete(option);
            }
        }
    }

    private void DrawNewOption(in ImcAttributeCache cache)
    {
        var dis       = cache.LowestUnsetMask is 0;
        var name      = editor.DrawNewOptionBase(group, group.Options.Count);
        var validName = name.Length > 0;
        var tt = dis
            ? "No Free Attribute Slots for New Options..."u8
            : validName
                ? "Add a new option to this group."u8
                : "Please enter a name for the new option."u8;
        if (ImEx.Icon.Button(LunaStyle.AddObjectIcon, tt, !validName || dis))
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
            using var id        = Im.Id.Push(i);
            var       flag      = 1 << i;
            var       value     = (mask & flag) is not 0;
            var       inDefault = defaultMask.HasValue && (defaultMask & flag) is not 0;
            using (Im.Disabled(defaultMask is not null && !cache.CanChange(i)))
            {
                if (inDefault ? NegativeCheckbox.Instance.Draw(""u8, ref value) : Im.Checkbox(""u8, ref value))
                {
                    if (data is ImcModGroup g)
                        editor.ChangeDefaultAttribute(g, cache, i, value);
                    else
                        editor.ChangeOptionAttribute((ImcSubMod)data, cache, i, value);
                }
            }

            Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, "ABCDEFGHIJ"u8.Slice(i, 1));
            if (i != 9)
                Im.Line.SameInner();
        }
    }

    private sealed class NegativeCheckbox : OtterGui.Text.Widget.MultiStateCheckbox<bool>
    {
        public static readonly NegativeCheckbox Instance = new();

        protected override void RenderSymbol(bool value, Vector2 position, float size)
        {
            if (value)
                Im.Render.Cross(Im.Window.DrawList, position, ImGuiColor.CheckMark.Get(), size);
        }

        protected override bool NextValue(bool value)
            => !value;

        protected override bool PreviousValue(bool value)
            => !value;
    }
}

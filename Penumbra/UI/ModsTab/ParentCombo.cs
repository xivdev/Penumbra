using ImSharp;
using Luna;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Manager;
using Penumbra.Mods.SubMods;

namespace Penumbra.UI.ModsTab;

internal sealed class ParentCombo(ModManager mods) : ModObjectCombo
{
    public void Draw(ReadOnlySpan<byte> label, IModGroup group, float width)
    {
        Group = group;
        Mod   = group.Mod;
        var current = group.ParentSetting;
        if (!base.Draw(label, current?.Name ?? "Select Parent...",
                "Selecting a group here places this group below it, possibly indented. Selecting an option places this group below the option if the option is not inside a combo."u8,
                width, out var parent))
            return;

        // Additional security, should not be necessary through available items.
        if (!CycleChecker.Check(group, parent.Object))
            return;

        mods.OptionEditor.SetParent(group, parent.Object);
    }


    protected override IEnumerable<ModObjectCache> GetItems()
    {
        if (Mod is null)
            yield break;

        yield return ModObjectCache.None;

        foreach (var group in Mod.Groups)
        {
            if (group == Group)
                continue;

            var cycle  = !CycleChecker.Check(Group!, (IModObject?)group);
            var parent = new ModObjectCache(group, null) { CausesCycle = cycle };
            yield return parent;

            if (cycle)
                continue;

            foreach (var option in group.Options)
                yield return new ModObjectCache(option, parent);
        }
    }


    protected override bool DrawItem(in ModObjectCache item, int globalIndex, bool selected)
    {
        bool ret;
        using (Im.Disabled(item.CausesCycle))
        {
            if (item.Group is not { } group)
            {
                ret = Im.Selectable(item.Name.Utf8, selected);
            }
            else if (group.Visible)
            {
                using var indent = Im.Indent();
                ret = Im.Selectable(item.Name.Utf8, selected);
            }
            else
            {
                ret = Im.Selectable(item.Name.Utf8, selected);
                if (!item.GroupName.IsEmpty)
                {
                    Im.Line.NoSpacing();
                    using (ImGuiColor.Text.Push(Im.Style[ImGuiColor.TextDisabled]))
                    {
                        ImEx.TextRightAligned(item.GroupName.Utf8, Im.Style.FramePadding.X);
                    }
                }
            }
        }

        if (item.CausesCycle)
            Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, "Setting this parent would cause a cycle in the ancestors."u8, true);

        return ret;
    }

    protected override bool IsSelected(ModObjectCache item, int globalIndex)
        => Group!.ParentSetting == item.Object;
}

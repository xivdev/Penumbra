using ImSharp;
using Penumbra.Mods.Groups;
using Penumbra.Mods.SubMods;

namespace Penumbra.UI.ModsTab;

internal sealed class ConditionCombo : ModObjectCombo
{
    private          IModOption?             _optionSelected;
    private          SettingCondition?       _currentCondition;
    private readonly Im.ColorStyleDisposable _style = new();

    public ConditionCombo()
        => PreviewAlignment = new Vector2(0.5f);

    protected override void PostDrawCombo(float width)
        => _style.PopColor();

    public bool Draw(ReadOnlySpan<byte> label, IModObject @object, float width, SettingCondition? existingCondition,
        out SettingCondition? newCondition)
    {
        Mod               = @object.Mod;
        Group             = @object.Group;
        _optionSelected   = @object as IModOption;
        _currentCondition = existingCondition;

        var color = Rgba32.Blue.WithAlpha(51);
        _style.PushDefault(ImStyleDouble.FramePadding)
            .PushDefault(ImGuiColor.ChildBackground)
            .PushDefault(ImGuiColor.Border)
            .Push(ImGuiColor.FrameBackground, color);

        if (!base.Draw(label, _currentCondition?.Option.FullName ?? "Select New Condition...",
                "Selecting an option here makes the display and application of this option or entire group dependent on the option being enabled."u8,
                width, out var parent)
         || parent.Object is not IModOption option
         || option == existingCondition?.Option)
        {
            newCondition = existingCondition;
            _style.Dispose();
            return false;
        }

        if (_currentCondition is null)
        {
            newCondition = new SettingCondition(option);
        }
        else
        {
            newCondition         = _currentCondition;
            newCondition!.Option = option;
        }

        _style.Dispose();
        return true;
    }

    protected override IEnumerable<ModObjectCache> GetItems()
    {
        if (Mod is null)
            yield break;

        foreach (var group in Mod.Groups)
        {
            var parent = new ModObjectCache(group, null)
            {
                CausesCycle = false,
                Visible     = false,
            };
            if (group == Group)
            {
                // Can not make a group dependent on itself, or an option in a single select group dependent on any options in its group.
                if (_optionSelected is null || group.Behaviour is GroupDrawBehaviour.SingleSelection)
                    continue;

                // TODO: Check cycles and sensibility.
                foreach (var option in group.Options)
                {
                    // Can not depend on itself.
                    if (option == _optionSelected)
                        continue;

                    yield return new ModObjectCache(option, parent);
                }
            }
            else
            {
                // TODO: Check cycles and sensibility.
                foreach (var option in group.Options)
                    yield return new ModObjectCache(option, parent);
            }
        }
    }

    protected override bool DrawItem(in ModObjectCache item, int globalIndex, bool selected)
    {
        var ret = Im.Selectable(item.Name.IsEmpty ? "<No Name>"u8 : item.Name.Utf8, selected);
        Im.Line.NoSpacing();
        using (ImGuiColor.Text.Push(Im.Style[ImGuiColor.TextDisabled]))
        {
            ImEx.TextRightAligned(item.GroupName.Utf8, Im.Style.FramePadding.X);
        }

        return ret;
    }

    protected override bool IsSelected(ModObjectCache item, int globalIndex)
        => _currentCondition?.Option == item.Object;
}

using ImSharp;
using Luna;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Settings;
using Penumbra.UI.ModsTab.Groups;

namespace Penumbra.UI;

public sealed class SingleGroupCombo : FilterComboBase<SingleGroupCombo.Test>, IUiService
{
    private class OptionFilter : Utf8FilterBase<Test>
    {
        protected override ReadOnlySpan<byte> ToFilterString(in Test item, int globalIndex)
            => item.Name;
    }

    public SingleGroupCombo()
        => Filter  = new OptionFilter();

    public readonly record struct Test(int OptionIndex, StringU8 Name, StringU8 Description);

    private readonly WeakReference<SingleModGroup> _group = new(null!);
    private          Setting                       _currentOption;

    public void Draw(ModGroupDrawer parent, SingleModGroup group, int groupIndex, Setting currentOption)
    {
        _currentOption = currentOption;
        _group.SetTarget(group);
        if (base.Draw(group.Name, group.OptionData[currentOption.AsIndex].Name, StringU8.Empty, UiHelpers.InputTextWidth.X * 3 / 4,
                out var newOption))
            parent.SetModSetting(group, groupIndex, Setting.Single(newOption.OptionIndex));
    }

    protected override IEnumerable<Test> GetItems()
        => _group.TryGetTarget(out var target)
            ? target.OptionData.Select(o => new Test(o.GetIndex(), new StringU8(o.Name), new StringU8(o.Description)))
            : [];

    protected override float ItemHeight
        => Im.Style.TextHeightWithSpacing;

    protected override bool DrawItem(in Test item, int globalIndex, bool selected)
    {
        var ret = Im.Selectable(item.Name, selected);
        if (item.Description.Length > 0)
            LunaStyle.DrawHelpMarker(item.Description, treatAsHovered: Im.Item.Hovered());
        return ret;
    }

    protected override bool IsSelected(Test item, int globalIndex)
        => item.OptionIndex == _currentOption.AsIndex;
}

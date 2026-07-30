using ImSharp;
using Luna;
using Penumbra.Mods.Settings;
using Penumbra.UI.ModsTab.Settings;

namespace Penumbra.UI;

public sealed class SingleGroupCombo : FilterComboBase<ModSettingOption>, IUiService
{
    private class OptionFilter : Utf8FilterBase<ModSettingOption>
    {
        protected override ReadOnlySpan<byte> ToFilterString(in ModSettingOption item, int globalIndex)
            => item.Name;
    }

    public SingleGroupCombo()
    {
        Filter                   = new OptionFilter();
        ComputeWidth             = true;
        DirtyCacheOnClosingPopup = true;
    }

    protected override FilterComboBaseCache<ModSettingOption> CreateCache()
        => new Cache(this);

    private readonly WeakReference<ModSettingGroup> _group = new(null!);
    private          Setting                                       _currentOption;
    private          Vector4                                       _currentColor;

    public void Draw(ModGroupDrawer parent, ModSettingGroup group, Setting currentOption, float width)
    {
        if (!group.IsCombo)
            return;

        using var id = Im.Id.Push(group.Group.Index);
        _currentOption = currentOption;
        var currentValue = group.AllOptions[currentOption.AsIndex];
        _currentColor = currentValue.Color;
        _group.SetTarget(group);
        if (base.Draw(StringU8.Empty, currentValue.Name, StringU8.Empty, width, out var newOption))
            parent.SetModSetting(group.Group, Setting.Single(newOption.Data.Index));
    }

    protected override void PreDrawCombo(float width)
        => ImGuiColor.Text.Push(_currentColor);

    protected override void PostDrawCombo(float width)
        => Im.ColorDisposable.PopUnsafe();

    protected override IEnumerable<ModSettingOption> GetItems()
        => _group.TryGetTarget(out var target) ? target.VisibleChildren.Take(target.NumOptions).Cast<ModSettingOption>() : [];

    protected override float ItemHeight
        => Im.Style.TextHeightWithSpacing;

    protected override bool DrawItem(in ModSettingOption item, int globalIndex, bool selected)
    {
        bool ret;
        using (Im.Disabled(item.Disabled))
        {
            using (ImGuiColor.Text.Push(item.Color))
            {
                ret = Im.Selectable(item.Name, selected);
            }
        }

        if (item.Separator)
        {
            var right = Im.Item.LowerRightCorner;
            Im.Window.DrawList.Shape.Line(right with { X = Im.Item.UpperLeftCorner.X }, right, ImGuiColor.Separator.Get());
        }

        if (item.Description.Length > 0)
        {
            Im.Line.SameInner();
            LunaStyle.DrawHelpMarker(item.Description, treatAsHovered: Im.Item.Hovered(HoveredFlags.AllowWhenDisabled));
        }

        return ret;
    }

    protected override bool IsSelected(ModSettingOption item, int globalIndex)
        => item.Data.Index == _currentOption.AsIndex;

    private sealed class Cache(SingleGroupCombo parent) : FilterComboBaseCache<ModSettingOption>(parent)
    {
        protected override void ComputeWidth()
        {
            ComboWidth = AllItems.Max(i => i.Width + (i.Description.Length > 0 ? Im.Style.FrameHeightWithSpacing : 0))
              + 2 * Im.Style.FramePadding.X
              + Im.Style.ScrollbarSize;
        }
    }
}

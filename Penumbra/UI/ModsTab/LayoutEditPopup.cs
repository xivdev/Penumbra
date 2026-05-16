using ImSharp;
using Luna;
using Penumbra.Mods.Groups;
using Penumbra.Mods.SubMods;

namespace Penumbra.UI.ModsTab;

public sealed class LayoutEditPopup : ObjectEditPopup, IUiService
{
    public void Open(IModGroup group)
        => Open((object)group);

    public void Open(IModOption option)
        => Open((object)option);

    protected override ReadOnlySpan<byte> PopupId
        => "LayoutEdit"u8;

    protected override void DrawInternal()
    {
        switch (Current)
        {
            case IModGroup group:
            {
                //var parentGroup = group.ParentSetting ?? string.Empty;
                //if (Im.Input.Text("##parentGroup"u8, ref parentGroup, "Parent Group..."u8))
                //    group.ParentSetting = parentGroup.Length is 0
                //        ? ParentSetting.None
                //        : new ParentSetting(parentGroup, group.ParentSetting.Option);
                //using (Im.Disabled(parentGroup.Length is 0))
                //{
                //    Im.Line.SameInner();
                //    var parentOption = group.ParentSetting.Option ?? string.Empty;
                //    if (Im.Input.Text("##parentOption"u8, ref parentOption, "Parent Option..."u8))
                //        group.ParentSetting = new ParentSetting(parentGroup, parentOption.Length is 0 ? null : parentOption);
                //}

                var layout = group.Layout;
                if (Im.Checkbox("Disable When Condition Not Met"u8, ref layout, ModSettingsLayout.Disable))
                    group.Layout = layout;
                Im.Tooltip.OnHover(
                    "When this is checked, this group is not hidden when its conditions are not met, and instead still displays, but is disabled."u8);
                if (Im.Checkbox("Indent When Placed Under Parent"u8, ref layout, ModSettingsLayout.Indent))
                    group.Layout = layout;
                Im.Tooltip.OnHover("When this is checked, this group is indented if it is placed under a parent group or option."u8);
                if (Im.Checkbox("Hide Group Name When Placed Under Parent"u8, ref layout, ModSettingsLayout.ParentHeader))
                    group.Layout = layout;
                Im.Tooltip.OnHover(
                    "When this is checked, this group only shows its options and not the group header if it is placed under a parent group or option."u8);
            }
                break;
            case IModOption option:
            {
                var layout = option.Layout;
                if (Im.Checkbox("Disable When Condition Not Met"u8, ref layout, ModSettingsLayout.Disable))
                    option.Layout = layout;
                Im.Tooltip.OnHover(
                    "When this is checked, this option is not hidden when its conditions are not met, and instead still displays, but is disabled."u8);
            }
                break;
        }
    }
}

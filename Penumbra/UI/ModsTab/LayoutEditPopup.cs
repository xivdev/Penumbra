using ImSharp;
using Luna;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Manager;
using Penumbra.Mods.SubMods;
using Penumbra.UI.Classes;

namespace Penumbra.UI.ModsTab;

public sealed class LayoutEditPopup(ModManager mods) : ObjectEditPopup, IUiService
{
    private readonly ParentCombo _parentCombo = new(mods);

    public void Open(IModObject @object)
        => Open((object)@object);

    protected override ReadOnlySpan<byte> PopupId
        => "LayoutEdit"u8;

    private void DrawGroup(IModGroup group)
    {
        DrawIdentifier(group);
        _parentCombo.Draw("Parent Setting"u8, group, ImEx.GuidInputWidth + Im.Style.ItemInnerSpacing.X + Im.Style.FrameHeight);
        var layout = group.Layout;
        if (Im.Checkbox("Disable When Condition Not Met"u8, ref layout, ModSettingsLayout.Disable))
            mods.OptionEditor.SetLayout(group, layout);
        Im.Tooltip.OnHover(
            "When this is checked, this group is not hidden when its conditions are not met, and instead still displays, but is disabled."u8);
        if (Im.Checkbox("Collapsed By Default"u8, ref layout, ModSettingsLayout.DefaultClosed))
            mods.OptionEditor.SetLayout(group, layout);
        Im.Tooltip.OnHover(
            "When this is checked and this group is displayed with a collapsible header, the header is closed by default instead of open by default."u8);
        if (Im.Checkbox("Indent When Placed Under Parent"u8, ref layout, ModSettingsLayout.Indent))
            mods.OptionEditor.SetLayout(group, layout);
        Im.Tooltip.OnHover("When this is checked, this group is indented if it is placed under a parent group or option."u8);
        if (Im.Checkbox("Hide Group Name When Placed Under Parent"u8, ref layout, ModSettingsLayout.ParentHeader))
            mods.OptionEditor.SetLayout(group, layout);
        Im.Tooltip.OnHover(
            "When this is checked, this group only shows its options and not the group header if it is placed under a parent group or option."u8);
    }

    private void DrawIdentifier(IModObject @object)
    {
        Guid? guid = @object.Id;
        if (ImEx.GuidInput("##guid"u8, ref guid) && guid.HasValue)
            mods.OptionEditor.ForceIdentifier(@object, guid.Value);
        Im.Line.SameInner();
        if (ImEx.Icon.Button(LunaStyle.RefreshIcon, "Set a new GUID for this object."u8))
            mods.OptionEditor.ForceIdentifier(@object, Guid.NewGuid());
        Im.Line.SameInner();
        ImEx.TextFrameAligned("Identifier"u8);
    }

    private void DrawOption(IModOption option)
    {
        DrawIdentifier(option);
        DrawColorCombo(option);
        var layout = option.Layout;
        if (Im.Checkbox("Disable When Condition Not Met"u8, ref layout, ModSettingsLayout.Disable))
            mods.OptionEditor.SetLayout(option, layout);
        Im.Tooltip.OnHover(
            "When this is checked, this option is not hidden when its conditions are not met, and instead still displays, but is disabled."u8);

        if (Im.Checkbox("Add Separator"u8, ref layout, ModSettingsLayout.Separator))
            mods.OptionEditor.SetLayout(option, layout);
        Im.Tooltip.OnHover(
            "When this is checked, a separator line is placed below this option both when displayed as a checkbox or radio toggle as well as in a combo."u8);

        if (Im.Checkbox("Hide Option Label (Single Line)"u8, ref layout, ModSettingsLayout.HideOptionLabel))
            mods.OptionEditor.SetLayout(option, layout);
        Im.Tooltip.OnHover(
            "When this is checked, and this option is a single checkbox option on the same line as its group label, only display the checkbox, not the option's name or description as a label."u8);
    }

    protected override void DrawInternal()
    {
        switch (Current)
        {
            case IModGroup group:   DrawGroup(group); break;
            case IModOption option: DrawOption(option); break;
        }
    }

    private static readonly IReadOnlyList<StringU8> ColorNames =
    [
        new("Default"u8),
        new("Option Color 1"u8),
        new("Option Color 2"u8),
        new("Option Color 3"u8),
        new("Option Color 4"u8),
        new("Option Color 5"u8),
        new("Option Color 6"u8),
        new("Option Color 7"u8),
        new("Option Color 8"u8),
    ];

    private void DrawColorCombo(IModOption option)
    {
        var       name  = ColorNames[option.ColorAsInteger];
        var       color = option.Color is 0 ? ColorParameter.Default : option.Color.Value();
        ImGuiId   popupId;
        Rectangle bb;
        using (ImGuiColor.Text.Push(color))
        {
            Im.Item.SetNextWidth(ImEx.GuidInputWidth + Im.Style.ItemInnerSpacing.X + Im.Style.FrameHeight);
            Im.Combo.DrawPreview("##Color"u8, name, out popupId, out bb, ComboFlags.HeightLargest);
        }

        Im.Tooltip.OnHover(
            "Note that these colors are user-configurable, the preview here only displays your configured colors, which may differ from those of a user."u8);

        ImEx.TextLabel("Color"u8);
        DrawColorPopup(option, popupId, bb);
    }

    private void DrawColorPopup(IModOption option, ImGuiId id, in Rectangle boundingBox)
    {
        using var popup = Im.Combo.DrawPopup(id, boundingBox, ComboFlags.HeightLargest);
        if (!popup)
            return;

        for (var i = 0; i < ColorNames.Count; ++i)
        {
            var       tmpName  = ColorNames[i];
            var       tmpValue = IModOption.ConvertColor(i);
            var       tmpColor = i is 0 ? ColorParameter.Default : tmpValue.Value();
            using var c        = ImGuiColor.Text.Push(tmpColor);
            if (Im.Selectable(tmpName, tmpValue == option.Color))
                mods.OptionEditor.SetColor(option, i);
        }
    }
}

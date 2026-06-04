using ImSharp;
using Luna;
using Penumbra.Mods;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Manager;
using Penumbra.Mods.SubMods;
using Penumbra.UI.Classes;

namespace Penumbra.UI.ModsTab;

public static class OptionGroupHeader
{
    public static bool DrawMainHeader(Utf8StringHandler<LabelStringHandlerBuffer> label, ColorParameter lineColor)
    {
        if (!ImEx.SplitLabel(ref label, out var visible, out var id))
            return false;

        var available       = Im.ContentRegion.Available;
        var frameHeight     = Im.Style.FrameHeight;
        var frameHeightEven = float.IsEvenInteger(frameHeight);
        var separatorColor  = lineColor.CheckDefault(ImGuiColor.Border);
        var (lineThickness, linePosition)   = frameHeightEven ? (2, (frameHeight - 1) / 2) : (1, frameHeight / 2);
        var shapes   = Im.Window.DrawList.Shape;
        var startPos = Im.Cursor.Position with { Y = linePosition };
        var endPos   = startPos with { X = startPos.X + available.X };
        var textWidth = Im.Font.CalculateSize(visible, false).X + 2 * Im.Style.FramePadding.X;

        Im.Cursor.X += 30 * Im.Style.GlobalScale;
        shapes.Line(startPos, endPos, separatorColor, lineThickness);
        ImEx.TextFramed(visible, default, separatorColor, ColorParameter.Default, separatorColor);
        return true;
    }
}

public sealed class LayoutEditPopup(ModManager mods) : ObjectEditPopup, IUiService
{
    private readonly ParentCombo _parentCombo = new(mods);

    public void Open(IModObject @object)
        => Open((object)@object);

    protected override ReadOnlySpan<byte> PopupId
        => "LayoutEdit"u8;

    private void DrawGroup(IModGroup group)
    {
        _parentCombo.Draw("Parent Setting"u8, group, 200 * Im.Style.GlobalScale);
        var layout = group.Layout;
        if (Im.Checkbox("Disable When Condition Not Met"u8, ref layout, ModSettingsLayout.Disable))
            mods.OptionEditor.SetLayout(group, layout);
        Im.Tooltip.OnHover(
            "When this is checked, this group is not hidden when its conditions are not met, and instead still displays, but is disabled."u8);
        if (Im.Checkbox("Indent When Placed Under Parent"u8, ref layout, ModSettingsLayout.Indent))
            mods.OptionEditor.SetLayout(group, layout);
        Im.Tooltip.OnHover("When this is checked, this group is indented if it is placed under a parent group or option."u8);
        if (Im.Checkbox("Hide Group Name When Placed Under Parent"u8, ref layout, ModSettingsLayout.ParentHeader))
            mods.OptionEditor.SetLayout(group, layout);
        Im.Tooltip.OnHover(
            "When this is checked, this group only shows its options and not the group header if it is placed under a parent group or option."u8);
    }

    private void DrawOption(IModOption option)
    {
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
            Im.Item.SetNextWidthScaled(150);
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

    private sealed class ParentCombo : FilterComboBase<ParentCombo.Parent>
    {
        private readonly ModManager _mods;
        private          Mod?       _mod;
        private          IModGroup? _group;

        public ParentCombo(ModManager mods)
        {
            _mods                    = mods;
            DirtyCacheOnClosingPopup = true;
            Filter                   = new ItemFilter();
        }

        public void Draw(ReadOnlySpan<byte> label, IModGroup group, float width)
        {
            _group = group;
            _mod   = group.Mod;
            var current = group.ParentSetting;
            if (!Draw(label, current?.Name ?? "Select Parent...",
                    "Selecting a group here places this group below it, possibly indented. Selecting an option places this group below the option if the option is not inside a combo."u8,
                    width, out var parent))
                return;

            // Additional security, should not be necessary through available items.
            if (!CycleChecker.Check(group, parent.Object))
                return;

            _mods.OptionEditor.SetParent(group, parent.Object);
        }

        public sealed class Parent
        {
            public readonly IModObject? Object;
            public readonly Parent?     Group;
            public readonly StringPair  Name;
            public readonly StringU8    FullName;
            public          bool        CausesCycle;
            public          bool        Visible;

            public Guid Id
                => Object?.Id ?? Guid.Empty;

            private Parent()
            {
                Name     = new StringPair("None"u8);
                Visible  = true;
                FullName = new StringU8("None"u8);
            }

            public Parent(IModObject o, Parent? parent)
            {
                Object   = o;
                Group    = parent;
                Name     = new StringPair(o.Name);
                FullName = parent is null ? StringU8.Empty : new StringU8($"{parent.Name.Utf8}: {o.Name}");
            }

            public static readonly Parent None = new();
        }

        protected override float ItemHeight
            => Im.Style.TextHeightWithSpacing;

        protected override IEnumerable<Parent> GetItems()
        {
            if (_mod is null)
                yield break;

            yield return Parent.None;

            foreach (var group in _mod.Groups)
            {
                if (group == _group)
                    continue;

                var cycle  = !CycleChecker.Check(_group!, (IModObject?)group);
                var parent = new Parent(group, null) { CausesCycle = cycle };
                yield return parent;

                if (cycle)
                    continue;

                foreach (var option in group.Options)
                    yield return new Parent(option, parent);
            }
        }


        protected override bool DrawItem(in Parent item, int globalIndex, bool selected)
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
                    ret = Im.Selectable(item.FullName, selected);
                }
            }

            if (item.CausesCycle)
                Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, "Setting this parent would cause a cycle in the ancestors."u8, true);

            return ret;
        }

        protected override bool IsSelected(Parent item, int globalIndex)
            => _group!.ParentSetting == item.Object;

        private sealed class ItemFilter : TextFilterBase<Parent>
        {
            public override bool WouldBeVisible(in Parent item, int globalIndex)
                => item.Visible = base.WouldBeVisible(in item, globalIndex);

            protected override string ToFilterString(in Parent item, int globalIndex)
                => item.Name.Utf16;
        }
    }
}

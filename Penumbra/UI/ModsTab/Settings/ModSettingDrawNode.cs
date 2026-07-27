using ImSharp;
using Luna;
using Penumbra.Mods.Settings;
using Penumbra.UI.Classes;
using Penumbra.UI.ModsTab.Groups;

namespace Penumbra.UI.ModsTab.Settings;

public readonly record struct ModSettingsDrawLine(float X, float Min, float Length)
{
    public void Draw(ModSettingsCache cache)
    {
        var startPos = Im.Cursor.ScreenPosition + new Vector2(X, Min);
        var endPos   = startPos with { Y = startPos.Y + Length };
        Im.Window.DrawList.Shape.Line(startPos, endPos, ColorId.OptionTreeLine.Value, cache.LineWidth);
    }
}

public readonly struct ModSettingDrawNode
{
    public static readonly ModSettingDrawNode Space = new()
    {
        Node     = null!,
        Id       = ImGuiId.Invalid,
        DrawMode = Mode.Space,
    };

    public required ModSettingDataNode Node { get; init; }
    public required ImGuiId            Id   { get; init; }

    public Vector2 LabelWidth { get; init; }
    public Vector2 ComboWidth { get; init; }

    public          float Indent            { get; init; }
    public          float SecondItemOffset  { get; init; }
    public          float IncomingLineWidth { get; init; }
    public required Mode  DrawMode          { get; init; }
    public          bool  Collapsible       { get; init; }
    public          bool  Expanded          { get; init; }


    public enum Mode : byte
    {
        PageHeader,
        Checkbox,
        RadioButton,
        Label,
        CheckboxLabel,
        ComboLabel,
        Space,
        Combo,
    }

    private void DrawIncomingLine(ModSettingsCache cache)
    {
        if (IncomingLineWidth <= 0)
            return;

        var endPos   = Im.Cursor.ScreenPosition + new Vector2(Indent, cache.HalfHeight);
        var startPos = endPos with { X = endPos.X - IncomingLineWidth + MathF.Round(cache.LineWidth / 2) };
        Im.Window.DrawList.Shape.Line(startPos, endPos, ColorId.OptionTreeLine.Value, cache.LineWidth);
    }

    public void Draw(ModGroupDrawer drawer, ModSettingsCache cache)
    {
        DrawIncomingLine(cache);
        _ = DrawMode switch
        {
            Mode.PageHeader    => DrawPageHeader(cache),
            Mode.Checkbox      => DrawCheckbox(drawer, cache),
            Mode.RadioButton   => DrawRadio(drawer, cache),
            Mode.Label         => DrawLabel(drawer, cache),
            Mode.CheckboxLabel => DrawCheckboxLabel(drawer, cache),
            Mode.ComboLabel    => DrawComboLabel(drawer, cache),
            Mode.Space         => DrawSpace(),
            Mode.Combo         => DrawCombo(drawer, cache),
            _                  => false,
        };
    }

    private bool DrawPageHeader(ModSettingsCache cache)
    {
        var (border, text, background) = Expanded
            ? (ColorId.GroupLabelBorderExpanded.Value, ColorId.GroupLabelTextExpanded.Value, ColorId.GroupLabelBackgroundExpanded.Value)
            : (ColorId.GroupLabelBorderCollapsed.Value, ColorId.GroupLabelTextCollapsed.Value, ColorId.GroupLabelBackgroundCollapsed.Value);

        using var colors = ImStyleBorder.Frame.Push(border, cache.LineWidth)
            .Push(ImGuiColor.Text,   text)
            .Push(ImGuiColor.Header, background);
        Im.Tree.SetNextOpen(Expanded);

        Im.Tree.Header(Node.Name);
        if (Im.Tree.ToggledOpen())
        {
            cache.Storage.SetBool(Id, !Expanded);
            cache.DrawDirty = true;
        }

        return true;
    }

    private bool DrawLabel(ModGroupDrawer _, ModSettingsCache cache)
    {
        Im.Cursor.X += Indent;

        if (!Collapsible)
        {
            using var position = ImStyleBorder.Frame.Push(ColorId.GroupLabelBorder.Vector, cache.LineWidth)
                .PushX(ImStyleDouble.ButtonTextAlign, 0);
            ImEx.TextFramed(Node.Name, LabelWidth, ColorId.GroupLabelBackground.Value, ColorId.GroupLabelText.Value,
                ColorId.GroupLabelBorder.Value);
            if (!Node.Description.IsEmpty)
            {
                Im.Window.DrawList.Text(AwesomeIcon.Font, AwesomeIcon.Font.Size,
                    Im.Item.LowerRightCorner - new Vector2(cache.HelpIconSize + Im.Style.FramePadding.X, cache.Height - Im.Style.FramePadding.Y),
                    ImGuiColor.TextDisabled.Get(), LunaStyle.HelpMarker.Span);
                Im.Tooltip.OnHover(Node.Description);
            }

            return true;
        }

        var button = new CaretButton
        {
            BorderWidth = cache.LineWidth,
            TooltipIcon = LunaStyle.HelpMarker,
            Collapsed = new CaretButton.Colors
            {
                Background = ColorId.GroupLabelBackgroundCollapsed.Value,
                Text       = ColorId.GroupLabelTextCollapsed.Value,
                Caret      = ColorId.GroupLabelTextCollapsed.Value,
                Border     = ColorId.GroupLabelBorderCollapsed.Value,
            },
            Expanded = new CaretButton.Colors
            {
                Background = ColorId.GroupLabelBackgroundExpanded.Value,
                Text       = ColorId.GroupLabelTextExpanded.Value,
                Caret      = ColorId.GroupLabelTextExpanded.Value,
                Border     = ColorId.GroupLabelBorderExpanded.Value,
            },
        };

        if (button.Draw(Node.Name, Node.Description, LabelWidth, Expanded).Clicked)
        {
            cache.Storage.SetBool(Id, !Expanded);
            cache.DrawDirty = true;
        }

        return true;
    }

    private bool DrawCheckboxLabel(ModGroupDrawer drawer, ModSettingsCache cache)
    {
        var group  = (ModSettingGroup)Node;
        var option = (ModSettingOption)group.VisibleChildren[0];
        DrawLabel(drawer, cache);
        DrawConnector(cache);
        DoDrawCheckbox(drawer, cache, option);
        return true;
    }

    private static void DrawConnector(ModSettingsCache cache)
    {
        Im.Line.Same(0, cache.CenterSpacing);
        var endPos   = Im.Cursor.ScreenPosition.AddY(cache.HalfHeight);
        var startPos = endPos.AddX(-cache.CenterSpacing);
        Im.Window.DrawList.Shape.Line(startPos, endPos, ColorId.OptionTreeLine.Value, cache.LineWidth);
    }

    private bool DrawComboLabel(ModGroupDrawer drawer, ModSettingsCache cache)
    {
        var group = (ModSettingGroup)Node;
        DrawLabel(drawer, cache);
        DrawConnector(cache);
        using (ImStyleBorder.Frame.Push(ColorId.OptionBorder.Vector, cache.LineWidth))
        {
            using var disabled = Im.Disabled(group.Disabled);
            drawer.Combo.Draw(drawer, group, drawer.GetModSetting(group.Group), ComboWidth.X);
        }

        return true;
    }

    private bool DrawCombo(ModGroupDrawer drawer, ModSettingsCache cache)
    {
        var group = (ModSettingGroup)Node;
        Im.Cursor.X += SecondItemOffset;
        using (ImStyleBorder.Frame.Push(ColorId.OptionBorder.Vector, cache.LineWidth))
        {
            using var disabled = Im.Disabled(group.Disabled || drawer.Locked);
            drawer.Combo.Draw(drawer, group, drawer.GetModSetting(group.Group), ComboWidth.X);
        }

        return true;
    }

    private bool DrawSpace()
    {
        Im.FrameDummy();
        return true;
    }

    private bool DrawCheckbox(ModGroupDrawer drawer, ModSettingsCache cache)
    {
        if (Node is ModSettingOption option)
        {
            Im.Cursor.X += Indent;
        }
        else
        {
            option      =  (ModSettingOption)((ModSettingGroup)Node).VisibleChildren[0];
            Im.Cursor.X += SecondItemOffset;
        }

        DoDrawCheckbox(drawer, cache, option);
        return true;
    }

    private static void DoDrawCheckbox(ModGroupDrawer drawer, ModSettingsCache cache, ModSettingOption option)
    {
        using var id      = Im.Id.Push(option.Data.Index);
        var       setting = drawer.GetModSetting(option.Data.Group);
        var       value   = setting.HasFlag(option.Data.Index);

        using (ImStyleBorder.Frame.Push(ColorId.OptionBorder.Vector, cache.LineWidth)
                   .Push(ImGuiColor.Text, option.Color))
        {
            using var disabled = Im.Disabled(option.Disabled || drawer.Locked);
            if (Im.Checkbox(option.HideLabel ? "##check"u8 : option.Name, value))
                drawer.SetModSetting(option.Data.Group, setting.SetBit(option.Data.Index, !value));
        }

        if (!option.Description.IsEmpty)
        {
            Im.Line.SameInner();
            LunaStyle.DrawAlignedHelpMarker(option.Description, treatAsHovered: Im.Item.Hovered(HoveredFlags.AllowWhenDisabled));
        }
    }

    private bool DrawRadio(ModGroupDrawer drawer, ModSettingsCache cache)
    {
        var option  = (ModSettingOption)Node;
        var setting = drawer.GetModSetting(option.Data.Group);
        var value   = setting.AsIndex == option.Data.Index;
        Im.Cursor.X += Indent;
        using (ImStyleBorder.Frame.Push(ColorId.OptionBorder.Vector, cache.LineWidth)
                   .Push(ImGuiColor.Text, option.Color))
        {
            using var _ = Im.Disabled(option.Disabled || drawer.Locked);
            if (Im.RadioButton(Node.Name, value))
                drawer.SetModSetting(option.Data.Group, Setting.Single(option.Data.Index));
        }

        if (!option.Description.IsEmpty)
        {
            Im.Line.SameInner();
            LunaStyle.DrawAlignedHelpMarker(option.Description, treatAsHovered: Im.Item.Hovered(HoveredFlags.AllowWhenDisabled));
        }

        return true;
    }
}

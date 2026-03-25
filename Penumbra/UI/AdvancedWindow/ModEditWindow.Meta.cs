using ImSharp;
using Luna;
using Penumbra.Api.Api;
using Penumbra.Api.Enums;
using Penumbra.GameData.Data;
using Penumbra.GameData.Enums;
using Penumbra.Meta.Manipulations;
using Penumbra.UI.AdvancedWindow.Meta;
using Penumbra.UI.Classes;

namespace Penumbra.UI.AdvancedWindow;

public partial class ModEditWindow
{
    private readonly MetaDrawers          _metaDrawers;
    private          MetaManipulationType _selected = MetaManipulationType.Eqp;

    private void DrawMetaTab()
    {
        using var tab = Im.TabBar.BeginItem("Meta Manipulations"u8);
        if (!tab)
            return;

        Im.Cursor.Y += Im.Style.ItemSpacing.Y;
        using var id        = Im.Id.Push(Mod!.Identifier);
        var       setsEqual = !_editor.MetaEditor.Changes;
        var       tt        = setsEqual ? "No changes staged."u8 : "Apply the currently staged changes to the option."u8;
        if (ImEx.Button("Apply Changes"u8, Vector2.Zero, tt, setsEqual))
            _editor.MetaEditor.Apply(_editor.Option!);

        Im.Line.Same();
        tt = setsEqual ? "No changes staged."u8 : "Revert all currently staged changes."u8;
        if (ImEx.Button("Revert Changes"u8, Vector2.Zero, tt, setsEqual))
            _editor.MetaEditor.Load(_editor.Mod!, _editor.Option!);

        Im.Line.Same();
        AddFromClipboardButton();
        Im.Line.Same();
        SetFromClipboardButton();
        Im.Line.Same();
        CopyToClipboardButton("Copy all current manipulations to clipboard."u8, _iconSize, _editor.MetaEditor);
        Im.Line.Same();
        if (Im.Button("Write as TexTools Files"u8))
            _metaFileManager.WriteAllTexToolsMeta(Mod!);
        Im.Line.Same();
        if (ImEx.Button("Remove All Default-Values"u8, "Delete any entries from all lists that set the value to its default value."u8))
            _editor.MetaEditor.DeleteDefaultValues();
        Im.Line.Same();
        DrawAtchDragDrop();

        Im.Cursor.Y += Im.Style.ItemSpacing.Y;
        var buttonSize = new Vector2(Im.ContentRegion.Available.X / 10, Im.Style.FrameHeight);
        DrawEditHeader(MetaManipulationType.Eqp,       buttonSize);
        DrawVerticalSeparator();
        DrawEditHeader(MetaManipulationType.Eqdp,      buttonSize);
        DrawVerticalSeparator();
        DrawEditHeader(MetaManipulationType.Imc,       buttonSize);
        DrawVerticalSeparator();
        DrawEditHeader(MetaManipulationType.Est,       buttonSize);
        DrawVerticalSeparator();
        DrawEditHeader(MetaManipulationType.Gmp,       buttonSize);
        DrawVerticalSeparator();
        DrawEditHeader(MetaManipulationType.Rsp,       buttonSize);
        DrawVerticalSeparator();
        DrawEditHeader(MetaManipulationType.Atch,      buttonSize);
        DrawVerticalSeparator();
        DrawEditHeader(MetaManipulationType.Shp,       buttonSize);
        DrawVerticalSeparator();
        DrawEditHeader(MetaManipulationType.Atr,       buttonSize);
        DrawVerticalSeparator();
        DrawEditHeader(MetaManipulationType.GlobalEqp, buttonSize);

        Im.Cursor.Y -= Im.Style.ItemSpacing.Y;
        if (_metaDrawers.Get(_selected) is { } drawer)
            DrawTable(drawer);
    }

    private static void DrawVerticalSeparator()
    {
        var lowerRight = Im.Item.LowerRightCorner;
        var upperLeft  = Im.Item.UpperLeftCorner with { X = lowerRight.X };
        Im.Window.DrawList.Shape.Line(upperLeft, lowerRight, ImGuiColor.Separator.Get(), Im.Style.GlobalScale);
        Im.Line.NoSpacing();
    }

    private void DrawAtchDragDrop()
    {
        _dragDropManager.CreateImGuiSource("atchDrag", f => f.Extensions.Contains(".atch"), f =>
        {
            var gr = Parser.ParseRaceCode(f.Files.FirstOrDefault() ?? string.Empty);
            if (gr is GenderRace.Unknown)
                return false;

            Im.Text($"Dragging .atch for {gr.ToName()}...");
            return true;
        });
        var hasAtch = _editor.Files.GetByType(ResourceType.Atch).Count > 0;
        if (ImEx.Button("Import .atch"u8, Vector2.Zero,
                _dragDropManager.IsDragging
                    ? ""u8
                    : hasAtch
                        ? "Drag a .atch file containing its race code in the path here to import its values.\n\nClick to select an .atch file from the mod."u8
                        : "Drag a .atch file containing its race code in the path here to import its values."u8,
                !_dragDropManager.IsDragging && !hasAtch)
         && hasAtch)
            Im.Popup.Open("##atchPopup"u8);
        if (_dragDropManager.CreateImGuiTarget("atchDrag", out var files, out _) && files.FirstOrDefault() is { } file)
            _metaDrawers.Atch.ImportFile(file);

        using var popup = Im.Popup.Begin("##atchPopup"u8);
        if (!popup)
            return;

        if (!hasAtch)
        {
            Im.Popup.CloseCurrent();
            return;
        }

        foreach (var atchFile in _editor.Files.GetByType(ResourceType.Atch))
        {
            if (Im.Selectable(atchFile.RelPath.Path.Span) && atchFile.File.Exists)
                _metaDrawers.Atch.ImportFile(atchFile.File.FullName);
        }
    }

    private void DrawEditHeader(MetaManipulationType type, Vector2 buttonSize)
    {
        var drawer = _metaDrawers.Get(type);
        if (drawer is null)
            return;

        var otherData = _editor.MetaEditor.OtherData[type];
        using (Im.Style.Push(ImStyleSingle.FrameRounding, 0))
        {
            var color = Im.Color.Push(ImGuiColor.Button, Im.Style[ImGuiColor.ButtonHovered], _selected == type)
                .Push(ImGuiColor.ButtonHovered,  Im.Style[ImGuiColor.ButtonHovered], _selected == type)
                .Push(ImGuiColor.ButtonActive,   Im.Style[ImGuiColor.ButtonHovered], _selected == type);

            //if (Im.Button($"{(drawer.Count > 0 ? $"{drawer.Count} " : StringU8.Empty)}{drawer.Header}{(otherData.TotalCount > 0 ? $" ({otherData.TotalCount})" : StringU8.Empty)}###{drawer.Label}", buttonSize))
            if (Im.Button(drawer.Header, buttonSize))
                _selected = type;
            if (drawer.Count > 0)
            {
                var position = Im.Item.UpperLeftCorner + Im.Style.FramePadding;
                Im.Window.DrawList.Text(position, ColorId.NewMod.Value().FullAlpha(), $"({drawer.Count})");
            }
            if (otherData.TotalCount > 0)
            {
                var position = Im.Item.LowerRightCorner - Im.Style.FramePadding - Im.Font.CalculateSize($"({otherData.TotalCount})");
                Im.Window.DrawList.Text(position, ColorId.RedundantAssignment.Value().FullAlpha(), $"({otherData.TotalCount})");
            }
            color.Dispose();

            if (Im.Item.Hovered())
            {
                using var tt = Im.Tooltip.Begin();
                Im.Text(drawer.Tooltip);
                Im.Cursor.Y += Im.Style.ItemInnerSpacing.Y;
                Im.Separator();
                Im.Cursor.Y += Im.Style.ItemInnerSpacing.Y;
                Im.Text($"{drawer.Count} Edits in this Option.");
                Im.Text($"{otherData.TotalCount} Edits in {otherData.Count} other Option{(otherData.Count is not 1 ? "s"u8 : StringU8.Empty)}.");
                if (otherData.TotalCount > 0)
                {
                    Im.Cursor.Y += Im.Style.ItemInnerSpacing.Y;
                    Im.Separator();
                    Im.Cursor.Y += Im.Style.ItemInnerSpacing.Y;
                    foreach (var name in otherData)
                        Im.BulletText(name);
                }
            }
        }
    }

    private static void DrawTable(IMetaDrawer drawer)
    {
        const TableFlags flags = TableFlags.RowBackground | TableFlags.SizingFixedFit | TableFlags.BordersInnerVertical | TableFlags.BordersOuter | TableFlags.ScrollY;
        using var        table = Im.Table.Begin(drawer.Label, drawer.NumColumns, flags, Im.ContentRegion.Available);
        if (!table)
            return;

        table.SetupScrollFreeze(0, 1);
        drawer.Draw();
    }

    private static void CopyToClipboardButton(ReadOnlySpan<byte> tooltip, Vector2 iconSize, MetaDictionary manipulations)
    {
        if (!ImEx.Icon.Button(LunaStyle.ToClipboardIcon, tooltip, iconSize))
            return;

        var text = CompressionFunctions.ToCompressedBase64(manipulations, 0);
        if (text.Length > 0)
            Im.Clipboard.Set(text);
    }

    private void AddFromClipboardButton()
    {
        if (Im.Button("Add from Clipboard"u8))
        {
            var clipboard = Im.Clipboard.GetUtf16();

            if (MetaApi.ConvertManips(clipboard, out var manips, out _))
            {
                _editor.MetaEditor.UpdateTo(manips);
                _editor.MetaEditor.Changes = true;
            }
        }

        Im.Tooltip.OnHover(
            "Try to add meta manipulations currently stored in the clipboard to the current manipulations.\nOverwrites already existing manipulations."u8);
    }

    private void SetFromClipboardButton()
    {
        if (Im.Button("Set from Clipboard"u8))
        {
            var clipboard = Im.Clipboard.GetUtf16();
            if (MetaApi.ConvertManips(clipboard, out var manips, out _))
            {
                _editor.MetaEditor.SetTo(manips);
                _editor.MetaEditor.Changes = true;
            }
        }

        Im.Tooltip.OnHover(
            "Try to set the current meta manipulations to the set currently stored in the clipboard.\nRemoves all other manipulations."u8);
    }
}

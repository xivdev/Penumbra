using ImSharp;
using Luna;
using Penumbra.Api.Api;
using Penumbra.GameData.Data;
using Penumbra.GameData.Enums;
using Penumbra.Meta.Manipulations;
using Penumbra.UI.AdvancedWindow.Meta;
using Penumbra.UI.Classes;

namespace Penumbra.UI.AdvancedWindow;

public partial class ModEditWindow
{
    private readonly MetaDrawers _metaDrawers;

    private void DrawMetaTab()
    {
        using var tab = Im.TabBar.BeginItem("Meta Manipulations"u8);
        if (!tab)
            return;

        DrawOptionSelectHeader();

        var setsEqual = !_editor.MetaEditor.Changes;
        var tt        = setsEqual ? "No changes staged."u8 : "Apply the currently staged changes to the option."u8;
        Im.Line.New();
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

        using var child = Im.Child.Begin("##meta"u8, Im.ContentRegion.Available, true);
        if (!child)
            return;

        DrawEditHeader(MetaManipulationType.Eqp);
        DrawEditHeader(MetaManipulationType.Eqdp);
        DrawEditHeader(MetaManipulationType.Imc);
        DrawEditHeader(MetaManipulationType.Est);
        DrawEditHeader(MetaManipulationType.Gmp);
        DrawEditHeader(MetaManipulationType.Rsp);
        DrawEditHeader(MetaManipulationType.Atch);
        DrawEditHeader(MetaManipulationType.Shp);
        DrawEditHeader(MetaManipulationType.Atr);
        DrawEditHeader(MetaManipulationType.GlobalEqp);
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
        var hasAtch = _editor.Files.Atch.Count > 0;
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

        foreach (var atchFile in _editor.Files.Atch)
        {
            if (Im.Selectable(atchFile.RelPath.Path.Span) && atchFile.File.Exists)
                _metaDrawers.Atch.ImportFile(atchFile.File.FullName);
        }
    }

    private void DrawEditHeader(MetaManipulationType type)
    {
        var drawer = _metaDrawers.Get(type);
        if (drawer is null)
            return;

        var oldPos = Im.Cursor.Y;
        var header = Im.Tree.Header($"{_editor.MetaEditor.GetCount(type)} {drawer.Label}");
        DrawOtherOptionData(type, oldPos, Im.Cursor.Position);
        if (!header)
            return;

        DrawTable(drawer);
    }

    private static void DrawTable(IMetaDrawer drawer)
    {
        const TableFlags flags = TableFlags.RowBackground | TableFlags.SizingFixedFit | TableFlags.BordersInnerVertical;
        using var        table = Im.Table.Begin(drawer.Label, drawer.NumColumns, flags);
        if (!table)
            return;

        drawer.Draw();
        Im.Line.New();
    }

    private void DrawOtherOptionData(MetaManipulationType type, float oldPos, Vector2 newPos)
    {
        var otherOptionData = _editor.MetaEditor.OtherData[type];
        if (otherOptionData.TotalCount <= 0)
            return;

        Utf8StringHandler<TextStringHandlerBuffer> text = $"{otherOptionData.TotalCount} Edits in other Options";
        var                                        size = Im.Font.CalculateSize(ref text).X;
        Im.Cursor.Position = new Vector2(Im.ContentRegion.Available.X - size, oldPos + Im.Style.FramePadding.Y);
        Im.Text(text, ColorId.RedundantAssignment.Value().FullAlpha());
        if (Im.Item.Hovered())
        {
            using var tt = Im.Tooltip.Begin();
            foreach (var name in otherOptionData)
                Im.Text(name);
        }

        Im.Cursor.Position = newPos;
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

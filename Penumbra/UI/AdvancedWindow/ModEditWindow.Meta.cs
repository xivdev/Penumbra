using Dalamud.Interface;
using Dalamud.Bindings.ImGui;
using ImSharp;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Text;
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
        using var tab = ImUtf8.TabItem("Meta Manipulations"u8);
        if (!tab)
            return;

        DrawOptionSelectHeader();

        var setsEqual = !_editor.MetaEditor.Changes;
        var tt        = setsEqual ? "No changes staged."u8 : "Apply the currently staged changes to the option."u8;
        Im.Line.New();
        if (ImUtf8.ButtonEx("Apply Changes"u8, tt, Vector2.Zero, setsEqual))
            _editor.MetaEditor.Apply(_editor.Option!);

        Im.Line.Same();
        tt = setsEqual ? "No changes staged."u8 : "Revert all currently staged changes."u8;
        if (ImUtf8.ButtonEx("Revert Changes"u8, tt, Vector2.Zero, setsEqual))
            _editor.MetaEditor.Load(_editor.Mod!, _editor.Option!);

        Im.Line.Same();
        AddFromClipboardButton();
        Im.Line.Same();
        SetFromClipboardButton();
        Im.Line.Same();
        CopyToClipboardButton("Copy all current manipulations to clipboard.", _iconSize, _editor.MetaEditor);
        Im.Line.Same();
        if (ImUtf8.Button("Write as TexTools Files"u8))
            _metaFileManager.WriteAllTexToolsMeta(Mod!);
        Im.Line.Same();
        if (ImUtf8.ButtonEx("Remove All Default-Values"u8, "Delete any entries from all lists that set the value to its default value."u8))
            _editor.MetaEditor.DeleteDefaultValues();
        Im.Line.Same();
        DrawAtchDragDrop();

        using var child = ImRaii.Child("##meta", -Vector2.One, true);
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

            ImUtf8.Text($"Dragging .atch for {gr.ToName()}...");
            return true;
        });
        var hasAtch = _editor.Files.Atch.Count > 0;
        if (ImUtf8.ButtonEx("Import .atch"u8,
                _dragDropManager.IsDragging
                    ? ""u8
                    : hasAtch
                        ? "Drag a .atch file containing its race code in the path here to import its values.\n\nClick to select an .atch file from the mod."u8
                        : "Drag a .atch file containing its race code in the path here to import its values."u8, default,
                !_dragDropManager.IsDragging && !hasAtch)
         && hasAtch)
            ImUtf8.OpenPopup("##atchPopup"u8);
        if (_dragDropManager.CreateImGuiTarget("atchDrag", out var files, out _) && files.FirstOrDefault() is { } file)
            _metaDrawers.Atch.ImportFile(file);

        using var popup = ImUtf8.Popup("##atchPopup"u8);
        if (!popup)
            return;

        if (!hasAtch)
        {
            ImGui.CloseCurrentPopup();
            return;
        }

        foreach (var atchFile in _editor.Files.Atch)
        {
            if (ImUtf8.Selectable(atchFile.RelPath.Path.Span) && atchFile.File.Exists)
                _metaDrawers.Atch.ImportFile(atchFile.File.FullName);
        }
    }

    private void DrawEditHeader(MetaManipulationType type)
    {
        var drawer = _metaDrawers.Get(type);
        if (drawer == null)
            return;

        var oldPos = ImGui.GetCursorPosY();
        var header = ImUtf8.CollapsingHeader($"{_editor.MetaEditor.GetCount(type)} {drawer.Label}");
        DrawOtherOptionData(type, oldPos, ImGui.GetCursorPos());
        if (!header)
            return;

        DrawTable(drawer);
    }

    private static void DrawTable(IMetaDrawer drawer)
    {
        const ImGuiTableFlags flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.BordersInnerV;
        using var             table = ImUtf8.Table(drawer.Label, drawer.NumColumns, flags);
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

        var text = $"{otherOptionData.TotalCount} Edits in other Options";
        var size = ImGui.CalcTextSize(text).X;
        ImGui.SetCursorPos(new Vector2(Im.ContentRegion.Available.X - size, oldPos + ImGui.GetStyle().FramePadding.Y));
        Im.Text(text, ColorId.RedundantAssignment.Value().FullAlpha());
        if (ImGui.IsItemHovered())
        {
            using var tt = ImUtf8.Tooltip();
            foreach (var name in otherOptionData)
                ImUtf8.Text(name);
        }

        ImGui.SetCursorPos(newPos);
    }

    private static void CopyToClipboardButton(string tooltip, Vector2 iconSize, MetaDictionary manipulations)
    {
        if (!ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Clipboard.ToIconString(), iconSize, tooltip, false, true))
            return;

        var text = Functions.ToCompressedBase64(manipulations, 0);
        if (text.Length > 0)
            ImGui.SetClipboardText(text);
    }

    private void AddFromClipboardButton()
    {
        if (ImUtf8.Button("Add from Clipboard"u8))
        {
            var clipboard = ImGuiUtil.GetClipboardText();

            if (MetaApi.ConvertManips(clipboard, out var manips, out _))
            {
                _editor.MetaEditor.UpdateTo(manips);
                _editor.MetaEditor.Changes = true;
            }
        }

        ImUtf8.HoverTooltip(
            "Try to add meta manipulations currently stored in the clipboard to the current manipulations.\nOverwrites already existing manipulations."u8);
    }

    private void SetFromClipboardButton()
    {
        if (ImUtf8.Button("Set from Clipboard"u8))
        {
            var clipboard = ImGuiUtil.GetClipboardText();
            if (MetaApi.ConvertManips(clipboard, out var manips, out _))
            {
                _editor.MetaEditor.SetTo(manips);
                _editor.MetaEditor.Changes = true;
            }
        }

        ImUtf8.HoverTooltip(
            "Try to set the current meta manipulations to the set currently stored in the clipboard.\nRemoves all other manipulations."u8);
    }
}

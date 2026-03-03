using Dalamud.Interface.DragDrop;
using ImSharp;
using Lumina.Data.Files;
using Penumbra.UI.Classes;

namespace Penumbra.UI.Tabs.Debug;

public class TexHeaderDrawer(IDragDropManager dragDrop) : Luna.IUiService
{
    private string?           _path;
    private TexFile.TexHeader _header;
    private byte[]?           _tex;
    private Exception?        _exception;

    public void Draw()
    {
        using var header = Im.Tree.HeaderId("Tex Header"u8);
        if (!header)
            return;

        DrawDragDrop();
        DrawData();
    }

    private void DrawDragDrop()
    {
        dragDrop.CreateImGuiSource("TexFileDragDrop", m => m.Files.Count == 1 && m.Extensions.Contains(".tex"), m =>
        {
            Im.Text($"Dragging {m.Files[0]}...");
            return true;
        });

        Im.Button("Drag .tex here..."u8);
        if (dragDrop.CreateImGuiTarget("TexFileDragDrop", out var files, out _))
            ReadTex(files[0]);
    }

    private void DrawData()
    {
        if (_path == null)
            return;

        ImEx.TextFramed(_path, default, 0, borderColor: 0xFFFFFFFF);


        if (_exception != null)
        {
            using var color = ImGuiColor.Text.Push(Colors.RegexWarningBorder);
            Im.TextWrapped($"Failure to load file:\n{_exception}");
        }
        else if (_tex != null)
        {
            using var table = Im.Table.Begin("table"u8, 2, TableFlags.SizingFixedFit | TableFlags.RowBackground);
            if (!table)
                return;

            table.DrawDataPair("Format"u8,     _header.Format);
            table.DrawDataPair("Width"u8,      _header.Width);
            table.DrawDataPair("Height"u8,     _header.Height);
            table.DrawDataPair("Depth"u8,      _header.Depth);
            table.DrawDataPair("Mip Levels"u8, _header.MipCount);
            table.DrawDataPair("Array Size"u8, _header.ArraySize);
            table.DrawDataPair("Type"u8,       _header.Type);
            table.DrawDataPair("Mip Flag"u8,   _header.MipUnknownFlag);
            table.DrawDataPair("Byte Size"u8,  _tex.Length);
            unsafe
            {
                table.DrawDataPair("LoD Offset 0"u8,  _header.LodOffset[0]);
                table.DrawDataPair("LoD Offset 1"u8,  _header.LodOffset[1]);
                table.DrawDataPair("LoD Offset 2"u8,  _header.LodOffset[2]);
                table.DrawDataPair("LoD Offset 0"u8,  _header.OffsetToSurface[0]);
                table.DrawDataPair("LoD Offset 1"u8,  _header.OffsetToSurface[1]);
                table.DrawDataPair("LoD Offset 2"u8,  _header.OffsetToSurface[2]);
                table.DrawDataPair("LoD Offset 3"u8,  _header.OffsetToSurface[3]);
                table.DrawDataPair("LoD Offset 4"u8,  _header.OffsetToSurface[4]);
                table.DrawDataPair("LoD Offset 5"u8,  _header.OffsetToSurface[5]);
                table.DrawDataPair("LoD Offset 6"u8,  _header.OffsetToSurface[6]);
                table.DrawDataPair("LoD Offset 7"u8,  _header.OffsetToSurface[7]);
                table.DrawDataPair("LoD Offset 8"u8,  _header.OffsetToSurface[8]);
                table.DrawDataPair("LoD Offset 9"u8,  _header.OffsetToSurface[9]);
                table.DrawDataPair("LoD Offset 10"u8, _header.OffsetToSurface[10]);
                table.DrawDataPair("LoD Offset 11"u8, _header.OffsetToSurface[11]);
                table.DrawDataPair("LoD Offset 12"u8, _header.OffsetToSurface[12]);
            }
        }
    }

    private unsafe void ReadTex(string path)
    {
        try
        {
            _path = path;
            _tex  = File.ReadAllBytes(_path);
            if (_tex.Length < sizeof(TexFile.TexHeader))
                throw new Exception($"Size {_tex.Length} does not include a header.");

            _header    = MemoryMarshal.Read<TexFile.TexHeader>(_tex);
            _exception = null;
        }
        catch (Exception ex)
        {
            _tex       = null;
            _exception = ex;
        }
    }
}

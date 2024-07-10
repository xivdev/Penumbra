using Dalamud.Interface.DragDrop;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using Lumina.Data.Files;
using OtterGui.Services;
using OtterGui.Text;
using Penumbra.UI.Classes;

namespace Penumbra.UI.Tabs.Debug;

public class TexHeaderDrawer(IDragDropManager dragDrop) : IUiService
{
    private string?           _path;
    private TexFile.TexHeader _header;
    private byte[]?           _tex;
    private Exception?        _exception;

    public void Draw()
    {
        using var header = ImUtf8.CollapsingHeaderId("Tex Header"u8);
        if (!header)
            return;

        DrawDragDrop();
        DrawData();
    }

    private void DrawDragDrop()
    {
        dragDrop.CreateImGuiSource("TexFileDragDrop", m => m.Files.Count == 1 && m.Extensions.Contains(".tex"), m =>
        {
            ImUtf8.Text($"Dragging {m.Files[0]}...");
            return true;
        });

        ImUtf8.Button("Drag .tex here...");
        if (dragDrop.CreateImGuiTarget("TexFileDragDrop", out var files, out _))
            ReadTex(files[0]);
    }

    private void DrawData()
    {
        if (_path == null)
            return;

        ImUtf8.TextFramed(_path, 0, borderColor: 0xFFFFFFFF);


        if (_exception != null)
        {
            using var color = ImRaii.PushColor(ImGuiCol.Text, Colors.RegexWarningBorder);
            ImUtf8.TextWrapped($"Failure to load file:\n{_exception}");
        }
        else if (_tex != null)
        {
            using var table = ImRaii.Table("table", 2, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg);
            if (!table)
                return;

            TableLine("Format"u8,     _header.Format);
            TableLine("Width"u8,      _header.Width);
            TableLine("Height"u8,     _header.Height);
            TableLine("Depth"u8,      _header.Depth);
            TableLine("Mip Levels"u8, _header.MipCount);
            TableLine("Array Size"u8, _header.ArraySize);
            TableLine("Type"u8,       _header.Type);
            TableLine("Mip Flag"u8,   _header.MipUnknownFlag);
            TableLine("Byte Size"u8,  _tex.Length);
            unsafe
            {
                TableLine("LoD Offset 0"u8,  _header.LodOffset[0]);
                TableLine("LoD Offset 1"u8,  _header.LodOffset[1]);
                TableLine("LoD Offset 2"u8,  _header.LodOffset[2]);
                TableLine("LoD Offset 0"u8,  _header.OffsetToSurface[0]);
                TableLine("LoD Offset 1"u8,  _header.OffsetToSurface[1]);
                TableLine("LoD Offset 2"u8,  _header.OffsetToSurface[2]);
                TableLine("LoD Offset 3"u8,  _header.OffsetToSurface[3]);
                TableLine("LoD Offset 4"u8,  _header.OffsetToSurface[4]);
                TableLine("LoD Offset 5"u8,  _header.OffsetToSurface[5]);
                TableLine("LoD Offset 6"u8,  _header.OffsetToSurface[6]);
                TableLine("LoD Offset 7"u8,  _header.OffsetToSurface[7]);
                TableLine("LoD Offset 8"u8,  _header.OffsetToSurface[8]);
                TableLine("LoD Offset 9"u8,  _header.OffsetToSurface[9]);
                TableLine("LoD Offset 10"u8, _header.OffsetToSurface[10]);
                TableLine("LoD Offset 11"u8, _header.OffsetToSurface[11]);
                TableLine("LoD Offset 12"u8, _header.OffsetToSurface[12]);
            }
        }
    }

    private static void TableLine<T>(ReadOnlySpan<byte> text, T value)
    {
        ImGui.TableNextColumn();
        ImUtf8.Text(text);
        ImGui.TableNextColumn();
        ImUtf8.Text($"{value}");
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

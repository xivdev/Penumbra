using Dalamud.Utility;

namespace Penumbra.GameData.Structs;

// A wrapper for the clothing dyes the game provides with their RGBA color value, game ID, unmodified color value and name.
public readonly struct Stain
{
    // An empty stain with transparent color.
    public static readonly Stain None = new("None");

    public readonly string Name;
    public readonly uint   RgbaColor;
    public readonly byte   RowIndex;
    public readonly bool   Gloss;

    public byte R
        => (byte)(RgbaColor & 0xFF);

    public byte G
        => (byte)((RgbaColor >> 8) & 0xFF);

    public byte B
        => (byte)((RgbaColor >> 16) & 0xFF);

    public byte Intensity
        => (byte)((1 + R + G + B) / 3);

    // R and B need to be shuffled and Alpha set to max.
    private static uint SeColorToRgba(uint color)
        => ((color & 0xFF) << 16) | ((color >> 16) & 0xFF) | (color & 0xFF00) | 0xFF000000;

    public Stain(Lumina.Excel.GeneratedSheets.Stain stain)
        : this(stain.Name.ToDalamudString().ToString(), SeColorToRgba(stain.Color), (byte)stain.RowId, stain.Unknown5)
    { }

    internal Stain(string name, uint dye, byte index, bool gloss)
    {
        Name      = name;
        RowIndex  = index;
        Gloss     = gloss;
        RgbaColor = dye;
    }

    // Only used by None.
    private Stain(string name)
    {
        Name      = name;
        RowIndex  = 0;
        RgbaColor = 0;
        Gloss     = false;
    }
}

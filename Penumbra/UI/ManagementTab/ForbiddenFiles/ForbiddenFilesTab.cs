using System.Collections.Frozen;
using ImSharp;
using Luna;
using Penumbra.Communication;
using Penumbra.Import.Textures;
using Penumbra.Mods.Manager;
using Penumbra.String;

namespace Penumbra.UI.ManagementTab;

public sealed class ForbiddenFilesTab(ModManager mods, TextureManager textures, UiNavigator navigator, Configuration config)
    : ITab<ManagementTabType>
{
    private readonly ForbiddenFilesTable _table = new(mods, textures, navigator, config);

    public static readonly FrozenDictionary<uint, CiByteString> ForbiddenFiles = (((uint, CiByteString)[])
    [
        (0x90E4EE2F, new CiByteString("common/graphics/texture/dummy.tex"u8,    MetaDataComputation.All)),
        (0x84815A1A, new CiByteString("chara/common/texture/white.tex"u8,       MetaDataComputation.All)),
        (0x749091FB, new CiByteString("chara/common/texture/black.tex"u8,       MetaDataComputation.All)),
        (0x5CB9681A, new CiByteString("chara/common/texture/id_16.tex"u8,       MetaDataComputation.All)),
        (0x7E78D000, new CiByteString("chara/common/texture/red.tex"u8,         MetaDataComputation.All)),
        (0xBDC0BFD3, new CiByteString("chara/common/texture/green.tex"u8,       MetaDataComputation.All)),
        (0xC410E850, new CiByteString("chara/common/texture/blue.tex"u8,        MetaDataComputation.All)),
        (0xD5CFA221, new CiByteString("chara/common/texture/null_normal.tex"u8, MetaDataComputation.All)),
        (0xBE48CA67, new CiByteString("chara/common/texture/skin_mask.tex"u8,   MetaDataComputation.All)),
    ]).ToFrozenDictionary(p => p.Item1, p =>
    {
        Debug.Assert((uint)p.Item2.Crc32 == p.Item1,
            $"Invalid hash computation in forbidden files for {p.Item2} ({p.Item1:X} vs {p.Item2.Crc32:X}).");
        return p.Item2;
    });

    public void PostTabButton()
    {
        if (Im.Item.Hovered())
            DrawTooltip();
    }

    public ReadOnlySpan<byte> Label
        => "Forbidden Files"u8;

    public void DrawContent()
    {
        var hovered = LunaStyle.DrawAlignedHelpMarker();
        Im.Line.SameInner();
        ImEx.TextFrameAligned("What are Forbidden Files?"u8);
        if (hovered || Im.Item.Hovered())
            DrawTooltip();

        _table.Draw();
    }

    public ManagementTabType Identifier
        => ManagementTabType.ForbiddenFiles;

    private static void DrawTooltip()
    {
        Im.Window.SetNextSize(ImEx.ScaledVectorX(800));
        using var tt = Im.Tooltip.Begin();
        Im.TextWrapped(
            "Forbidden files are used in a multitude of places in the game and expected to have very specific semantics, so that manipulating them generally will cause unintended side-effects. Allowing their redirection will cause graphical glitches in the best case, or make the game crash or hang indefinitely in loading screens in worse cases.\n\nThere are not many forbidden files, and they are blocked from being applied even if not fixed, so if you are unsure how to fix a mod, you do not need to worry about this warning.\n\nThe forbidden files are:"u8);
        using (Im.Group())
        {
            foreach (var name in ForbiddenFiles.Values)
                Im.BulletText(name.Span);
        }

        Im.Line.Same();
        using (Im.Group())
        {
            foreach (var id in ForbiddenFiles.Keys)
                Im.Text(Description(id));
        }

        return;

        static ReadOnlySpan<byte> Description(uint hash)
        {
            return hash switch
            {
                0x90E4EE2F => "Intended to be a pure white texture of minimal size."u8,
                0x84815A1A => "Required to be a solid white square with full alpha."u8,
                0x749091FB => "Required to be a solid black square with full alpha."u8,
                0x5CB9681A => "Used as a default ID-mapping, required to be a solid square of #780000 with full alpha."u8,
                0x7E78D000 => "Required to be a solid red square with full alpha."u8,
                0xBDC0BFD3 => "Required to be a solid green square with full alpha."u8,
                0xC410E850 => "Required to be a solid blue square with full alpha."u8,
                0xD5CFA221 => "Used as a default normal map, required to be a solid square of #7E7FFF with full alpha."u8,
                0xBE48CA67 => "Used as the default skin mask, required to be a solid square of #A5749A with full alpha."u8,
                _          => StringU8.Empty,
            };
        }
    }
}

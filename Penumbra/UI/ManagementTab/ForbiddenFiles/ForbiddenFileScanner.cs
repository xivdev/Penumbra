using System.Collections.Frozen;
using ImSharp;
using Penumbra.Import.Textures;
using Penumbra.Mods.Manager;
using Penumbra.Mods.SubMods;
using Penumbra.String.Classes;

namespace Penumbra.UI.ManagementTab;

public sealed class ForbiddenFileScanner(ModManager mods, TextureManager textures) : RedirectionScanner<ForbiddenFileRedirection>(mods)
{
    private readonly FrozenDictionary<uint, Rgba32>? _originalFiles = ForbiddenFilesTab.ForbiddenFiles.ToFrozenDictionary(kvp => kvp.Key, kvp =>
    {
        var file = textures.LoadTex(kvp.Value.ToString());
        var data = file.IsSolidColor();
        return data.IsDefault
            ? throw new Exception($"Forbidden file {kvp.Value} could not be loaded from game files.")
            : data.Color!.Value;
    });

    protected override ForbiddenFileRedirection Create(Utf8GamePath path, FullPath redirection, IModDataContainer container, bool swap)
    {
        if (swap)
            return new ForbiddenFileRedirection(path, redirection, container, true, false, false);

        try
        {
            if (!File.Exists(redirection.FullName))
                return new ForbiddenFileRedirection(path, redirection, container, false, false, false);

            var data             = textures.LoadTex(redirection.FullName);
            var redirectionColor = data.IsSolidColor();
            return new ForbiddenFileRedirection(path, redirection, container, false, false,
                ContextuallyEqual((uint)path.Path.Crc32, redirectionColor));
        }
        catch (Exception ex)
        {
            Penumbra.Log.Warning($"Could not read forbidden file redirection file {redirection}:\n{ex}");
            return new ForbiddenFileRedirection(path, redirection, container, false, false, false, true);
        }
    }

    private bool ContextuallyEqual(uint crc32, ColorParameter parameter)
    {
        if (parameter.IsDefault)
            return false;

        var origColor = _originalFiles![crc32];
        switch (crc32)
        {
            case 0x90E4EE2F: // dummy.tex  
            case 0x84815A1A: // white.tex  
            case 0x7E78D000: // red.tex    
            case 0xBDC0BFD3: // green.tex  
            case 0x749091FB: // black.tex  
            case 0xC410E850: // blue.tex   
            case 0xBE48CA67: // skin_mask.tex - This can possibly be relaxed more?
                return parameter.Color!.Value == origColor;
            case 0x5CB9681A: // id_16.tex  
                return parameter.Color!.Value.Color is >= 0xFF00006Fu and <= 0xFF00007Fu;
            case 0xD5CFA221: // null_normal.tex
                // red and green in ranges, blue and alpha full.
                return (origColor.Color & 0xFF00u) is >= 0x7E00u and <= 0x8100u
                 && (origColor.Color & 0xFFFF00FFu) is >= 0xFFFF007Eu and <= 0xFFFF0081u;
        }

        return false;
    }

    protected override bool DoCreateRedirection(Utf8GamePath path, FullPath redirection, IModDataContainer container, bool swap)
        => ForbiddenFilesTab.ForbiddenFiles.ContainsKey((uint)path.Path.Crc32);
}

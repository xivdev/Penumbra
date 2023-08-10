using System;
using Dalamud.Plugin.Services;
using Penumbra.Collections.Manager;
using Penumbra.String.Classes;

namespace Penumbra.Api;

public class DalamudSubstitutionProvider : IDisposable
{
    private readonly ITextureSubstitutionProvider _substitution;
    private readonly ActiveCollectionData         _activeCollectionData;

    public DalamudSubstitutionProvider(ITextureSubstitutionProvider substitution, ActiveCollectionData activeCollectionData)
    {
        _substitution                      =  substitution;
        _activeCollectionData              =  activeCollectionData;
        _substitution.InterceptTexDataLoad += Substitute;
    }

    public void Dispose()
        => _substitution.InterceptTexDataLoad -= Substitute;

    private void Substitute(string path, ref string? replacementPath)
    {
        // Let other plugins prioritize replacement paths.
        if (replacementPath != null)
            return;

        // Only replace interface textures.
        if (!path.StartsWith("ui/") && !path.StartsWith("common/font/"))
            return;

        try
        {
            if (!Utf8GamePath.FromString(path, out var utf8Path, true))
                return;

            var resolved = _activeCollectionData.Interface.ResolvePath(utf8Path);
            replacementPath = resolved?.FullName;
        }
        catch
        {
            // ignored
        }
    }
}
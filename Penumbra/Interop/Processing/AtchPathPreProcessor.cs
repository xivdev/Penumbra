using Penumbra.Api.Enums;
using Penumbra.Collections;
using Penumbra.GameData.Enums;
using Penumbra.Interop.PathResolving;
using Penumbra.String;
using Penumbra.String.Classes;

namespace Penumbra.Interop.Processing;

public sealed class AtchPathPreProcessor : IPathPreProcessor
{
    public ResourceType Type
        => ResourceType.Atch;

    public FullPath? PreProcess(ResolveData resolveData, CiByteString path, Utf8GamePath _, bool nonDefault, FullPath? resolved)
    {
        if (!resolveData.Valid)
            return resolved;

        if (!TryGetAtchGenderRace(path, out var gr))
            return resolved;

        Penumbra.Log.Excessive($"Pre-Processed {path} with {resolveData.ModCollection} for {gr.ToName()}.");
        if (resolveData.ModCollection.MetaCache?.Atch.GetFile(gr, out var file) == true)
            return PathDataHandler.CreateAtch(path, resolveData.ModCollection);

        return resolved;
    }

    public static bool TryGetAtchGenderRace(CiByteString originalGamePath, out GenderRace genderRace)
    {
        if (originalGamePath[^6] != '1'
         || originalGamePath[^7] != '0'
         || !ushort.TryParse(originalGamePath.Span[^9..^7], out var grInt)
         || grInt > 18)
        {
            genderRace = GenderRace.Unknown;
            return false;
        }

        genderRace = (GenderRace)(grInt * 100 + 1);
        return true;
    }
}

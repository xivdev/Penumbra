using OtterGui.Services;
using Penumbra.GameData.Enums;
using Penumbra.Interop.PathResolving;
using Penumbra.Meta;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Interop.Hooks.Meta;

public class RspHeightHook : FastHook<RspHeightHook.Delegate>, IDisposable
{
    public delegate float Delegate(nint cmpResource, Race clan, byte gender, byte isSecondSubRace, byte bodyType, byte height);

    private readonly MetaState       _metaState;
    private readonly MetaFileManager _metaFileManager;

    public RspHeightHook(HookManager hooks, MetaState metaState, MetaFileManager metaFileManager)
    {
        _metaState = metaState;
        _metaFileManager = metaFileManager;
        Task = hooks.CreateHook<Delegate>("GetRspHeight", "E8 ?? ?? ?? ?? 48 8B 8E ?? ?? ?? ?? 44 8B CF", Detour, metaState.Config.EnableMods);
        _metaState.Config.ModsEnabled += Toggle;
    }

    private unsafe float Detour(nint cmpResource, Race race, byte gender, byte isSecondSubRace, byte bodyType, byte height)
    {
        float scale;
        if (bodyType < 2
         && _metaState.RspCollection.TryPeek(out var collection)
         && collection is { Valid: true, ModCollection.MetaCache: { } cache })
        {
            // Special cases.
            if (height == 0xFF)
                return 1.0f;
            if (height > 100)
                height = 0;

            var clan = (SubRace)(((int)race - 1) * 2 + 1 + isSecondSubRace);
            var (minIdent, maxIdent) = gender == 0
                ? (new RspIdentifier(clan, RspAttribute.MaleMinSize), new RspIdentifier(clan,   RspAttribute.MaleMaxSize))
                : (new RspIdentifier(clan, RspAttribute.FemaleMinSize), new RspIdentifier(clan, RspAttribute.FemaleMaxSize));

            float minEntry, maxEntry;
            if (cache.Rsp.TryGetValue(minIdent, out var min))
            {
                minEntry = min.Entry.Value;
                maxEntry = cache.Rsp.TryGetValue(maxIdent, out var max)
                    ? max.Entry.Value
                    : CmpFile.GetDefault(_metaFileManager, minIdent.SubRace, maxIdent.Attribute).Value;
            }
            else
            {
                var ptr = CmpFile.GetDefaults(_metaFileManager, minIdent.SubRace, minIdent.Attribute);
                if (cache.Rsp.TryGetValue(maxIdent, out var max))
                {
                    minEntry = ptr->Value;
                    maxEntry = max.Entry.Value;
                }
                else
                {
                    minEntry = ptr[0].Value;
                    maxEntry = ptr[1].Value;
                }
            }

            scale = (maxEntry - minEntry) * height / 100f + minEntry;
        }
        else
        {
            scale = Task.Result.Original(cmpResource, race, gender, isSecondSubRace, bodyType, height);
        }

        Penumbra.Log.Excessive(
            $"[GetRspHeight] Invoked on 0x{cmpResource:X} with {race}, {(Gender)(gender + 1)}, {isSecondSubRace == 1}, {bodyType}, {height}, returned {scale}.");
        return scale;
    }

    public void Dispose()
        => _metaState.Config.ModsEnabled -= Toggle;
}

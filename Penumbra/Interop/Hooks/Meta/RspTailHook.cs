using OtterGui.Services;
using Penumbra.GameData;
using Penumbra.GameData.Enums;
using Penumbra.Interop.PathResolving;
using Penumbra.Meta;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Interop.Hooks.Meta;

public class RspTailHook : FastHook<RspTailHook.Delegate>, IDisposable
{
    public delegate float Delegate(nint cmpResource, Race clan, byte gender, byte isSecondSubRace, byte bodyType, byte height);

    private readonly MetaState       _metaState;
    private readonly MetaFileManager _metaFileManager;

    public RspTailHook(HookManager hooks, MetaState metaState, MetaFileManager metaFileManager)
    {
        _metaState       = metaState;
        _metaFileManager = metaFileManager;
        Task = hooks.CreateHook<Delegate>("GetRspTail", Sigs.GetRspTail, Detour,
            metaState.Config.EnableMods && !HookOverrides.Instance.Meta.RspTailHook);
        if (!HookOverrides.Instance.Meta.RspTailHook)
            _metaState.Config.ModsEnabled += Toggle;
    }

    private unsafe float Detour(nint cmpResource, Race race, byte gender, byte isSecondSubRace, byte bodyType, byte tailLength)
    {
        float scale;
        if (bodyType < 2
         && _metaState.RspCollection.TryPeek(out var collection)
         && collection is { Valid: true, ModCollection.MetaCache: { } cache })
        {
            var clan = (SubRace)(((int)race - 1) * 2 + 1 + isSecondSubRace);
            var (minIdent, maxIdent) = gender == 0
                ? (new RspIdentifier(clan, RspAttribute.MaleMinTail), new RspIdentifier(clan,   RspAttribute.MaleMaxTail))
                : (new RspIdentifier(clan, RspAttribute.FemaleMinTail), new RspIdentifier(clan, RspAttribute.FemaleMaxTail));

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

            scale = (maxEntry - minEntry) * tailLength / 100f + minEntry;
        }
        else
        {
            scale = Task.Result.Original(cmpResource, race, gender, isSecondSubRace, bodyType, tailLength);
        }

        Penumbra.Log.Excessive(
            $"[GetRspTail] Invoked on 0x{cmpResource:X} with {race}, {(Gender)(gender + 1)}, {isSecondSubRace == 1}, {bodyType}, {tailLength}, returned {scale}.");
        return scale;
    }

    public void Dispose()
        => _metaState.Config.ModsEnabled -= Toggle;
}

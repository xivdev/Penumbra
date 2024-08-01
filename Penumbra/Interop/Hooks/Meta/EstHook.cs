using OtterGui.Services;
using Penumbra.GameData;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Interop.PathResolving;
using Penumbra.Interop.Structs;
using Penumbra.Meta.Manipulations;
using CharacterUtility = Penumbra.Interop.Services.CharacterUtility;

namespace Penumbra.Interop.Hooks.Meta;

public unsafe class EstHook : FastHook<EstHook.Delegate>, IDisposable
{
    public delegate EstEntry Delegate(ResourceHandle* estResource, uint id, uint genderRace);

    private readonly CharacterUtility _characterUtility;
    private readonly MetaState        _metaState;

    public EstHook(HookManager hooks, MetaState metaState, CharacterUtility characterUtility)
    {
        _metaState        = metaState;
        _characterUtility = characterUtility;
        Task = hooks.CreateHook<Delegate>("FindEstEntry", Sigs.FindEstEntry, Detour,
            metaState.Config.EnableMods && !HookOverrides.Instance.Meta.EstHook);
        if (!HookOverrides.Instance.Meta.EstHook)
            _metaState.Config.ModsEnabled += Toggle;
    }

    private EstEntry Detour(ResourceHandle* estResource, uint genderRace, uint id)
    {
        EstEntry ret;
        if (_metaState.EstCollection.TryPeek(out var collection)
         && collection is { Valid: true, ModCollection.MetaCache: { } cache }
         && cache.Est.TryGetValue(Convert(estResource, genderRace, id), out var entry))
            ret = entry.Entry;
        else
            ret = Task.Result.Original(estResource, genderRace, id);

        Penumbra.Log.Excessive($"[FindEstEntry] Invoked with 0x{(nint)estResource:X}, {genderRace}, {id}, returned {ret.Value}.");
        return ret;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private EstIdentifier Convert(ResourceHandle* estResource, uint genderRace, uint id)
    {
        var i  = new PrimaryId((ushort)id);
        var gr = (GenderRace)genderRace;

        if (estResource == _characterUtility.Address->BodyEstResource)
            return new EstIdentifier(i, EstType.Body, gr);
        if (estResource == _characterUtility.Address->HairEstResource)
            return new EstIdentifier(i, EstType.Hair, gr);
        if (estResource == _characterUtility.Address->FaceEstResource)
            return new EstIdentifier(i, EstType.Face, gr);
        if (estResource == _characterUtility.Address->HeadEstResource)
            return new EstIdentifier(i, EstType.Head, gr);

        return new EstIdentifier(i, 0, gr);
    }

    public void Dispose()
        => _metaState.Config.ModsEnabled -= Toggle;
}

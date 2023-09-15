using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Penumbra.GameData.Actors;
using Penumbra.Interop.Services;

namespace Penumbra.Interop.PathResolving;

public class CutsceneService : IDisposable
{
    public const int CutsceneStartIdx = (int)ScreenActor.CutsceneStart;
    public const int CutsceneEndIdx   = (int)ScreenActor.CutsceneEnd;
    public const int CutsceneSlots    = CutsceneEndIdx - CutsceneStartIdx;

    private readonly GameEventManager _events;
    private readonly IObjectTable     _objects;
    private readonly short[]          _copiedCharacters = Enumerable.Repeat((short)-1, CutsceneSlots).ToArray();

    public IEnumerable<KeyValuePair<int, Dalamud.Game.ClientState.Objects.Types.GameObject>> Actors
        => Enumerable.Range(CutsceneStartIdx, CutsceneSlots)
            .Where(i => _objects[i] != null)
            .Select(i => KeyValuePair.Create(i, this[i] ?? _objects[i]!));

    public unsafe CutsceneService(IObjectTable objects, GameEventManager events)
    {
        _objects                    =  objects;
        _events                     =  events;
        _events.CopyCharacter       += OnCharacterCopy;
        _events.CharacterDestructor += OnCharacterDestructor;
    }

    /// <summary>
    /// Get the related actor to a cutscene actor.
    /// Does not check for valid input index.
    /// Returns null if no connected actor is set or the actor does not exist anymore.
    /// </summary>
    public Dalamud.Game.ClientState.Objects.Types.GameObject? this[int idx]
    {
        get
        {
            Debug.Assert(idx is >= CutsceneStartIdx and < CutsceneEndIdx);
            idx = _copiedCharacters[idx - CutsceneStartIdx];
            return idx < 0 ? null : _objects[idx];
        }
    }

    /// <summary> Return the currently set index of a parent or -1 if none is set or the index is invalid. </summary>
    public int GetParentIndex(int idx)
    {
        if (idx is >= CutsceneStartIdx and < CutsceneEndIdx)
            return _copiedCharacters[idx - CutsceneStartIdx];

        return -1;
    }

    public unsafe void Dispose()
    {
        _events.CopyCharacter       -= OnCharacterCopy;
        _events.CharacterDestructor -= OnCharacterDestructor;
    }

    private unsafe void OnCharacterDestructor(Character* character)
    {
        if (character->GameObject.ObjectIndex is < CutsceneStartIdx or >= CutsceneEndIdx)
            return;

        var idx = character->GameObject.ObjectIndex - CutsceneStartIdx;
        _copiedCharacters[idx] = -1;
    }

    private unsafe void OnCharacterCopy(Character* target, Character* source)
    {
        if (target == null || target->GameObject.ObjectIndex is < CutsceneStartIdx or >= CutsceneEndIdx)
            return;

        var idx = target->GameObject.ObjectIndex - CutsceneStartIdx;
        _copiedCharacters[idx] = (short)(source != null ? source->GameObject.ObjectIndex : -1);
    }
}

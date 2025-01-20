using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using OtterGui.Services;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Interop;
using Penumbra.Interop.Hooks.Objects;
using Penumbra.String;

namespace Penumbra.Interop.PathResolving;

public sealed class CutsceneService : IRequiredService, IDisposable
{
    public const int CutsceneStartIdx = (int)ScreenActor.CutsceneStart;
    public const int CutsceneEndIdx   = (int)ScreenActor.CutsceneEnd;
    public const int CutsceneSlots    = CutsceneEndIdx - CutsceneStartIdx;

    private readonly ObjectManager              _objects;
    private readonly CopyCharacter              _copyCharacter;
    private readonly CharacterDestructor        _characterDestructor;
    private readonly ConstructCutsceneCharacter _constructCutsceneCharacter;
    private readonly short[]                    _copiedCharacters = Enumerable.Repeat((short)-1, CutsceneSlots).ToArray();

    public IEnumerable<KeyValuePair<int, IGameObject>> Actors
        => Enumerable.Range(CutsceneStartIdx, CutsceneSlots)
            .Where(i => _objects[i].Valid)
            .Select(i => KeyValuePair.Create(i, this[i] ?? _objects.GetDalamudObject(i)!));

    public unsafe CutsceneService(ObjectManager objects, CopyCharacter copyCharacter, CharacterDestructor characterDestructor,
        ConstructCutsceneCharacter constructCutsceneCharacter, IClientState clientState)
    {
        _objects                    = objects;
        _copyCharacter              = copyCharacter;
        _characterDestructor        = characterDestructor;
        _constructCutsceneCharacter = constructCutsceneCharacter;
        _copyCharacter.Subscribe(OnCharacterCopy, CopyCharacter.Priority.CutsceneService);
        _characterDestructor.Subscribe(OnCharacterDestructor, CharacterDestructor.Priority.CutsceneService);
        _constructCutsceneCharacter.Subscribe(OnSetupPlayerNpc, ConstructCutsceneCharacter.Priority.CutsceneService);
        if (clientState.IsGPosing)
            RecoverGPoseActors();
    }


    /// <summary>
    /// Get the related actor to a cutscene actor.
    /// Does not check for valid input index.
    /// Returns null if no connected actor is set or the actor does not exist anymore.
    /// </summary>
    private IGameObject? this[int idx]
    {
        get
        {
            Debug.Assert(idx is >= CutsceneStartIdx and < CutsceneEndIdx);
            idx = _copiedCharacters[idx - CutsceneStartIdx];
            return idx < 0 ? null : _objects.GetDalamudObject(idx);
        }
    }

    /// <summary> Return the currently set index of a parent or -1 if none is set or the index is invalid. </summary>
    public int GetParentIndex(int idx)
        => GetParentIndex((ushort)idx);

    public bool SetParentIndex(int copyIdx, int parentIdx)
    {
        if (copyIdx is < CutsceneStartIdx or >= CutsceneEndIdx)
            return false;

        if (parentIdx is < -1 or >= CutsceneEndIdx)
            return false;

        if (!_objects[copyIdx].Valid)
            return false;

        if (parentIdx != -1 && !_objects[parentIdx].Valid)
            return false;

        _copiedCharacters[copyIdx - CutsceneStartIdx] = (short)parentIdx;
        return true;
    }

    public short GetParentIndex(ushort idx)
    {
        if (idx is >= CutsceneStartIdx and < CutsceneEndIdx)
            return _copiedCharacters[idx - CutsceneStartIdx];

        return -1;
    }

    public unsafe void Dispose()
    {
        _copyCharacter.Unsubscribe(OnCharacterCopy);
        _characterDestructor.Unsubscribe(OnCharacterDestructor);
        _constructCutsceneCharacter.Unsubscribe(OnSetupPlayerNpc);
    }

    private unsafe void OnCharacterDestructor(Character* character)
    {
        if (character->GameObject.ObjectIndex < CutsceneStartIdx)
        {
            // Remove all associations for now non-existing actor.
            for (var i = 0; i < _copiedCharacters.Length; ++i)
            {
                if (_copiedCharacters[i] == character->GameObject.ObjectIndex)
                {
                    // A hack to deal with GPose actors leaving and thus losing the link, we just set the home world instead.
                    // I do not think this breaks anything?
                    var address = _objects[i + CutsceneStartIdx];
                    if (address.IsPlayer)
                        address.AsCharacter->HomeWorld = character->HomeWorld;

                    _copiedCharacters[i] = -1;
                }
            }
        }
        else if (character->GameObject.ObjectIndex < CutsceneEndIdx)
        {
            var idx = character->GameObject.ObjectIndex - CutsceneStartIdx;
            _copiedCharacters[idx] = -1;
        }
    }

    private unsafe void OnCharacterCopy(Character* target, Character* source)
    {
        if (target == null || target->GameObject.ObjectIndex is < CutsceneStartIdx or >= CutsceneEndIdx)
            return;

        var idx = target->GameObject.ObjectIndex - CutsceneStartIdx;
        _copiedCharacters[idx] = (short)(source != null ? source->GameObject.ObjectIndex : -1);
    }

    private unsafe void OnSetupPlayerNpc(Character* npc)
    {
        if (npc == null || npc->ObjectIndex is < CutsceneStartIdx or >= CutsceneEndIdx)
            return;

        var idx = npc->GameObject.ObjectIndex - CutsceneStartIdx;
        _copiedCharacters[idx] = 0;
    }

    /// <summary> Try to recover GPose actors on reloads into a running game. </summary>
    /// <remarks> This is not 100% accurate due to world IDs, minions etc., but will be mostly sane. </remarks>
    private void RecoverGPoseActors()
    {
        Dictionary<ByteString, short>? actors = null;

        for (var i = CutsceneStartIdx; i < CutsceneEndIdx; ++i)
        {
            if (!TryGetName(i, out var name))
                continue;

            if ((actors ??= CreateActors()).TryGetValue(name, out var idx))
                _copiedCharacters[i - CutsceneStartIdx] = idx;
        }

        return;

        bool TryGetName(int idx, out ByteString name)
        {
            name = ByteString.Empty;
            var address = _objects[idx];
            if (!address.Valid)
                return false;

            name = address.Utf8Name;
            return !name.IsEmpty;
        }

        Dictionary<ByteString, short> CreateActors()
        {
            var ret = new Dictionary<ByteString, short>();
            for (short i = 0; i < CutsceneStartIdx; ++i)
            {
                if (TryGetName(i, out var name))
                    ret.TryAdd(name, i);
            }

            return ret;
        }
    }
}

using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using OtterGui.Services;
using Penumbra.GameData.Enums;
using Penumbra.Interop.Hooks.Objects;
using Penumbra.String;

namespace Penumbra.Interop.PathResolving;

public sealed class CutsceneService : IService, IDisposable
{
    public const int CutsceneStartIdx = (int)ScreenActor.CutsceneStart;
    public const int CutsceneEndIdx   = (int)ScreenActor.CutsceneEnd;
    public const int CutsceneSlots    = CutsceneEndIdx - CutsceneStartIdx;

    private readonly IObjectTable        _objects;
    private readonly CopyCharacter       _copyCharacter;
    private readonly CharacterDestructor _characterDestructor;
    private readonly short[]             _copiedCharacters = Enumerable.Repeat((short)-1, CutsceneSlots).ToArray();

    public IEnumerable<KeyValuePair<int, Dalamud.Game.ClientState.Objects.Types.GameObject>> Actors
        => Enumerable.Range(CutsceneStartIdx, CutsceneSlots)
            .Where(i => _objects[i] != null)
            .Select(i => KeyValuePair.Create(i, this[i] ?? _objects[i]!));

    public unsafe CutsceneService(IObjectTable objects, CopyCharacter copyCharacter, CharacterDestructor characterDestructor,
        IClientState clientState)
    {
        _objects             = objects;
        _copyCharacter       = copyCharacter;
        _characterDestructor = characterDestructor;
        _copyCharacter.Subscribe(OnCharacterCopy, CopyCharacter.Priority.CutsceneService);
        _characterDestructor.Subscribe(OnCharacterDestructor, CharacterDestructor.Priority.CutsceneService);
        if (clientState.IsGPosing)
            RecoverGPoseActors();
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
        => GetParentIndex((ushort)idx);

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
                    var address = (GameObject*)_objects.GetObjectAddress(i + CutsceneStartIdx);
                    if (address != null && address->GetObjectKind() is (byte)ObjectKind.Pc)
                        ((Character*)address)->HomeWorld = character->HomeWorld;

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

    /// <summary> Try to recover GPose actors on reloads into a running game. </summary>
    /// <remarks> This is not 100% accurate due to world IDs, minions etc., but will be mostly sane. </remarks>
    private unsafe void RecoverGPoseActors()
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
            var address = (GameObject*)_objects.GetObjectAddress(idx);
            if (address == null)
                return false;

            name = new ByteString(address->Name);
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

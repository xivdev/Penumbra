using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using Penumbra.GameData.Interop;
using Object = FFXIVClientStructs.FFXIV.Client.Graphics.Scene.Object;
using Penumbra.GameData.Structs;
using Penumbra.Interop.Hooks.Objects;

namespace Penumbra.Interop.PathResolving;

public sealed class DrawObjectState : IDisposable, IReadOnlyDictionary<Model, (Actor, ObjectIndex, bool)>, Luna.IService
{
    private readonly ObjectManager           _objects;
    private readonly CreateCharacterBase     _createCharacterBase;
    private readonly WeaponReload            _weaponReload;
    private readonly CharacterBaseDestructor _characterBaseDestructor;
    private readonly CharacterDestructor     _characterDestructor;
    private readonly GameState               _gameState;

    private readonly Dictionary<Model, (Actor GameObject, ObjectIndex Index, bool IsChild)> _drawObjectToGameObject = [];

    public nint LastGameObject
        => _gameState.LastGameObject;

    public unsafe DrawObjectState(ObjectManager objects, CreateCharacterBase createCharacterBase, WeaponReload weaponReload,
        CharacterBaseDestructor characterBaseDestructor, GameState gameState, IFramework framework, CharacterDestructor characterDestructor)
    {
        _objects                 = objects;
        _createCharacterBase     = createCharacterBase;
        _weaponReload            = weaponReload;
        _characterBaseDestructor = characterBaseDestructor;
        _gameState               = gameState;
        _characterDestructor     = characterDestructor;
        framework.RunOnFrameworkThread(InitializeDrawObjects);

        _weaponReload.Subscribe(OnWeaponReloading, WeaponReload.Priority.DrawObjectState);
        _weaponReload.Subscribe(OnWeaponReloaded,  WeaponReload.PostEvent.Priority.DrawObjectState);
        _createCharacterBase.Subscribe(OnCharacterBaseCreated, CreateCharacterBase.PostEvent.Priority.DrawObjectState);
        _characterBaseDestructor.Subscribe(OnCharacterBaseDestructor, CharacterBaseDestructor.Priority.DrawObjectState);
        _characterDestructor.Subscribe(OnCharacterDestructor, CharacterDestructor.Priority.DrawObjectState);
    }

    public bool ContainsKey(Model key)
        => _drawObjectToGameObject.ContainsKey(key);

    public IEnumerator<KeyValuePair<Model, (Actor, ObjectIndex, bool)>> GetEnumerator()
        => _drawObjectToGameObject.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public int Count
        => _drawObjectToGameObject.Count;

    public bool TryGetValue(Model drawObject, out (Actor, ObjectIndex, bool) gameObject)
    {
        if (!_drawObjectToGameObject.TryGetValue(drawObject, out gameObject))
            return false;

        var currentObject = _objects[gameObject.Item2];
        if (currentObject != gameObject.Item1)
        {
            Penumbra.Log.Warning($"[DrawObjectState] Stored association {drawObject} -> {gameObject.Item1} has index {gameObject.Item2}, which resolves to {currentObject}.");
            return false;
        }

        return true;
    }

    public (Actor, ObjectIndex, bool) this[Model key]
        => _drawObjectToGameObject[key];

    public IEnumerable<Model> Keys
        => _drawObjectToGameObject.Keys;

    public IEnumerable<(Actor, ObjectIndex, bool)> Values
        => _drawObjectToGameObject.Values;

    public unsafe void Dispose()
    {
        _weaponReload.Unsubscribe(OnWeaponReloading);
        _weaponReload.Unsubscribe(OnWeaponReloaded);
        _createCharacterBase.Unsubscribe(OnCharacterBaseCreated);
        _characterBaseDestructor.Unsubscribe(OnCharacterBaseDestructor);
        _characterDestructor.Unsubscribe(OnCharacterDestructor);
    }

    /// <remarks>
    /// Seems like sometimes the draw object of a game object is destroyed in frames after the original game object is already destroyed.
    /// So protect against outdated game object pointers in the dictionary.
    /// </remarks>
    private unsafe void OnCharacterDestructor(Character* a)
    {
        if (a is null)
            return;

        var character = (nint)a;
        var delete    = stackalloc nint[5];
        var current   = 0;
        foreach (var (drawObject, (gameObject, _, _)) in _drawObjectToGameObject)
        {
            if (gameObject != character)
                continue;

            delete[current++] = drawObject;
            if (current is 4)
                break;
        }

        for (var ptr = delete; *ptr != nint.Zero; ++ptr)
        {
            _drawObjectToGameObject.Remove(*ptr, out var pair);
            Penumbra.Log.Excessive(
                $"[DrawObjectState] Removed draw object 0x{*ptr:X} -> 0x{(nint)a:X} (actual: 0x{pair.GameObject.Address:X}, {pair.IsChild}).");
        }
    }

    private unsafe void OnWeaponReloading(DrawDataContainer* _, Character* character, CharacterWeapon* _2)
        => _gameState.QueueGameObject((nint)character);

    private unsafe void OnWeaponReloaded(DrawDataContainer* _, Character* character)
    {
        _gameState.DequeueGameObject();
        IterateDrawObjectTree((Object*)character->GameObject.DrawObject, (nint)character, false, false);
    }

    private unsafe void OnCharacterBaseDestructor(CharacterBase* characterBase)
        => _drawObjectToGameObject.Remove((nint)characterBase);

    private unsafe void OnCharacterBaseCreated(ModelCharaId modelCharaId, CustomizeArray* customize, CharacterArmor* equipment,
        CharacterBase* drawObject)
    {
        Actor gameObject = LastGameObject;
        if (gameObject.Valid)
            _drawObjectToGameObject[(nint)drawObject] = (gameObject, gameObject.Index, false);
    }

    /// <summary>
    /// Find all current DrawObjects used in the GameObject table.
    /// We do not iterate the Dalamud table because it does not work when not logged in.
    /// </summary>
    private unsafe void InitializeDrawObjects()
    {
        foreach (var actor in _objects)
        {
            if (actor is { IsCharacter: true, Model.Valid: true })
                IterateDrawObjectTree((Object*)actor.Model.Address, actor, false, false);
        }
    }

    private unsafe void IterateDrawObjectTree(Object* drawObject, Actor gameObject, bool isChild, bool iterate)
    {
        if (drawObject == null)
            return;

        _drawObjectToGameObject[drawObject] = (gameObject, gameObject.Index, isChild);
        IterateDrawObjectTree(drawObject->ChildObject, gameObject, true, true);
        if (!iterate)
            return;

        var nextSibling = drawObject->NextSiblingObject;
        while (nextSibling != null && nextSibling != drawObject)
        {
            IterateDrawObjectTree(nextSibling, gameObject, true, false);
            nextSibling = nextSibling->NextSiblingObject;
        }

        var prevSibling = drawObject->PreviousSiblingObject;
        while (prevSibling != null && prevSibling != drawObject)
        {
            IterateDrawObjectTree(prevSibling, gameObject, true, false);
            prevSibling = prevSibling->PreviousSiblingObject;
        }
    }
}

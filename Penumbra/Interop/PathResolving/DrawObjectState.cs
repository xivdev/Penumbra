using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using OtterGui.Services;
using Penumbra.GameData.Interop;
using Object = FFXIVClientStructs.FFXIV.Client.Graphics.Scene.Object;
using Penumbra.GameData.Structs;
using Penumbra.Interop.Hooks.Objects;

namespace Penumbra.Interop.PathResolving;

public sealed class DrawObjectState : IDisposable, IReadOnlyDictionary<nint, (nint, bool)>, IService
{
    private readonly ObjectManager           _objects;
    private readonly CreateCharacterBase     _createCharacterBase;
    private readonly WeaponReload            _weaponReload;
    private readonly CharacterBaseDestructor _characterBaseDestructor;
    private readonly GameState               _gameState;

    private readonly Dictionary<nint, (nint GameObject, bool IsChild)> _drawObjectToGameObject = [];

    public nint LastGameObject
        => _gameState.LastGameObject;

    public unsafe DrawObjectState(ObjectManager objects, CreateCharacterBase createCharacterBase, WeaponReload weaponReload,
        CharacterBaseDestructor characterBaseDestructor, GameState gameState, IFramework framework)
    {
        _objects                 = objects;
        _createCharacterBase     = createCharacterBase;
        _weaponReload            = weaponReload;
        _characterBaseDestructor = characterBaseDestructor;
        _gameState               = gameState;
        framework.RunOnFrameworkThread(InitializeDrawObjects);

        _weaponReload.Subscribe(OnWeaponReloading, WeaponReload.Priority.DrawObjectState);
        _weaponReload.Subscribe(OnWeaponReloaded,  WeaponReload.PostEvent.Priority.DrawObjectState);
        _createCharacterBase.Subscribe(OnCharacterBaseCreated, CreateCharacterBase.PostEvent.Priority.DrawObjectState);
        _characterBaseDestructor.Subscribe(OnCharacterBaseDestructor, CharacterBaseDestructor.Priority.DrawObjectState);
    }

    public bool ContainsKey(nint key)
        => _drawObjectToGameObject.ContainsKey(key);

    public IEnumerator<KeyValuePair<nint, (nint, bool)>> GetEnumerator()
        => _drawObjectToGameObject.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public int Count
        => _drawObjectToGameObject.Count;

    public bool TryGetValue(nint drawObject, out (nint, bool) gameObject)
        => _drawObjectToGameObject.TryGetValue(drawObject, out gameObject);

    public (nint, bool) this[nint key]
        => _drawObjectToGameObject[key];

    public IEnumerable<nint> Keys
        => _drawObjectToGameObject.Keys;

    public IEnumerable<(nint, bool)> Values
        => _drawObjectToGameObject.Values;

    public unsafe void Dispose()
    {
        _weaponReload.Unsubscribe(OnWeaponReloading);
        _weaponReload.Unsubscribe(OnWeaponReloaded);
        _createCharacterBase.Unsubscribe(OnCharacterBaseCreated);
        _characterBaseDestructor.Unsubscribe(OnCharacterBaseDestructor);
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
        var gameObject = LastGameObject;
        if (gameObject != nint.Zero)
            _drawObjectToGameObject[(nint)drawObject] = (gameObject, false);
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

    private unsafe void IterateDrawObjectTree(Object* drawObject, nint gameObject, bool isChild, bool iterate)
    {
        if (drawObject == null)
            return;

        _drawObjectToGameObject[(nint)drawObject] = (gameObject, isChild);
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

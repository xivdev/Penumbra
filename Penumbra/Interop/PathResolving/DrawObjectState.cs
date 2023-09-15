using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Penumbra.GameData;
using Penumbra.Interop.Services;
using Object = FFXIVClientStructs.FFXIV.Client.Graphics.Scene.Object;

namespace Penumbra.Interop.PathResolving;

public class DrawObjectState : IDisposable, IReadOnlyDictionary<nint, (nint, bool)>
{
    private readonly IObjectTable      _objects;
    private readonly GameEventManager _gameEvents;

    private readonly Dictionary<nint, (nint GameObject, bool IsChild)> _drawObjectToGameObject = new();

    private readonly ThreadLocal<Queue<nint>> _lastGameObject = new(() => new Queue<nint>());

    public nint LastGameObject
        => _lastGameObject.IsValueCreated && _lastGameObject.Value!.Count > 0 ? _lastGameObject.Value.Peek() : nint.Zero;

    public DrawObjectState(IObjectTable objects, GameEventManager gameEvents)
    {
        SignatureHelper.Initialise(this);
        _enableDrawHook.Enable();
        _objects                            =  objects;
        _gameEvents                         =  gameEvents;
        _gameEvents.WeaponReloading         += OnWeaponReloading;
        _gameEvents.WeaponReloaded          += OnWeaponReloaded;
        _gameEvents.CharacterBaseCreated    += OnCharacterBaseCreated;
        _gameEvents.CharacterBaseDestructor += OnCharacterBaseDestructor;
        InitializeDrawObjects();
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

    public void Dispose()
    {
        _gameEvents.WeaponReloading         -= OnWeaponReloading;
        _gameEvents.WeaponReloaded          -= OnWeaponReloaded;
        _gameEvents.CharacterBaseCreated    -= OnCharacterBaseCreated;
        _gameEvents.CharacterBaseDestructor -= OnCharacterBaseDestructor;
        _enableDrawHook.Dispose();
    }

    private void OnWeaponReloading(nint _, nint gameObject)
        => _lastGameObject.Value!.Enqueue(gameObject);

    private unsafe void OnWeaponReloaded(nint _, nint gameObject)
    {
        _lastGameObject.Value!.Dequeue();
        IterateDrawObjectTree((Object*) ((GameObject*) gameObject)->DrawObject, gameObject, false, false);
    }

    private void OnCharacterBaseDestructor(nint characterBase)
        => _drawObjectToGameObject.Remove(characterBase);

    private void OnCharacterBaseCreated(uint modelCharaId, nint customize, nint equipment, nint drawObject)
    {
        var gameObject = LastGameObject;
        if (gameObject != nint.Zero)
            _drawObjectToGameObject[drawObject] = (gameObject, false);
    }

    /// <summary>
    /// Find all current DrawObjects used in the GameObject table.
    /// We do not iterate the Dalamud table because it does not work when not logged in.
    /// </summary>
    private unsafe void InitializeDrawObjects()
    {
        for (var i = 0; i < _objects.Length; ++i)
        {
            var ptr = (GameObject*)_objects.GetObjectAddress(i);
            if (ptr != null && ptr->IsCharacter() && ptr->DrawObject != null)
                IterateDrawObjectTree(&ptr->DrawObject->Object, (nint)ptr, false, false);
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

    /// <summary>
    /// EnableDraw is what creates DrawObjects for gameObjects,
    /// so we always keep track of the current GameObject to be able to link it to the DrawObject.
    /// </summary>
    private delegate void EnableDrawDelegate(nint gameObject);

    [Signature(Sigs.EnableDraw, DetourName = nameof(EnableDrawDetour))]
    private readonly Hook<EnableDrawDelegate> _enableDrawHook = null!;

    private void EnableDrawDetour(nint gameObject)
    {
        _lastGameObject.Value!.Enqueue(gameObject);
        _enableDrawHook.Original.Invoke(gameObject);
        _lastGameObject.Value!.TryDequeue(out _);
    }
}

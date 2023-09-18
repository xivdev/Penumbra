using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using Penumbra.GameData;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Penumbra.Interop.Structs;

namespace Penumbra.Interop.Services;

public unsafe class GameEventManager : IDisposable
{
    private const string Prefix = $"[{nameof(GameEventManager)}]";

    public event CharacterDestructorEvent?      CharacterDestructor;
    public event CopyCharacterEvent?            CopyCharacter;
    public event ResourceHandleDestructorEvent? ResourceHandleDestructor;
    public event CreatingCharacterBaseEvent?    CreatingCharacterBase;
    public event CharacterBaseCreatedEvent?     CharacterBaseCreated;
    public event CharacterBaseDestructorEvent?  CharacterBaseDestructor;
    public event WeaponReloadingEvent?          WeaponReloading;
    public event WeaponReloadedEvent?           WeaponReloaded;

    public GameEventManager()
    {
        SignatureHelper.Initialise(this);
        _characterDtorHook.Enable();
        _copyCharacterHook.Enable();
        _resourceHandleDestructorHook.Enable();
        _characterBaseCreateHook.Enable();
        _characterBaseDestructorHook.Enable();
        _weaponReloadHook.Enable();
        EnableDebugHook();
        Penumbra.Log.Verbose($"{Prefix} Created.");
    }

    public void Dispose()
    {
        _characterDtorHook.Dispose();
        _copyCharacterHook.Dispose();
        _resourceHandleDestructorHook.Dispose();
        _characterBaseCreateHook.Dispose();
        _characterBaseDestructorHook.Dispose();
        _weaponReloadHook.Dispose();
        DisposeDebugHook();
        Penumbra.Log.Verbose($"{Prefix} Disposed.");
    }

    #region Character Destructor

    private delegate void CharacterDestructorDelegate(Character* character);

    [Signature(Sigs.CharacterDestructor, DetourName = nameof(CharacterDestructorDetour))]
    private readonly Hook<CharacterDestructorDelegate> _characterDtorHook = null!;

    private void CharacterDestructorDetour(Character* character)
    {
        if (CharacterDestructor != null)
            foreach (var subscriber in CharacterDestructor.GetInvocationList())
            {
                try
                {
                    ((CharacterDestructorEvent)subscriber).Invoke(character);
                }
                catch (Exception ex)
                {
                    Penumbra.Log.Error($"{Prefix} Error in {nameof(CharacterDestructor)} event when executing {subscriber.Method.Name}:\n{ex}");
                }
            }

        Penumbra.Log.Verbose($"{Prefix} {nameof(CharacterDestructor)} triggered with 0x{(nint)character:X}.");
        _characterDtorHook.Original(character);
    }

    public delegate void CharacterDestructorEvent(Character* character);

    #endregion

    #region Copy Character

    private delegate ulong CopyCharacterDelegate(GameObject* target, GameObject* source, uint unk);

    [Signature(Sigs.CopyCharacter, DetourName = nameof(CopyCharacterDetour))]
    private readonly Hook<CopyCharacterDelegate> _copyCharacterHook = null!;

    private ulong CopyCharacterDetour(GameObject* target, GameObject* source, uint unk)
    {
        if (CopyCharacter != null)
            foreach (var subscriber in CopyCharacter.GetInvocationList())
            {
                try
                {
                    ((CopyCharacterEvent)subscriber).Invoke((Character*)target, (Character*)source);
                }
                catch (Exception ex)
                {
                    Penumbra.Log.Error(
                        $"{Prefix} Error in {nameof(CopyCharacter)} event when executing {subscriber.Method.Name}:\n{ex}");
                }
            }

        Penumbra.Log.Verbose(
            $"{Prefix} {nameof(CopyCharacter)} triggered with target 0x{(nint)target:X} and source 0x{(nint)source:X}.");
        return _copyCharacterHook.Original(target, source, unk);
    }

    public delegate void CopyCharacterEvent(Character* target, Character* source);

    #endregion

    #region ResourceHandle Destructor

    private delegate IntPtr ResourceHandleDestructorDelegate(ResourceHandle* handle);

    [Signature(Sigs.ResourceHandleDestructor, DetourName = nameof(ResourceHandleDestructorDetour))]
    private readonly Hook<ResourceHandleDestructorDelegate> _resourceHandleDestructorHook = null!;

    private IntPtr ResourceHandleDestructorDetour(ResourceHandle* handle)
    {
        if (ResourceHandleDestructor != null)
            foreach (var subscriber in ResourceHandleDestructor.GetInvocationList())
            {
                try
                {
                    ((ResourceHandleDestructorEvent)subscriber).Invoke(handle);
                }
                catch (Exception ex)
                {
                    Penumbra.Log.Error(
                        $"{Prefix} Error in {nameof(ResourceHandleDestructor)} event when executing {subscriber.Method.Name}:\n{ex}");
                }
            }

        Penumbra.Log.Excessive($"{Prefix} {nameof(ResourceHandleDestructor)} triggered with 0x{(nint)handle:X}.");
        return _resourceHandleDestructorHook!.Original(handle);
    }

    public delegate void ResourceHandleDestructorEvent(ResourceHandle* handle);

    #endregion

    #region CharacterBaseCreate

    private delegate nint CharacterBaseCreateDelegate(uint a, nint b, nint c, byte d);

    [Signature(Sigs.CharacterBaseCreate, DetourName = nameof(CharacterBaseCreateDetour))]
    private readonly Hook<CharacterBaseCreateDelegate> _characterBaseCreateHook = null!;

    private nint CharacterBaseCreateDetour(uint a, nint b, nint c, byte d)
    {
        if (CreatingCharacterBase != null)
            foreach (var subscriber in CreatingCharacterBase.GetInvocationList())
            {
                try
                {
                    ((CreatingCharacterBaseEvent)subscriber).Invoke((nint)(&a), b, c);
                }
                catch (Exception ex)
                {
                    Penumbra.Log.Error(
                        $"{Prefix} Error in {nameof(CharacterBaseCreateDetour)} event when executing {subscriber.Method.Name}:\n{ex}");
                }
            }

        var ret = _characterBaseCreateHook.Original(a, b, c, d);
        if (CharacterBaseCreated != null)
            foreach (var subscriber in CharacterBaseCreated.GetInvocationList())
            {
                try
                {
                    ((CharacterBaseCreatedEvent)subscriber).Invoke(a, b, c, ret);
                }
                catch (Exception ex)
                {
                    Penumbra.Log.Error(
                        $"{Prefix} Error in {nameof(CharacterBaseCreateDetour)} event when executing {subscriber.Method.Name}:\n{ex}");
                }
            }

        return ret;
    }

    public delegate void CreatingCharacterBaseEvent(nint modelCharaId, nint customize, nint equipment);
    public delegate void CharacterBaseCreatedEvent(uint modelCharaId, nint customize, nint equipment, nint drawObject);

    #endregion

    #region CharacterBase Destructor

    public delegate void CharacterBaseDestructorEvent(nint drawBase);

    [Signature(Sigs.CharacterBaseDestructor, DetourName = nameof(CharacterBaseDestructorDetour))]
    private readonly Hook<CharacterBaseDestructorEvent> _characterBaseDestructorHook = null!;

    private void CharacterBaseDestructorDetour(IntPtr drawBase)
    {
        if (CharacterBaseDestructor != null)
            foreach (var subscriber in CharacterBaseDestructor.GetInvocationList())
            {
                try
                {
                    ((CharacterBaseDestructorEvent)subscriber).Invoke(drawBase);
                }
                catch (Exception ex)
                {
                    Penumbra.Log.Error(
                        $"{Prefix} Error in {nameof(CharacterBaseDestructorDetour)} event when executing {subscriber.Method.Name}:\n{ex}");
                }
            }

        _characterBaseDestructorHook.Original.Invoke(drawBase);
    }

    #endregion

    #region Weapon Reload

    private delegate void WeaponReloadFunc(nint a1, uint a2, nint a3, byte a4, byte a5, byte a6, byte a7);

    [Signature(Sigs.WeaponReload, DetourName = nameof(WeaponReloadDetour))]
    private readonly Hook<WeaponReloadFunc> _weaponReloadHook = null!;

    private void WeaponReloadDetour(nint a1, uint a2, nint a3, byte a4, byte a5, byte a6, byte a7)
    {
        var gameObject = *(nint*)(a1 + 8);
        if (WeaponReloading != null)
            foreach (var subscriber in WeaponReloading.GetInvocationList())
            {
                try
                {
                    ((WeaponReloadingEvent)subscriber).Invoke(a1, gameObject);
                }
                catch (Exception ex)
                {
                    Penumbra.Log.Error(
                        $"{Prefix} Error in {nameof(WeaponReloadDetour)} event when executing {subscriber.Method.Name}:\n{ex}");
                }
            }

        _weaponReloadHook.Original(a1, a2, a3, a4, a5, a6, a7);

        if (WeaponReloaded != null)
            foreach (var subscriber in WeaponReloaded.GetInvocationList())
            {
                try
                {
                    ((WeaponReloadedEvent)subscriber).Invoke(a1, gameObject);
                }
                catch (Exception ex)
                {
                    Penumbra.Log.Error(
                        $"{Prefix} Error in {nameof(WeaponReloadDetour)} event when executing {subscriber.Method.Name}:\n{ex}");
                }
            }
    }

    public delegate void WeaponReloadingEvent(nint drawDataContainer, nint gameObject);
    public delegate void WeaponReloadedEvent(nint drawDataContainer, nint gameObject);

    #endregion

    #region Testing

#if DEBUG
    //[Signature("48 89 5C 24 ?? 48 89 74 24 ?? 89 54 24 ?? 57 48 83 EC ?? 48 8B F9", DetourName = nameof(TestDetour))]
    private readonly Hook<TestDelegate>? _testHook = null;

    private delegate void TestDelegate(nint a1, int a2);

    private void TestDetour(nint a1, int a2)
    {
        Penumbra.Log.Information($"Test: {a1:X} {a2}");
        _testHook!.Original(a1, a2);
    }

    private void EnableDebugHook()
        => _testHook?.Enable();

    private void DisposeDebugHook()
        => _testHook?.Dispose();
#else
    private void EnableDebugHook()
    { }

    private void DisposeDebugHook()
    { }
#endif

    #endregion
}

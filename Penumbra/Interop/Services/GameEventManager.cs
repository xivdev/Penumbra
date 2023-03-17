using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using Penumbra.GameData;
using System;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using OtterGui.Log;
using Penumbra.Interop.Structs;

namespace Penumbra.Interop.Services;

public unsafe class GameEventManager : IDisposable
{
    private const string Prefix = $"[{nameof(GameEventManager)}]";

    public GameEventManager()
    {
        SignatureHelper.Initialise(this);
        _characterDtorHook.Enable();
        _copyCharacterHook.Enable();
        _resourceHandleDestructorHook.Enable();
        Penumbra.Log.Verbose($"{Prefix} Created.");
    }

    public void Dispose()
    {
        _characterDtorHook.Dispose();
        _copyCharacterHook.Dispose();
        _resourceHandleDestructorHook.Dispose();
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
    public event CharacterDestructorEvent? CharacterDestructor;

    #endregion

    #region Copy Character

    private unsafe delegate ulong CopyCharacterDelegate(GameObject* target, GameObject* source, uint unk);

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
    public event CopyCharacterEvent? CopyCharacter;

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

        Penumbra.Log.Verbose($"{Prefix} {nameof(ResourceHandleDestructor)} triggered with 0x{(nint)handle:X}.");
        return _resourceHandleDestructorHook!.Original(handle);
    }

    public delegate void ResourceHandleDestructorEvent(ResourceHandle* handle);
    public event ResourceHandleDestructorEvent? ResourceHandleDestructor;

    #endregion
}

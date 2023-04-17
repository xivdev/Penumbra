using System;
using System.Runtime.CompilerServices;
using Dalamud.Data;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.GameData;
using Penumbra.Interop.Services;
using Penumbra.Interop.Structs;
using Penumbra.Meta.Files;
using Penumbra.Services;
using ResidentResourceManager = Penumbra.Interop.Services.ResidentResourceManager;

namespace Penumbra.Meta;

public unsafe class MetaFileManager
{
    internal readonly Configuration           Config;
    internal readonly CharacterUtility        CharacterUtility;
    internal readonly ResidentResourceManager ResidentResources;
    internal readonly DataManager             GameData;
    internal readonly ActiveCollections       ActiveCollections;
    internal readonly ValidityChecker         ValidityChecker;
    internal readonly IdentifierService       Identifier;

    public MetaFileManager(CharacterUtility characterUtility, ResidentResourceManager residentResources, DataManager gameData,
        ActiveCollections activeCollections, Configuration config, ValidityChecker validityChecker, IdentifierService identifier)
    {
        CharacterUtility  = characterUtility;
        ResidentResources = residentResources;
        GameData          = gameData;
        ActiveCollections = activeCollections;
        Config            = config;
        ValidityChecker   = validityChecker;
        Identifier        = identifier;
        SignatureHelper.Initialise(this);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void SetFile(MetaBaseFile? file, MetaIndex metaIndex)
    {
        if (file == null)
            CharacterUtility.ResetResource(metaIndex);
        else
            CharacterUtility.SetResource(metaIndex, (nint)file.Data, file.Length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public CharacterUtility.MetaList.MetaReverter TemporarilySetFile(MetaBaseFile? file, MetaIndex metaIndex)
        => file == null
            ? CharacterUtility.TemporarilyResetResource(metaIndex)
            : CharacterUtility.TemporarilySetResource(metaIndex, (nint)file.Data, file.Length);

    public void ApplyDefaultFiles(ModCollection collection)
    {
        if (ActiveCollections.Default != collection || !CharacterUtility.Ready || !Config.EnableMods)
            return;

        ResidentResources.Reload();
        collection._cache?.Meta.SetFiles();
    }

    /// <summary>
    /// Allocate in the games space for file storage.
    /// We only need this if using any meta file.
    /// </summary>
    [Signature(Sigs.GetFileSpace)]
    private readonly nint _getFileSpaceAddress = nint.Zero;

    public IMemorySpace* GetFileSpace()
        => ((delegate* unmanaged<IMemorySpace*>)_getFileSpaceAddress)();

    public void* AllocateFileMemory(ulong length, ulong alignment = 0)
        => GetFileSpace()->Malloc(length, alignment);

    public void* AllocateFileMemory(int length, int alignment = 0)
        => AllocateFileMemory((ulong)length, (ulong)alignment);

    public void* AllocateDefaultMemory(ulong length, ulong alignment = 0)
        => GetFileSpace()->Malloc(length, alignment);

    public void* AllocateDefaultMemory(int length, int alignment = 0)
        => IMemorySpace.GetDefaultSpace()->Malloc((ulong)length, (ulong)alignment);

    public void Free(nint ptr, int length)
        => IMemorySpace.Free((void*)ptr, (ulong)length);
}

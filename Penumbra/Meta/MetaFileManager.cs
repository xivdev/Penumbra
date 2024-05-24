using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using OtterGui.Compression;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.GameData;
using Penumbra.GameData.Data;
using Penumbra.Import;
using Penumbra.Interop.Services;
using Penumbra.Interop.Structs;
using Penumbra.Meta.Files;
using Penumbra.Mods;
using Penumbra.Mods.Groups;
using Penumbra.Services;
using ResidentResourceManager = Penumbra.Interop.Services.ResidentResourceManager;

namespace Penumbra.Meta;

public unsafe class MetaFileManager
{
    internal readonly Configuration           Config;
    internal readonly CharacterUtility        CharacterUtility;
    internal readonly ResidentResourceManager ResidentResources;
    internal readonly IDataManager            GameData;
    internal readonly ActiveCollectionData    ActiveCollections;
    internal readonly ValidityChecker         ValidityChecker;
    internal readonly ObjectIdentification    Identifier;
    internal readonly FileCompactor           Compactor;
    internal readonly ImcChecker              ImcChecker;

    public MetaFileManager(CharacterUtility characterUtility, ResidentResourceManager residentResources, IDataManager gameData,
        ActiveCollectionData activeCollections, Configuration config, ValidityChecker validityChecker, ObjectIdentification identifier,
        FileCompactor compactor, IGameInteropProvider interop)
    {
        CharacterUtility  = characterUtility;
        ResidentResources = residentResources;
        GameData          = gameData;
        ActiveCollections = activeCollections;
        Config            = config;
        ValidityChecker   = validityChecker;
        Identifier        = identifier;
        Compactor         = compactor;
        ImcChecker        = new ImcChecker(this);
        interop.InitializeFromAttributes(this);
    }

    public void WriteAllTexToolsMeta(Mod mod)
    {
        try
        {
            TexToolsMeta.WriteTexToolsMeta(this, mod.Default.Manipulations, mod.ModPath);
            foreach (var group in mod.Groups)
            {
                if (group is not ITexToolsGroup texToolsGroup)
                    continue;

                var dir = ModCreator.NewOptionDirectory(mod.ModPath, group.Name, Config.ReplaceNonAsciiOnImport);
                if (!dir.Exists)
                    dir.Create();


                foreach (var option in texToolsGroup.OptionData)
                {
                    var optionDir = ModCreator.NewOptionDirectory(dir, option.Name, Config.ReplaceNonAsciiOnImport);
                    if (!optionDir.Exists)
                        optionDir.Create();

                    TexToolsMeta.WriteTexToolsMeta(this, option.Manipulations, optionDir);
                }
            }
        }
        catch (Exception e)
        {
            Penumbra.Log.Error($"Error writing TexToolsMeta:\n{e}");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void SetFile(MetaBaseFile? file, MetaIndex metaIndex)
    {
        if (file == null || !Config.EnableMods)
            CharacterUtility.ResetResource(metaIndex);
        else
            CharacterUtility.SetResource(metaIndex, (nint)file.Data, file.Length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public MetaList.MetaReverter TemporarilySetFile(MetaBaseFile? file, MetaIndex metaIndex)
        => Config.EnableMods
            ? file == null
                ? CharacterUtility.TemporarilyResetResource(metaIndex)
                : CharacterUtility.TemporarilySetResource(metaIndex, (nint)file.Data, file.Length)
            : MetaList.MetaReverter.Disabled;

    public void ApplyDefaultFiles(ModCollection? collection)
    {
        if (ActiveCollections.Default != collection || !CharacterUtility.Ready || !Config.EnableMods)
            return;

        ResidentResources.Reload();
        if (collection._cache == null)
            CharacterUtility.ResetAll();
        else
            collection._cache.Meta.SetFiles();
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

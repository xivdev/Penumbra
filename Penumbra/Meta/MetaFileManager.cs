using Dalamud.Plugin.Services;
using OtterGui.Compression;
using OtterGui.Services;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.GameData.Data;
using Penumbra.Import;
using Penumbra.Interop.Hooks.Meta;
using Penumbra.Interop.Services;
using Penumbra.Meta.Files;
using Penumbra.Mods;
using Penumbra.Mods.Groups;
using Penumbra.Services;
using ResidentResourceManager = Penumbra.Interop.Services.ResidentResourceManager;

namespace Penumbra.Meta;

public class MetaFileManager : IService
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
    internal readonly AtchManager             AtchManager;
    internal readonly IFileAllocator          MarshalAllocator = new MarshalAllocator();
    internal readonly IFileAllocator          XivFileAllocator;
    internal readonly IFileAllocator          XivDefaultAllocator;


    public MetaFileManager(CharacterUtility characterUtility, ResidentResourceManager residentResources, IDataManager gameData,
        ActiveCollectionData activeCollections, Configuration config, ValidityChecker validityChecker, ObjectIdentification identifier,
        FileCompactor compactor, IGameInteropProvider interop, AtchManager atchManager)
    {
        CharacterUtility    = characterUtility;
        ResidentResources   = residentResources;
        GameData            = gameData;
        ActiveCollections   = activeCollections;
        Config              = config;
        ValidityChecker     = validityChecker;
        Identifier          = identifier;
        Compactor           = compactor;
        AtchManager         = atchManager;
        ImcChecker          = new ImcChecker(this);
        XivFileAllocator    = new XivFileAllocator(interop);
        XivDefaultAllocator = new XivDefaultAllocator();
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

    public void ApplyDefaultFiles(ModCollection? collection)
    {
        if (ActiveCollections.Default != collection || !CharacterUtility.Ready || !Config.EnableMods)
            return;

        ResidentResources.Reload();
    }
}

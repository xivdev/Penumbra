using Luna;
using Penumbra.Files;
using Penumbra.UI.ModsTab.Selector;

namespace Penumbra.Mods.Manager;

public sealed class ModFileSystemSaver(
    LunaLogger log,
    BaseFileSystem fileSystem,
    SaveService saveService,
    ModStorage mods,
    LocalModDatabase localModDatabase)
    : FileSystemSaver<SaveService, FilenameService>(log, fileSystem, saveService)
{
    protected override string LockedFile(FilenameService provider)
        => provider.FileSystemLockedNodes;

    protected override string ExpandedFile(FilenameService provider)
        => provider.FileSystemExpandedFolders;

    protected override string EmptyFoldersMigrationFile(FilenameService provider)
        => provider.FileSystemEmptyFoldersMigration;

    protected override string SelectionFile(FilenameService provider)
        => provider.FileSystemSelectedNodes;

    protected override string OrganizationFile(FilenameService provider)
        => provider.FileSystemOrganization;

    protected override string MigrationFile(FilenameService provider)
        => provider.OldFilesystemFile;

    protected override bool GetValueFromIdentifier(ReadOnlySpan<char> identifier, [NotNullWhen(true)] out IFileSystemValue? value)
    {
        if (mods.TryGetMod(identifier, out var mod))
        {
            value = mod;
            return true;
        }

        value = null;
        return false;
    }

    protected override void CreateDataNodes()
    {
        foreach (var mod in mods)
        {
            try
            {
                var folder = mod.Path.Folder.Length is 0 ? FileSystem.Root : FileSystem.FindOrCreateAllFolders(mod.Path.Folder);
                FileSystem.CreateDuplicateDataNode(folder, mod.Path.SortName ?? mod.Name, mod);
            }
            catch (Exception e)
            {
                Log.Error($"Could not create folder structure for mod {mod.Name} at path {mod.Path.Folder}: {e}");
            }
        }
    }

    protected override ISortMode? ParseSortMode(string name)
        => ISortMode.Valid.GetValueOrDefault(name);

    protected override void SaveDataValue(IFileSystemValue value)
    {
        if (value is Mod mod)
            localModDatabase.UpsertPath(mod);
    }
}

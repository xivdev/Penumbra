using Luna;
using Penumbra.Services;

namespace Penumbra.Mods.Manager;

public sealed class ModFileSystemSaver(Logger log, BaseFileSystem fileSystem, SaveService saveService, ModStorage mods)
    : FileSystemSaver<SaveService, FilenameService>(log, fileSystem, saveService)
{
    protected override string LockedFile(FilenameService provider)
        => provider.FileSystemLockedNodes;

    protected override string ExpandedFile(FilenameService provider)
        => provider.FileSystemExpandedFolders;

    protected override string EmptyFoldersFile(FilenameService provider)
        => provider.FileSystemEmptyFolders;

    protected override string SelectionFile(FilenameService provider)
        => provider.FileSystemSelectedNodes;

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

    protected override void SaveDataValue(IFileSystemValue value)
    {
        if (value is Mod mod)
            SaveService.QueueSave(new ModLocalData(mod));
    }
}

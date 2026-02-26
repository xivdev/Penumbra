using Dalamud.Interface.ImGuiNotification;
using Luna;
using Penumbra.Communication;
using Penumbra.Services;

namespace Penumbra.Mods.Manager;

public sealed class ModFileSystem : BaseFileSystem, IDisposable, IRequiredService
{
    private readonly Configuration       _config;
    private readonly CommunicatorService _communicator;
    private readonly ModFileSystemSaver  _saver;

    public ModFileSystem(Configuration config, CommunicatorService communicator, SaveService saveService, Logger log, ModStorage modStorage)
        : base("ModFileSystem", log, true)
    {
        _config       = config;
        _communicator = communicator;
        _saver        = new ModFileSystemSaver(log, this, saveService, modStorage);
        _communicator.ModPathChanged.Subscribe(OnModPathChange, ModPathChanged.Priority.ModFileSystem);
        _communicator.ModDiscoveryFinished.Subscribe(_saver.Load, ModDiscoveryFinished.Priority.ModFileSystem);
        _communicator.ModDataChanged.Subscribe(OnModDataChange, ModDataChanged.Priority.ModFileSystem);
        _saver.Load();
    }

    public void Dispose()
    {
        _communicator.ModPathChanged.Unsubscribe(OnModPathChange);
        _communicator.ModDiscoveryFinished.Unsubscribe(_saver.Load);
        _communicator.ModDataChanged.Unsubscribe(OnModDataChange);
    }

    // Update sort order when defaulted mod names change.
    private void OnModDataChange(in ModDataChanged.Arguments arguments)
    {
        if (arguments.Type.HasFlag(ModDataChangeType.FileSystemFolder))
            RenameAndMoveWithDuplicates(arguments.Mod.Node!, arguments.Mod.Path.GetIntendedPath(arguments.Mod.Name));
        else if (arguments.Type.HasFlag(ModDataChangeType.Name) && arguments.Mod.Path.SortName is null
              || arguments.Type.HasFlag(ModDataChangeType.FileSystemSortOrder))
            RenameWithDuplicates(arguments.Mod.Node!, arguments.Mod.Path.GetIntendedName(arguments.Mod.Name));
    }

    // Update the filesystem if a mod has been added or removed.
    // Save it, if the mod directory has been moved, since this will change the save format.
    private void OnModPathChange(in ModPathChanged.Arguments arguments)
    {
        switch (arguments.Type)
        {
            case ModPathChangeType.Added:
                var parent = Root;
                if (_config.DefaultImportFolder.Length is not 0)
                    try
                    {
                        parent = FindOrCreateAllFolders(_config.DefaultImportFolder);
                    }
                    catch (Exception e)
                    {
                        Penumbra.Messager.NotificationMessage(e,
                            $"Could not move newly imported mod {arguments.Mod.Name} to default import folder {_config.DefaultImportFolder}.",
                            NotificationType.Warning);
                    }

                var (data, _) = CreateDuplicateDataNode(parent, arguments.Mod.Name, arguments.Mod);
                Selection.Select(data);
                break;
            case ModPathChangeType.Deleted:
                if (arguments.Mod.Node is { } node)
                {
                    // Unselect all because of event spaghetti.
                    // If two nodes are selected and one is deleted, the remaining one
                    // will try to fetch settings down the line and possibly break.
                    // Untangling the events is hard.
                    if (node.Selected)
                        Selection.UnselectAll();
                    Delete(node);
                }

                break;
            case ModPathChangeType.Reloaded:
                // Nothing
                break;
        }
    }
}

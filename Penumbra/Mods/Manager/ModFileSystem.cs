using Dalamud.Interface.ImGuiNotification;
using ImSharp;
using Luna;
using OtterGui.Filesystem;
using Penumbra.Api.Enums;
using Penumbra.Communication;
using Penumbra.Services;
using Penumbra.UI.Classes;
using Penumbra.UI.ModsTab;
using Penumbra.UI.ModsTab.Selector;
using FileSystemChangeType = OtterGui.Filesystem.FileSystemChangeType;

namespace Penumbra.Mods.Manager;

public sealed class ModTab : TwoPanelLayout, ITab<TabType>
{
    public override ReadOnlySpan<byte> Label
        => "Mods2"u8;

    public ModTab(ModFileSystemDrawer drawer, ModPanel panel, CollectionSelectHeader collectionHeader, RedrawFooter redrawFooter)
    {
        LeftHeader  = drawer.Header;
        LeftFooter  = drawer.Footer;
        LeftPanel   = drawer;
        RightPanel  = panel;
        RightHeader = collectionHeader;
        RightFooter = redrawFooter;
    }

    public void DrawContent()
        => Draw();

    public TabType Identifier
        => TabType.Mods;

    protected override void SetSize(Vector2 newSize)
    {
        base.SetSize(newSize);
        ((ModFileSystemDrawer)LeftPanel).Config.Ephemeral.CurrentModSelectorWidth = newSize.X / Im.Style.GlobalScale;
    }

    protected override float MinimumWidth
        => ((ModFileSystemDrawer)LeftPanel).Footer.Buttons.Count * Im.Style.FrameHeight;

    protected override float MaximumWidth
        => Im.Window.Width - 500 * Im.Style.GlobalScale;
}

public sealed class ModFileSystem2 : BaseFileSystem, IDisposable, IRequiredService
{
    private readonly Configuration       _config;
    private readonly CommunicatorService _communicator;
    private readonly ModFileSystemSaver  _saver;

    public ModFileSystem2(Configuration config, CommunicatorService communicator, SaveService saveService, Logger log, ModStorage modStorage)
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

                CreateDuplicateDataNode(parent, arguments.Mod.Name, arguments.Mod);
                break;
            case ModPathChangeType.Deleted:
                if (arguments.Mod.Node is not null)
                    Delete(arguments.Mod.Node);
                break;
            case ModPathChangeType.Reloaded:
                // Nothing
                break;
        }
    }
}

public sealed class ModFileSystem : FileSystem<Mod>, IDisposable, ISavable, IService
{
    private readonly ModManager          _modManager;
    private readonly CommunicatorService _communicator;
    private readonly SaveService         _saveService;
    private readonly Configuration       _config;

    // Create a new ModFileSystem from the currently loaded mods and the current sort order file.
    public ModFileSystem(ModManager modManager, CommunicatorService communicator, SaveService saveService, Configuration config)
    {
        _modManager   = modManager;
        _communicator = communicator;
        _saveService  = saveService;
        _config       = config;
        Reload();
        Changed += OnChange;
        _communicator.ModDiscoveryFinished.Subscribe(Reload, ModDiscoveryFinished.Priority.ModFileSystem);
        _communicator.ModDataChanged.Subscribe(OnModDataChange, ModDataChanged.Priority.ModFileSystem);
        _communicator.ModPathChanged.Subscribe(OnModPathChange, ModPathChanged.Priority.ModFileSystem);
    }

    public void Dispose()
    {
        _communicator.ModPathChanged.Unsubscribe(OnModPathChange);
        _communicator.ModDiscoveryFinished.Unsubscribe(Reload);
        _communicator.ModDataChanged.Unsubscribe(OnModDataChange);
    }

    public struct ImportDate : ISortMode<Mod>
    {
        public ReadOnlySpan<byte> Name
            => "Import Date (Older First)"u8;

        public ReadOnlySpan<byte> Description
            => "In each folder, sort all subfolders lexicographically, then sort all leaves using their import date."u8;

        public IEnumerable<IPath> GetChildren(Folder f)
            => f.GetSubFolders().Cast<IPath>().Concat(f.GetLeaves().OrderBy(l => l.Value.ImportDate));
    }

    public struct InverseImportDate : ISortMode<Mod>
    {
        public ReadOnlySpan<byte> Name
            => "Import Date (Newer First)"u8;

        public ReadOnlySpan<byte> Description
            => "In each folder, sort all subfolders lexicographically, then sort all leaves using their inverse import date."u8;

        public IEnumerable<IPath> GetChildren(Folder f)
            => f.GetSubFolders().Cast<IPath>().Concat(f.GetLeaves().OrderByDescending(l => l.Value.ImportDate));
    }

    // Reload the whole filesystem from currently loaded mods and the current sort order file.
    // Used on construction and on mod rediscoveries.
    private void Reload()
    {
        var jObj = BackupService.GetJObjectForFile(_saveService.FileNames, _saveService.FileNames.OldFilesystemFile);
        if (Load(jObj, _modManager, ModToIdentifier, ModToName))
            _saveService.ImmediateSave(this);

        Penumbra.Log.Debug("Reloaded mod filesystem.");
    }

    // Save the filesystem on every filesystem change except full reloading.
    private void OnChange(FileSystemChangeType type, IPath _1, IPath? _2, IPath? _3)
    {
        if (type != FileSystemChangeType.Reload)
            _saveService.DelaySave(this);
    }

    // Update sort order when defaulted mod names change.
    private void OnModDataChange(in ModDataChanged.Arguments arguments)
    {
        if (!arguments.Type.HasFlag(ModDataChangeType.Name) || arguments.OldName == null || !TryGetValue(arguments.Mod, out var leaf))
            return;

        var old = Extensions.FixName(arguments.OldName);
        if (old == leaf.Name || Extensions.IsDuplicateName(leaf.Name, out var baseName, out _) && baseName == old)
            RenameWithDuplicates(leaf, arguments.Mod.Name);
    }

    // Update the filesystem if a mod has been added or removed.
    // Save it, if the mod directory has been moved, since this will change the save format.
    private void OnModPathChange(in ModPathChanged.Arguments arguments)
    {
        switch (arguments.Type)
        {
            case ModPathChangeType.Added:
                var parent = Root;
                if (_config.DefaultImportFolder.Length != 0)
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

                CreateDuplicateLeaf(parent, arguments.Mod.Name, arguments.Mod);
                break;
            case ModPathChangeType.Deleted:
                if (TryGetValue(arguments.Mod, out var leaf))
                    Delete(leaf);

                break;
            case ModPathChangeType.Moved: _saveService.DelaySave(this); break;
            case ModPathChangeType.Reloaded:
                // Nothing
                break;
        }
    }

    // Used for saving and loading.
    private static string ModToIdentifier(Mod mod)
        => mod.ModPath.Name;

    private static string ModToName(Mod mod)
        => Extensions.FixName(mod.Name);

    // Return whether a mod has a custom path or is just a numbered default path.
    public static bool ModHasDefaultPath(Mod mod, string fullPath)
    {
        var regex = new Regex($@"^{Regex.Escape(ModToName(mod))}( \(\d+\))?$");
        return regex.IsMatch(fullPath);
    }

    private static (string, bool) SaveMod(Mod mod, string fullPath)
        // Only save pairs with non-default paths.
        => ModHasDefaultPath(mod, fullPath)
            ? (string.Empty, false)
            : (ModToIdentifier(mod), true);

    public string ToFilePath(FilenameService fileNames)
        => fileNames.OldFilesystemFile;

    public void Save(StreamWriter writer)
        => SaveToFile(writer, SaveMod, true);

    public string TypeName
        => "Mod File System";

    public string LogName(string _)
        => "to file";
}

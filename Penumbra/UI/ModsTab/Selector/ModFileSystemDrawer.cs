using Luna;
using Penumbra.Collections.Manager;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Settings;
using Penumbra.Services;
using Penumbra.UI.Classes;

namespace Penumbra.UI.ModsTab.Selector;

public sealed class ModFileSystemDrawer : FileSystemDrawer<ModFileSystemCache.ModData>, IDisposable
{
    public readonly ModManager          ModManager;
    public readonly CollectionManager   CollectionManager;
    public readonly Configuration       Config;
    public readonly ModImportManager    ModImport;
    public readonly FileDialogService   FileService;
    public readonly TutorialService     Tutorial;
    public readonly CommunicatorService Communicator;

    public ModFileSystemDrawer(Services.MessageService messager, ModFileSystem fileSystem, ModManager modManager,
        CollectionManager collectionManager, Configuration config,
        ModImportManager modImport, FileDialogService fileService, TutorialService tutorial, CommunicatorService communicator)
        : base(messager, fileSystem, new ModFilter(modManager, collectionManager.Active, config))
    {
        ModManager        = modManager;
        CollectionManager = collectionManager;
        Config            = config;
        ModImport         = modImport;
        FileService       = fileService;
        Tutorial          = tutorial;
        Communicator      = communicator;
        SortMode          = Config.SortMode;

        Config.ShowRenameChanged += SetRenameFields;

        MainContext.AddButton(new ClearTemporarySettingsButton(this),   105);
        MainContext.AddButton(new ClearDefaultImportFolderButton(this), -10);
        MainContext.AddButton(new ClearQuickMoveFoldersButtons(this),   -20);

        FolderContext.AddButton(new SetDescendantsButton(this, true,  null),  11);
        FolderContext.AddButton(new SetDescendantsButton(this, false, null),  10);
        FolderContext.AddButton(new SetDescendantsButton(this, true,  true),  6);
        FolderContext.AddButton(new SetDescendantsButton(this, true,  false), 5);
        FolderContext.AddButton(new SetDefaultImportFolderButton(this),       -50);
        FolderContext.AddButton(new SetQuickMoveFoldersButtons(this),         -70);

        DataContext.AddButton(new ToggleFavoriteButton(this),          10);
        DataContext.AddButton(new TemporaryButtons(this),              20);
        DataContext.AddButton(new MoveToQuickMoveFoldersButtons(this), -100);
        SetRenameFields(Config.ShowRename, default);

        Footer.Buttons.AddButton(new AddNewModButton(this),       1000);
        Footer.Buttons.AddButton(new ImportModButton(this),       900);
        Footer.Buttons.AddButton(new HelpButton(this),            500);
        Footer.Buttons.AddButton(new DeleteSelectionButton(this), -100);
    }

    public override ReadOnlySpan<byte> Id
        => "ModFileSystem"u8;

    protected override FileSystemCache<ModFileSystemCache.ModData> CreateCache()
        => new ModFileSystemCache(this);

    public void SetDescendants(IFileSystemFolder folder, bool enabled, bool? inherit)
    {
        var mods = folder.GetDescendants().OfType<IFileSystemData<Mod>>().Select(l =>
        {
            // Any mod handled here should not stay new.
            ModManager.SetKnown(l.Value);
            return l.Value;
        });

        if (Config.DefaultTemporaryMode)
        {
            var collection = CollectionManager.Active.Current;
            foreach (var mod in folder.GetDescendants().OfType<IFileSystemData<Mod>>())
            {
                if (!CollectionManager.Editor.CanSetTemporarySettings(collection, mod.Value, 0))
                    continue;

                var settings = new TemporaryModSettings(mod.Value, collection.GetActualSettings(mod.Value.Index).Settings)
                {
                    ForceInherit = inherit ?? false,
                };
                if (!inherit.HasValue)
                    settings.Enabled = enabled;
                CollectionManager.Editor.SetTemporarySettings(collection, mod.Value, settings);
            }
        }
        else
        {
            if (inherit.HasValue)
                CollectionManager.Editor.SetMultipleModInheritances(CollectionManager.Active.Current, mods, inherit.Value);
            else
                CollectionManager.Editor.SetMultipleModStates(CollectionManager.Active.Current, mods, enabled);
        }
    }

    public void Dispose()
        => Config.ShowRenameChanged -= SetRenameFields;

    private void SetRenameFields(RenameField newField, RenameField _)
    {
        DataContext.RemoveButtons<MoveModInput>();
        DataContext.RemoveButtons<RenameModInput>();
        switch (newField)
        {
            case RenameField.RenameSearchPath: DataContext.AddButton(new RenameModInput(this), -1000); break;
            case RenameField.RenameData:       DataContext.AddButton(new MoveModInput(this),   -1000); break;
            case RenameField.BothSearchPathPrio:
                DataContext.AddButton(new RenameModInput(this), -1000);
                DataContext.AddButton(new MoveModInput(this),   -1001);
                break;
            case RenameField.BothDataPrio:
                DataContext.AddButton(new RenameModInput(this), -1001);
                DataContext.AddButton(new MoveModInput(this),   -1000);
                break;
        }
    }
}

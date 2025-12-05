using Luna;
using Penumbra.Collections.Manager;
using Penumbra.Mods;
using Penumbra.Mods.Manager;

namespace Penumbra.UI.ModsTab.Selector;

public sealed class ModFileSystemDrawer : FileSystemDrawer<ModFileSystemCache.ModData>
{
    public readonly ModManager        ModManager;
    public readonly CollectionManager CollectionManager;
    public readonly Configuration     Config;

    public ModFileSystemDrawer(ModFileSystem2 fileSystem, ModManager modManager, CollectionManager collectionManager, Configuration config)
        : base(fileSystem, null)
    {
        ModManager        = modManager;
        CollectionManager = collectionManager;
        Config            = config;

        MainContext.AddButton(new ClearTemporarySettingsButton(this),   105);
        MainContext.AddButton(new ClearDefaultImportFolderButton(this), 10);

        FolderContext.AddButton(new SetDescendantsButton(this, true),        11);
        FolderContext.AddButton(new SetDescendantsButton(this, false),       10);
        FolderContext.AddButton(new SetDescendantsButton(this, true,  true), 6);
        FolderContext.AddButton(new SetDescendantsButton(this, false, true), 5);
        FolderContext.AddButton(new SetDefaultImportFolderButton(this),      -100);

        DataContext.AddButton(new ToggleFavoriteButton(this), 10);

        Footer.Buttons.AddButton(new AddNewModButton(this), 1000);

    }

    public override ReadOnlySpan<byte> Id
        => "ModFileSystem"u8;

    protected override FileSystemCache<ModFileSystemCache.ModData> CreateCache()
        => new ModFileSystemCache(this);


    public void SetDescendants(IFileSystemFolder folder, bool enabled, bool inherit = false)
    {
        var mods = folder.GetDescendants().OfType<IFileSystemData<Mod>>().Select(l =>
        {
            // Any mod handled here should not stay new.
            ModManager.SetKnown(l.Value);
            return l.Value;
        });

        if (inherit)
            CollectionManager.Editor.SetMultipleModInheritances(CollectionManager.Active.Current, mods, enabled);
        else
            CollectionManager.Editor.SetMultipleModStates(CollectionManager.Active.Current, mods, enabled);
    }
}

using ImSharp;
using Luna;
using Penumbra.Api.Enums;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.Communication;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Settings;
using Penumbra.UI.Classes;

namespace Penumbra.UI.ModsTab.Selector;

public sealed class ModFileSystemCache : FileSystemCache<ModFileSystemCache.ModData>, IService
{
    private new ModFileSystemDrawer Parent
        => (ModFileSystemDrawer)base.Parent;

    public ModFileSystemCache(ModFileSystemDrawer parent)
        : base(parent)
    {
        Parent.Communicator.CollectionChange.Subscribe(OnCollectionChange, CollectionChange.Priority.ModFileSystemCache);
        Parent.Communicator.CollectionInheritanceChanged.Subscribe(OnInheritanceChange,
            CollectionInheritanceChanged.Priority.ModFileSystemCache);
        Parent.Communicator.ModSettingChanged.Subscribe(OnSettingChangeBeforeConflicts,
            ModSettingChanged.Priority.ModFileSystemCacheBeforeConflicts);
        Parent.Communicator.ModSettingChanged.Subscribe(OnSettingChangeAfterConflicts,
            ModSettingChanged.Priority.ModFileSystemCacheAfterConflicts);
        Parent.Communicator.ModDataChanged.Subscribe(OnModDataChange, ModDataChanged.Priority.ModFileSystemCache);
    }

    private void OnModDataChange(in ModDataChanged.Arguments arguments)
    {
        if (arguments.Type.HasFlag(ModDataChangeType.Deletion))
        {
            Dirty |= IManagedCache.DirtyFlags.Custom;
            return;
        }

        const ModDataChangeType relevantFlags =
            ModDataChangeType.Name
          | ModDataChangeType.Author
          | ModDataChangeType.ModTags
          | ModDataChangeType.LocalTags
          | ModDataChangeType.Favorite
          | ModDataChangeType.ImportDate;
        if ((arguments.Type & relevantFlags) is not 0 && !Filter.IsEmpty
         || arguments.Type.HasFlag(ModDataChangeType.ImportDate) && Parent.SortMode is ImportDate or InverseImportDate)
            VisibleDirty = true;

        if (arguments.Mod.Node is { } node && AllNodes.TryGetValue(node, out var cache))
            cache.Dirty = true;
    }

    private void OnSettingChangeBeforeConflicts(in ModSettingChanged.Arguments arguments)
    {
        if (!Filter.IsEmpty)
            VisibleDirty = true;

        if (arguments.Type is ModSettingChange.MultiEnableState or ModSettingChange.MultiInheritance)
        {
            foreach (var mod in AllNodes.Values)
                mod.Dirty = true;
            return;
        }

        if (arguments.Mod?.Node is { } node && AllNodes.TryGetValue(node, out var cache))
            cache.Dirty = true;

        HandleConflicts(arguments.Mod);
    }

    private void OnSettingChangeAfterConflicts(in ModSettingChanged.Arguments arguments)
        => HandleConflicts(arguments.Mod);

    private void HandleConflicts(Mod? mod)
    {
        if (Parent.CollectionManager.Active.Current.Cache is not { } collectionCache || mod is null)
            return;

        var conflicts = collectionCache.Conflicts(mod);
        foreach (var conflict in conflicts)
        {
            if (conflict.Mod2 is Mod { Node: { } node2 } && AllNodes.TryGetValue(node2, out var cache2))
                cache2.Dirty = true;
        }
    }

    private void OnInheritanceChange(in CollectionInheritanceChanged.Arguments arguments)
    {
        if (arguments.Collection == Parent.CollectionManager.Active.Current)
        {
            if (!Filter.IsEmpty)
                VisibleDirty = true;

            foreach (var node in AllNodes.Values)
                node.Dirty = true;
        }
    }

    private void OnCollectionChange(in CollectionChange.Arguments arguments)
    {
        if (arguments.Type is CollectionType.Current && arguments.OldCollection != arguments.NewCollection)
        {
            if (!Filter.IsEmpty)
                VisibleDirty = true;

            foreach (var node in AllNodes.Values)
                node.Dirty = true;
        }
    }

    public sealed class ModData(IFileSystemData<Mod> node) : BaseFileSystemNodeCache<ModData>
    {
        public readonly IFileSystemData<Mod> Node = node;
        public          Vector4              TextColor;
        public          ModPriority          Priority;
        public          StringU8             PriorityText = StringU8.Empty;
        public          ModSettings?         Settings;
        public          ModCollection        Collection = ModCollection.Empty;
        public          StringU8             Name       = new(node.Value.Name);

        public override void Update(FileSystemCache cache, IFileSystemNode node)
        {
            base.Update(cache, node);
            var currentCollection = ((ModFileSystemDrawer)cache.Parent).CollectionManager.Active.Current;
            (Settings, Collection) = currentCollection.GetActualSettings(Node.Value.Index);
            TextColor              = UpdateColor(cache, currentCollection);
            Name                   = new StringU8(Node.Value.Name);
            var priority = Settings?.Priority ?? ModPriority.Default;
            if (priority != Priority)
            {
                Priority     = priority;
                PriorityText = priority.IsDefault ? StringU8.Empty : new StringU8($"[{priority}]");
            }
        }

        private Vector4 UpdateColor(FileSystemCache cache, ModCollection current)
        {
            var modManager = ((ModFileSystemDrawer)cache.Parent).ModManager;
            var tint = (Settings.IsTemporary() ? ColorId.TemporaryModSettingsTint :
                modManager.IsNew(Node.Value)   ? ColorId.NewModTint : ColorId.NoTint).Value().ToVector();
            if (Settings is null)
                return Rgba32.TintColor(ColorId.UndefinedMod.Value().ToVector(), tint);

            if (!Settings.Enabled)
                return Rgba32.TintColor((Collection != current ? ColorId.InheritedDisabledMod : ColorId.DisabledMod).Value().ToVector(), tint);

            var conflicts = current.Conflicts(Node.Value);
            if (conflicts.Count is 0)
                return Rgba32.TintColor((Collection != current ? ColorId.InheritedMod : ColorId.EnabledMod).Value().ToVector(), tint);

            return Rgba32.TintColor((conflicts.Any(c => !c.Solved) ? ColorId.ConflictingMod : ColorId.HandledConflictMod).Value().ToVector(),
                tint);
        }

        protected override void DrawInternal(FileSystemCache<ModData> cache, IFileSystemNode node)
        {
            using var color = ImGuiColor.Text.Push(TextColor)
                .Push(ImGuiColor.HeaderHovered, 0x4000FFFF, Node.Value.Favorite);
            using var           id        = Im.Id.Push(Node.Value.Index);
            const TreeNodeFlags baseFlags = TreeNodeFlags.NoTreePushOnOpen;
            var                 flags     = node.Selected ? baseFlags | TreeNodeFlags.Selected : baseFlags;
            Im.Tree.Leaf(Name, flags);
            if (Im.Item.MiddleClicked())
                OnMiddleClick(cache);
            DrawPriority(cache);
        }

        private void OnMiddleClick(FileSystemCache<ModData> cache)
        {
            var modManager        = ((ModFileSystemDrawer)cache.Parent).ModManager;
            var collectionManager = ((ModFileSystemDrawer)cache.Parent).CollectionManager;
            var config            = ((ModFileSystemDrawer)cache.Parent).Config;

            modManager.SetKnown(Node.Value);
            var (setting, collection) = collectionManager.Active.Current.GetActualSettings(Node.Value.Index);
            if (config.DeleteModModifier.ForcedModifier(new DoubleModifier(ModifierHotkey.Control, ModifierHotkey.Shift)).IsActive())
            {
                // Delete temporary settings if they exist, regardless of mode, or set to inheriting if none exist.
                if (collectionManager.Active.Current.GetTempSettings(Node.Value.Index) is not null)
                    collectionManager.Editor.SetTemporarySettings(collectionManager.Active.Current, Node.Value, null);
                else
                    collectionManager.Editor.SetModInheritance(collectionManager.Active.Current, Node.Value, true);
            }
            else
            {
                if (config.DefaultTemporaryMode)
                {
                    var settings = new TemporaryModSettings(Node.Value, setting) { ForceInherit = false };
                    settings.Enabled = !settings.Enabled;
                    collectionManager.Editor.SetTemporarySettings(collectionManager.Active.Current, Node.Value, settings);
                }
                else
                {
                    var inherited = collection != collectionManager.Active.Current;
                    if (inherited)
                        collectionManager.Editor.SetModInheritance(collectionManager.Active.Current, Node.Value, false);
                    collectionManager.Editor.SetModState(collectionManager.Active.Current, Node.Value, setting is not { Enabled: true });
                }
            }
        }

        private void DrawPriority(FileSystemCache<ModData> cache)
        {
            if (Priority.IsDefault)
                return;

            var config = ((ModFileSystemDrawer)cache.Parent).Config;
            if (config.HidePrioritiesInSelector)
                return;

            var line           = Im.Item.UpperLeftCorner.Y;
            var itemPos        = Im.Item.LowerRightCorner.X;
            var maxWidth       = Im.Window.Position.X + Im.Window.MaximumContentRegion.X;
            var remainingSpace = maxWidth - itemPos;
            var offset         = remainingSpace - PriorityText.CalculateSize().X;
            if (Im.Scroll.MaximumY is 0)
                offset -= Im.Style.ItemInnerSpacing.X;

            if (offset > Im.Style.ItemSpacing.X)
                Im.Window.DrawList.Text(new Vector2(itemPos + offset, line), ColorId.SelectorPriority.Value().Color, PriorityText);
        }
    }

    public override void Update()
    {
        if (!ColorsDirty)
            return;

        CollapsedFolderColor =  ColorId.FolderCollapsed.Value().ToVector();
        ExpandedFolderColor  =  ColorId.FolderExpanded.Value().ToVector();
        LineColor            =  ColorId.FolderLine.Value().ToVector();
        Dirty                &= ~IManagedCache.DirtyFlags.Colors;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Parent.Communicator.CollectionChange.Unsubscribe(OnCollectionChange);
        Parent.Communicator.CollectionInheritanceChanged.Unsubscribe(OnInheritanceChange);
        Parent.Communicator.ModSettingChanged.Unsubscribe(OnSettingChangeBeforeConflicts);
        Parent.Communicator.ModSettingChanged.Unsubscribe(OnSettingChangeAfterConflicts);
        Parent.Communicator.ModDataChanged.Unsubscribe(OnModDataChange);
    }

    protected override ModData ConvertNode(in IFileSystemNode node)
        => new((IFileSystemData<Mod>)node);
}

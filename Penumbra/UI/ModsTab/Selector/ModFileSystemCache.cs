using ImSharp;
using Luna;
using Penumbra.Collections;
using Penumbra.Mods;
using Penumbra.Mods.Settings;
using Penumbra.UI.Classes;

namespace Penumbra.UI.ModsTab.Selector;

public sealed class ModFileSystemCache(ModFileSystemDrawer parent)
    : FileSystemCache<ModFileSystemCache.ModData>(parent), IService
{
    public sealed class ModData(IFileSystemData<Mod> node) : BaseFileSystemNodeCache<ModData>
    {
        public readonly IFileSystemData<Mod> Node = node;
        public          Vector4              TextColor;
        public          ModPriority          Priority;
        public          SizedString          PriorityText = SizedString.Empty;
        public          ModSettings?         Settings;
        public          ModCollection        Collection = ModCollection.Empty;

        public override void Update(FileSystemCache cache, IFileSystemNode node)
        {
            base.Update(cache, node);
            var currentCollection = ((ModFileSystemDrawer)cache.Parent).CollectionManager.Active.Current;
            (Settings, Collection) = currentCollection.GetActualSettings(Node.Value.Index);
            TextColor              = UpdateColor(cache, currentCollection);
            var priority = Settings?.Priority ?? ModPriority.Default;
            if (priority != Priority)
            {
                Priority     = priority;
                PriorityText = priority.IsDefault ? SizedString.Empty : new SizedString($"[{priority}]");
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
            using var id = Im.Id.Push(Node.Value.Index);
            base.DrawInternal(cache, node);
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
            var offset         = remainingSpace - PriorityText.Size.X;
            if (Im.Scroll.MaximumY is 0)
                offset -= Im.Style.ItemInnerSpacing.X;

            if (offset > Im.Style.ItemSpacing.X)
                Im.Window.DrawList.Text(new Vector2(itemPos + offset, line), ColorId.SelectorPriority.Value().Color, PriorityText.Text);
        }
    }

    public override void Update()
    {
        if (ColorsDirty)
        {
            CollapsedFolderColor =  ColorId.FolderCollapsed.Value().ToVector();
            ExpandedFolderColor  =  ColorId.FolderExpanded.Value().ToVector();
            LineColor            =  ColorId.FolderLine.Value().ToVector();
            Dirty                &= ~IManagedCache.DirtyFlags.Colors;
        }
    }

    protected override ModData ConvertNode(in IFileSystemNode node)
        => new((IFileSystemData<Mod>)node);
}

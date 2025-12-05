using Dalamud.Interface;
using ImSharp;
using Luna;
using Penumbra.Api.Enums;
using Penumbra.Collections.Manager;
using Penumbra.Communication;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Editor;
using Penumbra.Services;
using Penumbra.String.Classes;
using Penumbra.UI.Classes;

namespace Penumbra.UI.MainWindow;

public sealed class EffectiveTab(
    CollectionManager collectionManager,
    CollectionSelectHeader collectionHeader,
    CommunicatorService communicatorService)
    : ITab<TabType>
{
    public ReadOnlySpan<byte> Label
        => "Effective Changes"u8;

    public void DrawContent()
    {
        collectionHeader.Draw(true);
        var cache = CacheManager.Instance.GetOrCreateCache(Im.Id.Current, () => new Cache(collectionManager, communicatorService, _filter));
        cache.Draw();
    }

    public TabType Identifier
        => TabType.EffectiveChanges;

    private readonly PairFilter<Item> _filter = new(new GamePathFilter(), new FullPathFilter());

    private sealed class Cache : BasicFilterCache<Item>, IPanel
    {
        private readonly        CollectionManager   _collectionManager;
        private readonly        CommunicatorService _communicator;
        private                 float               _arrowSize;
        private                 float               _gamePathSize;
        private static readonly AwesomeIcon         Arrow = FontAwesomeIcon.LongArrowAltLeft;

        private new PairFilter<Item> Filter
            => (PairFilter<Item>)base.Filter;

        public override void Update()
        {
            if (FontDirty)
            {
                _arrowSize    =  ImEx.Icon.CalculateSize(Arrow).X;
                _gamePathSize =  450 * Im.Style.GlobalScale;
                Dirty         &= ~IManagedCache.DirtyFlags.Font;
            }

            base.Update();
        }

        public Cache(CollectionManager collectionManager, CommunicatorService communicator, IFilter<Item> filter)
            : base(filter)
        {
            _collectionManager = collectionManager;
            _communicator      = communicator;

            _communicator.CollectionChange.Subscribe(OnCollectionChange, CollectionChange.Priority.EffectiveChangesCache);
            _communicator.ResolvedFileChanged.Subscribe(OnResolvedFileChange, ResolvedFileChanged.Priority.EffectiveChangesCache);
            _communicator.ResolvedMetaChanged.Subscribe(OnResolvedMetaChange, ResolvedMetaChanged.Priority.EffectiveChangesCache);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            _communicator.CollectionChange.Unsubscribe(OnCollectionChange);
            _communicator.ResolvedFileChanged.Unsubscribe(OnResolvedFileChange);
            _communicator.ResolvedMetaChanged.Unsubscribe(OnResolvedMetaChange);
        }

        private void OnResolvedFileChange(in ResolvedFileChanged.Arguments arguments)
            => Dirty |= IManagedCache.DirtyFlags.Custom;

        private void OnResolvedMetaChange(in ResolvedMetaChanged.Arguments arguments)
            => Dirty |= IManagedCache.DirtyFlags.Custom;

        private void OnCollectionChange(in CollectionChange.Arguments arguments)
        {
            if (arguments.Type is CollectionType.Current)
                Dirty |= IManagedCache.DirtyFlags.Custom;
        }

        protected override IEnumerable<Item> GetItems()
            => _collectionManager.Active.Current.Cache is null
                ? []
                : _collectionManager.Active.Current.Cache.ResolvedFiles.Select(f => new Item(f.Value.Mod, f.Key.Path.Span, f.Value.Path))
                    .OrderBy(i => i.GamePath.Utf16)
                    .Concat(_collectionManager.Active.Current.Cache.Meta.IdentifierSources.Select(s => new Item(s.Item2, s.Item1))
                        .OrderBy(i => i.GamePath.Utf16));

        public ReadOnlySpan<byte> Id
            => "EC"u8;

        public void Draw()
        {
            DrawFilters();
            DrawTable();
        }

        private void DrawFilters()
        {
            using var style = ImStyleSingle.FrameRounding.Push(0).PushX(ImStyleDouble.ItemSpacing, 0);

            Filter.Filter1.DrawFilter("Filter game path..."u8, new Vector2(_gamePathSize + Im.Style.CellPadding.X, Im.Style.FrameHeight));
            Im.Line.Same(0, _arrowSize + 2 * Im.Style.CellPadding.X);
            Filter.Filter2.DrawFilter("Filter file path..."u8, Im.ContentRegion.Available with { Y = Im.Style.FrameHeight });

        }

        private void DrawTable()
        {
            using var table = Im.Table.Begin("t"u8, 3, TableFlags.RowBackground | TableFlags.ScrollY, Im.ContentRegion.Available);
            if (!table)
                return;

            table.SetupColumn("gp"u8, TableColumnFlags.WidthFixed, _gamePathSize);
            table.SetupColumn("a"u8,  TableColumnFlags.WidthFixed, _arrowSize);
            table.SetupColumn("fp"u8, TableColumnFlags.WidthStretch);

            using var clipper = new Im.ListClipper(Count, Im.Style.TextHeightWithSpacing);
            foreach (var item in clipper.Iterate(this))
            {
                table.NextColumn();
                ImEx.CopyOnClickSelectable(item.GamePath.Utf8);

                table.NextColumn();
                ImEx.Icon.Draw(Arrow);

                table.NextColumn();
                ImEx.CopyOnClickSelectable(item.FilePath.InternalName.Span);
                if (!item.IsMeta)
                    Im.Tooltip.OnHover($"\nChanged by {item.Mod.Name}.");
            }
        }
    }

    private sealed class GamePathFilter : RegexFilterBase<Item>
    {
        protected override string ToFilterString(in Item item, int globalIndex)
            => item.GamePath.Utf16;
    }

    private sealed class FullPathFilter : RegexFilterBase<Item>
    {
        protected override string ToFilterString(in Item item, int globalIndex)
            => item.FilePath.FullName;
    }

    private sealed class Item
    {
        public IMod       Mod;
        public StringPair GamePath;
        public FullPath   FilePath;
        public bool       IsMeta;

        public Item(IMod mod, ReadOnlySpan<byte> gamePath, FullPath filePath)
        {
            Mod      = mod;
            GamePath = new StringPair(gamePath);
            FilePath = filePath;
            IsMeta   = false;
        }

        public Item(IMod mod, IMetaIdentifier identifier)
        {
            Mod      = mod;
            GamePath = new StringPair($"{identifier}");
            FilePath = new FullPath(mod.Name);
            IsMeta   = true;
        }
    }
}

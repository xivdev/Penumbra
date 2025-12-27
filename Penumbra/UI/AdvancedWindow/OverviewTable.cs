using ImSharp;
using ImSharp.Containers;
using ImSharp.Table;
using Penumbra.Mods.Editor;
using Penumbra.Mods.SubMods;
using Penumbra.String.Classes;

namespace Penumbra.UI.AdvancedWindow;

public sealed class OverviewTable(ModEditor parent)
    : DefaultTable<OverviewTable.OverviewFile>(new StringU8("##overview"u8),
        new FileColumn
        {
            Label = new StringU8("File"u8),
            Flags = TableColumnFlags.WidthStretch,
        },
        new PathColumn
        {
            Label = new StringU8("Path"u8),
            Flags = TableColumnFlags.WidthStretch,
        },
        new OptionColumn
        {
            Label = new StringU8("Option"u8),
            Flags = TableColumnFlags.WidthStretch,
        })
{
    public sealed record OverviewFile(
        StringPair File,
        StringPair Path,
        StringPair OptionName,
        IModDataContainer? Option,
        ColorParameter Color)
    {
        public ColorParameter Color { get; set; } = Color;

        public OverviewFile(FileRegistry file)
            : this(new StringPair(file.RelPath.Path.Span), new StringPair("Unused", new StringU8("Unused"u8)), StringPair.Empty, null,
                0x40000080)
        { }

        public OverviewFile(FileRegistry file, IModDataContainer option, Utf8GamePath gamePath, bool tint)
            : this(new StringPair(file.RelPath.Path.Span), new StringPair(gamePath.Path.Span), new StringPair(option.GetFullName()),
                option, tint ? 0x40008000 : ColorParameter.Default)
        { }
    }

    private sealed class FileColumn : TextColumn<OverviewFile>
    {
        protected override string ComparisonText(in OverviewFile item, int globalIndex)
            => item.File;

        protected override StringU8 DisplayText(in OverviewFile item, int globalIndex)
            => item.File;

        public override void DrawColumn(in OverviewFile item, int globalIndex)
        {
            Im.Table.SetBackgroundColor(TableBackgroundTarget.Cell, item.Color);
            ImEx.CopyOnClickSelectable(item.File.Utf8);
        }

        public override float ComputeWidth(IEnumerable<OverviewFile> _)
            => 3 / 8f;
    }

    private sealed class PathColumn : TextColumn<OverviewFile>
    {
        protected override string ComparisonText(in OverviewFile item, int globalIndex)
            => item.Path;

        protected override StringU8 DisplayText(in OverviewFile item, int globalIndex)
            => item.Path;

        public override void DrawColumn(in OverviewFile item, int globalIndex)
        {
            Im.Table.SetBackgroundColor(TableBackgroundTarget.Cell, item.Color);
            ImEx.CopyOnClickSelectable(item.Path.Utf8);
        }

        public override float ComputeWidth(IEnumerable<OverviewFile> _)
            => 3 / 8f;
    }

    private sealed class OptionColumn : TextColumn<OverviewFile>
    {
        protected override string ComparisonText(in OverviewFile item, int globalIndex)
            => item.OptionName;

        protected override StringU8 DisplayText(in OverviewFile item, int globalIndex)
            => item.OptionName;

        public override void DrawColumn(in OverviewFile item, int globalIndex)
        {
            Im.Table.SetBackgroundColor(TableBackgroundTarget.Cell, item.Color);
            ImEx.CopyOnClickSelectable(item.OptionName.Utf8);
        }

        public override float ComputeWidth(IEnumerable<OverviewFile> _)
            => 2 / 8f;
    }

    public override IEnumerable<OverviewFile> GetItems()
        => parent.Files.Available.SelectMany(f =>
        {
            return f.SubModUsage.Count is 0
                ? [new OverviewFile(f)]
                : f.SubModUsage.Select(s => new OverviewFile(f, s.Item1, s.Item2, parent.Option! == s.Item1 && parent.Mod!.HasOptions));
        });

    protected override TableCache<OverviewFile> CreateCache()
        => new Cache(this, parent);

    private sealed class Cache : TableCache<OverviewFile>
    {
        private readonly ModEditor _editor;

        public Cache(OverviewTable table, ModEditor editor)
            : base(table)
        {
            _editor                          =  editor;
            _editor.Files.Available.OnChange += OnChange;
            _editor.OptionLoaded             += OnOptionLoaded;
        }

        private void OnOptionLoaded()
        {
            foreach (var item in UnfilteredItems.Where(i => i.Option is not null))
                item.Color = _editor.Option == item.Option && _editor.Mod!.HasOptions ? 0x40008000 : ColorParameter.Default;
        }

        private void OnChange(in ObservableList<FileRegistry>.ChangeArguments args)
            => Dirty |= IManagedCache.DirtyFlags.Custom;

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            _editor.Files.Available.OnChange -= OnChange;
            _editor.OptionLoaded             -= OnOptionLoaded;
        }
    }
}

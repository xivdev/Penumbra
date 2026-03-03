using Dalamud.Interface;
using ImSharp;
using ImSharp.Containers;
using ImSharp.Table;
using Luna;
using Penumbra.Enums;
using Penumbra.Interop.Structs;
using Penumbra.UI.Classes;

namespace Penumbra.UI.ResourceWatcher;

[Flags]
public enum BoolEnum : byte
{
    True    = 0x01,
    False   = 0x02,
    Unknown = 0x04,
}

public static class BoolEnumExtensions
{
    public const BoolEnum All = BoolEnum.True | BoolEnum.False | BoolEnum.Unknown;
}

[Flags]
public enum LoadStateFlag : byte
{
    Success   = 0x01,
    Async     = 0x02,
    Failed    = 0x04,
    FailedSub = 0x08,
    Unknown   = 0x10,
    None      = 0xFF,
}

public static class LoadStateExtensions
{
    public const LoadStateFlag All = LoadStateFlag.Success
      | LoadStateFlag.Async
      | LoadStateFlag.Failed
      | LoadStateFlag.FailedSub
      | LoadStateFlag.Unknown
      | LoadStateFlag.None;
}

internal sealed unsafe class CachedRecord(Record record)
{
    public readonly Record     Record = record;
    public readonly string     PathU16 = record.Path.ToString();
    public readonly StringU8   TypeName = new(record.RecordType.ToNameU8());
    public readonly StringU8   Time = new($"{record.Time.ToLongTimeString()}.{record.Time.Millisecond:D4}");
    public readonly StringPair Crc64 = new($"{record.Crc64:X16}");
    public readonly StringU8   Collection = record.Collection is null ? StringU8.Empty : new StringU8(record.Collection.Identity.Name);
    public readonly StringU8   AssociatedGameObject = new(record.AssociatedGameObject);
    public readonly string     OriginalPath = record.OriginalPath.ToString();
    public readonly StringU8   ResourceCategory = new($"{record.Category}");
    public readonly StringU8   ResourceType = new(record.ResourceType.ToString().ToLowerInvariant());
    public readonly string     HandleU16 = $"0x{(nint)record.Handle:X}";
    public readonly StringPair Thread = new($"{record.OsThreadId}");
    public readonly StringPair RefCount = new($"{record.RefCount}");
}

internal sealed class ResourceWatcherTable : TableBase<CachedRecord, TableCache<CachedRecord>>
{
    private readonly IReadOnlyList<Record> _records;

    internal interface ICheckRecord
    {
        public bool WouldBeVisible(in Record record);
    }

    public bool WouldBeVisible(Record record)
        => Columns.OfType<ICheckRecord>().All(column => column.WouldBeVisible(record));

    public ResourceWatcherTable(FilterConfig filterConfig, IReadOnlyList<Record> records)
        : base(new StringU8("##records"u8),
            new PathColumn(filterConfig) { Label             = new StringU8("Path"u8) },
            new RecordTypeColumn(filterConfig) { Label       = new StringU8("Record"u8) },
            new CollectionColumn(filterConfig) { Label       = new StringU8("Collection"u8) },
            new ObjectColumn(filterConfig) { Label           = new StringU8("Game Object"u8) },
            new CustomLoadColumn(filterConfig) { Label       = new StringU8("Custom"u8) },
            new SynchronousLoadColumn(filterConfig) { Label  = new StringU8("Sync"u8) },
            new OriginalPathColumn(filterConfig) { Label     = new StringU8("Original Path"u8) },
            new ResourceCategoryColumn(filterConfig) { Label = new StringU8("Category"u8) },
            new ResourceTypeColumn(filterConfig) { Label     = new StringU8("Type"u8) },
            new HandleColumn(filterConfig) { Label           = new StringU8("Resource"u8) },
            new LoadStateColumn(filterConfig) { Label        = new StringU8("State"u8) },
            new RefCountColumn(filterConfig) { Label         = new StringU8("#Ref"u8) },
            new DateColumn { Label                           = new StringU8("Time"u8) },
            new Crc64Column(filterConfig) { Label            = new StringU8("Crc64"u8) },
            new OsThreadColumn(filterConfig) { Label         = new StringU8("TID"u8) }
        )
        => _records = records;

    private static void DrawByteString(StringU8 path, float length)
    {
        if (path.IsEmpty)
            return;

        var size    = Im.Font.CalculateSize(path.Span);
        var clicked = false;
        if (size.X <= length)
        {
            clicked = Im.Selectable(path.Span);
        }
        else
        {
            var fileName = path.Span.LastIndexOf((byte)'/');
            using (Im.Group())
            {
                ReadOnlySpan<byte> shortPath;
                var                icon = FontAwesomeIcon.EllipsisH.Icon();
                if (fileName is not -1)
                {
                    using var font = AwesomeIcon.Font.Push();
                    clicked = Im.Selectable(icon.Span);
                    Im.Line.SameInner();
                    shortPath = path.Span.Slice(fileName, path.Length - fileName);
                }
                else
                {
                    shortPath = path;
                }

                clicked |= Im.Selectable(shortPath, false, SelectableFlags.AllowOverlap);
            }

            Im.Tooltip.OnHover(path.Span);
        }

        if (clicked)
            Im.Clipboard.Set(path.Span);
    }


    private sealed class PathColumn : TextColumn<CachedRecord>, ICheckRecord
    {
        public PathColumn(FilterConfig config)
        {
            UnscaledWidth = 300;
            Filter.Set(config.ResourceLoggerPathFilter);
            Filter.FilterChanged += () => config.ResourceLoggerPathFilter = Filter.Text;
        }

        public override void DrawColumn(in CachedRecord item, int globalIndex)
            => DrawByteString(item.Record.Path, 290 * Im.Style.GlobalScale);

        protected override string ComparisonText(in CachedRecord item, int globalIndex)
            => item.PathU16;

        protected override StringU8 DisplayText(in CachedRecord item, int globalIndex)
            => item.Record.Path;

        public bool WouldBeVisible(in Record record)
            => Filter.Text.Length is 0 || Filter.WouldBeVisible(record.Path.ToString());
    }

    private sealed class RecordTypeColumn : FlagColumn<RecordType, CachedRecord>, ICheckRecord
    {
        public RecordTypeColumn(FilterConfig config)
        {
            UnscaledWidth = 80;
            Filter.LoadValue(config.ResourceLoggerRecordFilter);
            Filter.FilterChanged += () => config.ResourceLoggerRecordFilter = Filter.FilterValue;
        }

        protected override StringU8 DisplayString(in CachedRecord item, int globalIndex)
            => item.TypeName;

        protected override IReadOnlyList<(RecordType Value, StringU8 Name)> EnumData
            => RecordType.Values.Select(t => (t, new StringU8(t.ToNameU8()))).ToArray();

        protected override RecordType GetValue(in CachedRecord item, int globalIndex)
            => item.Record.RecordType;

        public bool WouldBeVisible(in Record record)
            => Filter.FilterValue.HasFlag(record.RecordType);
    }

    private sealed class DateColumn : BasicColumn<CachedRecord>
    {
        public DateColumn()
            => UnscaledWidth = 80;

        public override int Compare(in CachedRecord lhs, int lhsGlobalIndex, in CachedRecord rhs, int rhsGlobalIndex)
            => lhs.Record.Time.CompareTo(rhs.Record.Time);

        public override void DrawColumn(in CachedRecord item, int globalIndex)
            => Im.Text(item.Time);
    }

    private sealed class Crc64Column : TextColumn<CachedRecord>, ICheckRecord
    {
        public Crc64Column(FilterConfig config)
        {
            UnscaledWidth = 17 * 8;
            Filter.Set(config.ResourceLoggerCrcFilter);
            Filter.FilterChanged += () => config.ResourceLoggerCrcFilter = Filter.Text;
        }

        public override int Compare(in CachedRecord lhs, int lhsGlobalIndex, in CachedRecord rhs, int rhsGlobalIndex)
            => lhs.Record.Crc64.CompareTo(rhs.Record.Crc64);

        public override void DrawColumn(in CachedRecord item, int globalIndex)
        {
            if (item.Record.Crc64 is 0)
                return;

            using var font = Im.Font.PushMono();
            base.DrawColumn(in item, globalIndex);
        }

        protected override string ComparisonText(in CachedRecord item, int globalIndex)
            => item.Crc64;

        protected override StringU8 DisplayText(in CachedRecord item, int globalIndex)
            => item.Crc64;

        public bool WouldBeVisible(in Record record)
            => Filter.Text.Length is 0 || Filter.WouldBeVisible(record.Crc64.ToString("X16"));
    }


    private sealed class CollectionColumn : TextColumn<CachedRecord>, ICheckRecord
    {
        public CollectionColumn(FilterConfig config)
        {
            UnscaledWidth = 80;
            Filter.Set(config.ResourceLoggerCollectionFilter);
            Filter.FilterChanged += () => config.ResourceLoggerCollectionFilter = Filter.Text;
        }

        protected override string ComparisonText(in CachedRecord item, int globalIndex)
            => item.Record.Collection?.Identity.Name ?? string.Empty;

        protected override StringU8 DisplayText(in CachedRecord item, int globalIndex)
            => item.Collection;

        public bool WouldBeVisible(in Record record)
            => Filter.WouldBeVisible(record.Collection?.Identity.Name ?? string.Empty);
    }

    private sealed class ObjectColumn : TextColumn<CachedRecord>, ICheckRecord
    {
        public ObjectColumn(FilterConfig config)
        {
            UnscaledWidth = 150;
            Filter.Set(config.ResourceLoggerObjectFilter);
            Filter.FilterChanged += () => config.ResourceLoggerObjectFilter = Filter.Text;
        }

        protected override string ComparisonText(in CachedRecord item, int globalIndex)
            => item.Record.AssociatedGameObject;

        protected override StringU8 DisplayText(in CachedRecord item, int globalIndex)
            => item.AssociatedGameObject;

        public bool WouldBeVisible(in Record record)
            => Filter.WouldBeVisible(record.AssociatedGameObject);
    }

    private sealed class OriginalPathColumn : TextColumn<CachedRecord>, ICheckRecord
    {
        public OriginalPathColumn(FilterConfig config)
        {
            UnscaledWidth = 200;
            Filter.Set(config.ResourceLoggerOriginalPathFilter);
            Filter.FilterChanged += () => config.ResourceLoggerOriginalPathFilter = Filter.Text;
        }

        public override void DrawColumn(in CachedRecord item, int globalIndex)
        {
            DrawByteString(item.Record.OriginalPath, 190 * Im.Style.GlobalScale);
        }

        protected override string ComparisonText(in CachedRecord item, int globalIndex)
            => item.OriginalPath;

        protected override StringU8 DisplayText(in CachedRecord item, int globalIndex)
            => item.Record.OriginalPath;

        public bool WouldBeVisible(in Record record)
            => Filter.Text.Length is 0 || Filter.WouldBeVisible(record.OriginalPath.ToString());
    }

    private sealed class ResourceCategoryColumn : FlagColumn<ResourceCategoryFlag, CachedRecord>, ICheckRecord
    {
        public ResourceCategoryColumn(FilterConfig config)
        {
            UnscaledWidth = 80;
            Filter.LoadValue(config.ResourceLoggerCategoryFilter);
            Filter.FilterChanged += () => config.ResourceLoggerCategoryFilter = Filter.FilterValue;
        }

        protected override StringU8 DisplayString(in CachedRecord item, int globalIndex)
            => item.ResourceCategory;

        protected override IReadOnlyList<(ResourceCategoryFlag Value, StringU8 Name)> EnumData { get; } =
            ResourceCategoryFlag.Values.Select(r => (r, new StringU8($"{r}"))).ToArray();

        protected override ResourceCategoryFlag GetValue(in CachedRecord item, int globalIndex)
            => item.Record.Category;

        public bool WouldBeVisible(in Record record)
            => Filter.FilterValue.HasFlag(record.Category);
    }

    private sealed class ResourceTypeColumn : FlagColumn<ResourceTypeFlag, CachedRecord>, ICheckRecord
    {
        public ResourceTypeColumn(FilterConfig config)
        {
            UnscaledWidth = 50;
            Filter.LoadValue(config.ResourceLoggerTypeFilter);
            Filter.FilterChanged += () => config.ResourceLoggerTypeFilter = Filter.FilterValue;
        }

        protected override IReadOnlyList<(ResourceTypeFlag Value, StringU8 Name)> EnumData { get; } =
            ResourceTypeFlag.Values.Select(r => (r, new StringU8(r.ToString().ToLowerInvariant()))).ToArray();

        protected override StringU8 DisplayString(in CachedRecord item, int globalIndex)
            => item.ResourceType;

        protected override ResourceTypeFlag GetValue(in CachedRecord item, int globalIndex)
            => item.Record.ResourceType;

        public bool WouldBeVisible(in Record record)
            => Filter.FilterValue.HasFlag(record.ResourceType);
    }

    private sealed class LoadStateColumn : FlagColumn<LoadStateFlag, CachedRecord>, ICheckRecord
    {
        public LoadStateColumn(FilterConfig config)
        {
            UnscaledWidth = 50;
            Filter.LoadValue(config.ResourceLoggerLoadStateFilter);
            Filter.FilterChanged += () => config.ResourceLoggerLoadStateFilter = Filter.FilterValue;
        }

        public override void DrawColumn(in CachedRecord item, int globalIndex)
        {
            if (item.Record.LoadState == LoadState.None)
                return;

            var (icon, color, tt) = item.Record.LoadState switch
            {
                LoadState.Success => (FontAwesomeIcon.CheckCircle, ColorId.IncreasedMetaValue.Value(),
                    new StringU8($"Successfully loaded ({(byte)item.Record.LoadState}).")),
                LoadState.FailedSubResource => (FontAwesomeIcon.ExclamationCircle, ColorId.DecreasedMetaValue.Value(),
                    new StringU8($"Dependencies failed to load ({(byte)item.Record.LoadState}).")),
                <= LoadState.Constructed => (FontAwesomeIcon.QuestionCircle, ColorId.UndefinedMod.Value(),
                    new StringU8($"Not yet loaded ({(byte)item.Record.LoadState}).")),
                < LoadState.Success => (FontAwesomeIcon.Clock, ColorId.FolderLine.Value(),
                    new StringU8($"Loading asynchronously ({(byte)item.Record.LoadState}).")),
                > LoadState.Success => (FontAwesomeIcon.Times, ColorId.DecreasedMetaValue.Value(),
                    new StringU8($"Failed to load ({(byte)item.Record.LoadState}).")),
            };
            ImEx.Icon.Draw(icon.Icon(), color);
            Im.Tooltip.OnHover(tt);
        }

        public override int Compare(in CachedRecord lhs, int lhsGlobalIndex, in CachedRecord rhs, int rhsGlobalIndex)
            => lhs.Record.LoadState.CompareTo(rhs.Record.LoadState);

        protected override StringU8 DisplayString(in CachedRecord item, int globalIndex)
            => StringU8.Empty;

        protected override IReadOnlyList<(LoadStateFlag Value, StringU8 Name)> EnumData { get; }
            =
            [
                (LoadStateFlag.Success, new StringU8("Loaded"u8)),
                (LoadStateFlag.Async, new StringU8("Loading"u8)),
                (LoadStateFlag.Failed, new StringU8("Failed"u8)),
                (LoadStateFlag.FailedSub, new StringU8("Dependency Failed"u8)),
                (LoadStateFlag.Unknown, new StringU8("Unknown"u8)),
                (LoadStateFlag.None, new StringU8("None"u8)),
            ];

        protected override LoadStateFlag GetValue(in CachedRecord item, int globalIndex)
            => GetValue(item.Record.LoadState);


        public bool WouldBeVisible(in Record record)
            => Filter.FilterValue.HasFlag(GetValue(record.LoadState));

        private static LoadStateFlag GetValue(LoadState value)
            => value switch
            {
                LoadState.None              => LoadStateFlag.None,
                LoadState.Success           => LoadStateFlag.Success,
                LoadState.FailedSubResource => LoadStateFlag.FailedSub,
                <= LoadState.Constructed    => LoadStateFlag.Unknown,
                < LoadState.Success         => LoadStateFlag.Async,
                > LoadState.Success         => LoadStateFlag.Failed,
            };
    }

    private sealed class HandleColumn : TextColumn<CachedRecord>, ICheckRecord
    {
        public HandleColumn(FilterConfig config)
        {
            UnscaledWidth = 120;
            Filter.Set(config.ResourceLoggerResourceFilter);
            Filter.FilterChanged += () => config.ResourceLoggerResourceFilter = Filter.Text;
        }

        public override unsafe void DrawColumn(in CachedRecord item, int globalIndex)
        {
            if (item.Record.RecordType is RecordType.Request)
                return;

            Penumbra.Dynamis.DrawPointer(item.Record.Handle);
        }

        protected override string ComparisonText(in CachedRecord item, int globalIndex)
            => item.HandleU16;

        protected override StringU8 DisplayText(in CachedRecord item, int globalIndex)
            => StringU8.Empty;

        public unsafe bool WouldBeVisible(in Record record)
            => Filter.Text.Length is 0 || Filter.WouldBeVisible($"0x{(nint)record.Handle:X}");
    }


    private abstract class OptBoolColumn : FlagColumn<BoolEnum, CachedRecord>
    {
        protected OptBoolColumn(float width)
        {
            UnscaledWidth =  width;
            Flags         &= ~TableColumnFlags.NoSort;
        }

        public override void DrawColumn(in CachedRecord item, int globalIndex)
        {
            var value = GetValue(item, globalIndex);
            if (value is BoolEnum.Unknown)
                return;

            ImEx.Icon.Draw(value is BoolEnum.True
                ? FontAwesomeIcon.Check.Icon()
                : FontAwesomeIcon.Times.Icon()
            );
        }

        protected override IReadOnlyList<(BoolEnum Value, StringU8 Name)> EnumData { get; } =
        [
            (BoolEnum.True, new StringU8("True"u8)),
            (BoolEnum.False, new StringU8("False"u8)),
            (BoolEnum.Unknown, new StringU8("Unknown"u8)),
        ];

        protected override StringU8 DisplayString(in CachedRecord item, int globalIndex)
            => StringU8.Empty;

        protected static BoolEnum ToValue(OptionalBool value)
            => value.Value switch
            {
                true  => BoolEnum.True,
                false => BoolEnum.False,
                null  => BoolEnum.Unknown,
            };
    }

    private sealed class CustomLoadColumn : OptBoolColumn, ICheckRecord
    {
        public CustomLoadColumn(FilterConfig config)
            : base(60f)
        {
            Filter.LoadValue(config.ResourceLoggerCustomFilter);
            Filter.FilterChanged += () => config.ResourceLoggerCustomFilter = Filter.FilterValue;
        }

        protected override BoolEnum GetValue(in CachedRecord item, int globalIndex)
            => ToValue(item.Record.CustomLoad);

        public bool WouldBeVisible(in Record record)
            => Filter.FilterValue.HasFlag(ToValue(record.CustomLoad));
    }

    private sealed class SynchronousLoadColumn : OptBoolColumn, ICheckRecord
    {
        public SynchronousLoadColumn(FilterConfig config)
            : base(45)
        {
            Filter.LoadValue(config.ResourceLoggerSyncFilter);
            Filter.FilterChanged += () => config.ResourceLoggerSyncFilter = Filter.FilterValue;
        }

        protected override BoolEnum GetValue(in CachedRecord item, int globalIndex)
            => ToValue(item.Record.Synchronously);

        public bool WouldBeVisible(in Record record)
            => Filter.FilterValue.HasFlag(ToValue(record.Synchronously));
    }

    private sealed class RefCountColumn : NumberColumn<uint, CachedRecord>, ICheckRecord
    {
        public RefCountColumn(FilterConfig config)
        {
            UnscaledWidth = 60;
            Filter.Set(config.ResourceLoggerRefFilter);
            Filter.FilterChanged += () => config.ResourceLoggerRefFilter = Filter.Text;
        }

        public override uint ToValue(in CachedRecord item, int globalIndex)
            => item.Record.RefCount;

        protected override StringU8 DisplayNumber(in CachedRecord item, int globalIndex)
            => item.RefCount;

        protected override string ComparisonText(in CachedRecord item, int globalIndex)
            => item.RefCount;

        public bool WouldBeVisible(in Record record)
            => Filter.WouldBeVisible(record.RefCount);
    }

    private sealed class OsThreadColumn : NumberColumn<uint, CachedRecord>, ICheckRecord
    {
        public OsThreadColumn(FilterConfig config)
        {
            UnscaledWidth = 60;
            Filter.Set(config.ResourceLoggerThreadFilter);
            Filter.FilterChanged += () => config.ResourceLoggerThreadFilter = Filter.Text;
        }

        public override uint ToValue(in CachedRecord item, int globalIndex)
            => item.Record.OsThreadId;

        protected override StringU8 DisplayNumber(in CachedRecord item, int globalIndex)
            => item.Thread;

        protected override string ComparisonText(in CachedRecord item, int globalIndex)
            => item.Thread;

        public bool WouldBeVisible(in Record record)
            => Filter.WouldBeVisible(record.OsThreadId);
    }

    public override IEnumerable<CachedRecord> GetItems()
        => new CacheListAdapter<Record, CachedRecord>(_records, arg => new CachedRecord(arg));

    protected override TableCache<CachedRecord> CreateCache()
        => new(this);
}

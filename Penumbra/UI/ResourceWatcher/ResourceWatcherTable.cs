using Dalamud.Interface;
using ImSharp;
using ImSharp.Containers;
using ImSharp.Table;
using Luna;
using Penumbra.Enums;
using Penumbra.Interop.Structs;
using Penumbra.UI.Classes;

namespace Penumbra.UI.ResourceWatcher;

public class ResourceWatcherConfig
{
    public int                  Version            = 1;
    public bool                 Enabled            = false;
    public int                  MaxEntries         = 500;
    public bool                 StoreOnlyMatching  = true;
    public bool                 WriteToLog         = false;
    public string               LogFilter          = string.Empty;
    public string               PathFilter         = string.Empty;
    public string               CollectionFilter   = string.Empty;
    public string               ObjectFilter       = string.Empty;
    public string               OriginalPathFilter = string.Empty;
    public string               ResourceFilter     = string.Empty;
    public string               CrcFilter          = string.Empty;
    public string               RefFilter          = string.Empty;
    public string               ThreadFilter       = string.Empty;
    public RecordType           RecordFilter       = Enum.GetValues<RecordType>().Or();
    public BoolEnum             CustomFilter       = BoolEnum.True | BoolEnum.False | BoolEnum.Unknown;
    public BoolEnum             SyncFilter         = BoolEnum.True | BoolEnum.False | BoolEnum.Unknown;
    public ResourceCategoryFlag CategoryFilter     = ResourceExtensions.AllResourceCategories;
    public ResourceTypeFlag     TypeFilter         = ResourceExtensions.AllResourceTypes;
    public LoadStateFlag        LoadStateFilter    = Enum.GetValues<LoadStateFlag>().Or();

    public void Save()
    { }
}

[Flags]
public enum BoolEnum : byte
{
    True    = 0x01,
    False   = 0x02,
    Unknown = 0x04,
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

internal sealed unsafe class CachedRecord(Record record)
{
    public readonly Record          Record = record;
    public readonly string          PathU16 = record.Path.ToString();
    public readonly StringU8        TypeName = new(record.RecordType.ToName());
    public readonly StringU8        Time = new($"{record.Time.ToLongTimeString()}.{record.Time.Millisecond:D4}");
    public readonly StringPair      Crc64 = new($"{record.Crc64:X16}");
    public readonly StringU8        Collection = record.Collection is null ? StringU8.Empty : new StringU8(record.Collection.Identity.Name);
    public readonly StringU8        AssociatedGameObject = new(record.AssociatedGameObject);
    public readonly string          OriginalPath = record.OriginalPath.ToString();
    public readonly StringU8        ResourceCategory = new($"{record.Category}");
    public readonly StringU8        ResourceType = new(record.ResourceType.ToString().ToLowerInvariant());
    public readonly string          HandleU16 = $"0x{(nint)record.Handle:X}";
    public readonly SizedStringPair Thread = new($"{record.OsThreadId}");
    public readonly SizedStringPair RefCount = new($"{record.RefCount}");
}

internal sealed class ResourceWatcherTable : TableBase<CachedRecord, TableCache<CachedRecord>>
{
    private readonly IReadOnlyList<Record> _records;

    public bool WouldBeVisible(Record record)
    {
        var cached = new CachedRecord(record);
        return Columns.All(c => c.WouldBeVisible(cached, -1));
    }

    public ResourceWatcherTable(ResourceWatcherConfig config, IReadOnlyList<Record> records)
        : base(new StringU8("##records"u8),
            new PathColumn(config) { Label             = new StringU8("Path"u8) },
            new RecordTypeColumn(config) { Label       = new StringU8("Record"u8) },
            new CollectionColumn(config) { Label       = new StringU8("Collection"u8) },
            new ObjectColumn(config) { Label           = new StringU8("Game Object"u8) },
            new CustomLoadColumn(config) { Label       = new StringU8("Custom"u8) },
            new SynchronousLoadColumn(config) { Label  = new StringU8("Sync"u8) },
            new OriginalPathColumn(config) { Label     = new StringU8("Original Path"u8) },
            new ResourceCategoryColumn(config) { Label = new StringU8("Category"u8) },
            new ResourceTypeColumn(config) { Label     = new StringU8("Type"u8) },
            new HandleColumn(config) { Label           = new StringU8("Resource"u8) },
            new LoadStateColumn(config) { Label        = new StringU8("State"u8) },
            new RefCountColumn(config) { Label         = new StringU8("#Ref"u8) },
            new DateColumn { Label                     = new StringU8("Time"u8) },
            new Crc64Column(config) { Label            = new StringU8("Crc64"u8) },
            new OsThreadColumn(config) { Label         = new StringU8("TID"u8) }
        )
    {
        _records = records;
    }

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


    private sealed class PathColumn : TextColumn<CachedRecord>
    {
        private readonly ResourceWatcherConfig _config;

        public PathColumn(ResourceWatcherConfig config)
        {
            _config       = config;
            UnscaledWidth = 300;
            Filter.Set(config.PathFilter);
            Filter.FilterChanged += OnFilterChanged;
        }

        private void OnFilterChanged()
        {
            _config.PathFilter = Filter.Text;
            _config.Save();
        }

        public override void DrawColumn(in CachedRecord item, int globalIndex)
        {
            DrawByteString(item.Record.Path, 290 * Im.Style.GlobalScale);
        }

        protected override string ComparisonText(in CachedRecord item, int globalIndex)
            => item.PathU16;

        protected override StringU8 DisplayText(in CachedRecord item, int globalIndex)
            => item.Record.Path;
    }

    private sealed class RecordTypeColumn : FlagColumn<RecordType, CachedRecord>
    {
        private readonly ResourceWatcherConfig _config;

        public RecordTypeColumn(ResourceWatcherConfig config)
        {
            _config       = config;
            UnscaledWidth = 80;
            Filter.LoadValue(config.RecordFilter);
            Filter.FilterChanged += OnFilterChanged;
        }

        private void OnFilterChanged()
        {
            _config.RecordFilter = Filter.FilterValue;
            _config.Save();
        }

        protected override StringU8 DisplayString(in CachedRecord item, int globalIndex)
            => item.TypeName;

        protected override IReadOnlyList<(RecordType Value, StringU8 Name)> EnumData
            => Enum.GetValues<RecordType>().Select(t => (t, new StringU8(t.ToName()))).ToArray();

        protected override RecordType GetValue(in CachedRecord item, int globalIndex)
            => item.Record.RecordType;
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

    private sealed class Crc64Column : TextColumn<CachedRecord>
    {
        private readonly ResourceWatcherConfig _config;

        public Crc64Column(ResourceWatcherConfig config)
        {
            _config       = config;
            UnscaledWidth = 17 * 8;
            Filter.Set(config.CrcFilter);
            Filter.FilterChanged += OnFilterChanged;
        }

        private void OnFilterChanged()
        {
            _config.CrcFilter = Filter.Text;
            _config.Save();
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
    }


    private sealed class CollectionColumn : TextColumn<CachedRecord>
    {
        private readonly ResourceWatcherConfig _config;

        public CollectionColumn(ResourceWatcherConfig config)
        {
            _config       = config;
            UnscaledWidth = 80;
            Filter.Set(config.CollectionFilter);
            Filter.FilterChanged += OnFilterChanged;
        }

        private void OnFilterChanged()
        {
            _config.CollectionFilter = Filter.Text;
            _config.Save();
        }

        protected override string ComparisonText(in CachedRecord item, int globalIndex)
            => item.Record.Collection?.Identity.Name ?? string.Empty;

        protected override StringU8 DisplayText(in CachedRecord item, int globalIndex)
            => item.Collection;
    }

    private sealed class ObjectColumn : TextColumn<CachedRecord>
    {
        private readonly ResourceWatcherConfig _config;

        public ObjectColumn(ResourceWatcherConfig config)
        {
            _config       = config;
            UnscaledWidth = 150;
            Filter.Set(config.ObjectFilter);
            Filter.FilterChanged += OnFilterChanged;
        }

        private void OnFilterChanged()
        {
            _config.ObjectFilter = Filter.Text;
            _config.Save();
        }

        protected override string ComparisonText(in CachedRecord item, int globalIndex)
            => item.Record.AssociatedGameObject;

        protected override StringU8 DisplayText(in CachedRecord item, int globalIndex)
            => item.AssociatedGameObject;
    }

    private sealed class OriginalPathColumn : TextColumn<CachedRecord>
    {
        private readonly ResourceWatcherConfig _config;

        public OriginalPathColumn(ResourceWatcherConfig config)
        {
            _config       = config;
            UnscaledWidth = 200;
            Filter.Set(config.OriginalPathFilter);
            Filter.FilterChanged += OnFilterChanged;
        }

        private void OnFilterChanged()
        {
            _config.OriginalPathFilter = Filter.Text;
            _config.Save();
        }

        public override void DrawColumn(in CachedRecord item, int globalIndex)
        {
            DrawByteString(item.Record.OriginalPath, 190 * Im.Style.GlobalScale);
        }

        protected override string ComparisonText(in CachedRecord item, int globalIndex)
            => item.OriginalPath;

        protected override StringU8 DisplayText(in CachedRecord item, int globalIndex)
            => item.Record.OriginalPath;
    }

    private sealed class ResourceCategoryColumn : FlagColumn<ResourceCategoryFlag, CachedRecord>
    {
        private readonly ResourceWatcherConfig _config;

        public ResourceCategoryColumn(ResourceWatcherConfig config)
        {
            _config       = config;
            UnscaledWidth = 80;
            Filter.LoadValue(config.CategoryFilter);
            Filter.FilterChanged += OnFilterChanged;
        }

        private void OnFilterChanged()
        {
            _config.CategoryFilter = Filter.FilterValue;
            _config.Save();
        }

        protected override StringU8 DisplayString(in CachedRecord item, int globalIndex)
            => item.ResourceCategory;

        protected override IReadOnlyList<(ResourceCategoryFlag Value, StringU8 Name)> EnumData { get; } =
            Enum.GetValues<ResourceCategoryFlag>().Select(r => (r, new StringU8($"{r}"))).ToArray();

        protected override ResourceCategoryFlag GetValue(in CachedRecord item, int globalIndex)
            => item.Record.Category;
    }

    private sealed class ResourceTypeColumn : FlagColumn<ResourceTypeFlag, CachedRecord>
    {
        private readonly ResourceWatcherConfig _config;

        public ResourceTypeColumn(ResourceWatcherConfig config)
        {
            _config       = config;
            UnscaledWidth = 50;
            Filter.LoadValue(config.TypeFilter);
            Filter.FilterChanged += OnFilterChanged;
        }

        private void OnFilterChanged()
        {
            _config.TypeFilter = Filter.FilterValue;
            _config.Save();
        }

        protected override IReadOnlyList<(ResourceTypeFlag Value, StringU8 Name)> EnumData { get; } =
            Enum.GetValues<ResourceTypeFlag>().Select(r => (r, new StringU8(r.ToString().ToLowerInvariant()))).ToArray();

        protected override StringU8 DisplayString(in CachedRecord item, int globalIndex)
            => item.ResourceType;

        protected override ResourceTypeFlag GetValue(in CachedRecord item, int globalIndex)
            => item.Record.ResourceType;
    }

    private sealed class LoadStateColumn : FlagColumn<LoadStateFlag, CachedRecord>
    {
        private readonly ResourceWatcherConfig _config;

        public LoadStateColumn(ResourceWatcherConfig config)
        {
            _config       = config;
            UnscaledWidth = 50;
            Filter.LoadValue(config.LoadStateFilter);
            Filter.FilterChanged += OnFilterChanged;
        }

        private void OnFilterChanged()
        {
            _config.LoadStateFilter = Filter.FilterValue;
            _config.Save();
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
            => item.Record.LoadState switch
            {
                LoadState.None              => LoadStateFlag.None,
                LoadState.Success           => LoadStateFlag.Success,
                LoadState.FailedSubResource => LoadStateFlag.FailedSub,
                <= LoadState.Constructed    => LoadStateFlag.Unknown,
                < LoadState.Success         => LoadStateFlag.Async,
                > LoadState.Success         => LoadStateFlag.Failed,
            };
    }

    private sealed class HandleColumn : TextColumn<CachedRecord>
    {
        private readonly ResourceWatcherConfig _config;

        public HandleColumn(ResourceWatcherConfig config)
        {
            _config       = config;
            UnscaledWidth = 120;
            Filter.Set(config.ResourceFilter);
            Filter.FilterChanged += OnFilterChanged;
        }

        private void OnFilterChanged()
        {
            _config.ResourceFilter = Filter.Text;
            _config.Save();
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

    private sealed class CustomLoadColumn : OptBoolColumn
    {
        private readonly ResourceWatcherConfig _config;

        public CustomLoadColumn(ResourceWatcherConfig config)
            : base(60f)
        {
            _config = config;
            Filter.LoadValue(config.CustomFilter);
            Filter.FilterChanged += OnFilterChanged;
        }

        private void OnFilterChanged()
        {
            _config.CustomFilter = Filter.FilterValue;
            _config.Save();
        }

        protected override BoolEnum GetValue(in CachedRecord item, int globalIndex)
            => ToValue(item.Record.CustomLoad);
    }

    private sealed class SynchronousLoadColumn : OptBoolColumn
    {
        private readonly ResourceWatcherConfig _config;

        public SynchronousLoadColumn(ResourceWatcherConfig config)
            : base(45)
        {
            _config = config;
            Filter.LoadValue(config.SyncFilter);
            Filter.FilterChanged += OnFilterChanged;
        }

        private void OnFilterChanged()
        {
            _config.SyncFilter = Filter.FilterValue;
            _config.Save();
        }

        protected override BoolEnum GetValue(in CachedRecord item, int globalIndex)
            => ToValue(item.Record.Synchronously);
    }

    private sealed class RefCountColumn : NumberColumn<uint, CachedRecord>
    {
        private readonly ResourceWatcherConfig _config;

        public RefCountColumn(ResourceWatcherConfig config)
        {
            _config       = config;
            UnscaledWidth = 60;
            Filter.Set(config.RefFilter);
            Filter.FilterChanged += OnFilterChanged;
        }

        private void OnFilterChanged()
        {
            _config.RefFilter = Filter.Text;
            _config.Save();
        }

        public override uint ToValue(in CachedRecord item, int globalIndex)
            => item.Record.RefCount;

        protected override SizedString DisplayNumber(in CachedRecord item, int globalIndex)
            => item.RefCount;

        protected override string ComparisonText(in CachedRecord item, int globalIndex)
            => item.RefCount;
    }

    private sealed class OsThreadColumn : NumberColumn<uint, CachedRecord>
    {
        private readonly ResourceWatcherConfig _config;

        public OsThreadColumn(ResourceWatcherConfig config)
        {
            _config       = config;
            UnscaledWidth = 60;
            Filter.Set(config.ThreadFilter);
            Filter.FilterChanged += OnFilterChanged;
        }

        private void OnFilterChanged()
        {
            _config.ThreadFilter = Filter.Text;
            _config.Save();
        }

        public override uint ToValue(in CachedRecord item, int globalIndex)
            => item.Record.OsThreadId;

        protected override SizedString DisplayNumber(in CachedRecord item, int globalIndex)
            => item.Thread;

        protected override string ComparisonText(in CachedRecord item, int globalIndex)
            => item.Thread;
    }

    public override IEnumerable<CachedRecord> GetItems()
        => new CacheListAdapter<Record, CachedRecord>(_records, arg => new CachedRecord(arg));

    protected override TableCache<CachedRecord> CreateCache()
        => new(this);
}

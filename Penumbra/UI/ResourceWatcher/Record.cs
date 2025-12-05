using ImSharp;
using Luna;
using Luna.Generators;
using Penumbra.Collections;
using Penumbra.Enums;
using Penumbra.Interop;
using Penumbra.Interop.Structs;
using Penumbra.String;
using Penumbra.String.Classes;

namespace Penumbra.UI.ResourceWatcher;

[Flags]
[NamedEnum(Utf16: false)]
public enum RecordType : byte
{
    [Name("REQ")]
    Request = 0x01,

    [Name("LOAD")]
    ResourceLoad = 0x02,

    [Name("FILE")]
    FileLoad = 0x04,

    [Name("DEST")]
    Destruction = 0x08,

    [Name("DONE")]
    ResourceComplete = 0x10,
}

internal unsafe struct Record()
{
    public DateTime             Time;
    public StringU8             Path;
    public StringU8             OriginalPath;
    public string               AssociatedGameObject = string.Empty;
    public ModCollection?       Collection;
    public ResourceHandle*      Handle;
    public ResourceTypeFlag     ResourceType;
    public ulong                Crc64;
    public uint                 RefCount;
    public ResourceCategoryFlag Category;
    public RecordType           RecordType;
    public OptionalBool         Synchronously;
    public OptionalBool         ReturnValue;
    public OptionalBool         CustomLoad;
    public LoadState            LoadState;
    public uint                 OsThreadId;


    public static Record CreateRequest(CiByteString path, bool sync)
        => new()
        {
            Time                 = DateTime.UtcNow,
            Path                 = new StringU8(path.Span, false),
            OriginalPath         = StringU8.Empty,
            Collection           = null,
            Handle               = null,
            ResourceType         = ResourceExtensions.Type(path).ToFlag(),
            Category             = ResourceExtensions.Category(path).ToFlag(),
            RefCount             = 0,
            RecordType           = RecordType.Request,
            Synchronously        = sync,
            ReturnValue          = OptionalBool.Null,
            CustomLoad           = OptionalBool.Null,
            AssociatedGameObject = string.Empty,
            LoadState            = LoadState.None,
            Crc64                = 0,
            OsThreadId           = ProcessThreadApi.GetCurrentThreadId(),
        };

    public static Record CreateRequest(CiByteString path, bool sync, FullPath fullPath, ResolveData resolve)
        => new()
        {
            Time                 = DateTime.UtcNow,
            Path                 = new StringU8(fullPath.InternalName.Span, false),
            OriginalPath         = new StringU8(path.Span,                  false),
            Collection           = resolve.Valid ? resolve.ModCollection : null,
            Handle               = null,
            ResourceType         = ResourceExtensions.Type(path).ToFlag(),
            Category             = ResourceExtensions.Category(path).ToFlag(),
            RefCount             = 0,
            RecordType           = RecordType.Request,
            Synchronously        = sync,
            ReturnValue          = OptionalBool.Null,
            CustomLoad           = fullPath.InternalName != path,
            AssociatedGameObject = string.Empty,
            LoadState            = LoadState.None,
            Crc64                = fullPath.Crc64,
            OsThreadId           = ProcessThreadApi.GetCurrentThreadId(),
        };

    public static Record CreateDefaultLoad(CiByteString path, ResourceHandle* handle, ModCollection collection, string associatedGameObject)
    {
        var p = new StringU8(path.Span, false);
        return new Record
        {
            Time                 = DateTime.UtcNow,
            Path                 = p,
            OriginalPath         = p,
            Collection           = collection,
            Handle               = handle,
            ResourceType         = handle->FileType.ToFlag(),
            Category             = handle->Category.ToFlag(),
            RefCount             = handle->RefCount,
            RecordType           = RecordType.ResourceLoad,
            Synchronously        = OptionalBool.Null,
            ReturnValue          = OptionalBool.Null,
            CustomLoad           = false,
            AssociatedGameObject = associatedGameObject,
            LoadState            = handle->LoadState,
            Crc64                = 0,
            OsThreadId           = ProcessThreadApi.GetCurrentThreadId(),
        };
    }

    public static Record CreateLoad(FullPath path, CiByteString originalPath, ResourceHandle* handle, ModCollection collection,
        string associatedGameObject)
        => new()
        {
            Time                 = DateTime.UtcNow,
            Path                 = new StringU8(path.InternalName.Span, false),
            OriginalPath         = new StringU8(originalPath.Span,      false),
            Collection           = collection,
            Handle               = handle,
            ResourceType         = handle->FileType.ToFlag(),
            Category             = handle->Category.ToFlag(),
            RefCount             = handle->RefCount,
            RecordType           = RecordType.ResourceLoad,
            Synchronously        = OptionalBool.Null,
            ReturnValue          = OptionalBool.Null,
            CustomLoad           = true,
            AssociatedGameObject = associatedGameObject,
            LoadState            = handle->LoadState,
            Crc64                = path.Crc64,
            OsThreadId           = ProcessThreadApi.GetCurrentThreadId(),
        };

    public static Record CreateDestruction(ResourceHandle* handle)
    {
        var path = new StringU8(handle->FileName().Span, false);
        return new Record
        {
            Time                 = DateTime.UtcNow,
            Path                 = path,
            OriginalPath         = StringU8.Empty,
            Collection           = null,
            Handle               = handle,
            ResourceType         = handle->FileType.ToFlag(),
            Category             = handle->Category.ToFlag(),
            RefCount             = handle->RefCount,
            RecordType           = RecordType.Destruction,
            Synchronously        = OptionalBool.Null,
            ReturnValue          = OptionalBool.Null,
            CustomLoad           = OptionalBool.Null,
            AssociatedGameObject = string.Empty,
            LoadState            = handle->LoadState,
            Crc64                = 0,
            OsThreadId           = ProcessThreadApi.GetCurrentThreadId(),
        };
    }

    public static Record CreateFileLoad(CiByteString path, ResourceHandle* handle, bool ret, bool custom)
        => new()
        {
            Time                 = DateTime.UtcNow,
            Path                 = new StringU8(path.Span, false),
            OriginalPath         = StringU8.Empty,
            Collection           = null,
            Handle               = handle,
            ResourceType         = handle->FileType.ToFlag(),
            Category             = handle->Category.ToFlag(),
            RefCount             = handle->RefCount,
            RecordType           = RecordType.FileLoad,
            Synchronously        = OptionalBool.Null,
            ReturnValue          = ret,
            CustomLoad           = custom,
            AssociatedGameObject = string.Empty,
            LoadState            = handle->LoadState,
            Crc64                = 0,
            OsThreadId           = ProcessThreadApi.GetCurrentThreadId(),
        };

    public static Record CreateResourceComplete(CiByteString path, ResourceHandle* handle, Utf8GamePath originalPath,
        ReadOnlySpan<byte> additionalData)
        => new()
        {
            Time                 = DateTime.UtcNow,
            Path                 = CombinedPath(path, additionalData),
            OriginalPath         = new StringU8(originalPath.Path.Span, false),
            Collection           = null,
            Handle               = handle,
            ResourceType         = handle->FileType.ToFlag(),
            Category             = handle->Category.ToFlag(),
            RefCount             = handle->RefCount,
            RecordType           = RecordType.ResourceComplete,
            Synchronously        = false,
            ReturnValue          = OptionalBool.Null,
            CustomLoad           = OptionalBool.Null,
            AssociatedGameObject = string.Empty,
            LoadState            = handle->LoadState,
            Crc64                = 0,
            OsThreadId           = ProcessThreadApi.GetCurrentThreadId(),
        };

    private static StringU8 CombinedPath(CiByteString path, ReadOnlySpan<byte> additionalData)
    {
        if (additionalData.Length is 0)
            return new StringU8(path.Span, false);

        fixed (byte* ptr = additionalData)
        {
            // If a path has additional data and is split, it is always in the form of |{additionalData}|{path},
            // so we can just read from the start of additional data - 1 and sum their length +2 for the pipes.
            return new StringU8(new ReadOnlySpan<byte>(ptr - 1, additionalData.Length + 2 + path.Length));
        }
    }
}

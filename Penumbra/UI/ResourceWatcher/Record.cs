using OtterGui.Classes;
using Penumbra.Collections;
using Penumbra.Enums;
using Penumbra.Interop.Structs;
using Penumbra.String;
using Penumbra.String.Classes;

namespace Penumbra.UI.ResourceWatcher;

[Flags]
public enum RecordType : byte
{
    Request          = 0x01,
    ResourceLoad     = 0x02,
    FileLoad         = 0x04,
    Destruction      = 0x08,
    ResourceComplete = 0x10,
}

internal unsafe struct Record
{
    public DateTime             Time;
    public CiByteString         Path;
    public CiByteString         OriginalPath;
    public string               AssociatedGameObject;
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


    public static Record CreateRequest(CiByteString path, bool sync)
        => new()
        {
            Time                 = DateTime.UtcNow,
            Path                 = path.IsOwned ? path : path.Clone(),
            OriginalPath         = CiByteString.Empty,
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
        };

    public static Record CreateDefaultLoad(CiByteString path, ResourceHandle* handle, ModCollection collection, string associatedGameObject)
    {
        path = path.IsOwned ? path : path.Clone();
        return new Record
        {
            Time                 = DateTime.UtcNow,
            Path                 = path,
            OriginalPath         = path,
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
        };
    }

    public static Record CreateLoad(FullPath path, CiByteString originalPath, ResourceHandle* handle, ModCollection collection,
        string associatedGameObject)
        => new()
        {
            Time                 = DateTime.UtcNow,
            Path                 = path.InternalName.IsOwned ? path.InternalName : path.InternalName.Clone(),
            OriginalPath         = originalPath.IsOwned ? originalPath : originalPath.Clone(),
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
        };

    public static Record CreateDestruction(ResourceHandle* handle)
    {
        var path = handle->FileName().Clone();
        return new Record
        {
            Time                 = DateTime.UtcNow,
            Path                 = path,
            OriginalPath         = CiByteString.Empty,
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
        };
    }

    public static Record CreateFileLoad(CiByteString path, ResourceHandle* handle, bool ret, bool custom)
        => new()
        {
            Time                 = DateTime.UtcNow,
            Path                 = path.IsOwned ? path : path.Clone(),
            OriginalPath         = CiByteString.Empty,
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
        };

    public static Record CreateResourceComplete(CiByteString path, ResourceHandle* handle, Utf8GamePath originalPath, ReadOnlySpan<byte> additionalData)
        => new()
        {
            Time                 = DateTime.UtcNow,
            Path                 = CombinedPath(path, additionalData),
            OriginalPath         = originalPath.Path.IsOwned ? originalPath.Path : originalPath.Path.Clone(),
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
        };

    private static CiByteString CombinedPath(CiByteString path, ReadOnlySpan<byte> additionalData)
    {
        if (additionalData.Length is 0)
            return path.IsOwned ? path : path.Clone();

        fixed (byte* ptr = additionalData)
        {
            // If a path has additional data and is split, it is always in the form of |{additionalData}|{path},
            // so we can just read from the start of additional data - 1 and sum their length +2 for the pipes.
            return new CiByteString(new ReadOnlySpan<byte>(ptr - 1, additionalData.Length + 2 + path.Length)).Clone();
        }
    }
}

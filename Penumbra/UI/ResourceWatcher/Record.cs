using OtterGui.Classes;
using Penumbra.Collections;
using Penumbra.Enums;
using Penumbra.Interop.Structs;
using Penumbra.String;

namespace Penumbra.UI.ResourceWatcher;

[Flags]
public enum RecordType : byte
{
    Request      = 0x01,
    ResourceLoad = 0x02,
    FileLoad     = 0x04,
    Destruction  = 0x08,
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
    public ResourceCategoryFlag Category;
    public uint                 RefCount;
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
        };
    }

    public static Record CreateLoad(CiByteString path, CiByteString originalPath, ResourceHandle* handle, ModCollection collection,
        string associatedGameObject)
        => new()
        {
            Time                 = DateTime.UtcNow,
            Path                 = path.IsOwned ? path : path.Clone(),
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
        };
}

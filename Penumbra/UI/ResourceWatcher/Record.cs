using System;
using OtterGui.Classes;
using Penumbra.Collections;
using Penumbra.GameData.Enums;
using Penumbra.Interop.Structs;
using Penumbra.String;

namespace Penumbra.UI;

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
    public ByteString           Path;
    public ByteString           OriginalPath;
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

    public static Record CreateRequest(ByteString path, bool sync)
        => new()
        {
            Time                 = DateTime.UtcNow,
            Path                 = path.IsOwned ? path : path.Clone(),
            OriginalPath         = ByteString.Empty,
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
        };

    public static Record CreateDefaultLoad(ByteString path, ResourceHandle* handle, ModCollection collection, string associatedGameObject)
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
        };
    }

    public static Record CreateLoad(ByteString path, ByteString originalPath, ResourceHandle* handle, ModCollection collection,
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
        };

    public static Record CreateDestruction(ResourceHandle* handle)
    {
        var path = handle->FileName().Clone();
        return new Record
        {
            Time                 = DateTime.UtcNow,
            Path                 = path,
            OriginalPath         = ByteString.Empty,
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
        };
    }

    public static Record CreateFileLoad(ByteString path, ResourceHandle* handle, bool ret, bool custom)
        => new()
        {
            Time                 = DateTime.UtcNow,
            Path                 = path.IsOwned ? path : path.Clone(),
            OriginalPath         = ByteString.Empty,
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
        };
}

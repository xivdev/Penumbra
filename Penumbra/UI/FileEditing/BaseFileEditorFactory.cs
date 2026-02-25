using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using Penumbra.Interop.PathResolving;

namespace Penumbra.UI.FileEditing;

public abstract class BaseFileEditorFactory(IDataManager gameData) : IFileEditorFactory
{
    protected readonly IDataManager GameData = gameData;

    public virtual bool SupportsFile(string path)
        => SupportsPath(path) && File.Exists(path);

    public virtual IFileEditor CreateForFile(string path, bool writable, FileEditingContext? context)
        => CreateForData(File.ReadAllBytes(path), path, writable, context);

    public virtual bool SupportsGameFile(string path)
        => SupportsPath(path) && GameData.FileExists(path);

    public virtual IFileEditor CreateForGameFile(string path, FileEditingContext? context)
    {
        var file = GameData.GetFile(path);
        if (file is null)
            throw new Exception($"File {path} not found in game index");

        return CreateForData(file.Data, path, false, context);
    }

    public virtual unsafe bool SupportsResourceHandle(ResourceHandle* handle)
    {
        PathDataHandler.Split(handle->FileName.AsSpan(), out var path, out _);

        return SupportsPath(Encoding.UTF8.GetString(path)) && !handle->GetDataSpan().IsEmpty;
    }

    public virtual unsafe IFileEditor CreateForResourceHandle(ResourceHandle* handle, FileEditingContext? context)
    {
        PathDataHandler.Split(handle->FileName.AsSpan(), out var path, out _);
        var pathStr = Encoding.UTF8.GetString(path);

        var data = handle->GetDataSpan();
        if (data.IsEmpty)
            throw new Exception($"Resource handle at 0x{(nint)handle:X} ({pathStr}) has no data");

        return CreateForData(data, pathStr, false, context);
    }

    public abstract bool SupportsPath(string path);

    public abstract IFileEditor CreateForData(byte[] data, string path, bool writable, FileEditingContext? context);

    public virtual IFileEditor CreateForData(ReadOnlySpan<byte> data, string path, bool writable, FileEditingContext? context)
        => CreateForData(data.ToArray(), path, writable, context);
}

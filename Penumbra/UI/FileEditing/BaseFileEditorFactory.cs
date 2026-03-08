using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using Penumbra.Api.Enums;
using Penumbra.Interop.PathResolving;

namespace Penumbra.UI.FileEditing;

public abstract class BaseFileEditorFactory(IDataManager gameData) : IFileEditorFactory
{
    protected readonly IDataManager GameData = gameData;

    public abstract string Identifier { get; }

    public abstract string DisplayName { get; }

    public abstract IEnumerable<ResourceType>? SupportedResourceTypes { get; }

    public virtual bool SupportsFile(string path, string? gamePath)
        => SupportsPath(path, gamePath) && File.Exists(path);

    public virtual IFileEditor CreateForFile(string path, bool writable, string? gamePath, FileEditingContext? context)
        => CreateForData(File.ReadAllBytes(path), path, writable, gamePath, context);

    public virtual bool SupportsGameFile(string path)
        => SupportsPath(path, path) && GameData.FileExists(path);

    public virtual IFileEditor CreateForGameFile(string path, FileEditingContext? context)
    {
        var file = GameData.GetFile(path);
        if (file is null)
            throw new Exception($"File {path} not found in game index");

        return CreateForData(file.Data, path, false, path, context);
    }

    public virtual unsafe bool SupportsResourceHandle(ResourceHandle* handle, string? gamePath)
    {
        PathDataHandler.Split(handle->FileName.AsSpan(), out var path, out _);

        return SupportsPath(Encoding.UTF8.GetString(path), gamePath) && !handle->GetDataSpan().IsEmpty;
    }

    public virtual unsafe IFileEditor CreateForResourceHandle(ResourceHandle* handle, string? gamePath, FileEditingContext? context)
    {
        PathDataHandler.Split(handle->FileName.AsSpan(), out var path, out _);
        var pathStr = Encoding.UTF8.GetString(path);

        var data = handle->GetDataSpan();
        if (data.IsEmpty)
            throw new Exception($"Resource handle at 0x{(nint)handle:X} ({pathStr}) has no data");

        return CreateForData(data, pathStr, false, gamePath, context);
    }

    public virtual bool SupportsPath(string path, string? gamePath)
        => SupportedResourceTypes switch
        {
            null          => true,
            var supported => supported.Contains(ResourceType.FromPath(path)),
        };

    public abstract IFileEditor CreateForData(byte[] data, string path, bool writable, string? gamePath, FileEditingContext? context);

    public virtual IFileEditor CreateForData(ReadOnlySpan<byte> data, string path, bool writable, string? gamePath, FileEditingContext? context)
        => CreateForData(data.ToArray(), path, writable, gamePath, context);
}

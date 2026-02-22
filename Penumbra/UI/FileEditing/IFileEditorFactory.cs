using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;

namespace Penumbra.UI.FileEditing;

public interface IFileEditorFactory
{
    public bool SupportsFile(string path);

    public IFileEditor CreateForFile(string path, bool writable, FileEditingContext? context);

    public bool SupportsGameFile(string path);

    public IFileEditor CreateForGameFile(string path, FileEditingContext? context);

    public unsafe bool SupportsResourceHandle(ResourceHandle* handle);

    public unsafe IFileEditor CreateForResourceHandle(ResourceHandle* handle, FileEditingContext? context);
}

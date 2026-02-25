using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;

namespace Penumbra.UI.FileEditing;

/// <summary>
///   Creates ImGui-based editors for game files.
///   If the files come from a read-only location (such as the game archive or live memory), the editors should still allow saving a copy on disk, 
/// </summary>
public interface IFileEditorFactory
{
    /// <summary> Determines whether this factory supports creating an editor for the given on-disk file. </summary>
    /// <param name="path"> The path to the on-disk file to test. </param>
    /// <returns> Whether this factory supports the given file. </returns>
    /// <remarks> This should return <c>false</c> if the file does not exist. </remarks>
    public bool SupportsFile(string path);

    /// <summary> Creates an editor for the given on-disk file. </summary>
    /// <param name="path"> The path to the on-disk file to open. </param>
    /// <param name="writable"> Whether persistent changes may be made to <paramref name="path"/>. </param>
    /// <param name="context"> An editing context, if available, otherwise <c>null</c>. </param>
    /// <returns> An editor for the given file. </returns>
    public IFileEditor CreateForFile(string path, bool writable, FileEditingContext? context);

    /// <summary> Determines whether this factory supports creating an editor for the given game data file. </summary>
    /// <param name="path"> The path to the game data file to test. </param>
    /// <returns> Whether this factory supports the given file. </returns>
    /// <remarks> This should return false if the file does not exist. </remarks>
    public bool SupportsGameFile(string path);

    /// <summary>
    ///   Creates an editor for the given game data file.
    ///   The editor may not make persistent changes to the original file, but may make a copy, modified or not.
    /// </summary>
    /// <param name="path"> The path to the game data file to open. </param>
    /// <param name="context"> An editing context, if available, otherwise <c>null</c>. </param>
    /// <returns> An editor for the given file. </returns>
    public IFileEditor CreateForGameFile(string path, FileEditingContext? context);

    /// <summary> Determines whether this factory supports creating an editor for the given in-memory resource. </summary>
    /// <param name="handle"> A pointer to the in-memory resource to test. </param>
    /// <returns> Whether this factory supports the given resource. </returns>
    public unsafe bool SupportsResourceHandle(ResourceHandle* handle);

    /// <summary>
    ///   Creates an editor for the given in-memory resource.
    ///   The editor may not make persistent changes to the resource, but may make a copy, modified or not.
    /// </summary>
    /// <param name="handle"> A pointer to the in-memory resource to open. </param>
    /// <param name="context"> An editing context, if available, otherwise <c>null</c>. </param>
    /// <returns> An editor for the given resource. </returns>
    public unsafe IFileEditor CreateForResourceHandle(ResourceHandle* handle, FileEditingContext? context);
}

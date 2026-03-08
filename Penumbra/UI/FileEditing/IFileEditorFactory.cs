using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using Penumbra.Api.Enums;

namespace Penumbra.UI.FileEditing;

/// <summary>
///   Creates ImGui-based editors for game files.
///   If the files come from a read-only location (such as the game archive or live memory), the editors should still allow saving a copy on disk, 
/// </summary>
public interface IFileEditorFactory
{
    /// <summary> A unique identifier for this factory. Must not change once the factory is registered. </summary>
    /// <remarks> This should be the fully-qualified class name of the editor implementation, or based on it. </remarks>
    public string Identifier { get; }

    /// <summary> A user-visible name for this factory. </summary>
    public string DisplayName { get; }

    /// <summary> A set of resource types supported by this factory. Must not change once the factory is registered. </summary>
    /// <remarks>
    ///   Return <c>null</c> if this factory provides generic viewers/editors that are type-agnostic, for example hex viewers. <br />
    ///   Editors with a set of supported resource types will always take priority in the UI over generic ones.
    /// </remarks>
    public IEnumerable<ResourceType>? SupportedResourceTypes { get; }

    /// <summary> Determines whether this factory supports creating an editor for the given on-disk file. </summary>
    /// <param name="path"> The path to the on-disk file to test. </param>
    /// <param name="gamePath"> An optional game path to use as reference to resolve satellite files. </param>
    /// <returns> Whether this factory supports the given file. </returns>
    /// <remarks> This should return <c>false</c> if the file does not exist. </remarks>
    public bool SupportsFile(string path, string? gamePath);

    /// <summary> Creates an editor for the given on-disk file. </summary>
    /// <param name="path"> The path to the on-disk file to open. </param>
    /// <param name="writable"> Whether persistent changes may be made to <paramref name="path"/>. </param>
    /// <param name="gamePath"> An optional game path to use as reference to resolve satellite files. </param>
    /// <param name="context"> An editing context, if available, otherwise <c>null</c>. </param>
    /// <returns> An editor for the given file. </returns>
    public IFileEditor CreateForFile(string path, bool writable, string? gamePath, FileEditingContext? context);

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
    /// <param name="gamePath"> An optional game path to use as reference to resolve satellite files. </param>
    /// <returns> Whether this factory supports the given resource. </returns>
    public unsafe bool SupportsResourceHandle(ResourceHandle* handle, string? gamePath);

    /// <summary>
    ///   Creates an editor for the given in-memory resource.
    ///   The editor may not make persistent changes to the resource, but may make a copy, modified or not.
    /// </summary>
    /// <param name="handle"> A pointer to the in-memory resource to open. </param>
    /// <param name="gamePath"> An optional game path to use as reference to resolve satellite files. </param>
    /// <param name="context"> An editing context, if available, otherwise <c>null</c>. </param>
    /// <returns> An editor for the given resource. </returns>
    public unsafe IFileEditor CreateForResourceHandle(ResourceHandle* handle, string? gamePath, FileEditingContext? context);
}

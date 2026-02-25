using Penumbra.GameData.Files;

namespace Penumbra.UI.FileEditing;

public interface IFileEditor : IDisposable, IWritable
{
    /// <summary> Triggered when the user performs an action that should result in an immediate file save. </summary>
    public event Action? SaveRequested;

    /// <summary> Serializes the edited file's contents into a byte array. </summary>
    /// <returns> A task that eventually resolves to an array of the file's bytes. </returns>
    public Task<byte[]> WriteAsync();

    /// <summary> Draws the editor's toolbar, using ImGui calls. </summary>
    /// <param name="disabled"> Whether the editor is read-only. </param>
    /// <returns> Whether any change was made during this frame. </returns>
    /// <remarks> The toolbar may be drawn above the main editor frame, and should occupy a constant and rather limited height. </remarks>
    public bool DrawToolbar(bool disabled);

    /// <summary> Draws the editor's main panel, using ImGui calls. </summary>
    /// <param name="disabled"> Whether the editor is read-only. </param>
    /// <returns> Whether any change was made during this frame. </returns>
    public bool DrawPanel(bool disabled);
}

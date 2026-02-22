using Penumbra.GameData.Files;

namespace Penumbra.UI.FileEditing;

public interface IFileEditor : IDisposable, IWritable
{
    public event Action? SaveRequested;

    public bool DrawToolbar(bool disabled);

    public bool DrawPanel(bool disabled);
}

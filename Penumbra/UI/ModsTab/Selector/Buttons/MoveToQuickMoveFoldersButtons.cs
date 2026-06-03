using ImSharp;
using Luna;

namespace Penumbra.UI.ModsTab.Selector;

public sealed class MoveToQuickMoveFoldersButtons(ModFileSystemDrawer drawer) : BaseButton<IFileSystemData>
{
    public override ReadOnlySpan<byte> Label(in IFileSystemData data)
        => throw new NotImplementedException();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void MoveDataEntry(in IFileSystemData data, ReadOnlySpan<char> currentName, string currentPath, string folder, Im.IdDisposable id)
    {
        if (folder.Length is 0)
            return;

        var targetPath = $"{folder}/{currentName}";
        if (drawer.FileSystem.Equal(currentPath, targetPath))
            return;

        id.PushNext();
        if (Im.Menu.Item($"Move to {folder}"))
        {
            if (data.Selected)
                foreach (var path in drawer.FileSystem.Selection.OrderedNodes)
                    drawer.FileSystem.RenameAndMoveWithDuplicates(path, $"{folder}/{path.Name}");
            else
                drawer.FileSystem.RenameAndMoveWithDuplicates(data, targetPath);
        }

        Im.Tooltip.OnHover("Move the selected objects to a previously set-up quick move location, if possible."u8);
        id.Pop();
    }

    public override bool DrawMenuItem(in IFileSystemData data)
    {
        var       currentName = data.Name;
        var       currentPath = data.FullPath;
        using var id          = new Im.IdDisposable();

        MoveDataEntry(data, currentName, currentPath, drawer.Config.QuickMoveFolder1, id);
        MoveDataEntry(data, currentName, currentPath, drawer.Config.QuickMoveFolder2, id);
        MoveDataEntry(data, currentName, currentPath, drawer.Config.QuickMoveFolder3, id);
        return false;
    }
}

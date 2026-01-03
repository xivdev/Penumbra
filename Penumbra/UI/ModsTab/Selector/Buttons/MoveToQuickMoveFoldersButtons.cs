using ImSharp;
using Luna;

namespace Penumbra.UI.ModsTab.Selector;

public sealed class MoveToQuickMoveFoldersButtons(ModFileSystemDrawer drawer) : BaseButton<IFileSystemData>
{
    public override ReadOnlySpan<byte> Label(in IFileSystemData data)
        => throw new NotImplementedException();

    public override bool DrawMenuItem(in IFileSystemData data)
    {
        var       currentName = data.Name;
        var       currentPath = data.FullPath;
        using var id          = new Im.IdDisposable();
        if (drawer.Config.QuickMoveFolder1.Length > 0)
        {
            id.Push(0);
            var targetPath = $"{drawer.Config.QuickMoveFolder1}/{currentName}";
            if (!drawer.FileSystem.Equal(currentPath, targetPath))
            {
                if (Im.Menu.Item($"Move to {drawer.Config.QuickMoveFolder1}"))
                {
                    foreach (var path in drawer.FileSystem.Selection.OrderedNodes)
                    {
                        if (path != data)
                            drawer.FileSystem.RenameAndMoveWithDuplicates(path, $"{drawer.Config.QuickMoveFolder1}/{path.Name}");
                    }

                    drawer.FileSystem.RenameAndMoveWithDuplicates(data, targetPath);
                }
                Im.Tooltip.OnHover("Move the selected objects to a previously set-up quick move location, if possible."u8);
            }

            id.Pop();
        }

        if (drawer.Config.QuickMoveFolder2.Length > 0)
        {
            id.Push(1);
            var targetPath = $"{drawer.Config.QuickMoveFolder2}/{currentName}";
            if (!drawer.FileSystem.Equal(currentPath, targetPath))
            {
                if (Im.Menu.Item($"Move to {drawer.Config.QuickMoveFolder2}"))
                {
                    foreach (var path in drawer.FileSystem.Selection.OrderedNodes)
                    {
                        if (path != data)
                            drawer.FileSystem.RenameAndMoveWithDuplicates(path, $"{drawer.Config.QuickMoveFolder2}/{path.Name}");
                    }

                    drawer.FileSystem.RenameAndMoveWithDuplicates(data, targetPath);
                }
                Im.Tooltip.OnHover("Move the selected objects to a previously set-up quick move location, if possible."u8);
            }

            id.Pop();
        }

        if (drawer.Config.QuickMoveFolder3.Length > 0)
        {
            id.Push(2);
            var targetPath = $"{drawer.Config.QuickMoveFolder3}/{currentName}";
            if (!drawer.FileSystem.Equal(currentPath, targetPath))
            {
                if (Im.Menu.Item($"Move to {drawer.Config.QuickMoveFolder3}"))
                {
                    foreach (var path in drawer.FileSystem.Selection.OrderedNodes)
                    {
                        if (path != data)
                            drawer.FileSystem.RenameAndMoveWithDuplicates(path, $"{drawer.Config.QuickMoveFolder3}/{path.Name}");
                    }

                    drawer.FileSystem.RenameAndMoveWithDuplicates(data, targetPath);
                }
                Im.Tooltip.OnHover("Move the selected objects to a previously set-up quick move location, if possible."u8);
            }

            id.Pop();
        }

        return false;
    }
}

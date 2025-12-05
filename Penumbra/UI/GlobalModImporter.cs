using System.Collections.Frozen;
using Dalamud.Interface.DragDrop;
using ImSharp;
using Luna;
using Penumbra.Mods.Manager;

namespace Penumbra.UI;

public sealed class GlobalModImporter : IRequiredService, IDisposable
{
    public const     string           DragDropId = "ModDragDrop";
    private readonly DragDropManager  _dragDropManager;
    private readonly ModImportManager _importManager;

    /// <summary> All default extensions for valid mod imports. </summary>
    public static readonly FrozenSet<string> ValidModExtensions = FrozenSet.Create(StringComparer.OrdinalIgnoreCase,
        ".ttmp", ".ttmp2", ".pmp", ".pcp", ".zip", ".rar", ".7z"
    );

    public GlobalModImporter(DragDropManager dragDropManager, ModImportManager modImportManager)
    {
        _dragDropManager = dragDropManager;
        _importManager   = modImportManager;
        dragDropManager.AddSource(DragDropId, ValidExtension, DragTooltip);
        dragDropManager.AddTarget(DragDropId, ImportFiles);
    }

    public void Dispose()
    {
        _dragDropManager.RemoveSource(DragDropId);
        _dragDropManager.RemoveTarget(DragDropId);
    }

    private void ImportFiles(IReadOnlyList<string> files, IReadOnlyList<string> _)
        => _importManager.AddUnpack(files.Where(f => ValidModExtensions.Contains(Path.GetExtension(f))));

    private static bool ValidExtension(IDragDropManager manager)
        => manager.Extensions.Any(ValidModExtensions.Contains);

    private static bool DragTooltip(IDragDropManager manager)
    {
        Im.Text($"Dragging mods for import:\n\t{StringU8.Join("\n\t"u8, manager.Files.Select(Path.GetFileName))}");
        return true;
    }
}

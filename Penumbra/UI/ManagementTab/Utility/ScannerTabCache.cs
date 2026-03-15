using ImSharp;
using ImSharp.Table;

namespace Penumbra.UI.ManagementTab;

public abstract class ScannerTabCache<TCacheObject, TScannedObject> : TableCache<TCacheObject>
    where TScannedObject : IScannedObject
{
    protected ScannerTabCache(TableData<TCacheObject> parent, ObjectScanner<TScannedObject> scanner)
        : base(parent)
    {
        Scanner              = scanner;
        KeepAliveDuration    = TimeSpan.FromMinutes(5);
        UnfilteredItemsOwned = true;
    }

    public void DrawScanButtons()
    {
        var size = ImEx.ScaledVectorX(100);

        if (Im.Button("Scan"u8, size))
            StartScan();
        Im.Line.SameInner();
        var running = Scanner.Running;
        if (ImEx.Button("Cancel"u8, size, "Cancel the current scan process."u8, !running))
            Scanner.Cancel();
        if (running)
        {
            Im.Line.SameInner();
            Im.ProgressBar(Scanner.Progress, Vector2.Zero with { X = size.X * 2 + Im.Style.ItemInnerSpacing.X });
        }
    }

    public ObjectScanner<TScannedObject> Scanner { get; init; }

    protected abstract TCacheObject Convert(TScannedObject obj);

    public void StartScan()
    {
        Dirty |= IManagedCache.DirtyFlags.Custom;
        Scanner.Scan();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Scanner.Dispose();
    }

    public override void Update()
    {
        foreach (var item in Scanner.GetNewItems())
        {
            ((List<TCacheObject>)AllItems).Add(Convert(item));
            FilterDirty =  true;
            Dirty       |= IManagedCache.DirtyFlags.Font;
        }

        base.Update();
    }

    protected override IEnumerable<TCacheObject> GetItems()
        => Scanner.GetCurrentList().Select(Convert);
}

using Dalamud.Interface.ImGuiNotification;
using ImSharp;
using ImSharp.Table;
using Luna;

namespace Penumbra.UI.ManagementTab;

public sealed class TargetColumn<TCacheObject, TRedirection> : TextColumn<TCacheObject>
    where TCacheObject : RedirectionCacheObject<TRedirection>
    where TRedirection : BaseScannedRedirection
{
    public TargetColumn()
        => WidthDependsOnItems = true;

    protected override string ComparisonText(in TCacheObject item, int globalIndex)
        => item.Target;

    protected override StringU8 DisplayText(in TCacheObject item, int globalIndex)
        => item.Target;

    public override void DrawColumn(in TCacheObject item, int globalIndex)
    {
        if (item.ScannedObject.FileSwap)
        {
            base.DrawColumn(in item, globalIndex);
        }
        else
        {
            if (Im.Selectable(DisplayText(item, globalIndex)) && Path.GetDirectoryName(item.ScannedObject.Redirection.FullName) is { } dir)
                try
                {
                    Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    Penumbra.Messager.NotificationMessage(ex, $"Could not open Directory {dir}.", $"Could not open Directory {dir}",
                        NotificationType.Warning);
                }

            Im.Tooltip.OnHover("Click to open containing directory in the file explorer of your choice.");
        }
    }

    public override float ComputeWidth(IEnumerable<TCacheObject> obj)
        => obj.Max(o => o.Target.Utf8.CalculateSize().X, UnscaledWidth);
}

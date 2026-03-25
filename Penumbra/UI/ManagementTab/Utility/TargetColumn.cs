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

    private string _lastFile = string.Empty;
    private string _lastMod  = string.Empty;

    public override void PostDraw(in TableCache<TCacheObject> cache)
    {
        _lastFile = string.Empty;
        _lastMod  = string.Empty;
    }

    public override void DrawColumn(in TCacheObject item, int globalIndex)
    {
        if (item.ScannedObject.FileSwap)
        {
            base.DrawColumn(in item, globalIndex);
        }
        else
        {
            using (ImGuiColor.Text.Push(Im.Style[ImGuiColor.TextDisabled], _lastFile == item.Target.Utf16 && _lastMod == item.Mod.Utf16))
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
            }

            Im.Tooltip.OnHover("Click to open containing directory in the file explorer of your choice.");
        }

        _lastFile = item.Target.Utf16;
        _lastMod  = item.Mod.Utf16;
    }

    public override float ComputeWidth(IEnumerable<TCacheObject> obj)
        => obj.Max(o => o.Target.Utf8.CalculateSize().X, UnscaledWidth);
}

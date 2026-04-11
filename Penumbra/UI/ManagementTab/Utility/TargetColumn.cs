using Dalamud.Interface.ImGuiNotification;
using ImSharp;
using ImSharp.Table;
using Luna;

namespace Penumbra.UI.ManagementTab;

public abstract class FileColumn<TCacheObject> : TextColumn<TCacheObject>
{
    public FileColumn()
        => WidthDependsOnItems = true;

    protected override StringU8 DisplayText(in TCacheObject item, int globalIndex)
        => FileName(item, globalIndex).Utf8;

    protected override string ComparisonText(in TCacheObject item, int globalIndex)
        => FileName(item, globalIndex).Utf16;

    protected abstract StringPair FileName(in TCacheObject item, int globalIndex);
    protected abstract string     FileMod(in TCacheObject item, int globalIndex);
    protected abstract string     FullName(in TCacheObject item, int globalIndex);

    protected virtual bool DrawSimpleColumn(in TCacheObject item, int globalIndex)
        => false;

    private string _lastFile = string.Empty;
    private string _lastMod  = string.Empty;

    public override void PostDraw(in TableCache<TCacheObject> cache)
    {
        _lastFile = string.Empty;
        _lastMod  = string.Empty;
    }

    public override void DrawColumn(in TCacheObject item, int globalIndex)
    {
        if (!DrawSimpleColumn(in item, globalIndex))
        {
            var name    = FileName(item, globalIndex);
            var fileMod = FileMod(item, globalIndex);
            using (ImGuiColor.Text.Push(Im.Style[ImGuiColor.TextDisabled], _lastFile == name.Utf16 && _lastMod == fileMod))
            {
                if (Im.Selectable(DisplayText(item, globalIndex)) && Path.GetDirectoryName(FullName(item, globalIndex)) is { } dir)
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
            _lastFile = name.Utf16;
            _lastMod  = fileMod;
        }
        else
        {
            base.DrawColumn(in item, globalIndex);
            _lastFile = FileName(item, globalIndex).Utf16;
            _lastMod  = FileMod(item, globalIndex);
        }
    }

    public override float ComputeWidth(IEnumerable<TCacheObject> obj)
        => obj.Max(o => FileName(o, 0).Utf8.CalculateSize().X, UnscaledWidth);
}

public sealed class TargetColumn<TCacheObject, TRedirection> : FileColumn<TCacheObject>
    where TCacheObject : RedirectionCacheObject<TRedirection>
    where TRedirection : BaseScannedRedirection
{
    protected override StringPair FileName(in TCacheObject item, int globalIndex)
        => item.Target;

    protected override string FileMod(in TCacheObject item, int globalIndex)
        => item.Mod.Utf16;

    protected override string FullName(in TCacheObject item, int globalIndex)
        => item.ScannedObject.FilePath;

    protected override bool DrawSimpleColumn(in TCacheObject item, int globalIndex)
        => item.ScannedObject.FileSwap;
}

public sealed class FileColumn<TCacheObject, TFile> : FileColumn<TCacheObject>
    where TCacheObject : FileCacheObject<TFile>
    where TFile : BaseScannedFile
{
    protected override StringPair FileName(in TCacheObject item, int globalIndex)
        => item.File;

    protected override string FileMod(in TCacheObject item, int globalIndex)
        => item.Mod.Utf16;

    protected override string FullName(in TCacheObject item, int globalIndex)
        => item.ScannedObject.FilePath;
}

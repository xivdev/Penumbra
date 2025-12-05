using System.Text.Json;
using Dalamud.Interface.DragDrop;
using ImSharp;
using Penumbra.CrashHandler;
using Penumbra.Services;

namespace Penumbra.UI.Tabs.Debug;

public class CrashHandlerPanel(CrashHandlerService service, Configuration config, IDragDropManager dragDrop) : Luna.IService
{
    private CrashData? _lastDump;
    private string     _lastLoadedFile = string.Empty;
    private CrashData? _lastLoad;
    private Exception? _lastLoadException;

    public void Draw()
    {
        DrawDropSource();
        DrawData();
        DrawDropTarget();
    }

    private void DrawData()
    {
        using var _      = Im.Group();
        using var header = Im.Tree.HeaderId("Crash Handler"u8);
        if (!header)
            return;

        DrawButtons();
        DrawMainData();
        DrawObject("Last Manual Dump"u8, _lastDump, null);
        DrawObject(_lastLoadedFile.Length > 0 ? $"Loaded File ({_lastLoadedFile})###Loaded File" : "Loaded File"u8, _lastLoad,
            _lastLoadException);
    }

    private void DrawMainData()
    {
        using var table = Im.Table.Begin("##CrashHandlerTable"u8, 2, TableFlags.SizingFixedFit);
        if (!table)
            return;

        table.DrawColumn("Enabled"u8);
        table.DrawColumn($"{config.UseCrashHandler}");
        table.DrawColumn("Copied Executable Path"u8);
        table.DrawColumn(service.CopiedExe);
        table.DrawColumn("Original Executable Path"u8);
        table.DrawColumn(service.OriginalExe);
        table.DrawColumn("Log File Path"u8);
        table.DrawColumn(service.LogPath);
        table.DrawColumn("XIV Process ID"u8);
        table.DrawColumn($"{service.ProcessId}");
        table.DrawColumn("Crash Handler Running"u8);
        table.DrawColumn($"{service.IsRunning}");
        table.DrawColumn("Crash Handler Process ID"u8);
        table.DrawColumn($"{service.ChildProcessId}");
        table.DrawColumn("Crash Handler Exit Code"u8);
        table.DrawColumn($"{service.ChildExitCode}");
    }

    private void DrawButtons()
    {
        if (Im.Button("Dump Crash Handler Memory"u8))
            _lastDump = service.Dump()?.Deserialize<CrashData>();

        if (Im.Button("Enable"u8))
            service.Enable();

        Im.Line.Same();
        if (Im.Button("Disable"u8))
            service.Disable();

        if (Im.Button("Shutdown Crash Handler"u8))
            service.CloseCrashHandler();
        Im.Line.Same();
        if (Im.Button("Relaunch Crash Handler"u8))
            service.LaunchCrashHandler();
    }

    private void DrawDropSource()
    {
        dragDrop.CreateImGuiSource("LogDragDrop", m => m.Files.Any(f => f.EndsWith("Penumbra.log")), _ =>
        {
            Im.Text("Dragging Penumbra.log for import."u8);
            return true;
        });
    }

    private void DrawDropTarget()
    {
        if (!dragDrop.CreateImGuiTarget("LogDragDrop", out var files, out _))
            return;

        var file = files.FirstOrDefault(f => f.EndsWith("Penumbra.log"));
        if (file == null)
            return;

        _lastLoadedFile = file;
        try
        {
            var jObj = service.Load(file);
            _lastLoad          = jObj?.Deserialize<CrashData>();
            _lastLoadException = null;
        }
        catch (Exception ex)
        {
            _lastLoad          = null;
            _lastLoadException = ex;
        }
    }

    private static void DrawObject(Utf8StringHandler<LabelStringHandlerBuffer> name, CrashData? data, Exception? ex)
    {
        using var tree = Im.Tree.Node(name);
        if (!tree)
            return;

        if (ex is not null)
        {
            Im.TextWrapped($"{ex}");
            return;
        }

        if (data is null)
        {
            Im.Text("Nothing loaded."u8);
            return;
        }

        data.DrawMeta();
        data.DrawFiles();
        data.DrawCharacters();
        data.DrawVfxInvocations();
    }
}

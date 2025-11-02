using System.Text.Json;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.DragDrop;
using ImSharp;
using OtterGui;
using OtterGui.Raii;
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
        using var header = ImRaii.CollapsingHeader("Crash Handler");
        if (!header)
            return;

        DrawButtons();
        DrawMainData();
        DrawObject("Last Manual Dump", _lastDump, null);
        DrawObject(_lastLoadedFile.Length > 0 ? $"Loaded File ({_lastLoadedFile})###Loaded File" : "Loaded File", _lastLoad,
            _lastLoadException);
    }

    private void DrawMainData()
    {
        using var table = Im.Table.Begin("##CrashHandlerTable"u8, 2, TableFlags.SizingFixedFit);
        if (!table)
            return;

        PrintValue("Enabled",                  config.UseCrashHandler);
        PrintValue("Copied Executable Path",   service.CopiedExe);
        PrintValue("Original Executable Path", service.OriginalExe);
        PrintValue("Log File Path",            service.LogPath);
        PrintValue("XIV Process ID",           service.ProcessId.ToString());
        PrintValue("Crash Handler Running",    service.IsRunning.ToString());
        PrintValue("Crash Handler Process ID", service.ChildProcessId.ToString());
        PrintValue("Crash Handler Exit Code",  service.ChildExitCode.ToString());
    }

    private void DrawButtons()
    {
        if (ImGui.Button("Dump Crash Handler Memory"))
            _lastDump = service.Dump()?.Deserialize<CrashData>();

        if (ImGui.Button("Enable"))
            service.Enable();

        Im.Line.Same();
        if (ImGui.Button("Disable"))
            service.Disable();

        if (ImGui.Button("Shutdown Crash Handler"))
            service.CloseCrashHandler();
        Im.Line.Same();
        if (ImGui.Button("Relaunch Crash Handler"))
            service.LaunchCrashHandler();
    }

    private void DrawDropSource()
    {
        dragDrop.CreateImGuiSource("LogDragDrop", m => m.Files.Any(f => f.EndsWith("Penumbra.log")), m =>
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

    private static void DrawObject(string name, CrashData? data, Exception? ex)
    {
        using var tree = ImRaii.TreeNode(name);
        if (!tree)
            return;

        if (ex != null)
        {
            ImGuiUtil.TextWrapped(ex.ToString());
            return;
        }

        if (data == null)
        {
            Im.Text("Nothing loaded."u8);
            return;
        }

        data.DrawMeta();
        data.DrawFiles();
        data.DrawCharacters();
        data.DrawVfxInvocations();
    }

    private static void PrintValue<T>(string label, in T data)
    {
        ImGuiUtil.DrawTableColumn(label);
        ImGuiUtil.DrawTableColumn(data?.ToString() ?? "NULL");
    }
}

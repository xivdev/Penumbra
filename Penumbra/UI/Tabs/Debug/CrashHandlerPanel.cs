using System.Text.Json;
using Dalamud.Interface.DragDrop;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Services;
using Penumbra.CrashHandler;
using Penumbra.Services;

namespace Penumbra.UI.Tabs.Debug;

public class CrashHandlerPanel(CrashHandlerService _service, Configuration _config, IDragDropManager _dragDrop) : IService
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
        using var _      = ImRaii.Group();
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
        using var table = ImRaii.Table("##CrashHandlerTable", 2, ImGuiTableFlags.SizingFixedFit);
        if (!table)
            return;

        PrintValue("Enabled",                  _config.UseCrashHandler);
        PrintValue("Copied Executable Path",   _service.CopiedExe);
        PrintValue("Original Executable Path", _service.OriginalExe);
        PrintValue("Log File Path",            _service.LogPath);
        PrintValue("XIV Process ID",           _service.ProcessId.ToString());
        PrintValue("Crash Handler Running",    _service.IsRunning.ToString());
        PrintValue("Crash Handler Process ID", _service.ChildProcessId.ToString());
        PrintValue("Crash Handler Exit Code",  _service.ChildExitCode.ToString());
    }

    private void DrawButtons()
    {
        if (ImGui.Button("Dump Crash Handler Memory"))
            _lastDump = _service.Dump()?.Deserialize<CrashData>();

        if (ImGui.Button("Enable"))
            _service.Enable();

        ImGui.SameLine();
        if (ImGui.Button("Disable"))
            _service.Disable();

        if (ImGui.Button("Shutdown Crash Handler"))
            _service.CloseCrashHandler();
        ImGui.SameLine();
        if (ImGui.Button("Relaunch Crash Handler"))
            _service.LaunchCrashHandler();
    }

    private void DrawDropSource()
    {
        _dragDrop.CreateImGuiSource("LogDragDrop", m => m.Files.Any(f => f.EndsWith("Penumbra.log")), m =>
        {
            ImGui.TextUnformatted("Dragging Penumbra.log for import.");
            return true;
        });
    }

    private void DrawDropTarget()
    {
        if (!_dragDrop.CreateImGuiTarget("LogDragDrop", out var files, out _))
            return;

        var file = files.FirstOrDefault(f => f.EndsWith("Penumbra.log"));
        if (file == null)
            return;

        _lastLoadedFile = file;
        try
        {
            var jObj = _service.Load(file);
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
            ImGui.TextUnformatted("Nothing loaded.");
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

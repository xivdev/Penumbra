using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.System.Scheduler;
using FFXIVClientStructs.FFXIV.Client.System.Scheduler.Resource;
using FFXIVClientStructs.STD;
using ImSharp;
using OtterGui.Text;
using Penumbra.Interop.Services;
using Penumbra.Interop.Structs;
using Penumbra.String;
using ResidentResourceManager = Penumbra.Interop.Services.ResidentResourceManager;

namespace Penumbra.UI.Tabs.Debug;

public unsafe class GlobalVariablesDrawer(
    CharacterUtility characterUtility,
    ResidentResourceManager residentResources,
    SchedulerResourceManagementService scheduler) : Luna.IUiService
{
    /// <summary> Draw information about some game global variables. </summary>
    public void Draw()
    {
        var header = ImUtf8.CollapsingHeader("Global Variables"u8);
        ImUtf8.HoverTooltip("Draw information about global variables. Can provide useful starting points for a memory viewer."u8);
        if (!header)
            return;

        var actionManager = (ActionTimelineManager**)ActionTimelineManager.Instance();
        using (ImUtf8.Group())
        {
            Penumbra.Dynamis.DrawPointer(characterUtility.Address);
            Penumbra.Dynamis.DrawPointer(residentResources.Address);
            Penumbra.Dynamis.DrawPointer(ScheduleManagement.Instance());
            Penumbra.Dynamis.DrawPointer(actionManager);
            Penumbra.Dynamis.DrawPointer(actionManager != null ? *actionManager : null);
            Penumbra.Dynamis.DrawPointer(scheduler.Address);
            Penumbra.Dynamis.DrawPointer(scheduler.Address != null ? *scheduler.Address : null);
            Penumbra.Dynamis.DrawPointer(Device.Instance());
        }

        Im.Line.Same();
        using (ImUtf8.Group())
        {
            ImUtf8.Text("CharacterUtility"u8);
            ImUtf8.Text("ResidentResourceManager"u8);
            ImUtf8.Text("ScheduleManagement"u8);
            ImUtf8.Text("ActionTimelineManager*"u8);
            ImUtf8.Text("ActionTimelineManager"u8);
            ImUtf8.Text("SchedulerResourceManagement*"u8);
            ImUtf8.Text("SchedulerResourceManagement"u8);
            ImUtf8.Text("Device"u8);
        }

        DrawCharacterUtility();
        DrawResidentResources();
        DrawSchedulerResourcesMap();
        DrawSchedulerResourcesList();
    }


    /// <summary>
    /// Draw information about the character utility class from SE,
    /// displaying all files, their sizes, the default files and the default sizes.
    /// </summary>
    private void DrawCharacterUtility()
    {
        using var tree = ImUtf8.TreeNode("Character Utility"u8);
        if (!tree)
            return;

        using var table = ImUtf8.Table("##CharacterUtility"u8, 7, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit,
            -Vector2.UnitX);
        if (!table)
            return;

        for (var idx = 0; idx < CharacterUtility.ReverseIndices.Length; ++idx)
        {
            var intern   = CharacterUtility.ReverseIndices[idx];
            var resource = characterUtility.Address->Resource(idx);
            ImUtf8.DrawTableColumn($"[{idx}]");
            ImGui.TableNextColumn();
            Penumbra.Dynamis.DrawPointer(resource);
            if (resource == null)
            {
                ImGui.TableNextRow();
                continue;
            }

            ImUtf8.DrawTableColumn(resource->CsHandle.FileName.AsSpan());
            ImGui.TableNextColumn();
            var data   = (nint)resource->CsHandle.GetData();
            var length = resource->CsHandle.GetLength();
            Penumbra.Dynamis.DrawPointer(data);
            ImUtf8.DrawTableColumn(length.ToString());
            ImGui.TableNextColumn();
            if (intern.Value != -1)
            {
                Penumbra.Dynamis.DrawPointer(characterUtility.DefaultResource(intern).Address);
                ImUtf8.DrawTableColumn($"{characterUtility.DefaultResource(intern).Size}");
            }
            else
            {
                ImGui.TableNextColumn();
            }
        }
    }

    /// <summary> Draw information about the resident resource files. </summary>
    private void DrawResidentResources()
    {
        using var tree = ImUtf8.TreeNode("Resident Resources"u8);
        if (!tree)
            return;

        if (residentResources.Address == null || residentResources.Address->NumResources == 0)
            return;

        using var table = ImUtf8.Table("##ResidentResources"u8, 5, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit,
            -Vector2.UnitX);
        if (!table)
            return;

        for (var idx = 0; idx < residentResources.Address->NumResources; ++idx)
        {
            var resource = residentResources.Address->ResourceList[idx];
            ImUtf8.DrawTableColumn($"[{idx}]");
            ImGui.TableNextColumn();
            Penumbra.Dynamis.DrawPointer(resource);
            if (resource == null)
            {
                ImGui.TableNextRow();
                continue;
            }

            ImUtf8.DrawTableColumn(resource->CsHandle.FileName.AsSpan());
            ImGui.TableNextColumn();
            var data   = (nint)resource->CsHandle.GetData();
            var length = resource->CsHandle.GetLength();
            Penumbra.Dynamis.DrawPointer(data);
            ImUtf8.DrawTableColumn(length.ToString());
        }
    }

    private string       _schedulerFilterList   = string.Empty;
    private string       _schedulerFilterMap    = string.Empty;
    private CiByteString _schedulerFilterListU8 = CiByteString.Empty;
    private CiByteString _schedulerFilterMapU8  = CiByteString.Empty;
    private int          _shownResourcesList    = 0;
    private int          _shownResourcesMap     = 0;

    private void DrawSchedulerResourcesMap()
    {
        using var tree = ImUtf8.TreeNode("Scheduler Resources (Map)"u8);
        if (!tree)
            return;

        if (scheduler.Address == null || scheduler.Scheduler == null)
            return;

        if (ImUtf8.InputText("##SchedulerMapFilter"u8, ref _schedulerFilterMap, "Filter..."u8))
            _schedulerFilterMapU8 = CiByteString.FromString(_schedulerFilterMap, out var t, MetaDataComputation.All, false)
                ? t
                : CiByteString.Empty;
        ImUtf8.Text($"{_shownResourcesMap} / {scheduler.Scheduler->Resources.LongCount}");
        using var table = ImUtf8.Table("##SchedulerMapResources"u8, 10, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit,
            -Vector2.UnitX);
        if (!table)
            return;

        // TODO Remove cast when it'll have the right type in CS.
        var map   = (StdMap<int, FFXIVClientStructs.Interop.Pointer<SchedulerResource>>*)&scheduler.Scheduler->Resources;
        var total = 0;
        _shownResourcesMap = 0;
        foreach (var (key, resourcePtr) in *map)
        {
            var resource = resourcePtr.Value;
            if (_schedulerFilterMap.Length is 0 || resource->Name.Buffer.IndexOf(_schedulerFilterMapU8.Span) >= 0)
            {
                ImUtf8.DrawTableColumn($"[{total:D4}]");
                ImUtf8.DrawTableColumn($"{resource->Name.Unk1}");
                ImUtf8.DrawTableColumn(new CiByteString(resource->Name.Buffer, MetaDataComputation.None).Span);
                ImUtf8.DrawTableColumn($"{resource->Consumers}");
                ImUtf8.DrawTableColumn($"{resource->Unk1}"); // key
                ImGui.TableNextColumn();
                Penumbra.Dynamis.DrawPointer(resource);
                ImGui.TableNextColumn();
                var resourceHandle = *((ResourceHandle**)resource + 3);
                Penumbra.Dynamis.DrawPointer(resourceHandle);
                ImGui.TableNextColumn();
                ImUtf8.CopyOnClickSelectable(resourceHandle->FileName().Span);
                ImGui.TableNextColumn();
                uint dataLength = 0;
                Penumbra.Dynamis.DrawPointer(resource->GetResourceData(&dataLength));
                ImUtf8.DrawTableColumn($"{dataLength}");
                ++_shownResourcesMap;
            }

            ++total;
        }
    }

    private void DrawSchedulerResourcesList()
    {
        using var tree = ImUtf8.TreeNode("Scheduler Resources (List)"u8);
        if (!tree)
            return;

        if (scheduler.Address == null || scheduler.Scheduler == null)
            return;

        if (ImUtf8.InputText("##SchedulerListFilter"u8, ref _schedulerFilterList, "Filter..."u8))
            _schedulerFilterListU8 = CiByteString.FromString(_schedulerFilterList, out var t, MetaDataComputation.All, false)
                ? t
                : CiByteString.Empty;
        ImUtf8.Text($"{_shownResourcesList} / {scheduler.Scheduler->Resources.LongCount}");
        using var table = ImUtf8.Table("##SchedulerListResources"u8, 10, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit,
            -Vector2.UnitX);
        if (!table)
            return;

        var resource = scheduler.Scheduler->Begin;
        var total    = 0;
        _shownResourcesList = 0;
        while (resource != null && total < scheduler.Scheduler->Resources.Count)
        {
            if (_schedulerFilterList.Length is 0 || resource->Name.Buffer.IndexOf(_schedulerFilterListU8.Span) >= 0)
            {
                ImUtf8.DrawTableColumn($"[{total:D4}]");
                ImUtf8.DrawTableColumn($"{resource->Name.Unk1}");
                ImUtf8.DrawTableColumn(new CiByteString(resource->Name.Buffer, MetaDataComputation.None).Span);
                ImUtf8.DrawTableColumn($"{resource->Consumers}");
                ImUtf8.DrawTableColumn($"{resource->Unk1}"); // key
                ImGui.TableNextColumn();
                Penumbra.Dynamis.DrawPointer(resource);
                ImGui.TableNextColumn();
                var resourceHandle = *((ResourceHandle**)resource + 3);
                Penumbra.Dynamis.DrawPointer(resourceHandle);
                ImGui.TableNextColumn();
                ImUtf8.CopyOnClickSelectable(resourceHandle->FileName().Span);
                ImGui.TableNextColumn();
                uint dataLength = 0;
                Penumbra.Dynamis.DrawPointer(resource->GetResourceData(&dataLength));
                ImUtf8.DrawTableColumn($"{dataLength}");
                ++_shownResourcesList;
            }

            resource = resource->Previous;
            ++total;
        }
    }
}

using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.System.Scheduler;
using FFXIVClientStructs.FFXIV.Client.System.Scheduler.Resource;
using FFXIVClientStructs.Interop;
using FFXIVClientStructs.STD;
using ImGuiNET;
using OtterGui.Services;
using OtterGui.Text;
using Penumbra.Interop.Services;
using Penumbra.Interop.Structs;
using Penumbra.String;
using ResidentResourceManager = Penumbra.Interop.Services.ResidentResourceManager;

namespace Penumbra.UI.Tabs.Debug;

public unsafe class GlobalVariablesDrawer(
    CharacterUtility characterUtility,
    ResidentResourceManager residentResources,
    SchedulerResourceManagementService scheduler) : IUiService
{
    /// <summary> Draw information about some game global variables. </summary>
    public void Draw()
    {
        var header = ImUtf8.CollapsingHeader("Global Variables"u8);
        ImUtf8.HoverTooltip("Draw information about global variables. Can provide useful starting points for a memory viewer."u8);
        if (!header)
            return;

        var actionManager = (ActionTimelineManager**)ActionTimelineManager.Instance();
        DebugTab.DrawCopyableAddress("CharacterUtility"u8,             characterUtility.Address);
        DebugTab.DrawCopyableAddress("ResidentResourceManager"u8,      residentResources.Address);
        DebugTab.DrawCopyableAddress("ScheduleManagement"u8,           ScheduleManagement.Instance());
        DebugTab.DrawCopyableAddress("ActionTimelineManager*"u8,       actionManager);
        DebugTab.DrawCopyableAddress("ActionTimelineManager"u8,        actionManager != null ? *actionManager : null);
        DebugTab.DrawCopyableAddress("SchedulerResourceManagement*"u8, scheduler.Address);
        DebugTab.DrawCopyableAddress("SchedulerResourceManagement"u8,  scheduler.Address != null ? *scheduler.Address : null);
        DebugTab.DrawCopyableAddress("Device"u8,                       Device.Instance());
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
            ImUtf8.CopyOnClickSelectable($"0x{(ulong)resource:X}");
            if (resource == null)
            {
                ImGui.TableNextRow();
                continue;
            }

            ImUtf8.DrawTableColumn(resource->CsHandle.FileName.AsSpan());
            ImGui.TableNextColumn();
            var data   = (nint)resource->CsHandle.GetData();
            var length = resource->CsHandle.GetLength();
            if (ImUtf8.Selectable($"0x{data:X}"))
                if (data != nint.Zero && length > 0)
                    ImUtf8.SetClipboardText(string.Join("\n",
                        new ReadOnlySpan<byte>((byte*)data, (int)length).ToArray().Select(b => b.ToString("X2"))));

            ImUtf8.HoverTooltip("Click to copy bytes to clipboard."u8);
            ImUtf8.DrawTableColumn(length.ToString());

            ImGui.TableNextColumn();
            if (intern.Value != -1)
            {
                ImUtf8.Selectable($"0x{characterUtility.DefaultResource(intern).Address:X}");
                if (ImGui.IsItemClicked())
                    ImUtf8.SetClipboardText(string.Join("\n",
                        new ReadOnlySpan<byte>((byte*)characterUtility.DefaultResource(intern).Address,
                            characterUtility.DefaultResource(intern).Size).ToArray().Select(b => b.ToString("X2"))));

                ImUtf8.HoverTooltip("Click to copy bytes to clipboard."u8);

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
            ImUtf8.CopyOnClickSelectable($"0x{(ulong)resource:X}");
            if (resource == null)
            {
                ImGui.TableNextRow();
                continue;
            }

            ImUtf8.DrawTableColumn(resource->CsHandle.FileName.AsSpan());
            ImGui.TableNextColumn();
            var data   = (nint)resource->CsHandle.GetData();
            var length = resource->CsHandle.GetLength();
            if (ImUtf8.Selectable($"0x{data:X}"))
                if (data != nint.Zero && length > 0)
                    ImUtf8.SetClipboardText(string.Join("\n",
                        new ReadOnlySpan<byte>((byte*)data, (int)length).ToArray().Select(b => b.ToString("X2"))));

            ImUtf8.HoverTooltip("Click to copy bytes to clipboard."u8);
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
        ImUtf8.Text($"{_shownResourcesMap} / {scheduler.Scheduler->NumResources}");
        using var table = ImUtf8.Table("##SchedulerMapResources"u8, 10, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit,
            -Vector2.UnitX);
        if (!table)
            return;

        var map   = (StdMap<int, Pointer<SchedulerResource>>*)&scheduler.Scheduler->Unknown;
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
                ImUtf8.CopyOnClickSelectable($"0x{(ulong)resource:X}");
                ImGui.TableNextColumn();
                var resourceHandle = *((ResourceHandle**)resource + 3);
                ImUtf8.CopyOnClickSelectable($"0x{(ulong)resourceHandle:X}");
                ImGui.TableNextColumn();
                ImUtf8.CopyOnClickSelectable(resourceHandle->FileName().Span);
                ImGui.TableNextColumn();
                uint dataLength = 0;
                ImUtf8.CopyOnClickSelectable($"0x{(ulong)resource->GetResourceData(&dataLength):X}");
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
        ImUtf8.Text($"{_shownResourcesList} / {scheduler.Scheduler->NumResources}");
        using var table = ImUtf8.Table("##SchedulerListResources"u8, 10, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit,
            -Vector2.UnitX);
        if (!table)
            return;

        var resource = scheduler.Scheduler->Begin;
        var total    = 0;
        _shownResourcesList = 0;
        while (resource != null && total < (int)scheduler.Scheduler->NumResources)
        {
            if (_schedulerFilterList.Length is 0 || resource->Name.Buffer.IndexOf(_schedulerFilterListU8.Span) >= 0)
            {
                ImUtf8.DrawTableColumn($"[{total:D4}]");
                ImUtf8.DrawTableColumn($"{resource->Name.Unk1}");
                ImUtf8.DrawTableColumn(new CiByteString(resource->Name.Buffer, MetaDataComputation.None).Span);
                ImUtf8.DrawTableColumn($"{resource->Consumers}");
                ImUtf8.DrawTableColumn($"{resource->Unk1}"); // key
                ImGui.TableNextColumn();
                ImUtf8.CopyOnClickSelectable($"0x{(ulong)resource:X}");
                ImGui.TableNextColumn();
                var resourceHandle = *((ResourceHandle**)resource + 3);
                ImUtf8.CopyOnClickSelectable($"0x{(ulong)resourceHandle:X}");
                ImGui.TableNextColumn();
                ImUtf8.CopyOnClickSelectable(resourceHandle->FileName().Span);
                ImGui.TableNextColumn();
                uint dataLength = 0;
                ImUtf8.CopyOnClickSelectable($"0x{(ulong)resource->GetResourceData(&dataLength):X}");
                ImUtf8.DrawTableColumn($"{dataLength}");
                ++_shownResourcesList;
            }

            resource = resource->Previous;
            ++total;
        }
    }
}

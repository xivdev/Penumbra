using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.System.Scheduler;
using FFXIVClientStructs.FFXIV.Client.System.Scheduler.Resource;
using FFXIVClientStructs.STD;
using ImSharp;
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
        var header = Im.Tree.Header("Global Variables"u8);
        Im.Tooltip.OnHover("Draw information about global variables. Can provide useful starting points for a memory viewer."u8);
        if (!header)
            return;

        var actionManager = (ActionTimelineManager**)ActionTimelineManager.Instance();
        using (Im.Group())
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
        using (Im.Group())
        {
            Im.Text("CharacterUtility"u8);
            Im.Text("ResidentResourceManager"u8);
            Im.Text("ScheduleManagement"u8);
            Im.Text("ActionTimelineManager*"u8);
            Im.Text("ActionTimelineManager"u8);
            Im.Text("SchedulerResourceManagement*"u8);
            Im.Text("SchedulerResourceManagement"u8);
            Im.Text("Device"u8);
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
        using var tree = Im.Tree.Node("Character Utility"u8);
        if (!tree)
            return;

        using var table = Im.Table.Begin("##CharacterUtility"u8, 7, TableFlags.RowBackground | TableFlags.SizingFixedFit, -Vector2.UnitX);
        if (!table)
            return;

        for (var idx = 0; idx < CharacterUtility.ReverseIndices.Length; ++idx)
        {
            var intern   = CharacterUtility.ReverseIndices[idx];
            var resource = characterUtility.Address->Resource(idx);
            table.DrawColumn($"[{idx}]");
            table.NextColumn();
            Penumbra.Dynamis.DrawPointer(resource);
            if (resource is null)
            {
                table.NextRow();
                continue;
            }

            table.DrawColumn(resource->CsHandle.FileName.AsSpan());
            table.NextColumn();
            var data   = (nint)resource->CsHandle.GetData();
            var length = resource->CsHandle.GetLength();
            Penumbra.Dynamis.DrawPointer(data);
            table.DrawColumn($"{length}");
            table.NextColumn();
            if (intern.Value is -1)
            {
                Penumbra.Dynamis.DrawPointer(characterUtility.DefaultResource(intern).Address);
                table.DrawColumn($"{characterUtility.DefaultResource(intern).Size}");
            }
            else
            {
                table.NextColumn();
            }
        }
    }

    /// <summary> Draw information about the resident resource files. </summary>
    private void DrawResidentResources()
    {
        using var tree = Im.Tree.Node("Resident Resources"u8);
        if (!tree)
            return;

        if (residentResources.Address is null || residentResources.Address->NumResources is 0)
            return;

        using var table = Im.Table.Begin("##ResidentResources"u8, 5, TableFlags.RowBackground | TableFlags.SizingFixedFit,
            -Vector2.UnitX);
        if (!table)
            return;

        for (var idx = 0; idx < residentResources.Address->NumResources; ++idx)
        {
            var resource = residentResources.Address->ResourceList[idx];
            table.DrawColumn($"[{idx}]");
            table.NextColumn();
            Penumbra.Dynamis.DrawPointer(resource);
            if (resource is null)
            {
                table.NextRow();
                continue;
            }

            table.DrawColumn(resource->CsHandle.FileName.AsSpan());
            table.NextColumn();
            var data   = (nint)resource->CsHandle.GetData();
            var length = resource->CsHandle.GetLength();
            Penumbra.Dynamis.DrawPointer(data);
            table.DrawColumn($"{length}");
        }
    }

    private StringU8 _schedulerFilterList = StringU8.Empty;
    private StringU8 _schedulerFilterMap  = StringU8.Empty;
    private int      _shownResourcesList;
    private int      _shownResourcesMap;

    private void DrawSchedulerResourcesMap()
    {
        using var tree = Im.Tree.Node("Scheduler Resources (Map)"u8);
        if (!tree)
            return;

        if (scheduler.Address is null || scheduler.Scheduler is null)
            return;

        Im.Input.Text("##SchedulerMapFilter"u8, ref _schedulerFilterMap, "Filter..."u8);
        Im.Text($"{_shownResourcesMap} / {scheduler.Scheduler->Resources.LongCount}");
        using var table = Im.Table.Begin("##SchedulerMapResources"u8, 10, TableFlags.RowBackground | TableFlags.SizingFixedFit,
            -Vector2.UnitX);
        if (!table)
            return;

        // TODO Remove cast when it'll have the right type in CS.
        var map   = (StdMap<int, FFXIVClientStructs.Interop.Pointer<SchedulerResource>>*)&scheduler.Scheduler->Resources;
        var total = 0;
        _shownResourcesMap = 0;
        foreach (var (_, resourcePtr) in *map)
        {
            var resource = resourcePtr.Value;
            if (_schedulerFilterMap.Length is 0 || resource->Name.Buffer.IndexOf(_schedulerFilterMap.Span) >= 0)
            {
                table.DrawColumn($"[{total:D4}]");
                table.DrawColumn($"{resource->Name.Unk1}");
                table.DrawColumn(new CiByteString(resource->Name.Buffer).Span);
                table.DrawColumn($"{resource->Consumers}");
                table.DrawColumn($"{resource->Unk1}"); // key
                table.NextColumn();
                Penumbra.Dynamis.DrawPointer(resource);
                table.NextColumn();
                var resourceHandle = *((ResourceHandle**)resource + 3);
                Penumbra.Dynamis.DrawPointer(resourceHandle);
                table.NextColumn();
                ImEx.CopyOnClickSelectable(resourceHandle->FileName().Span);
                table.NextColumn();
                var dataLength = 0u;
                Penumbra.Dynamis.DrawPointer(resource->GetResourceData(&dataLength));
                table.DrawColumn($"{dataLength}");
                ++_shownResourcesMap;
            }

            ++total;
        }
    }

    private void DrawSchedulerResourcesList()
    {
        using var tree = Im.Tree.Node("Scheduler Resources (List)"u8);
        if (!tree)
            return;

        if (scheduler.Address is null || scheduler.Scheduler is null)
            return;

        Im.Input.Text("##SchedulerListFilter"u8, ref _schedulerFilterList, "Filter..."u8);
        Im.Text($"{_shownResourcesList} / {scheduler.Scheduler->Resources.LongCount}");
        using var table = Im.Table.Begin("##SchedulerListResources"u8, 10, TableFlags.RowBackground | TableFlags.SizingFixedFit,
            -Vector2.UnitX);
        if (!table)
            return;

        var resource = scheduler.Scheduler->Begin;
        var total    = 0;
        _shownResourcesList = 0;
        while (resource is not null && total < scheduler.Scheduler->Resources.Count)
        {
            if (_schedulerFilterList.Length is 0 || resource->Name.Buffer.IndexOf(_schedulerFilterList.Span) >= 0)
            {
                table.DrawColumn($"[{total:D4}]");
                table.DrawColumn($"{resource->Name.Unk1}");
                table.DrawColumn(new CiByteString(resource->Name.Buffer).Span);
                table.DrawColumn($"{resource->Consumers}");
                table.DrawColumn($"{resource->Unk1}"); // key
                table.NextColumn();
                Penumbra.Dynamis.DrawPointer(resource);
                table.NextColumn();
                var resourceHandle = *((ResourceHandle**)resource + 3);
                Penumbra.Dynamis.DrawPointer(resourceHandle);
                table.NextColumn();
                ImEx.CopyOnClickSelectable(resourceHandle->FileName().Span);
                table.NextColumn();
                uint dataLength = 0;
                Penumbra.Dynamis.DrawPointer(resource->GetResourceData(&dataLength));
                table.DrawColumn($"{dataLength}");
                ++_shownResourcesList;
            }

            resource = resource->Previous;
            ++total;
        }
    }
}

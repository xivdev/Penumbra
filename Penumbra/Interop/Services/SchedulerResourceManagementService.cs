using System.Collections.Frozen;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.Scheduler.Resource;
using Lumina.Excel.Sheets;
using OtterGui.Services;
using Penumbra.Collections;
using Penumbra.Communication;
using Penumbra.GameData;
using Penumbra.Mods.Editor;
using Penumbra.Services;
using Penumbra.String;
using Penumbra.String.Classes;

namespace Penumbra.Interop.Services;

public unsafe class SchedulerResourceManagementService : IService, IDisposable
{
    private static readonly CiByteString TmbExtension = new(".tmb"u8, MetaDataComputation.All);
    private static readonly CiByteString FolderPrefix = new("chara/action/"u8, MetaDataComputation.All);

    private readonly CommunicatorService                  _communicator;
    private readonly FrozenDictionary<CiByteString, uint> _actionTmbs;

    private readonly ConcurrentDictionary<uint, CiByteString> _listedTmbIds = [];

    public bool Contains(uint tmbId)
        => _listedTmbIds.ContainsKey(tmbId);

    public IReadOnlyDictionary<uint, CiByteString> ListedTmbs
        => _listedTmbIds;

    public IReadOnlyDictionary<CiByteString, uint> ActionTmbs
        => _actionTmbs;

    public SchedulerResourceManagementService(IGameInteropProvider interop, CommunicatorService communicator, IDataManager dataManager)
    {
        _communicator = communicator;
        _actionTmbs   = CreateActionTmbs(dataManager);
        _communicator.ResolvedFileChanged.Subscribe(OnResolvedFileChange, ResolvedFileChanged.Priority.SchedulerResourceManagementService);
        interop.InitializeFromAttributes(this);
    }

    private void OnResolvedFileChange(ModCollection collection, ResolvedFileChanged.Type type, Utf8GamePath gamePath, FullPath oldPath,
        FullPath newPath, IMod? mod)
    {
        switch (type)
        {
            case ResolvedFileChanged.Type.Added:
                CheckFile(gamePath);
                return;
            case ResolvedFileChanged.Type.FullRecomputeFinished:
                foreach (var path in collection.ResolvedFiles.Keys)
                    CheckFile(path);
                return;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void CheckFile(Utf8GamePath gamePath)
    {
        if (!gamePath.Extension().Equals(TmbExtension))
            return;

        if (!gamePath.Path.StartsWith(FolderPrefix))
            return;

        var tmb = gamePath.Path.Substring(FolderPrefix.Length, gamePath.Length - FolderPrefix.Length - TmbExtension.Length).Clone();
        if (_actionTmbs.TryGetValue(tmb, out var rowId))
            _listedTmbIds[rowId] = tmb;
        else
            Penumbra.Log.Verbose($"Action TMB {gamePath} encountered with no corresponding row ID.");
    }

    [Signature(Sigs.SchedulerResourceManagementInstance, ScanType = ScanType.StaticAddress)]
    public readonly SchedulerResourceManagement** Address = null;

    public SchedulerResourceManagement* Scheduler
        => *Address;

    public void Dispose()
    {
        _listedTmbIds.Clear();
        _communicator.ResolvedFileChanged.Unsubscribe(OnResolvedFileChange);
    }

    private static FrozenDictionary<CiByteString, uint> CreateActionTmbs(IDataManager dataManager)
    {
        var sheet = dataManager.GetExcelSheet<ActionTimeline>();
        return sheet.Where(row => !row.Key.IsEmpty).DistinctBy(row => row.Key).ToFrozenDictionary(row => new CiByteString(row.Key, MetaDataComputation.All).Clone(), row => row.RowId);
    }
}

using Dalamud.Plugin;
using ImSharp;
using Luna;
using Penumbra.Api.Api;
using Penumbra.Api.Enums;
using Penumbra.Api.IpcSubscribers;
using Penumbra.Collections.Manager;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.Services;

namespace Penumbra.Api.IpcTester;

public class TemporaryIpcTester(
    IDalamudPluginInterface pi,
    ModManager modManager,
    CollectionManager collections,
    TempModManager tempMods,
    TempCollectionManager tempCollections,
    SaveService saveService,
    Configuration config)
    : IUiService
{
    public Guid LastCreatedCollectionId = Guid.Empty;

    private readonly bool _debug = Assembly.GetAssembly(typeof(TemporaryIpcTester))?.GetName().Version?.Major >= 9;

    private Guid?         _tempGuid;
    private string        _tempCollectionName = string.Empty;
    private string        _tempModName        = string.Empty;
    private string        _modDirectory       = string.Empty;
    private string        _tempGamePath       = "test/game/path.mtrl";
    private string        _tempFilePath       = "test/success.mtrl";
    private string        _tempManipulation   = string.Empty;
    private string        _identity           = string.Empty;
    private PenumbraApiEc _lastTempError;
    private int           _tempActorIndex;
    private bool          _forceOverwrite;

    public void Draw()
    {
        using var _ = Im.Tree.Node("Temporary"u8);
        if (!_)
            return;

        Im.Input.Text("##identity"u8,       ref _identity,           "Identity..."u8);
        Im.Input.Text("##tempCollection"u8, ref _tempCollectionName, "Collection Name..."u8);
        ImEx.GuidInput("Collection ID##guid"u8, ref _tempGuid);
        Im.Input.Scalar("##tempActorIndex"u8, ref _tempActorIndex);
        Im.Input.Text("##tempMod"u8,   ref _tempModName,      "Temporary Mod Name..."u8);
        Im.Input.Text("##mod"u8,       ref _modDirectory,     "Existing Mod Name..."u8);
        Im.Input.Text("##tempGame"u8,  ref _tempGamePath,     "Game Path..."u8);
        Im.Input.Text("##tempFile"u8,  ref _tempFilePath,     "File Path..."u8);
        Im.Input.Text("##tempManip"u8, ref _tempManipulation, "Manipulation Base64 String..."u8);
        Im.Checkbox("Force Character Collection Overwrite"u8, ref _forceOverwrite);

        using var table = Im.Table.Begin(StringU8.Empty, 3, TableFlags.SizingFixedFit);
        if (!table)
            return;

        using (IpcTester.DrawIntro("Last Error", $"{_lastTempError}"))
        {
            table.DrawColumn("Last Created Collection"u8);
            table.NextColumn();
            LunaStyle.DrawGuid(LastCreatedCollectionId);
        }

        using (IpcTester.DrawIntro(CreateTemporaryCollection.Label, "Create Temporary Collection"u8))
        {
            table.NextColumn();
            if (Im.SmallButton("Create##Collection"u8))
            {
                _lastTempError = new CreateTemporaryCollection(pi).Invoke(_identity, _tempCollectionName, out LastCreatedCollectionId);
                if (_tempGuid is null)
                    _tempGuid = LastCreatedCollectionId;
            }
        }

        var guid = _tempGuid.GetValueOrDefault(Guid.Empty);

        using (IpcTester.DrawIntro(DeleteTemporaryCollection.Label, "Delete Temporary Collection"u8))
        {
            table.NextColumn();
            if (Im.SmallButton("Delete##Collection"u8))
                _lastTempError = new DeleteTemporaryCollection(pi).Invoke(guid);
            Im.Line.Same();
            if (Im.SmallButton("Delete Last##Collection"u8))
                _lastTempError = new DeleteTemporaryCollection(pi).Invoke(LastCreatedCollectionId);
        }

        using (IpcTester.DrawIntro(AssignTemporaryCollection.Label, "Assign Temporary Collection"u8))
        {
            table.NextColumn();
            if (Im.SmallButton("Assign##NamedCollection"u8))
                _lastTempError = new AssignTemporaryCollection(pi).Invoke(guid, _tempActorIndex, _forceOverwrite);
        }

        using (IpcTester.DrawIntro(AddTemporaryMod.Label, "Add Temporary Mod to specific Collection"u8))
        {
            table.NextColumn();
            if (Im.SmallButton("Add##Mod"u8))
                _lastTempError = new AddTemporaryMod(pi).Invoke(_tempModName, guid,
                    new Dictionary<string, string> { { _tempGamePath, _tempFilePath } },
                    _tempManipulation.Length > 0 ? _tempManipulation : string.Empty, int.MaxValue);
        }

        using (IpcTester.DrawIntro(CreateTemporaryCollection.Label, "Copy Existing Collection"u8))
        {
            table.NextColumn();
            if (ImEx.Button("Copy##Collection"u8, Vector2.Zero,
                    "Copies the effective list from the collection named in Temporary Mod Name..."u8,
                    !collections.Storage.ByName(_tempModName, out var copyCollection))
             && copyCollection is { HasCache: true })
            {
                var files  = copyCollection.ResolvedFiles.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value.Path.ToString());
                var manips = MetaApi.CompressMetaManipulations(copyCollection);
                _lastTempError = new AddTemporaryMod(pi).Invoke(_tempModName, guid, files, manips, 999);
            }
        }

        using (IpcTester.DrawIntro(AddTemporaryModAll.Label, "Add Temporary Mod to all Collections"u8))
        {
            table.NextColumn();
            if (Im.SmallButton("Add##All"u8))
                _lastTempError = new AddTemporaryModAll(pi).Invoke(_tempModName,
                    new Dictionary<string, string> { { _tempGamePath, _tempFilePath } },
                    _tempManipulation.Length > 0 ? _tempManipulation : string.Empty, int.MaxValue);
        }

        using (IpcTester.DrawIntro(RemoveTemporaryMod.Label, "Remove Temporary Mod from specific Collection"u8))
        {
            table.NextColumn();
            if (Im.SmallButton("Remove##Mod"u8))
                _lastTempError = new RemoveTemporaryMod(pi).Invoke(_tempModName, guid, int.MaxValue);
        }

        using (IpcTester.DrawIntro(RemoveTemporaryModAll.Label, "Remove Temporary Mod from all Collections"u8))
        {
            table.NextColumn();
            if (Im.SmallButton("Remove##ModAll"u8))
                _lastTempError = new RemoveTemporaryModAll(pi).Invoke(_tempModName, int.MaxValue);
        }

        using (IpcTester.DrawIntro(SetTemporaryModSettings.Label, "Set Temporary Mod Settings (to default) in specific Collection"u8))
        {
            table.NextColumn();
            if (Im.SmallButton("Set##SetTemporary"u8))
                _lastTempError = new SetTemporaryModSettings(pi).Invoke(guid, _modDirectory, false, true, 1337,
                    new Dictionary<string, IReadOnlyList<string>>(),
                    "IPC Tester", 1337);
        }

        using (IpcTester.DrawIntro(SetTemporaryModSettingsPlayer.Label, "Set Temporary Mod Settings (to default) in game object collection"u8))
        {
            table.NextColumn();
            if (Im.SmallButton("Set##SetTemporaryPlayer"u8))
                _lastTempError = new SetTemporaryModSettingsPlayer(pi).Invoke(_tempActorIndex, _modDirectory, false, true, 1337,
                    new Dictionary<string, IReadOnlyList<string>>(),
                    "IPC Tester", 1337);
        }

        using (IpcTester.DrawIntro(RemoveTemporaryModSettings.Label, "Remove Temporary Mod Settings from specific Collection"u8))
        {
            table.NextColumn();
            if (Im.SmallButton("Remove##RemoveTemporary"u8))
                _lastTempError = new RemoveTemporaryModSettings(pi).Invoke(guid, _modDirectory, 1337);
            Im.Line.Same();
            if (Im.SmallButton("Remove (Wrong Key)##RemoveTemporary"u8))
                _lastTempError = new RemoveTemporaryModSettings(pi).Invoke(guid, _modDirectory, 1338);
        }

        using (IpcTester.DrawIntro(RemoveTemporaryModSettingsPlayer.Label, "Remove Temporary Mod Settings from game object Collection"u8))
        {
            table.NextColumn();
            if (Im.SmallButton("Remove##RemoveTemporaryPlayer"u8))
                _lastTempError = new RemoveTemporaryModSettingsPlayer(pi).Invoke(_tempActorIndex, _modDirectory, 1337);
            Im.Line.Same();
            if (Im.SmallButton("Remove (Wrong Key)##RemoveTemporaryPlayer"u8))
                _lastTempError = new RemoveTemporaryModSettingsPlayer(pi).Invoke(_tempActorIndex, _modDirectory, 1338);
        }

        using (IpcTester.DrawIntro(RemoveAllTemporaryModSettings.Label, "Remove All Temporary Mod Settings from specific Collection"u8))
        {
            table.NextColumn();
            if (Im.SmallButton("Remove##RemoveAllTemporary"u8))
                _lastTempError = new RemoveAllTemporaryModSettings(pi).Invoke(guid, 1337);
            Im.Line.Same();
            if (Im.SmallButton("Remove (Wrong Key)##RemoveAllTemporary"u8))
                _lastTempError = new RemoveAllTemporaryModSettings(pi).Invoke(guid, 1338);
        }

        using (IpcTester.DrawIntro(RemoveAllTemporaryModSettingsPlayer.Label,
                   "Remove All Temporary Mod Settings from game object Collection"u8))
        {
            table.NextColumn();
            if (Im.SmallButton("Remove##RemoveAllTemporaryPlayer"u8))
                _lastTempError = new RemoveAllTemporaryModSettingsPlayer(pi).Invoke(_tempActorIndex, 1337);
            Im.Line.Same();
            if (Im.SmallButton("Remove (Wrong Key)##RemoveAllTemporaryPlayer"u8))
                _lastTempError = new RemoveAllTemporaryModSettingsPlayer(pi).Invoke(_tempActorIndex, 1338);
        }

        using (IpcTester.DrawIntro(QueryTemporaryModSettings.Label, "Query Temporary Mod Settings from specific Collection"u8))
        {
            table.NextColumn();
            Im.SmallButton("Query##QueryTemporaryModSettings"u8);
            if (Im.Item.Hovered())
            {
                _lastTempError = new QueryTemporaryModSettings(pi).Invoke(guid, _modDirectory, out var settings, out var source, 1337);
                DrawTooltip(settings, source);
            }

            Im.Line.Same();
            Im.SmallButton("Query (Wrong Key)##RemoveAllTemporary"u8);
            if (Im.Item.Hovered())
            {
                _lastTempError = new QueryTemporaryModSettings(pi).Invoke(guid, _modDirectory, out var settings, out var source, 1338);
                DrawTooltip(settings, source);
            }
        }

        using (IpcTester.DrawIntro(QueryTemporaryModSettingsPlayer.Label, "Query Temporary Mod Settings from game object Collection"u8))
        {
            table.NextColumn();
            Im.SmallButton("Query##QueryTemporaryModSettingsPlayer"u8);
            if (Im.Item.Hovered())
            {
                _lastTempError =
                    new QueryTemporaryModSettingsPlayer(pi).Invoke(_tempActorIndex, _modDirectory, out var settings, out var source, 1337);
                DrawTooltip(settings, source);
            }

            Im.Line.Same();
            Im.SmallButton("Query (Wrong Key)##RemoveAllTemporaryPlayer"u8);
            if (Im.Item.Hovered())
            {
                _lastTempError =
                    new QueryTemporaryModSettingsPlayer(pi).Invoke(_tempActorIndex, _modDirectory, out var settings, out var source, 1338);
                DrawTooltip(settings, source);
            }
        }

        return;

        void DrawTooltip((bool ForceInherit, bool Enabled, int Priority, Dictionary<string, List<string>> Settings)? settings, string source)
        {
            using var tt = Im.Tooltip.Begin();
            Im.Text($"Query returned {_lastTempError}");
            if (settings != null)
                Im.Text($"Settings created by {(source.Length == 0 ? "Unknown Source" : source)}:");
            else
                Im.Text(source.Length > 0 ? $"Locked by {source}." : "No settings exist.");
            Im.Separator();
            if (settings == null)
                return;

            using (Im.Group())
            {
                Im.Text("Force Inherit"u8);
                Im.Text("Enabled"u8);
                Im.Text("Priority"u8);
                foreach (var group in settings.Value.Settings.Keys)
                    Im.Text(group);
            }

            Im.Line.Same();
            using (Im.Group())
            {
                Im.Text($"{settings.Value.ForceInherit}");
                Im.Text($"{settings.Value.Enabled}");
                Im.Text($"{settings.Value.Priority}");
                foreach (var group in settings.Value.Settings.Values)
                    Im.Text(string.Join("; ", group));
            }
        }
    }

    public void DrawCollections()
    {
        using var collTree = Im.Tree.Node("Temporary Collections##TempCollections"u8);
        if (!collTree)
            return;

        using var table = Im.Table.Begin("##collTree"u8, 6, TableFlags.SizingFixedFit);
        if (!table)
            return;

        foreach (var (idx, collection) in tempCollections.Values.Index())
        {
            using var id = Im.Id.Push(idx);
            table.NextColumn();
            var character = tempCollections.Collections.Where(p => p.Collection == collection).Select(p => p.DisplayName)
                    .FirstOrDefault()
             ?? "Unknown";
            if (_debug && Im.Button("Save##Collection"u8))
                TemporaryMod.SaveTempCollection(config, saveService, modManager, collection, character);

            table.NextColumn();
            LunaStyle.DrawGuid(collection.Identity.Id);
            table.DrawColumn(collection.Identity.Name);
            table.DrawColumn($"{collection.ResolvedFiles.Count}");
            table.DrawColumn($"{collection.MetaCache?.Count ?? 0}");
            table.DrawColumn(string.Join(", ",
                tempCollections.Collections.Where(p => p.Collection == collection).Select(c => c.DisplayName)));
        }
    }

    public void DrawMods()
    {
        using var modTree = Im.Tree.Node("Temporary Mods##TempMods"u8);
        if (!modTree)
            return;

        using var table = Im.Table.Begin("##modTree"u8, 5, TableFlags.SizingFixedFit);
        if (!table)
            return;

        PrintList(table, "All"u8, tempMods.ModsForAllCollections);
        foreach (var (collection, list) in tempMods.Mods)
            PrintList(table, collection.Identity.Name, list);

        return;

        static void PrintList(in Im.TableDisposable table, Utf8StringHandler<TextStringHandlerBuffer> collectionName,
            IReadOnlyList<TemporaryMod> list)
        {
            foreach (var mod in list)
            {
                table.DrawColumn(mod.Name);
                table.DrawColumn($"{mod.Priority}");
                table.DrawColumn(ref collectionName);
                table.DrawColumn($"{mod.Default.Files.Count}");
                if (Im.Item.Hovered())
                {
                    using var tt = Im.Tooltip.Begin();
                    foreach (var (path, file) in mod.Default.Files)
                        Im.Text($"{path} -> {file}");
                }

                table.DrawColumn($"{mod.TotalManipulations}");
                if (Im.Item.Hovered())
                {
                    using var tt = Im.Tooltip.Begin();
                    foreach (var identifier in mod.Default.Manipulations.Identifiers)
                        Im.Text($"{identifier}");
                }
            }
        }
    }
}

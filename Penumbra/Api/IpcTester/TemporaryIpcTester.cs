using Dalamud.Interface;
using Dalamud.Plugin;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Services;
using OtterGui.Text;
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
    private string        _tempCollectionName     = string.Empty;
    private string        _tempCollectionGuidName = string.Empty;
    private string        _tempModName            = string.Empty;
    private string        _modDirectory           = string.Empty;
    private string        _tempGamePath           = "test/game/path.mtrl";
    private string        _tempFilePath           = "test/success.mtrl";
    private string        _tempManipulation       = string.Empty;
    private PenumbraApiEc _lastTempError;
    private int           _tempActorIndex;
    private bool          _forceOverwrite;

    public void Draw()
    {
        using var _ = ImRaii.TreeNode("Temporary");
        if (!_)
            return;

        ImGui.InputTextWithHint("##tempCollection", "Collection Name...", ref _tempCollectionName, 128);
        ImGuiUtil.GuidInput("##guid", "Collection GUID...", string.Empty, ref _tempGuid, ref _tempCollectionGuidName);
        ImGui.InputInt("##tempActorIndex", ref _tempActorIndex, 0, 0);
        ImGui.InputTextWithHint("##tempMod",  "Temporary Mod Name...", ref _tempModName,  32);
        ImGui.InputTextWithHint("##mod",      "Existing Mod Name...",  ref _modDirectory, 256);
        ImGui.InputTextWithHint("##tempGame", "Game Path...",          ref _tempGamePath, 256);
        ImGui.InputTextWithHint("##tempFile", "File Path...",          ref _tempFilePath, 256);
        ImUtf8.InputText("##tempManip"u8, ref _tempManipulation, "Manipulation Base64 String..."u8);
        ImGui.Checkbox("Force Character Collection Overwrite", ref _forceOverwrite);

        using var table = ImRaii.Table(string.Empty, 3, ImGuiTableFlags.SizingFixedFit);
        if (!table)
            return;

        IpcTester.DrawIntro("Last Error", _lastTempError.ToString());
        ImGuiUtil.DrawTableColumn("Last Created Collection");
        ImGui.TableNextColumn();
        using (ImRaii.PushFont(UiBuilder.MonoFont))
        {
            ImGuiUtil.CopyOnClickSelectable(LastCreatedCollectionId.ToString());
        }

        IpcTester.DrawIntro(CreateTemporaryCollection.Label, "Create Temporary Collection");
        if (ImGui.Button("Create##Collection"))
        {
            LastCreatedCollectionId = new CreateTemporaryCollection(pi).Invoke(_tempCollectionName);
            if (_tempGuid == null)
            {
                _tempGuid               = LastCreatedCollectionId;
                _tempCollectionGuidName = LastCreatedCollectionId.ToString();
            }
        }

        var guid = _tempGuid.GetValueOrDefault(Guid.Empty);

        IpcTester.DrawIntro(DeleteTemporaryCollection.Label, "Delete Temporary Collection");
        if (ImGui.Button("Delete##Collection"))
            _lastTempError = new DeleteTemporaryCollection(pi).Invoke(guid);
        ImGui.SameLine();
        if (ImGui.Button("Delete Last##Collection"))
            _lastTempError = new DeleteTemporaryCollection(pi).Invoke(LastCreatedCollectionId);

        IpcTester.DrawIntro(AssignTemporaryCollection.Label, "Assign Temporary Collection");
        if (ImGui.Button("Assign##NamedCollection"))
            _lastTempError = new AssignTemporaryCollection(pi).Invoke(guid, _tempActorIndex, _forceOverwrite);

        IpcTester.DrawIntro(AddTemporaryMod.Label, "Add Temporary Mod to specific Collection");
        if (ImGui.Button("Add##Mod"))
            _lastTempError = new AddTemporaryMod(pi).Invoke(_tempModName, guid,
                new Dictionary<string, string> { { _tempGamePath, _tempFilePath } },
                _tempManipulation.Length > 0 ? _tempManipulation : string.Empty, int.MaxValue);

        IpcTester.DrawIntro(CreateTemporaryCollection.Label, "Copy Existing Collection");
        if (ImGuiUtil.DrawDisabledButton("Copy##Collection", Vector2.Zero,
                "Copies the effective list from the collection named in Temporary Mod Name...",
                !collections.Storage.ByName(_tempModName, out var copyCollection))
         && copyCollection is { HasCache: true })
        {
            var files  = copyCollection.ResolvedFiles.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value.Path.ToString());
            var manips = MetaApi.CompressMetaManipulations(copyCollection);
            _lastTempError = new AddTemporaryMod(pi).Invoke(_tempModName, guid, files, manips, 999);
        }

        IpcTester.DrawIntro(AddTemporaryModAll.Label, "Add Temporary Mod to all Collections");
        if (ImGui.Button("Add##All"))
            _lastTempError = new AddTemporaryModAll(pi).Invoke(_tempModName,
                new Dictionary<string, string> { { _tempGamePath, _tempFilePath } },
                _tempManipulation.Length > 0 ? _tempManipulation : string.Empty, int.MaxValue);

        IpcTester.DrawIntro(RemoveTemporaryMod.Label, "Remove Temporary Mod from specific Collection");
        if (ImGui.Button("Remove##Mod"))
            _lastTempError = new RemoveTemporaryMod(pi).Invoke(_tempModName, guid, int.MaxValue);

        IpcTester.DrawIntro(RemoveTemporaryModAll.Label, "Remove Temporary Mod from all Collections");
        if (ImGui.Button("Remove##ModAll"))
            _lastTempError = new RemoveTemporaryModAll(pi).Invoke(_tempModName, int.MaxValue);

        IpcTester.DrawIntro(SetTemporaryModSettings.Label, "Set Temporary Mod Settings (to default) in specific Collection");
        if (ImUtf8.Button("Set##SetTemporary"u8))
            _lastTempError = new SetTemporaryModSettings(pi).Invoke(guid, _modDirectory, false, true, 1337,
                new Dictionary<string, IReadOnlyList<string>>(),
                "IPC Tester", 1337);

        IpcTester.DrawIntro(SetTemporaryModSettingsPlayer.Label, "Set Temporary Mod Settings (to default) in game object collection");
        if (ImUtf8.Button("Set##SetTemporaryPlayer"u8))
            _lastTempError = new SetTemporaryModSettingsPlayer(pi).Invoke(_tempActorIndex, _modDirectory, false, true, 1337,
                new Dictionary<string, IReadOnlyList<string>>(),
                "IPC Tester", 1337);

        IpcTester.DrawIntro(RemoveTemporaryModSettings.Label, "Remove Temporary Mod Settings from specific Collection");
        if (ImUtf8.Button("Remove##RemoveTemporary"u8))
            _lastTempError = new RemoveTemporaryModSettings(pi).Invoke(guid, _modDirectory, 1337);
        ImGui.SameLine();
        if (ImUtf8.Button("Remove (Wrong Key)##RemoveTemporary"u8))
            _lastTempError = new RemoveTemporaryModSettings(pi).Invoke(guid, _modDirectory, 1338);

        IpcTester.DrawIntro(RemoveTemporaryModSettingsPlayer.Label, "Remove Temporary Mod Settings from game object Collection");
        if (ImUtf8.Button("Remove##RemoveTemporaryPlayer"u8))
            _lastTempError = new RemoveTemporaryModSettingsPlayer(pi).Invoke(_tempActorIndex, _modDirectory, 1337);
        ImGui.SameLine();
        if (ImUtf8.Button("Remove (Wrong Key)##RemoveTemporaryPlayer"u8))
            _lastTempError = new RemoveTemporaryModSettingsPlayer(pi).Invoke(_tempActorIndex, _modDirectory, 1338);

        IpcTester.DrawIntro(RemoveAllTemporaryModSettings.Label, "Remove All Temporary Mod Settings from specific Collection");
        if (ImUtf8.Button("Remove##RemoveAllTemporary"u8))
            _lastTempError = new RemoveAllTemporaryModSettings(pi).Invoke(guid, 1337);
        ImGui.SameLine();
        if (ImUtf8.Button("Remove (Wrong Key)##RemoveAllTemporary"u8))
            _lastTempError = new RemoveAllTemporaryModSettings(pi).Invoke(guid, 1338);

        IpcTester.DrawIntro(RemoveAllTemporaryModSettingsPlayer.Label, "Remove All Temporary Mod Settings from game object Collection");
        if (ImUtf8.Button("Remove##RemoveAllTemporaryPlayer"u8))
            _lastTempError = new RemoveAllTemporaryModSettingsPlayer(pi).Invoke(_tempActorIndex, 1337);
        ImGui.SameLine();
        if (ImUtf8.Button("Remove (Wrong Key)##RemoveAllTemporaryPlayer"u8))
            _lastTempError = new RemoveAllTemporaryModSettingsPlayer(pi).Invoke(_tempActorIndex, 1338);

        IpcTester.DrawIntro(QueryTemporaryModSettings.Label, "Query Temporary Mod Settings from specific Collection");
        ImUtf8.Button("Query##QueryTemporaryModSettings"u8);
        if (ImGui.IsItemHovered())
        {
            _lastTempError = new QueryTemporaryModSettings(pi).Invoke(guid, _modDirectory, out var settings, out var source, 1337);
            DrawTooltip(settings, source);
        }

        ImGui.SameLine();
        ImUtf8.Button("Query (Wrong Key)##RemoveAllTemporary"u8);
        if (ImGui.IsItemHovered())
        {
            _lastTempError = new QueryTemporaryModSettings(pi).Invoke(guid, _modDirectory, out var settings, out var source, 1338);
            DrawTooltip(settings, source);
        }

        IpcTester.DrawIntro(QueryTemporaryModSettingsPlayer.Label, "Query Temporary Mod Settings from game object Collection");
        ImUtf8.Button("Query##QueryTemporaryModSettingsPlayer"u8);
        if (ImGui.IsItemHovered())
        {
            _lastTempError =
                new QueryTemporaryModSettingsPlayer(pi).Invoke(_tempActorIndex, _modDirectory, out var settings, out var source, 1337);
            DrawTooltip(settings, source);
        }

        ImGui.SameLine();
        ImUtf8.Button("Query (Wrong Key)##RemoveAllTemporaryPlayer"u8);
        if (ImGui.IsItemHovered())
        {
            _lastTempError =
                new QueryTemporaryModSettingsPlayer(pi).Invoke(_tempActorIndex, _modDirectory, out var settings, out var source, 1338);
            DrawTooltip(settings, source);
        }

        void DrawTooltip((bool ForceInherit, bool Enabled, int Priority, Dictionary<string, List<string>> Settings)? settings, string source)
        {
            using var tt = ImUtf8.Tooltip();
            ImUtf8.Text($"Query returned {_lastTempError}");
            if (settings != null)
                ImUtf8.Text($"Settings created by {(source.Length == 0 ? "Unknown Source" : source)}:");
            else
                ImUtf8.Text(source.Length > 0 ? $"Locked by {source}." : "No settings exist.");
            ImGui.Separator();
            if (settings == null)
            {
                
                return;
            }

            using (ImUtf8.Group())
            {
                ImUtf8.Text("Force Inherit"u8);
                ImUtf8.Text("Enabled"u8);
                ImUtf8.Text("Priority"u8);
                foreach (var group in settings.Value.Settings.Keys)
                    ImUtf8.Text(group);
            }

            ImGui.SameLine();
            using (ImUtf8.Group())
            {
                ImUtf8.Text($"{settings.Value.ForceInherit}");
                ImUtf8.Text($"{settings.Value.Enabled}");
                ImUtf8.Text($"{settings.Value.Priority}");
                foreach (var group in settings.Value.Settings.Values)
                    ImUtf8.Text(string.Join("; ", group));
            }
        }
    }

    public void DrawCollections()
    {
        using var collTree = ImUtf8.TreeNode("Temporary Collections##TempCollections"u8);
        if (!collTree)
            return;

        using var table = ImUtf8.Table("##collTree"u8, 6, ImGuiTableFlags.SizingFixedFit);
        if (!table)
            return;

        foreach (var (collection, idx) in tempCollections.Values.WithIndex())
        {
            using var id = ImRaii.PushId(idx);
            ImGui.TableNextColumn();
            var character = tempCollections.Collections.Where(p => p.Collection == collection).Select(p => p.DisplayName)
                    .FirstOrDefault()
             ?? "Unknown";
            if (_debug && ImUtf8.Button("Save##Collection"u8))
                TemporaryMod.SaveTempCollection(config, saveService, modManager, collection, character);

            using (ImRaii.PushFont(UiBuilder.MonoFont))
            {
                ImGui.TableNextColumn();
                ImGuiUtil.CopyOnClickSelectable(collection.Identity.Identifier);
            }

            ImGuiUtil.DrawTableColumn(collection.Identity.Name);
            ImGuiUtil.DrawTableColumn(collection.ResolvedFiles.Count.ToString());
            ImGuiUtil.DrawTableColumn(collection.MetaCache?.Count.ToString() ?? "0");
            ImGuiUtil.DrawTableColumn(string.Join(", ",
                tempCollections.Collections.Where(p => p.Collection == collection).Select(c => c.DisplayName)));
        }
    }

    public void DrawMods()
    {
        using var modTree = ImRaii.TreeNode("Temporary Mods##TempMods");
        if (!modTree)
            return;

        using var table = ImRaii.Table("##modTree", 5, ImGuiTableFlags.SizingFixedFit);

        void PrintList(string collectionName, IReadOnlyList<TemporaryMod> list)
        {
            foreach (var mod in list)
            {
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(mod.Name);
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(mod.Priority.ToString());
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(collectionName);
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(mod.Default.Files.Count.ToString());
                if (ImGui.IsItemHovered())
                {
                    using var tt = ImRaii.Tooltip();
                    foreach (var (path, file) in mod.Default.Files)
                        ImGui.TextUnformatted($"{path} -> {file}");
                }

                ImGui.TableNextColumn();
                ImGui.TextUnformatted(mod.TotalManipulations.ToString());
                if (ImGui.IsItemHovered())
                {
                    using var tt = ImRaii.Tooltip();
                    foreach (var identifier in mod.Default.Manipulations.Identifiers)
                        ImGui.TextUnformatted(identifier.ToString());
                }
            }
        }

        if (table)
        {
            PrintList("All", tempMods.ModsForAllCollections);
            foreach (var (collection, list) in tempMods.Mods)
                PrintList(collection.Identity.Name, list);
        }
    }
}

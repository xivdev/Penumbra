using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.System.Resource;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ImGuiNET;
using Microsoft.Extensions.DependencyInjection;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Services;
using OtterGui.Text;
using OtterGui.Widgets;
using Penumbra.Api;
using Penumbra.Collections.Manager;
using Penumbra.GameData.Actors;
using Penumbra.GameData.DataContainers;
using Penumbra.GameData.Files;
using Penumbra.GameData.Interop;
using Penumbra.Import.Structs;
using Penumbra.Import.Textures;
using Penumbra.Interop.PathResolving;
using Penumbra.Interop.Services;
using Penumbra.Interop.Structs;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.Services;
using Penumbra.String;
using Penumbra.UI.Classes;
using Penumbra.Util;
using static OtterGui.Raii.ImRaii;
using CharacterBase = FFXIVClientStructs.FFXIV.Client.Graphics.Scene.CharacterBase;
using ImGuiClip = OtterGui.ImGuiClip;
using Penumbra.Api.IpcTester;
using Penumbra.GameData.Data;
using Penumbra.Interop.Hooks.PostProcessing;
using Penumbra.Interop.Hooks.ResourceLoading;
using Penumbra.GameData.Files.StainMapStructs;
using Penumbra.String.Classes;
using Penumbra.UI.AdvancedWindow.Materials;

namespace Penumbra.UI.Tabs.Debug;

public class Diagnostics(ServiceManager provider) : IUiService
{
    public void DrawDiagnostics()
    {
        if (!ImGui.CollapsingHeader("Diagnostics"))
            return;

        using var table = ImRaii.Table("##data", 4, ImGuiTableFlags.RowBg);
        if (!table)
            return;

        foreach (var type in typeof(ActorManager).Assembly.GetTypes()
                     .Where(t => t is { IsAbstract: false, IsInterface: false } && t.IsAssignableTo(typeof(IAsyncDataContainer))))
        {
            var container = (IAsyncDataContainer)provider.Provider!.GetRequiredService(type);
            ImGuiUtil.DrawTableColumn(container.Name);
            ImGuiUtil.DrawTableColumn(container.Time.ToString());
            ImGuiUtil.DrawTableColumn(Functions.HumanReadableSize(container.Memory));
            ImGuiUtil.DrawTableColumn(container.TotalCount.ToString());
        }
    }
}

public class DebugTab : Window, ITab, IUiService
{
    private readonly PerformanceTracker                 _performance;
    private readonly Configuration                      _config;
    private readonly CollectionManager                  _collectionManager;
    private readonly ModManager                         _modManager;
    private readonly ValidityChecker                    _validityChecker;
    private readonly HttpApi                            _httpApi;
    private readonly ActorManager                       _actors;
    private readonly StainService                       _stains;
    private readonly GlobalVariablesDrawer              _globalVariablesDrawer;
    private readonly ResourceManagerService             _resourceManager;
    private readonly ResourceLoader                     _resourceLoader;
    private readonly CollectionResolver                 _collectionResolver;
    private readonly DrawObjectState                    _drawObjectState;
    private readonly PathState                          _pathState;
    private readonly SubfileHelper                      _subfileHelper;
    private readonly IdentifiedCollectionCache          _identifiedCollectionCache;
    private readonly CutsceneService                    _cutsceneService;
    private readonly ModImportManager                   _modImporter;
    private readonly ImportPopup                        _importPopup;
    private readonly FrameworkManager                   _framework;
    private readonly TextureManager                     _textureManager;
    private readonly ShaderReplacementFixer             _shaderReplacementFixer;
    private readonly RedrawService                      _redraws;
    private readonly DictEmote                          _emotes;
    private readonly Diagnostics                        _diagnostics;
    private readonly ObjectManager                      _objects;
    private readonly IClientState                       _clientState;
    private readonly IDataManager                       _dataManager;
    private readonly IpcTester                          _ipcTester;
    private readonly CrashHandlerPanel                  _crashHandlerPanel;
    private readonly TexHeaderDrawer                    _texHeaderDrawer;
    private readonly HookOverrideDrawer                 _hookOverrides;
    private readonly RsfService                         _rsfService;
    private readonly SchedulerResourceManagementService _schedulerService;
    private readonly ObjectIdentification               _objectIdentification;
    private readonly RenderTargetDrawer                 _renderTargetDrawer;

    public DebugTab(PerformanceTracker performance, Configuration config, CollectionManager collectionManager, ObjectManager objects,
        IClientState clientState, IDataManager dataManager,
        ValidityChecker validityChecker, ModManager modManager, HttpApi httpApi, ActorManager actors, StainService stains,
        ResourceManagerService resourceManager, ResourceLoader resourceLoader, CollectionResolver collectionResolver,
        DrawObjectState drawObjectState, PathState pathState, SubfileHelper subfileHelper, IdentifiedCollectionCache identifiedCollectionCache,
        CutsceneService cutsceneService, ModImportManager modImporter, ImportPopup importPopup, FrameworkManager framework,
        TextureManager textureManager, ShaderReplacementFixer shaderReplacementFixer, RedrawService redraws, DictEmote emotes,
        Diagnostics diagnostics, IpcTester ipcTester, CrashHandlerPanel crashHandlerPanel, TexHeaderDrawer texHeaderDrawer,
        HookOverrideDrawer hookOverrides, RsfService rsfService, GlobalVariablesDrawer globalVariablesDrawer,
        SchedulerResourceManagementService schedulerService, ObjectIdentification objectIdentification, RenderTargetDrawer renderTargetDrawer)
        : base("Penumbra Debug Window", ImGuiWindowFlags.NoCollapse)
    {
        IsOpen = true;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(200,  200),
            MaximumSize = new Vector2(2000, 2000),
        };
        _performance               = performance;
        _config                    = config;
        _collectionManager         = collectionManager;
        _validityChecker           = validityChecker;
        _modManager                = modManager;
        _httpApi                   = httpApi;
        _actors                    = actors;
        _stains                    = stains;
        _resourceManager           = resourceManager;
        _resourceLoader            = resourceLoader;
        _collectionResolver        = collectionResolver;
        _drawObjectState           = drawObjectState;
        _pathState                 = pathState;
        _subfileHelper             = subfileHelper;
        _identifiedCollectionCache = identifiedCollectionCache;
        _cutsceneService           = cutsceneService;
        _modImporter               = modImporter;
        _importPopup               = importPopup;
        _framework                 = framework;
        _textureManager            = textureManager;
        _shaderReplacementFixer    = shaderReplacementFixer;
        _redraws                   = redraws;
        _emotes                    = emotes;
        _diagnostics               = diagnostics;
        _ipcTester                 = ipcTester;
        _crashHandlerPanel         = crashHandlerPanel;
        _texHeaderDrawer           = texHeaderDrawer;
        _hookOverrides             = hookOverrides;
        _rsfService                = rsfService;
        _globalVariablesDrawer     = globalVariablesDrawer;
        _schedulerService          = schedulerService;
        _objectIdentification      = objectIdentification;
        _renderTargetDrawer        = renderTargetDrawer;
        _objects                   = objects;
        _clientState               = clientState;
        _dataManager               = dataManager;
    }

    public ReadOnlySpan<byte> Label
        => "Debug"u8;

    public bool IsVisible
        => _config is { DebugMode: true, Ephemeral.DebugSeparateWindow: false };

#if DEBUG
    private const string DebugVersionString = "(Debug)";
#else
    private const string DebugVersionString = "(Release)";
#endif

    public void DrawContent()
    {
        using var child = Child("##DebugTab", -Vector2.One);
        if (!child)
            return;

        DrawDebugTabGeneral();
        _crashHandlerPanel.Draw();
        DebugConfigurationDrawer.Draw();
        _diagnostics.DrawDiagnostics();
        DrawPerformanceTab();
        DrawPathResolverDebug();
        DrawActorsDebug();
        DrawCollectionCaches();
        _texHeaderDrawer.Draw();
        DrawShaderReplacementFixer();
        DrawData();
        DrawCrcCache();
        DrawResourceLoader();
        DrawResourceProblems();
        _renderTargetDrawer.Draw();
        _hookOverrides.Draw();
        DrawPlayerModelInfo();
        _globalVariablesDrawer.Draw();
        DrawDebugTabIpc();
    }


    private unsafe void DrawCollectionCaches()
    {
        if (!ImGui.CollapsingHeader(
                $"Collections ({_collectionManager.Caches.Count}/{_collectionManager.Storage.Count - 1} Caches)###Collections"))
            return;

        foreach (var collection in _collectionManager.Storage)
        {
            if (collection.HasCache)
            {
                using var color = PushColor(ImGuiCol.Text, ColorId.FolderExpanded.Value());
                using var node =
                    TreeNode($"{collection.Identity.Name} (Change Counter {collection.Counters.Change})###{collection.Identity.Name}");
                if (!node)
                    continue;

                color.Pop();
                using (var inheritanceNode = ImUtf8.TreeNode("Inheritance"u8))
                {
                    if (inheritanceNode)
                    {
                        using var table = ImUtf8.Table("table"u8, 3,
                            ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV);
                        if (table)
                        {
                            var max = Math.Max(
                                Math.Max(collection.Inheritance.DirectlyInheritedBy.Count, collection.Inheritance.DirectlyInheritsFrom.Count),
                                collection.Inheritance.FlatHierarchy.Count);
                            for (var i = 0; i < max; ++i)
                            {
                                ImGui.TableNextColumn();
                                if (i < collection.Inheritance.DirectlyInheritsFrom.Count)
                                    ImUtf8.Text(collection.Inheritance.DirectlyInheritsFrom[i].Identity.Name);
                                else
                                    ImGui.Dummy(new Vector2(200 * ImUtf8.GlobalScale, ImGui.GetTextLineHeight()));
                                ImGui.TableNextColumn();
                                if (i < collection.Inheritance.DirectlyInheritedBy.Count)
                                    ImUtf8.Text(collection.Inheritance.DirectlyInheritedBy[i].Identity.Name);
                                else
                                    ImGui.Dummy(new Vector2(200 * ImUtf8.GlobalScale, ImGui.GetTextLineHeight()));
                                ImGui.TableNextColumn();
                                if (i < collection.Inheritance.FlatHierarchy.Count)
                                    ImUtf8.Text(collection.Inheritance.FlatHierarchy[i].Identity.Name);
                                else
                                    ImGui.Dummy(new Vector2(200 * ImUtf8.GlobalScale, ImGui.GetTextLineHeight()));
                            }
                        }
                    }
                }

                using (var resourceNode = ImUtf8.TreeNode("Custom Resources"u8))
                {
                    if (resourceNode)
                        foreach (var (path, resource) in collection._cache!.CustomResources)
                        {
                            ImUtf8.TreeNode($"{path} -> 0x{(ulong)resource.ResourceHandle:X}",
                                ImGuiTreeNodeFlags.Bullet | ImGuiTreeNodeFlags.Leaf).Dispose();
                        }
                }

                using var modNode = ImUtf8.TreeNode("Enabled Mods"u8);
                if (modNode)
                    foreach (var (mod, paths, manips) in collection._cache!.ModData.Data.OrderBy(t => t.Item1.Name))
                    {
                        using var id    = mod is TemporaryMod t ? PushId(t.Priority.Value) : PushId(((Mod)mod).ModPath.Name);
                        using var node2 = TreeNode(mod.Name.Text);
                        if (!node2)
                            continue;

                        foreach (var path in paths)

                            TreeNode(path.ToString(), ImGuiTreeNodeFlags.Bullet | ImGuiTreeNodeFlags.Leaf).Dispose();

                        foreach (var manip in manips)
                            TreeNode(manip.ToString(), ImGuiTreeNodeFlags.Bullet | ImGuiTreeNodeFlags.Leaf).Dispose();
                    }
            }
            else
            {
                using var color = PushColor(ImGuiCol.Text, ColorId.UndefinedMod.Value());
                TreeNode($"{collection.Identity.Name} (Change Counter {collection.Counters.Change})",
                    ImGuiTreeNodeFlags.Bullet | ImGuiTreeNodeFlags.Leaf).Dispose();
            }
        }
    }

    /// <summary> Draw general information about mod and collection state. </summary>
    private void DrawDebugTabGeneral()
    {
        if (!ImGui.CollapsingHeader("General"))
            return;

        var separateWindow = _config.Ephemeral.DebugSeparateWindow;
        if (ImGui.Checkbox("Draw as Separate Window", ref separateWindow))
        {
            IsOpen                                = true;
            _config.Ephemeral.DebugSeparateWindow = separateWindow;
            _config.Ephemeral.Save();
        }

        using (var table = Table("##DebugGeneralTable", 2, ImGuiTableFlags.SizingFixedFit))
        {
            if (table)
            {
                PrintValue("Penumbra Version",                 $"{_validityChecker.Version} {DebugVersionString}");
                PrintValue("Git Commit Hash",                  _validityChecker.CommitHash);
                PrintValue(TutorialService.SelectedCollection, _collectionManager.Active.Current.Identity.Name);
                PrintValue("    has Cache",                    _collectionManager.Active.Current.HasCache.ToString());
                PrintValue(TutorialService.DefaultCollection,  _collectionManager.Active.Default.Identity.Name);
                PrintValue("    has Cache",                    _collectionManager.Active.Default.HasCache.ToString());
                PrintValue("Mod Manager BasePath",             _modManager.BasePath.Name);
                PrintValue("Mod Manager BasePath-Full",        _modManager.BasePath.FullName);
                PrintValue("Mod Manager BasePath IsRooted",    Path.IsPathRooted(_config.ModDirectory).ToString());
                PrintValue("Mod Manager BasePath Exists",      Directory.Exists(_modManager.BasePath.FullName).ToString());
                PrintValue("Mod Manager Valid",                _modManager.Valid.ToString());
                PrintValue("Web Server Enabled",               _httpApi.Enabled.ToString());
            }
        }


        var issues = _modManager.WithIndex().Count(p => p.Index != p.Value.Index);
        using (var tree = TreeNode($"Mods ({issues} Issues)###Mods"))
        {
            if (tree)
            {
                using var table = Table("##DebugModsTable", 3, ImGuiTableFlags.SizingFixedFit);
                if (table)
                {
                    var lastIndex = -1;
                    foreach (var mod in _modManager)
                    {
                        PrintValue(mod.Name, mod.Index.ToString("D5"));
                        ImGui.TableNextColumn();
                        var index = mod.Index;
                        if (index != lastIndex + 1)
                            ImGui.TextUnformatted("!!!");
                        lastIndex = index;
                    }
                }
            }
        }

        using (var tree = TreeNode("Mod Import"))
        {
            if (tree)
            {
                using var table = Table("##DebugModImport", 2, ImGuiTableFlags.SizingFixedFit);
                if (table)
                {
                    var importing = _modImporter.IsImporting(out var importer);
                    PrintValue("Is Importing",            importing.ToString());
                    PrintValue("Importer State",          (importer?.State ?? ImporterState.None).ToString());
                    PrintValue("Import Window Was Drawn", _importPopup.WasDrawn.ToString());
                    PrintValue("Import Popup Was Drawn",  _importPopup.PopupWasDrawn.ToString());
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted("Import Batches");
                    ImGui.TableNextColumn();
                    foreach (var (batch, index) in _modImporter.ModBatches.WithIndex())
                    {
                        foreach (var mod in batch)
                            PrintValue(index.ToString(), mod);
                    }

                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted("Addable Mods");
                    ImGui.TableNextColumn();
                    foreach (var mod in _modImporter.AddableMods)
                    {
                        ImGui.TableNextColumn();
                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted(mod.Name);
                    }
                }
            }
        }

        using (var tree = TreeNode("Framework"))
        {
            if (tree)
            {
                using var table = Table("##DebugFramework", 2, ImGuiTableFlags.SizingFixedFit);
                if (table)
                {
                    foreach (var important in _framework.Important)
                        PrintValue(important, "Immediate");

                    foreach (var (onTick, idx) in _framework.OnTick.WithIndex())
                        PrintValue(onTick, $"{idx + 1} Tick(s) From Now");

                    foreach (var (time, name) in _framework.Delayed)
                    {
                        var span = time - DateTime.UtcNow;
                        PrintValue(name, $"After {span.Minutes:D2}:{span.Seconds:D2}.{span.Milliseconds / 10:D2} (+ Ticks)");
                    }
                }
            }
        }

        using (var tree = TreeNode($"Texture Manager {_textureManager.Tasks.Count}###Texture Manager"))
        {
            if (tree)
            {
                using var table = Table("##Tasks", 2, ImGuiTableFlags.RowBg);
                if (table)
                    foreach (var task in _textureManager.Tasks)
                    {
                        ImGuiUtil.DrawTableColumn(task.Key.ToString()!);
                        ImGuiUtil.DrawTableColumn(task.Value.Item1.Status.ToString());
                    }
            }
        }

        using (var tree = TreeNode("Redraw Service"))
        {
            if (tree)
            {
                using var table = Table("##redraws", 3, ImGuiTableFlags.RowBg);
                if (table)
                {
                    ImGuiUtil.DrawTableColumn("In GPose");
                    ImGuiUtil.DrawTableColumn(_redraws.InGPose.ToString());
                    ImGui.TableNextColumn();

                    ImGuiUtil.DrawTableColumn("Target");
                    ImGuiUtil.DrawTableColumn(_redraws.Target.ToString());
                    ImGui.TableNextColumn();

                    foreach (var (objectIdx, idx) in _redraws.Queue.WithIndex())
                    {
                        var (actualIdx, state) = objectIdx < 0 ? (~objectIdx, "Queued") : (objectIdx, "Invisible");
                        ImGuiUtil.DrawTableColumn($"Redraw Queue #{idx}");
                        ImGuiUtil.DrawTableColumn(actualIdx.ToString());
                        ImGuiUtil.DrawTableColumn(state);
                    }

                    foreach (var (objectIdx, idx) in _redraws.AfterGPoseQueue.WithIndex())
                    {
                        var (actualIdx, state) = objectIdx < 0 ? (~objectIdx, "Queued") : (objectIdx, "Invisible");
                        ImGuiUtil.DrawTableColumn($"GPose Queue #{idx}");
                        ImGuiUtil.DrawTableColumn(actualIdx.ToString());
                        ImGuiUtil.DrawTableColumn(state);
                    }

                    foreach (var (name, idx) in _redraws.GPoseNames.OfType<string>().WithIndex())
                    {
                        ImGuiUtil.DrawTableColumn($"GPose Name #{idx}");
                        ImGuiUtil.DrawTableColumn(name);
                        ImGui.TableNextColumn();
                    }
                }
            }
        }

        using (var tree = ImUtf8.TreeNode("String Memory"u8))
        {
            if (tree)
            {
                using (ImUtf8.Group())
                {
                    ImUtf8.Text("Currently Allocated Strings"u8);
                    ImUtf8.Text("Total Allocated Strings"u8);
                    ImUtf8.Text("Free'd Allocated Strings"u8);
                    ImUtf8.Text("Currently Allocated Bytes"u8);
                    ImUtf8.Text("Total Allocated Bytes"u8);
                    ImUtf8.Text("Free'd Allocated Bytes"u8);
                }

                ImGui.SameLine();
                using (ImUtf8.Group())
                {
                    ImUtf8.Text($"{PenumbraStringMemory.CurrentStrings}");
                    ImUtf8.Text($"{PenumbraStringMemory.AllocatedStrings}");
                    ImUtf8.Text($"{PenumbraStringMemory.FreedStrings}");
                    ImUtf8.Text($"{PenumbraStringMemory.CurrentBytes}");
                    ImUtf8.Text($"{PenumbraStringMemory.AllocatedBytes}");
                    ImUtf8.Text($"{PenumbraStringMemory.FreedBytes}");
                }
            }
        }
    }

    private void DrawPerformanceTab()
    {
        if (!ImGui.CollapsingHeader("Performance"))
            return;

        using (var start = TreeNode("Startup Performance", ImGuiTreeNodeFlags.DefaultOpen))
        {
            if (start)
                ImGui.NewLine();
        }

        _performance.Draw("##performance", "Enable Runtime Performance Tracking", TimingExtensions.ToName);
    }

    private unsafe void DrawActorsDebug()
    {
        if (!ImGui.CollapsingHeader("Actors"))
            return;

        using var table = Table("##actors", 8, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit,
            -Vector2.UnitX);
        if (!table)
            return;

        DrawSpecial("Current Player",  _actors.GetCurrentPlayer());
        DrawSpecial("Current Inspect", _actors.GetInspectPlayer());
        DrawSpecial("Current Card",    _actors.GetCardPlayer());
        DrawSpecial("Current Glamour", _actors.GetGlamourPlayer());

        foreach (var obj in _objects)
        {
            ImGuiUtil.DrawTableColumn(obj.Address != nint.Zero ? $"{((GameObject*)obj.Address)->ObjectIndex}" : "NULL");
            ImGui.TableNextColumn();
            ImGuiUtil.CopyOnClickSelectable($"0x{obj.Address:X}");
            ImGui.TableNextColumn();
            if (obj.Address != nint.Zero)
                ImGuiUtil.CopyOnClickSelectable($"0x{(nint)((Character*)obj.Address)->GameObject.GetDrawObject():X}");
            var identifier = _actors.FromObject(obj, out _, false, true, false);
            ImGuiUtil.DrawTableColumn(_actors.ToString(identifier));
            var id = obj.AsObject->ObjectKind is ObjectKind.BattleNpc
                ? $"{identifier.DataId} | {obj.AsObject->BaseId}"
                : identifier.DataId.ToString();
            ImGuiUtil.DrawTableColumn(id);
            ImGuiUtil.DrawTableColumn(obj.Address != nint.Zero ? $"0x{*(nint*)obj.Address:X}" : "NULL");
            ImGuiUtil.DrawTableColumn(obj.Address != nint.Zero ? $"0x{obj.AsObject->EntityId:X}" : "NULL");
            ImGuiUtil.DrawTableColumn(obj.Address != nint.Zero
                ? obj.AsObject->IsCharacter() ? $"Character: {obj.AsCharacter->ObjectKind}" : "No Character"
                : "NULL");
        }

        return;

        void DrawSpecial(string name, ActorIdentifier id)
        {
            if (!id.IsValid)
                return;

            ImGuiUtil.DrawTableColumn(name);
            ImGuiUtil.DrawTableColumn(string.Empty);
            ImGuiUtil.DrawTableColumn(string.Empty);
            ImGuiUtil.DrawTableColumn(_actors.ToString(id));
            ImGuiUtil.DrawTableColumn(string.Empty);
            ImGuiUtil.DrawTableColumn(string.Empty);
            ImGuiUtil.DrawTableColumn(string.Empty);
            ImGuiUtil.DrawTableColumn(string.Empty);
        }
    }

    /// <summary>
    /// Draw information about which draw objects correspond to which game objects
    /// and which paths are due to be loaded by which collection.
    /// </summary>
    private unsafe void DrawPathResolverDebug()
    {
        if (!ImGui.CollapsingHeader("Path Resolver"))
            return;

        ImGui.TextUnformatted(
            $"Last Game Object: 0x{_collectionResolver.IdentifyLastGameObjectCollection(true).AssociatedGameObject:X} ({_collectionResolver.IdentifyLastGameObjectCollection(true).ModCollection.Identity.Name})");
        using (var drawTree = TreeNode("Draw Object to Object"))
        {
            if (drawTree)
            {
                using var table = Table("###DrawObjectResolverTable", 6, ImGuiTableFlags.SizingFixedFit);
                if (table)
                    foreach (var (drawObject, (gameObjectPtr, child)) in _drawObjectState
                                 .OrderBy(kvp => ((GameObject*)kvp.Value.Item1)->ObjectIndex)
                                 .ThenBy(kvp => kvp.Value.Item2)
                                 .ThenBy(kvp => kvp.Key))
                    {
                        var gameObject = (GameObject*)gameObjectPtr;
                        ImGui.TableNextColumn();

                        ImGuiUtil.CopyOnClickSelectable($"0x{drawObject:X}");
                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted(gameObject->ObjectIndex.ToString());
                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted(child ? "Child" : "Main");
                        ImGui.TableNextColumn();
                        var (address, name) = ($"0x{gameObjectPtr:X}", new ByteString(gameObject->Name).ToString());
                        ImGuiUtil.CopyOnClickSelectable(address);
                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted(name);
                        ImGui.TableNextColumn();
                        var collection = _collectionResolver.IdentifyCollection(gameObject, true);
                        ImGui.TextUnformatted(collection.ModCollection.Identity.Name);
                    }
            }
        }

        using (var pathTree = TreeNode("Path Collections"))
        {
            if (pathTree)
            {
                using var table = Table("###PathCollectionResolverTable", 2, ImGuiTableFlags.SizingFixedFit);
                if (table)
                    foreach (var data in _pathState.CurrentData)
                    {
                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted($"{data.AssociatedGameObject:X}");
                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted(data.ModCollection.Identity.Name);
                    }
            }
        }

        using (var resourceTree = TreeNode("Subfile Collections"))
        {
            if (resourceTree)
            {
                using var table = Table("###ResourceCollectionResolverTable", 4, ImGuiTableFlags.SizingFixedFit);
                if (table)
                {
                    ImGuiUtil.DrawTableColumn("Current Mtrl Data");
                    ImGuiUtil.DrawTableColumn(_subfileHelper.MtrlData.ModCollection.Identity.Name);
                    ImGuiUtil.DrawTableColumn($"0x{_subfileHelper.MtrlData.AssociatedGameObject:X}");
                    ImGui.TableNextColumn();

                    ImGuiUtil.DrawTableColumn("Current Avfx Data");
                    ImGuiUtil.DrawTableColumn(_subfileHelper.AvfxData.ModCollection.Identity.Name);
                    ImGuiUtil.DrawTableColumn($"0x{_subfileHelper.AvfxData.AssociatedGameObject:X}");
                    ImGui.TableNextColumn();

                    ImGuiUtil.DrawTableColumn("Current Resources");
                    ImGuiUtil.DrawTableColumn(_subfileHelper.Count.ToString());
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();

                    foreach (var (resource, resolve) in _subfileHelper)
                    {
                        ImGuiUtil.DrawTableColumn($"0x{resource:X}");
                        ImGuiUtil.DrawTableColumn(resolve.ModCollection.Identity.Name);
                        ImGuiUtil.DrawTableColumn($"0x{resolve.AssociatedGameObject:X}");
                        ImGuiUtil.DrawTableColumn($"{((ResourceHandle*)resource)->FileName()}");
                    }
                }
            }
        }

        using (var identifiedTree = TreeNode("Identified Collections"))
        {
            if (identifiedTree)
            {
                using var table = Table("##PathCollectionsIdentifiedTable", 4, ImGuiTableFlags.SizingFixedFit);
                if (table)
                    foreach (var (address, identifier, collection) in _identifiedCollectionCache
                                 .OrderBy(kvp => ((GameObject*)kvp.Address)->ObjectIndex))
                    {
                        ImGuiUtil.DrawTableColumn($"{((GameObject*)address)->ObjectIndex}");
                        ImGuiUtil.DrawTableColumn($"0x{address:X}");
                        ImGuiUtil.DrawTableColumn(identifier.ToString());
                        ImGuiUtil.DrawTableColumn(collection.Identity.Name);
                    }
            }
        }

        using (var cutsceneTree = TreeNode("Cutscene Actors"))
        {
            if (cutsceneTree)
            {
                using var table = Table("###PCutsceneResolverTable", 2, ImGuiTableFlags.SizingFixedFit);
                if (table)
                    foreach (var (idx, actor) in _cutsceneService.Actors)
                    {
                        ImGuiUtil.DrawTableColumn($"Cutscene Actor {idx}");
                        ImGuiUtil.DrawTableColumn(actor.Name.ToString());
                    }
            }
        }

        using (var groupTree = TreeNode("Group"))
        {
            if (groupTree)
            {
                using var table = Table("###PGroupTable", 2, ImGuiTableFlags.SizingFixedFit);
                if (table)
                {
                    ImGuiUtil.DrawTableColumn("Group Members");
                    ImGuiUtil.DrawTableColumn(GroupManager.Instance()->MainGroup.MemberCount.ToString());
                    for (var i = 0; i < 8; ++i)
                    {
                        ImGuiUtil.DrawTableColumn($"Member #{i}");
                        var member = GroupManager.Instance()->MainGroup.GetPartyMemberByIndex(i);
                        ImGuiUtil.DrawTableColumn(member == null ? "NULL" : new ByteString(member->Name).ToString());
                    }
                }
            }
        }

        using (var bannerTree = TreeNode("Party Banner"))
        {
            if (bannerTree)
            {
                var agent = &AgentBannerParty.Instance()->AgentBannerInterface;
                if (agent->Data == null)
                    agent = &AgentBannerMIP.Instance()->AgentBannerInterface;

                if (agent->Data != null)
                {
                    using var table = Table("###PBannerTable", 2, ImGuiTableFlags.SizingFixedFit);
                    if (table)
                        for (var i = 0; i < 8; ++i)
                        {
                            ref var c = ref agent->Data->Characters[i];
                            ImGuiUtil.DrawTableColumn($"Character {i}");
                            var name = c.Name1.ToString();
                            ImGuiUtil.DrawTableColumn(name.Length == 0 ? "NULL" : $"{name} ({c.WorldId})");
                        }
                }
                else
                {
                    ImGui.TextUnformatted("INACTIVE");
                }
            }
        }

        using (var tmbCache = TreeNode("TMB Cache"))
        {
            if (tmbCache)
            {
                using var table = Table("###TmbTable", 2, ImGuiTableFlags.SizingFixedFit);
                if (table)
                    foreach (var (id, name) in _schedulerService.ListedTmbs.OrderBy(kvp => kvp.Key))
                    {
                        ImUtf8.DrawTableColumn($"{id:D6}");
                        ImUtf8.DrawTableColumn(name.Span);
                    }
            }
        }
    }

    private void DrawData()
    {
        if (!ImGui.CollapsingHeader("Game Data"))
            return;

        DrawEmotes();
        DrawActionTmbs();
        DrawStainTemplates();
        DrawAtch();
        DrawChangedItemTest();
    }

    private          string                                     _changedItemPath = string.Empty;
    private readonly Dictionary<string, IIdentifiedObjectData> _changedItems    = [];

    private void DrawChangedItemTest()
    {
        using var node = TreeNode("Changed Item Test");
        if (!node)
            return;

        if (ImUtf8.InputText("##ChangedItemTest"u8, ref _changedItemPath, "Changed Item File Path..."u8))
        {
            _changedItems.Clear();
            _objectIdentification.Identify(_changedItems, _changedItemPath);
        }

        if (_changedItems.Count == 0)
            return;

        using var list = ImUtf8.ListBox("##ChangedItemList"u8,
            new Vector2(ImGui.GetContentRegionAvail().X, 8 * ImGui.GetTextLineHeightWithSpacing()));
        if (!list)
            return;

        foreach (var item in _changedItems)
            ImUtf8.Selectable(item.Key);
    }


    private string _emoteSearchFile = string.Empty;
    private string _emoteSearchName = string.Empty;


    private AtchFile? _atchFile;

    private void DrawAtch()
    {
        try
        {
            _atchFile ??= new AtchFile(_dataManager.GetFile("chara/xls/attachOffset/c0101.atch")!.Data);
        }
        catch
        {
            // ignored
        }

        if (_atchFile == null)
            return;

        using var mainTree = ImUtf8.TreeNode("Atch File C0101"u8);
        if (!mainTree)
            return;

        AtchDrawer.Draw(_atchFile);
    }

    private void DrawEmotes()
    {
        using var mainTree = TreeNode("Emotes");
        if (!mainTree)
            return;

        ImGui.InputText("File Name",  ref _emoteSearchFile, 256);
        ImGui.InputText("Emote Name", ref _emoteSearchName, 256);
        using var table = Table("##table", 2, ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingFixedFit,
            new Vector2(-1, 12 * ImGui.GetTextLineHeightWithSpacing()));
        if (!table)
            return;

        var skips = ImGuiClip.GetNecessarySkips(ImGui.GetTextLineHeightWithSpacing());
        var dummy = ImGuiClip.FilteredClippedDraw(_emotes, skips,
            p => p.Key.Contains(_emoteSearchFile, StringComparison.OrdinalIgnoreCase)
             && (_emoteSearchName.Length == 0
                 || p.Value.Any(s => s.Name.ToDalamudString().TextValue.Contains(_emoteSearchName, StringComparison.OrdinalIgnoreCase))),
            p =>
            {
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(p.Key);
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(string.Join(", ", p.Value.Select(v => v.Name.ToDalamudString().TextValue)));
            });
        ImGuiClip.DrawEndDummy(dummy, ImGui.GetTextLineHeightWithSpacing());
    }

    private string       _tmbKeyFilter   = string.Empty;
    private CiByteString _tmbKeyFilterU8 = CiByteString.Empty;

    private void DrawActionTmbs()
    {
        using var mainTree = TreeNode("Action TMBs");
        if (!mainTree)
            return;

        if (ImGui.InputText("Key", ref _tmbKeyFilter, 256))
            _tmbKeyFilterU8 = CiByteString.FromString(_tmbKeyFilter, out var r, MetaDataComputation.All) ? r : CiByteString.Empty;
        using var table = Table("##table", 2, ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingFixedFit,
            new Vector2(-1, 12 * ImGui.GetTextLineHeightWithSpacing()));
        if (!table)
            return;

        var skips = ImGuiClip.GetNecessarySkips(ImGui.GetTextLineHeightWithSpacing());
        var dummy = ImGuiClip.FilteredClippedDraw(_schedulerService.ActionTmbs.OrderBy(r => r.Value), skips,
            kvp => kvp.Key.Contains(_tmbKeyFilterU8),
            p =>
            {
                ImUtf8.DrawTableColumn($"{p.Value}");
                ImUtf8.DrawTableColumn(p.Key.Span);
            });
        ImGuiClip.DrawEndDummy(dummy, ImGui.GetTextLineHeightWithSpacing());
    }

    private void DrawStainTemplates()
    {
        using var mainTree = TreeNode("Staining Templates");
        if (!mainTree)
            return;

        using (var legacyTree = TreeNode("stainingtemplate.stm"))
        {
            if (legacyTree)
                DrawStainTemplatesFile(_stains.LegacyStmFile);
        }

        using (var gudTree = TreeNode("stainingtemplate_gud.stm"))
        {
            if (gudTree)
                DrawStainTemplatesFile(_stains.GudStmFile);
        }
    }

    private static void DrawStainTemplatesFile<TDyePack>(StmFile<TDyePack> stmFile) where TDyePack : unmanaged, IDyePack
    {
        foreach (var (key, data) in stmFile.Entries)
        {
            using var tree = TreeNode($"Template {key}");
            if (!tree)
                continue;

            using var table = Table("##table", data.Colors.Length + data.Scalars.Length,
                ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg);
            if (!table)
                continue;

            for (var i = 0; i < StmFile<TDyePack>.StainingTemplateEntry.NumElements; ++i)
            {
                foreach (var list in data.Colors)
                {
                    var color = list[i];
                    ImGui.TableNextColumn();
                    var frame = new Vector2(ImGui.GetTextLineHeight());
                    ImGui.ColorButton("###color", new Vector4(MtrlTab.PseudoSqrtRgb((Vector3)color), 1), 0, frame);
                    ImGui.SameLine();
                    ImGui.TextUnformatted($"{color.Red:F6} | {color.Green:F6} | {color.Blue:F6}");
                }

                foreach (var list in data.Scalars)
                {
                    var scalar = list[i];
                    ImGuiUtil.DrawTableColumn($"{scalar:F6}");
                }
            }
        }
    }


    private void DrawShaderReplacementFixer()
    {
        if (!ImGui.CollapsingHeader("Shader Replacement Fixer"))
            return;

        var enableShaderReplacementFixer = _shaderReplacementFixer.Enabled;
        if (ImGui.Checkbox("Enable Shader Replacement Fixer", ref enableShaderReplacementFixer))
            _shaderReplacementFixer.Enabled = enableShaderReplacementFixer;

        if (!enableShaderReplacementFixer)
            return;

        using var table = Table("##ShaderReplacementFixer", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit,
            -Vector2.UnitX);
        if (!table)
            return;

        var slowPathCallDeltas = _shaderReplacementFixer.GetAndResetSlowPathCallDeltas();

        ImGui.TableSetupColumn("Shader Package Name",        ImGuiTableColumnFlags.WidthStretch, 0.6f);
        ImGui.TableSetupColumn("Materials with Modded ShPk", ImGuiTableColumnFlags.WidthStretch, 0.2f);
        ImGui.TableSetupColumn("\u0394 Slow-Path Calls",     ImGuiTableColumnFlags.WidthStretch, 0.2f);
        ImGui.TableHeadersRow();

        ImGui.TableNextColumn();
        ImGui.TextUnformatted("characterglass.shpk");
        ImGui.TableNextColumn();
        ImGui.TextUnformatted($"{_shaderReplacementFixer.ModdedCharacterGlassShpkCount}");
        ImGui.TableNextColumn();
        ImGui.TextUnformatted($"{slowPathCallDeltas.CharacterGlass}");

        ImGui.TableNextColumn();
        ImGui.TextUnformatted("characterlegacy.shpk");
        ImGui.TableNextColumn();
        ImGui.TextUnformatted($"{_shaderReplacementFixer.ModdedCharacterLegacyShpkCount}");
        ImGui.TableNextColumn();
        ImGui.TextUnformatted($"{slowPathCallDeltas.CharacterLegacy}");

        ImGui.TableNextColumn();
        ImGui.TextUnformatted("characterocclusion.shpk");
        ImGui.TableNextColumn();
        ImGui.TextUnformatted($"{_shaderReplacementFixer.ModdedCharacterOcclusionShpkCount}");
        ImGui.TableNextColumn();
        ImGui.TextUnformatted($"{slowPathCallDeltas.CharacterOcclusion}");

        ImGui.TableNextColumn();
        ImGui.TextUnformatted("characterstockings.shpk");
        ImGui.TableNextColumn();
        ImGui.TextUnformatted($"{_shaderReplacementFixer.ModdedCharacterStockingsShpkCount}");
        ImGui.TableNextColumn();
        ImGui.TextUnformatted($"{slowPathCallDeltas.CharacterStockings}");

        ImGui.TableNextColumn();
        ImGui.TextUnformatted("charactertattoo.shpk");
        ImGui.TableNextColumn();
        ImGui.TextUnformatted($"{_shaderReplacementFixer.ModdedCharacterTattooShpkCount}");
        ImGui.TableNextColumn();
        ImGui.TextUnformatted($"{slowPathCallDeltas.CharacterTattoo}");

        ImGui.TableNextColumn();
        ImGui.TextUnformatted("charactertransparency.shpk");
        ImGui.TableNextColumn();
        ImGui.TextUnformatted($"{_shaderReplacementFixer.ModdedCharacterTransparencyShpkCount}");
        ImGui.TableNextColumn();
        ImGui.TextUnformatted($"{slowPathCallDeltas.CharacterTransparency}");

        ImGui.TableNextColumn();
        ImGui.TextUnformatted("hairmask.shpk");
        ImGui.TableNextColumn();
        ImGui.TextUnformatted($"{_shaderReplacementFixer.ModdedHairMaskShpkCount}");
        ImGui.TableNextColumn();
        ImGui.TextUnformatted($"{slowPathCallDeltas.HairMask}");

        ImGui.TableNextColumn();
        ImGui.TextUnformatted("iris.shpk");
        ImGui.TableNextColumn();
        ImGui.TextUnformatted($"{_shaderReplacementFixer.ModdedIrisShpkCount}");
        ImGui.TableNextColumn();
        ImGui.TextUnformatted($"{slowPathCallDeltas.Iris}");

        ImGui.TableNextColumn();
        ImGui.TextUnformatted("skin.shpk");
        ImGui.TableNextColumn();
        ImGui.TextUnformatted($"{_shaderReplacementFixer.ModdedSkinShpkCount}");
        ImGui.TableNextColumn();
        ImGui.TextUnformatted($"{slowPathCallDeltas.Skin}");
    }

    /// <summary> Draw information about the models, materials and resources currently loaded by the local player. </summary>
    private unsafe void DrawPlayerModelInfo()
    {
        var player = _clientState.LocalPlayer;
        var name   = player?.Name.ToString() ?? "NULL";
        if (!ImGui.CollapsingHeader($"Player Model Info: {name}##Draw") || player == null)
            return;

        DrawCopyableAddress("PlayerCharacter"u8, player.Address);

        var model = (CharacterBase*)((Character*)player.Address)->GameObject.GetDrawObject();
        if (model == null)
            return;

        DrawCopyableAddress("CharacterBase"u8, model);

        using (var t1 = Table("##table", 2, ImGuiTableFlags.SizingFixedFit))
        {
            if (t1)
            {
                ImGuiUtil.DrawTableColumn("Flags");
                ImGuiUtil.DrawTableColumn($"{model->UnkFlags_01:X2}");
                ImGuiUtil.DrawTableColumn("Has Model In Slot Loaded");
                ImGuiUtil.DrawTableColumn($"{model->HasModelInSlotLoaded:X8}");
                ImGuiUtil.DrawTableColumn("Has Model Files In Slot Loaded");
                ImGuiUtil.DrawTableColumn($"{model->HasModelFilesInSlotLoaded:X8}");
            }
        }

        using var table = Table($"##{name}DrawTable", 5, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit);
        if (!table)
            return;

        ImGui.TableNextColumn();
        ImGui.TableHeader("Slot");
        ImGui.TableNextColumn();
        ImGui.TableHeader("Imc Ptr");
        ImGui.TableNextColumn();
        ImGui.TableHeader("Imc File");
        ImGui.TableNextColumn();
        ImGui.TableHeader("Model Ptr");
        ImGui.TableNextColumn();
        ImGui.TableHeader("Model File");

        for (var i = 0; i < model->SlotCount; ++i)
        {
            var imc = (ResourceHandle*)model->IMCArray[i];
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"Slot {i}");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(imc == null ? "NULL" : $"0x{(ulong)imc:X}");
            ImGui.TableNextColumn();
            if (imc != null)
                UiHelpers.Text(imc);

            var mdl = (RenderModel*)model->Models[i];
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(mdl == null ? "NULL" : $"0x{(ulong)mdl:X}");
            if (mdl == null || mdl->ResourceHandle == null || mdl->ResourceHandle->Category != ResourceCategory.Chara)
                continue;

            ImGui.TableNextColumn();
            {
                UiHelpers.Text(mdl->ResourceHandle);
            }
        }
    }

    private string   _crcInput = string.Empty;
    private FullPath _crcPath  = FullPath.Empty;

    private unsafe void DrawCrcCache()
    {
        var header = ImUtf8.CollapsingHeader("CRC Cache"u8);
        if (!header)
            return;

        if (ImUtf8.InputText("##crcInput"u8, ref _crcInput, "Input path for CRC..."u8))
            _crcPath = new FullPath(_crcInput);

        using var font = ImRaii.PushFont(UiBuilder.MonoFont);
        ImUtf8.Text($"   CRC32: {_crcPath.InternalName.CiCrc32:X8}");
        ImUtf8.Text($"CI CRC32: {_crcPath.InternalName.Crc32:X8}");
        ImUtf8.Text($"   CRC64: {_crcPath.Crc64:X16}");

        using var table = ImUtf8.Table("table"u8, 2);
        if (!table)
            return;

        ImUtf8.TableSetupColumn("Hash"u8, ImGuiTableColumnFlags.WidthFixed, 18 * UiBuilder.MonoFont.GetCharAdvance('0'));
        ImUtf8.TableSetupColumn("Type"u8, ImGuiTableColumnFlags.WidthFixed, 5 * UiBuilder.MonoFont.GetCharAdvance('0'));
        ImGui.TableHeadersRow();

        foreach (var (hash, type) in _rsfService.CustomCache)
        {
            ImGui.TableNextColumn();
            ImUtf8.Text($"{hash:X16}");
            ImGui.TableNextColumn();
            ImUtf8.Text($"{type}");
        }
    }

    private unsafe void DrawResourceLoader()
    {
        if (!ImUtf8.CollapsingHeader("Resource Loader"u8))
            return;

        var ongoingLoads     = _resourceLoader.OngoingLoads;
        var ongoingLoadCount = ongoingLoads.Count;
        ImUtf8.Text($"Ongoing Loads: {ongoingLoadCount}");

        if (ongoingLoadCount == 0)
            return;

        using var table = ImUtf8.Table("ongoingLoadTable"u8, 3);
        if (!table)
            return;

        ImUtf8.TableSetupColumn("Resource Handle"u8, ImGuiTableColumnFlags.WidthStretch, 0.2f);
        ImUtf8.TableSetupColumn("Actual Path"u8,     ImGuiTableColumnFlags.WidthStretch, 0.4f);
        ImUtf8.TableSetupColumn("Original Path"u8,   ImGuiTableColumnFlags.WidthStretch, 0.4f);
        ImGui.TableHeadersRow();

        foreach (var (handle, original) in ongoingLoads)
        {
            ImUtf8.DrawTableColumn($"0x{handle:X}");
            ImUtf8.DrawTableColumn(((ResourceHandle*)handle)->CsHandle.FileName);
            ImUtf8.DrawTableColumn(original.Path.Span);
        }
    }

    /// <summary> Draw resources with unusual reference count. </summary>
    private unsafe void DrawResourceProblems()
    {
        var header = ImGui.CollapsingHeader("Resource Problems");
        ImGuiUtil.HoverTooltip("Draw resources with unusually high reference count to detect overflows.");
        if (!header)
            return;

        using var table = Table("##ProblemsTable", 6, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit);
        if (!table)
            return;

        _resourceManager.IterateResources((_, r) =>
        {
            if (r->RefCount < 10000)
                return;

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(((ResourceCategory)r->Type.Value).ToString());
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(r->FileType.ToString("X"));
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(r->Id.ToString("X"));
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(((ulong)r).ToString("X"));
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(r->RefCount.ToString());
            ImGui.TableNextColumn();
            ref var name = ref r->FileName;
            if (name.Capacity > 15)
                UiHelpers.Text(name.BufferPtr, (int)name.Length);
            else
                fixed (byte* ptr = name.Buffer)
                {
                    UiHelpers.Text(ptr, (int)name.Length);
                }
        });
    }


    /// <summary> Draw information about IPC options and availability. </summary>
    private void DrawDebugTabIpc()
    {
        if (ImGui.CollapsingHeader("IPC"))
            _ipcTester.Draw();
    }

    /// <summary> Helper to print a property and its value in a 2-column table. </summary>
    private static void PrintValue(string name, string value)
    {
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(name);
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(value);
    }

    public override void Draw()
        => DrawContent();

    public override bool DrawConditions()
        => _config.DebugMode && _config.Ephemeral.DebugSeparateWindow;

    public override void OnClose()
    {
        _config.Ephemeral.DebugSeparateWindow = false;
        _config.Ephemeral.Save();
    }

    public static unsafe void DrawCopyableAddress(ReadOnlySpan<byte> label, void* address)
    {
        using (var _ = ImRaii.PushFont(UiBuilder.MonoFont))
        {
            if (ImUtf8.Selectable($"0x{(nint)address:X16}    {label}"))
                ImUtf8.SetClipboardText($"0x{(nint)address:X16}");
        }

        ImUtf8.HoverTooltip("Click to copy address to clipboard."u8);
    }

    public static unsafe void DrawCopyableAddress(ReadOnlySpan<byte> label, nint address)
        => DrawCopyableAddress(label, (void*)address);
}

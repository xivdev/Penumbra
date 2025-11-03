using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.System.Resource;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using ImSharp;
using Luna;
using Microsoft.Extensions.DependencyInjection;
using OtterGui;
using OtterGui.Text;
using Penumbra.Api;
using Penumbra.Api.Enums;
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
using CharacterBase = FFXIVClientStructs.FFXIV.Client.Graphics.Scene.CharacterBase;
using ImGuiClip = OtterGui.ImGuiClip;
using Penumbra.Api.IpcTester;
using Penumbra.GameData.Data;
using Penumbra.Interop.Hooks.PostProcessing;
using Penumbra.Interop.Hooks.ResourceLoading;
using Penumbra.GameData.Files.StainMapStructs;
using Penumbra.Interop;
using Penumbra.String.Classes;
using Penumbra.UI.AdvancedWindow.Materials;

namespace Penumbra.UI.Tabs.Debug;

public class Diagnostics(ServiceManager provider) : IUiService
{
    public void DrawDiagnostics()
    {
        if (!ImGui.CollapsingHeader("Diagnostics"))
            return;

        using var table = Im.Table.Begin("##data"u8, 4, TableFlags.RowBackground);
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

public sealed class DebugTab : Window, ITab<TabType>
{
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
    private readonly ModMigratorDebug                   _modMigratorDebug;
    private readonly ShapeInspector                     _shapeInspector;
    private readonly FileWatcher.FileWatcherDrawer      _fileWatcherDrawer;

    public DebugTab(Configuration config, CollectionManager collectionManager, ObjectManager objects,
        IClientState clientState, IDataManager dataManager,
        ValidityChecker validityChecker, ModManager modManager, HttpApi httpApi, ActorManager actors, StainService stains,
        ResourceManagerService resourceManager, ResourceLoader resourceLoader, CollectionResolver collectionResolver,
        DrawObjectState drawObjectState, PathState pathState, SubfileHelper subfileHelper, IdentifiedCollectionCache identifiedCollectionCache,
        CutsceneService cutsceneService, ModImportManager modImporter, ImportPopup importPopup, FrameworkManager framework,
        TextureManager textureManager, ShaderReplacementFixer shaderReplacementFixer, RedrawService redraws, DictEmote emotes,
        Diagnostics diagnostics, IpcTester ipcTester, CrashHandlerPanel crashHandlerPanel, TexHeaderDrawer texHeaderDrawer,
        HookOverrideDrawer hookOverrides, RsfService rsfService, GlobalVariablesDrawer globalVariablesDrawer,
        SchedulerResourceManagementService schedulerService, ObjectIdentification objectIdentification, RenderTargetDrawer renderTargetDrawer,
        ModMigratorDebug modMigratorDebug, ShapeInspector shapeInspector, FileWatcher.FileWatcherDrawer fileWatcherDrawer)
        : base("Penumbra Debug Window", WindowFlags.NoCollapse)
    {
        IsOpen = true;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(200,  200),
            MaximumSize = new Vector2(2000, 2000),
        };
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
        _modMigratorDebug          = modMigratorDebug;
        _shapeInspector            = shapeInspector;
        _fileWatcherDrawer    = fileWatcherDrawer;
        _objects                   = objects;
        _clientState               = clientState;
        _dataManager               = dataManager;
    }

    public ReadOnlySpan<byte> Label
        => "Debug"u8;

    public bool IsVisible
        => _config is { DebugMode: true, Ephemeral.DebugSeparateWindow: false };

    public TabType Identifier
        => TabType.Debug;

#if DEBUG
    private const string DebugVersionString = "(Debug)";
#else
    private const string DebugVersionString = "(Release)";
#endif

    public void DrawContent()
    {
        using var child = Im.Child.Begin("##DebugTab"u8, -Vector2.One);
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
        _modMigratorDebug.Draw();
        DrawShaderReplacementFixer();
        DrawData();
        DrawCrcCache();
        DrawResourceLoader();
        DrawResourceProblems();
        _renderTargetDrawer.Draw();
        _hookOverrides.Draw();
        DrawPlayerModelInfo();
        _globalVariablesDrawer.Draw();
        DrawCloudApi();
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
                using var color = ImGuiColor.Text.Push(ColorId.FolderExpanded.Value());
                using var node =
                    Im.Tree.Node($"{collection.Identity.Name} (Change Counter {collection.Counters.Change})###{collection.Identity.Name}");
                if (!node)
                    continue;

                color.Pop();
                using (var inheritanceNode = ImUtf8.TreeNode("Inheritance"u8))
                {
                    if (inheritanceNode)
                    {
                        using var table = Im.Table.Begin("table"u8, 3,
                            TableFlags.SizingFixedFit | TableFlags.RowBackground | TableFlags.BordersInnerVertical);
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
                                    ImGui.Dummy(new Vector2(200 * Im.Style.GlobalScale, Im.Style.TextHeight));
                                ImGui.TableNextColumn();
                                if (i < collection.Inheritance.DirectlyInheritedBy.Count)
                                    ImUtf8.Text(collection.Inheritance.DirectlyInheritedBy[i].Identity.Name);
                                else
                                    ImGui.Dummy(new Vector2(200 * Im.Style.GlobalScale, Im.Style.TextHeight));
                                ImGui.TableNextColumn();
                                if (i < collection.Inheritance.FlatHierarchy.Count)
                                    ImUtf8.Text(collection.Inheritance.FlatHierarchy[i].Identity.Name);
                                else
                                    ImGui.Dummy(new Vector2(200 * Im.Style.GlobalScale, Im.Style.TextHeight));
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
                        using var id    = mod is TemporaryMod t ? Im.Id.Push(t.Priority.Value) : Im.Id.Push(((Mod)mod).ModPath.Name);
                        using var node2 = Im.Tree.Node(mod.Name);
                        if (!node2)
                            continue;

                        foreach (var path in paths)

                            Im.Tree.Node(path.Path.Span, TreeNodeFlags.Bullet | TreeNodeFlags.Leaf).Dispose();

                        foreach (var manip in manips)
                            Im.Tree.Node($"{manip}", TreeNodeFlags.Bullet | TreeNodeFlags.Leaf).Dispose();
                    }
            }
            else
            {
                using var color = ImGuiColor.Text.Push(ColorId.UndefinedMod.Value());
                Im.Tree.Node($"{collection.Identity.Name} (Change Counter {collection.Counters.Change})",
                    TreeNodeFlags.Bullet | TreeNodeFlags.Leaf).Dispose();
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

        using (var table = Im.Table.Begin("##DebugGeneralTable"u8, 2, TableFlags.SizingFixedFit))
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


        var issues = _modManager.Index().Count(p => p.Index != p.Item.Index);
        using (var tree = Im.Tree.Node($"Mods ({issues} Issues)###Mods"))
        {
            if (tree)
            {
                using var table = Im.Table.Begin("##DebugModsTable"u8, 3, TableFlags.SizingFixedFit);
                if (table)
                {
                    var lastIndex = -1;
                    foreach (var mod in _modManager)
                    {
                        PrintValue(mod.Name, mod.Index.ToString("D5"));
                        ImGui.TableNextColumn();
                        var index = mod.Index;
                        if (index != lastIndex + 1)
                            Im.Text("!!!"u8);
                        lastIndex = index;
                    }
                }
            }
        }

        using (var tree = Im.Tree.Node("Mod Import"u8))
        {
            if (tree)
            {
                using var table = Im.Table.Begin("##DebugModImport"u8, 2, TableFlags.SizingFixedFit);
                if (table)
                {
                    var importing = _modImporter.IsImporting(out var importer);
                    PrintValue("Is Importing",            importing.ToString());
                    PrintValue("Importer State",          (importer?.State ?? ImporterState.None).ToString());
                    PrintValue("Import Window Was Drawn", _importPopup.WasDrawn.ToString());
                    PrintValue("Import Popup Was Drawn",  _importPopup.PopupWasDrawn.ToString());
                    ImGui.TableNextColumn();
                    Im.Text("Import Batches"u8);
                    ImGui.TableNextColumn();
                    foreach (var (index, batch) in _modImporter.ModBatches.Index())
                    {
                        foreach (var mod in batch)
                            PrintValue(index.ToString(), mod);
                    }

                    ImGui.TableNextColumn();
                    Im.Text("Addable Mods"u8);
                    ImGui.TableNextColumn();
                    foreach (var mod in _modImporter.AddableMods)
                    {
                        ImGui.TableNextColumn();
                        ImGui.TableNextColumn();
                        Im.Text(mod.Name);
                    }
                }
            }
        }

        using (var tree = Im.Tree.Node("Framework"u8))
        {
            if (tree)
            {
                using var table = Im.Table.Begin("##DebugFramework"u8, 2, TableFlags.SizingFixedFit);
                if (table)
                {
                    foreach (var important in _framework.Important)
                        PrintValue(important, "Immediate");

                    foreach (var (idx, onTick) in _framework.OnTick.Index())
                        PrintValue(onTick, $"{idx + 1} Tick(s) From Now");

                    foreach (var (time, name) in _framework.Delayed)
                    {
                        var span = time - DateTime.UtcNow;
                        PrintValue(name, $"After {span.Minutes:D2}:{span.Seconds:D2}.{span.Milliseconds / 10:D2} (+ Ticks)");
                    }
                }
            }
        }

        using (var tree = Im.Tree.Node($"Texture Manager {_textureManager.Tasks.Count}###Texture Manager"))
        {
            if (tree)
            {
                using var table = Im.Table.Begin("##Tasks"u8, 2, TableFlags.RowBackground);
                if (table)
                    foreach (var task in _textureManager.Tasks)
                    {
                        ImGuiUtil.DrawTableColumn(task.Key.ToString()!);
                        ImGuiUtil.DrawTableColumn(task.Value.Item1.Status.ToString());
                    }
            }
        }

        using (var tree = Im.Tree.Node("Redraw Service"u8))
        {
            if (tree)
            {
                using var table = Im.Table.Begin("##redraws"u8, 3, TableFlags.RowBackground);
                if (table)
                {
                    ImGuiUtil.DrawTableColumn("In GPose");
                    ImGuiUtil.DrawTableColumn(_redraws.InGPose.ToString());
                    ImGui.TableNextColumn();

                    ImGuiUtil.DrawTableColumn("Target");
                    ImGuiUtil.DrawTableColumn(_redraws.Target.ToString());
                    ImGui.TableNextColumn();

                    foreach (var (idx, objectIdx) in _redraws.Queue.Index())
                    {
                        var (actualIdx, state) = objectIdx < 0 ? (~objectIdx, "Queued") : (objectIdx, "Invisible");
                        ImGuiUtil.DrawTableColumn($"Redraw Queue #{idx}");
                        ImGuiUtil.DrawTableColumn(actualIdx.ToString());
                        ImGuiUtil.DrawTableColumn(state);
                    }

                    foreach (var (idx, objectIdx) in _redraws.AfterGPoseQueue.Index())
                    {
                        var (actualIdx, state) = objectIdx < 0 ? (~objectIdx, "Queued") : (objectIdx, "Invisible");
                        ImGuiUtil.DrawTableColumn($"GPose Queue #{idx}");
                        ImGuiUtil.DrawTableColumn(actualIdx.ToString());
                        ImGuiUtil.DrawTableColumn(state);
                    }

                    foreach (var (idx, name) in _redraws.GPoseNames.OfType<string>().Index())
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

                Im.Line.Same();
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

        _fileWatcherDrawer.Draw();
    }

    private void DrawPerformanceTab()
    {
        if (!ImGui.CollapsingHeader("Performance"))
            return;

        using (var start = Im.Tree.Node("Startup Performance"u8, TreeNodeFlags.DefaultOpen))
        {
            if (start)
                Im.Line.New();
        }
    }

    private unsafe void DrawActorsDebug()
    {
        if (!ImGui.CollapsingHeader("Actors"))
            return;

        using (var objectTree = ImUtf8.TreeNode("Object Manager"u8))
        {
            if (objectTree)
            {
                _objects.DrawDebug();

                using var table = Im.Table.Begin("##actors"u8, 8, TableFlags.RowBackground | TableFlags.SizingFixedFit,
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
                    Penumbra.Dynamis.DrawPointer(obj.Address);
                    ImGui.TableNextColumn();
                    if (obj.Address != nint.Zero)
                        Penumbra.Dynamis.DrawPointer((nint)((Character*)obj.Address)->GameObject.GetDrawObject());
                    var identifier = _actors.FromObject(obj, out _, false, true, false);
                    ImGuiUtil.DrawTableColumn(_actors.ToString(identifier));
                    var id = obj.AsObject->ObjectKind is ObjectKind.BattleNpc
                        ? $"{identifier.DataId} | {obj.AsObject->BaseId}"
                        : identifier.DataId.ToString();
                    ImGuiUtil.DrawTableColumn(id);
                    ImGui.TableNextColumn();
                    Penumbra.Dynamis.DrawPointer(obj.Address != nint.Zero ? *(nint*)obj.Address : nint.Zero);
                    ImGuiUtil.DrawTableColumn(obj.Address != nint.Zero ? $"0x{obj.AsObject->EntityId:X}" : "NULL");
                    ImGuiUtil.DrawTableColumn(obj.Address != nint.Zero
                        ? obj.AsObject->IsCharacter() ? $"Character: {obj.AsCharacter->ObjectKind}" : "No Character"
                        : "NULL");
                }
            }
        }

        using (var shapeTree = ImUtf8.TreeNode("Shape Inspector"u8))
        {
            if (shapeTree)
                _shapeInspector.Draw();
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

        Im.Text(
            $"Last Game Object: 0x{_collectionResolver.IdentifyLastGameObjectCollection(true).AssociatedGameObject:X} ({_collectionResolver.IdentifyLastGameObjectCollection(true).ModCollection.Identity.Name})");
        using (var drawTree = Im.Tree.Node("Draw Object to Object"u8))
        {
            if (drawTree)
            {
                using var table = Im.Table.Begin("###DrawObjectResolverTable"u8, 8, TableFlags.SizingFixedFit);
                if (table)
                    foreach (var (drawObject, (gameObjectPtr, idx, child)) in _drawObjectState
                                 .OrderBy(kvp => kvp.Value.Item2.Index)
                                 .ThenBy(kvp => kvp.Value.Item3)
                                 .ThenBy(kvp => kvp.Key.Address))
                    {
                        ImGui.TableNextColumn();
                        ImUtf8.CopyOnClickSelectable($"{drawObject}");
                        ImUtf8.DrawTableColumn($"{gameObjectPtr.Index}");
                        using (ImGuiColor.Text.Push(new Vector4(1, 0, 0, 1), gameObjectPtr.Index != idx))
                        {
                            ImUtf8.DrawTableColumn($"{idx}");
                        }

                        ImUtf8.DrawTableColumn(child ? "Child"u8 : "Main"u8);
                        ImGui.TableNextColumn();
                        ImUtf8.CopyOnClickSelectable($"{gameObjectPtr}");
                        using (ImGuiColor.Text.Push(new Vector4(1, 0, 0, 1), _objects[idx] != gameObjectPtr))
                        {
                            ImUtf8.DrawTableColumn($"{_objects[idx]}");
                        }

                        ImUtf8.DrawTableColumn(gameObjectPtr.Utf8Name.Span);
                        var collection = _collectionResolver.IdentifyCollection(gameObjectPtr.AsObject, true);
                        ImUtf8.DrawTableColumn(collection.ModCollection.Identity.Name);
                    }
            }
        }

        using (var pathTree = Im.Tree.Node("Path Collections"u8))
        {
            if (pathTree)
            {
                using var table = Im.Table.Begin("###PathCollectionResolverTable"u8, 2, TableFlags.SizingFixedFit);
                if (table)
                    foreach (var data in _pathState.CurrentData)
                    {
                        ImGui.TableNextColumn();
                        Im.Text($"{data.AssociatedGameObject:X}");
                        ImGui.TableNextColumn();
                        Im.Text(data.ModCollection.Identity.Name);
                    }
            }
        }

        using (var resourceTree = Im.Tree.Node("Subfile Collections"u8))
        {
            if (resourceTree)
            {
                using var table = Im.Table.Begin("###ResourceCollectionResolverTable"u8, 4, TableFlags.SizingFixedFit);
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

        using (var identifiedTree = Im.Tree.Node("Identified Collections"u8))
        {
            if (identifiedTree)
            {
                using var table = Im.Table.Begin("##PathCollectionsIdentifiedTable"u8, 4, TableFlags.SizingFixedFit);
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

        using (var cutsceneTree = Im.Tree.Node("Cutscene Actors"u8))
        {
            if (cutsceneTree)
            {
                using var table = Im.Table.Begin("###PCutsceneResolverTable"u8, 2, TableFlags.SizingFixedFit);
                if (table)
                    foreach (var (idx, actor) in _cutsceneService.Actors)
                    {
                        ImGuiUtil.DrawTableColumn($"Cutscene Actor {idx}");
                        ImGuiUtil.DrawTableColumn(actor.Name.ToString());
                    }
            }
        }

        using (var groupTree = Im.Tree.Node("Group"u8))
        {
            if (groupTree)
            {
                using var table = Im.Table.Begin("###PGroupTable"u8, 2, TableFlags.SizingFixedFit);
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

        using (var bannerTree = Im.Tree.Node("Party Banner"u8))
        {
            if (bannerTree)
            {
                var agent = &AgentBannerParty.Instance()->AgentBannerInterface;
                if (agent->Data == null)
                    agent = &AgentBannerMIP.Instance()->AgentBannerInterface;

                ImUtf8.Text("Agent: ");
                Im.Line.Same(0, 0);
                Penumbra.Dynamis.DrawPointer((nint)agent);
                if (agent->Data != null)
                {
                    using var table = Im.Table.Begin("###PBannerTable"u8, 2, TableFlags.SizingFixedFit);
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
                    Im.Text("INACTIVE"u8);
                }
            }
        }

        using (var tmbCache = Im.Tree.Node("TMB Cache"u8))
        {
            if (tmbCache)
            {
                using var table = Im.Table.Begin("###TmbTable"u8, 2, TableFlags.SizingFixedFit);
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
        DrawFileTest();
        DrawChangedItemTest();
    }

    private string  _filePath = string.Empty;
    private byte[]? _fileData;

    private void DrawFileTest()
    {
        using var node = Im.Tree.Node("Game File Test"u8);
        if (!node)
            return;

        if (Im.Input.Text("##Path"u8, ref _filePath, "File Path..."u8))
            _fileData = _dataManager.GetFile(_filePath)?.Data;

        using (Im.Group())
        {
            Im.Text("Exists"u8);
            Im.Text("File Size"u8);
        }

        Im.Line.SameInner();
        using (Im.Group())
        {
            Im.Text($"{_fileData is not null}");
            Im.Text($"{_fileData?.Length ?? 0}");
        }
    }

    private          string                                    _changedItemPath = string.Empty;
    private readonly Dictionary<string, IIdentifiedObjectData> _changedItems    = [];

    private void DrawChangedItemTest()
    {
        using var node = Im.Tree.Node("Changed Item Test"u8);
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
            new Vector2(Im.ContentRegion.Available.X, 8 * Im.Style.TextHeightWithSpacing));
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
        using var mainTree = Im.Tree.Node("Emotes"u8);
        if (!mainTree)
            return;

        ImGui.InputText("File Name",  ref _emoteSearchFile, 256);
        ImGui.InputText("Emote Name", ref _emoteSearchName, 256);
        using var table = Im.Table.Begin("##table"u8, 2, TableFlags.RowBackground | TableFlags.ScrollY | TableFlags.SizingFixedFit,
            new Vector2(-1, 12 * Im.Style.TextHeightWithSpacing));
        if (!table)
            return;

        var skips = ImGuiClip.GetNecessarySkips(Im.Style.TextHeightWithSpacing);
        var dummy = ImGuiClip.FilteredClippedDraw(_emotes, skips,
            p => p.Key.Contains(_emoteSearchFile, StringComparison.OrdinalIgnoreCase)
             && (_emoteSearchName.Length == 0
                 || p.Value.Any(s => s.Name.ToDalamudString().TextValue.Contains(_emoteSearchName, StringComparison.OrdinalIgnoreCase))),
            p =>
            {
                ImGui.TableNextColumn();
                Im.Text(p.Key);
                ImGui.TableNextColumn();
                Im.Text(StringU8.Join(", "u8, p.Value.Select(v => v.Name.ToDalamudString().TextValue)));
            });
        ImGuiClip.DrawEndDummy(dummy, Im.Style.TextHeightWithSpacing);
    }

    private string       _tmbKeyFilter   = string.Empty;
    private CiByteString _tmbKeyFilterU8 = CiByteString.Empty;

    private void DrawActionTmbs()
    {
        using var mainTree = Im.Tree.Node("Action TMBs"u8);
        if (!mainTree)
            return;

        if (ImGui.InputText("Key", ref _tmbKeyFilter, 256))
            _tmbKeyFilterU8 = CiByteString.FromString(_tmbKeyFilter, out var r, MetaDataComputation.All) ? r : CiByteString.Empty;
        using var table = Im.Table.Begin("##table"u8, 2, TableFlags.RowBackground | TableFlags.ScrollY | TableFlags.SizingFixedFit,
            new Vector2(-1, 12 * Im.Style.TextHeightWithSpacing));
        if (!table)
            return;

        var skips = ImGuiClip.GetNecessarySkips(Im.Style.TextHeightWithSpacing);
        var dummy = ImGuiClip.FilteredClippedDraw(_schedulerService.ActionTmbs.OrderBy(r => r.Value), skips,
            kvp => kvp.Key.Contains(_tmbKeyFilterU8),
            p =>
            {
                ImUtf8.DrawTableColumn($"{p.Value}");
                ImUtf8.DrawTableColumn(p.Key.Span);
            });
        ImGuiClip.DrawEndDummy(dummy, Im.Style.TextHeightWithSpacing);
    }

    private void DrawStainTemplates()
    {
        using var mainTree = Im.Tree.Node("Staining Templates"u8);
        if (!mainTree)
            return;

        using (var legacyTree = Im.Tree.Node("stainingtemplate.stm"u8))
        {
            if (legacyTree)
                DrawStainTemplatesFile(_stains.LegacyStmFile);
        }

        using (var gudTree = Im.Tree.Node("stainingtemplate_gud.stm"u8))
        {
            if (gudTree)
                DrawStainTemplatesFile(_stains.GudStmFile);
        }
    }

    private static void DrawStainTemplatesFile<TDyePack>(StmFile<TDyePack> stmFile) where TDyePack : unmanaged, IDyePack
    {
        foreach (var (key, data) in stmFile.Entries)
        {
            using var tree = Im.Tree.Node($"Template {key}");
            if (!tree)
                continue;

            using var table = Im.Table.Begin("##table"u8, data.Colors.Length + data.Scalars.Length,
                TableFlags.SizingFixedFit | TableFlags.RowBackground);
            if (!table)
                continue;

            for (var i = 0; i < StmFile<TDyePack>.StainingTemplateEntry.NumElements; ++i)
            {
                foreach (var list in data.Colors)
                {
                    var color = list[i];
                    ImGui.TableNextColumn();
                    var frame = new Vector2(Im.Style.TextHeight);
                    ImGui.ColorButton("###color", new Vector4(MtrlTab.PseudoSqrtRgb((Vector3)color), 1), 0, frame);
                    Im.Line.Same();
                    Im.Text($"{color.Red:F6} | {color.Green:F6} | {color.Blue:F6}");
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

        using var table = Im.Table.Begin("##ShaderReplacementFixer"u8, 3, TableFlags.RowBackground | TableFlags.SizingFixedFit,
            -Vector2.UnitX);
        if (!table)
            return;

        var slowPathCallDeltas = _shaderReplacementFixer.GetAndResetSlowPathCallDeltas();

        table.SetupColumn("Shader Package Name"u8,        TableColumnFlags.WidthStretch, 0.6f);
        table.SetupColumn("Materials with Modded ShPk"u8, TableColumnFlags.WidthStretch, 0.2f);
        table.SetupColumn("\u0394 Slow-Path Calls"u8,     TableColumnFlags.WidthStretch, 0.2f);
        ImGui.TableHeadersRow();

        table.DrawColumn("characterglass.shpk"u8);
        table.DrawColumn($"{_shaderReplacementFixer.ModdedCharacterGlassShpkCount}");
        table.DrawColumn($"{slowPathCallDeltas.CharacterGlass}");

        table.DrawColumn("characterlegacy.shpk"u8);
        table.DrawColumn($"{_shaderReplacementFixer.ModdedCharacterLegacyShpkCount}");
        table.DrawColumn($"{slowPathCallDeltas.CharacterLegacy}");

        table.DrawColumn("characterocclusion.shpk"u8);
        table.DrawColumn($"{_shaderReplacementFixer.ModdedCharacterOcclusionShpkCount}");
        table.DrawColumn($"{slowPathCallDeltas.CharacterOcclusion}");

        table.DrawColumn("characterstockings.shpk"u8);
        table.DrawColumn($"{_shaderReplacementFixer.ModdedCharacterStockingsShpkCount}");
        table.DrawColumn($"{slowPathCallDeltas.CharacterStockings}");

        table.DrawColumn("charactertattoo.shpk"u8);
        table.DrawColumn($"{_shaderReplacementFixer.ModdedCharacterTattooShpkCount}");
        table.DrawColumn($"{slowPathCallDeltas.CharacterTattoo}");

        table.DrawColumn("charactertransparency.shpk"u8);
        table.DrawColumn($"{_shaderReplacementFixer.ModdedCharacterTransparencyShpkCount}");
        table.DrawColumn($"{slowPathCallDeltas.CharacterTransparency}");

        table.DrawColumn("hairmask.shpk"u8);
        table.DrawColumn($"{_shaderReplacementFixer.ModdedHairMaskShpkCount}");
        table.DrawColumn($"{slowPathCallDeltas.HairMask}");

        table.DrawColumn("iris.shpk"u8);
        table.DrawColumn($"{_shaderReplacementFixer.ModdedIrisShpkCount}");
        table.DrawColumn($"{slowPathCallDeltas.Iris}");

        table.DrawColumn("skin.shpk"u8);
        table.DrawColumn($"{_shaderReplacementFixer.ModdedSkinShpkCount}");
        table.DrawColumn($"{slowPathCallDeltas.Skin}");
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

        using (var t1 = Im.Table.Begin("##table"u8, 2, TableFlags.SizingFixedFit))
        {
            if (t1)
            {
                ImGuiUtil.DrawTableColumn("Flags");
                ImGuiUtil.DrawTableColumn($"{model->StateFlags}");
                ImGuiUtil.DrawTableColumn("Has Model In Slot Loaded");
                ImGuiUtil.DrawTableColumn($"{model->HasModelInSlotLoaded:X8}");
                ImGuiUtil.DrawTableColumn("Has Model Files In Slot Loaded");
                ImGuiUtil.DrawTableColumn($"{model->HasModelFilesInSlotLoaded:X8}");
            }
        }

        using var table = Im.Table.Begin($"##{name}DrawTable", 5, TableFlags.RowBackground | TableFlags.SizingFixedFit);
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
            Im.Text($"Slot {i}");
            ImGui.TableNextColumn();
            Penumbra.Dynamis.DrawPointer((nint)imc);
            ImGui.TableNextColumn();
            if (imc is not null)
                Im.Text(imc->FileName().Span);

            var mdl = (RenderModel*)model->Models[i];
            ImGui.TableNextColumn();
            Penumbra.Dynamis.DrawPointer((nint)mdl);
            if (mdl == null || mdl->ResourceHandle == null || mdl->ResourceHandle->Category != ResourceCategory.Chara)
                continue;

            ImGui.TableNextColumn();
            Im.Text(mdl->ResourceHandle->FileName().Span);
        }
    }

    private string   _crcInput = string.Empty;
    private FullPath _crcPath  = FullPath.Empty;

    private void DrawCrcCache()
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

        using var table = Im.Table.Begin("table"u8, 2);
        if (!table)
            return;

        table.SetupColumn("Hash"u8, TableColumnFlags.WidthFixed, 18 * UiBuilder.MonoFont.GetCharAdvance('0'));
        table.SetupColumn("Type"u8, TableColumnFlags.WidthFixed, 5 * UiBuilder.MonoFont.GetCharAdvance('0'));
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

        using var table = Im.Table.Begin("ongoingLoadTable"u8, 3);
        if (!table)
            return;

        table.SetupColumn("Resource Handle"u8, TableColumnFlags.WidthStretch, 0.2f);
        table.SetupColumn("Actual Path"u8,     TableColumnFlags.WidthStretch, 0.4f);
        table.SetupColumn("Original Path"u8,   TableColumnFlags.WidthStretch, 0.4f);
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

        using var table = Im.Table.Begin("##ProblemsTable"u8, 6, TableFlags.RowBackground | TableFlags.SizingFixedFit);
        if (!table)
            return;

        _resourceManager.IterateResources((_, r) =>
        {
            if (r->RefCount < 10000)
                return;

            Im.Table.DrawColumn($"{(ResourceCategory)r->Type.Value}");
            Im.Table.DrawColumn($"{r->FileType:X}");
            Im.Table.DrawColumn($"{r->Id:X}");
            Im.Table.DrawColumn($"{(ulong)r:X}");
            Im.Table.DrawColumn($"{r->RefCount}");
            Im.Table.DrawColumn(r->FileName.AsSpan());
        });
    }


    private string     _cloudTesterPath = string.Empty;
    private bool?      _cloudTesterReturn;
    private Exception? _cloudTesterError;

    private void DrawCloudApi()
    {
        if (!ImUtf8.CollapsingHeader("Cloud API"u8))
            return;

        using var id = ImRaii.PushId("CloudApiTester"u8);

        if (ImUtf8.InputText("Path"u8, ref _cloudTesterPath, flags: ImGuiInputTextFlags.EnterReturnsTrue))
            try
            {
                _cloudTesterReturn = CloudApi.IsCloudSynced(_cloudTesterPath);
                _cloudTesterError  = null;
            }
            catch (Exception e)
            {
                _cloudTesterReturn = null;
                _cloudTesterError  = e;
            }

        if (_cloudTesterReturn.HasValue)
            ImUtf8.Text($"Is Cloud Synced? {_cloudTesterReturn}");

        if (_cloudTesterError is not null)
            Im.Text($"{_cloudTesterError}", ImGuiColors.DalamudRed);
    }


    /// <summary> Draw information about IPC options and availability. </summary>
    private void DrawDebugTabIpc()
    {
        if (!ImUtf8.CollapsingHeader("IPC"u8))
            return;

        using (var tree = ImUtf8.TreeNode("Dynamis"u8))
        {
            if (tree)
                Penumbra.Dynamis.DrawDebugInfo();
        }

        _ipcTester.Draw();
    }

    /// <summary> Helper to print a property and its value in a 2-column table. </summary>
    private static void PrintValue(string name, string value)
    {
        ImGui.TableNextColumn();
        Im.Text(name);
        ImGui.TableNextColumn();
        Im.Text(value);
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
        => DrawCopyableAddress(label, (nint)address);

    public static unsafe void DrawCopyableAddress(ReadOnlySpan<byte> label, nint address)
    {
        Penumbra.Dynamis.DrawPointer(address);
        Im.Line.SameInner();
        ImUtf8.Text(label);
    }
}

using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.System.Resource;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
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
        if (!Im.Tree.Header("Diagnostics"u8))
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
    private readonly DragDropManager                    _dragDropManager;

    public DebugTab(Configuration config, CollectionManager collectionManager, ObjectManager objects, IDataManager dataManager,
        ValidityChecker validityChecker, ModManager modManager, HttpApi httpApi, ActorManager actors, StainService stains,
        ResourceManagerService resourceManager, ResourceLoader resourceLoader, CollectionResolver collectionResolver,
        DrawObjectState drawObjectState, PathState pathState, SubfileHelper subfileHelper, IdentifiedCollectionCache identifiedCollectionCache,
        CutsceneService cutsceneService, ModImportManager modImporter, ImportPopup importPopup, FrameworkManager framework,
        TextureManager textureManager, ShaderReplacementFixer shaderReplacementFixer, RedrawService redraws, DictEmote emotes,
        Diagnostics diagnostics, IpcTester ipcTester, CrashHandlerPanel crashHandlerPanel, TexHeaderDrawer texHeaderDrawer,
        HookOverrideDrawer hookOverrides, RsfService rsfService, GlobalVariablesDrawer globalVariablesDrawer,
        SchedulerResourceManagementService schedulerService, ObjectIdentification objectIdentification, RenderTargetDrawer renderTargetDrawer,
        ModMigratorDebug modMigratorDebug, ShapeInspector shapeInspector, FileWatcher.FileWatcherDrawer fileWatcherDrawer,
        DragDropManager dragDropManager)
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
        _fileWatcherDrawer         = fileWatcherDrawer;
        _dragDropManager           = dragDropManager;
        _objects                   = objects;
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
        _dragDropManager.DrawDebugInfo();
    }


    private unsafe void DrawCollectionCaches()
    {
        if (!Im.Tree.Header(
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
                                table.NextColumn();
                                if (i < collection.Inheritance.DirectlyInheritsFrom.Count)
                                    ImUtf8.Text(collection.Inheritance.DirectlyInheritsFrom[i].Identity.Name);
                                else
                                    Im.Dummy(new Vector2(200 * Im.Style.GlobalScale, Im.Style.TextHeight));
                                table.NextColumn();
                                if (i < collection.Inheritance.DirectlyInheritedBy.Count)
                                    ImUtf8.Text(collection.Inheritance.DirectlyInheritedBy[i].Identity.Name);
                                else
                                    Im.Dummy(new Vector2(200 * Im.Style.GlobalScale, Im.Style.TextHeight));
                                table.NextColumn();
                                if (i < collection.Inheritance.FlatHierarchy.Count)
                                    ImUtf8.Text(collection.Inheritance.FlatHierarchy[i].Identity.Name);
                                else
                                    Im.Dummy(new Vector2(200 * Im.Style.GlobalScale, Im.Style.TextHeight));
                            }
                        }
                    }
                }

                using (var resourceNode = ImUtf8.TreeNode("Custom Resources"u8))
                {
                    if (resourceNode)
                        foreach (var (path, resource) in collection.Cache!.CustomResources)
                            Im.Tree.Leaf($"{path} -> 0x{(ulong)resource.ResourceHandle:X}");
                }

                using var modNode = ImUtf8.TreeNode("Enabled Mods"u8);
                if (modNode)
                    foreach (var (mod, paths, manips) in collection.Cache!.ModData.Data.OrderBy(t => t.Item1.Name))
                    {
                        using var id    = mod is TemporaryMod t ? Im.Id.Push(t.Priority.Value) : Im.Id.Push(((Mod)mod).ModPath.Name);
                        using var node2 = Im.Tree.Node(mod.Name);
                        if (!node2)
                            continue;

                        foreach (var path in paths)
                            Im.Tree.Leaf(path.Path.Span);

                        foreach (var manip in manips)
                            Im.Tree.Leaf($"{manip}");
                    }
            }
            else
            {
                using var color = ImGuiColor.Text.Push(ColorId.UndefinedMod.Value());
                Im.Tree.Leaf($"{collection.Identity.Name} (Change Counter {collection.Counters.Change})");
            }
        }
    }

    /// <summary> Draw general information about mod and collection state. </summary>
    private void DrawDebugTabGeneral()
    {
        if (!Im.Tree.Header("General"u8))
            return;

        var separateWindow = _config.Ephemeral.DebugSeparateWindow;
        if (Im.Checkbox("Draw as Separate Window"u8, ref separateWindow))
        {
            IsOpen                                = true;
            _config.Ephemeral.DebugSeparateWindow = separateWindow;
            _config.Ephemeral.Save();
        }

        using (var table = Im.Table.Begin("##DebugGeneralTable"u8, 2, TableFlags.SizingFixedFit))
        {
            if (table)
            {
                table.DrawDataPair("Penumbra Version"u8,              $"{_validityChecker.Version} {DebugVersionString}");
                table.DrawDataPair("Git Commit Hash"u8,               _validityChecker.CommitHash);
                table.DrawDataPair("Selected Collection"u8,           _collectionManager.Active.Current.Identity.Name);
                table.DrawDataPair("    has Cache"u8,                 _collectionManager.Active.Current.HasCache.ToString());
                table.DrawDataPair("Base Collection"u8,               _collectionManager.Active.Default.Identity.Name);
                table.DrawDataPair("    has Cache"u8,                 _collectionManager.Active.Default.HasCache.ToString());
                table.DrawDataPair("Mod Manager BasePath"u8,          _modManager.BasePath.Name);
                table.DrawDataPair("Mod Manager BasePath-Full"u8,     _modManager.BasePath.FullName);
                table.DrawDataPair("Mod Manager BasePath IsRooted"u8, Path.IsPathRooted(_config.ModDirectory).ToString());
                table.DrawDataPair("Mod Manager BasePath Exists"u8,   Directory.Exists(_modManager.BasePath.FullName).ToString());
                table.DrawDataPair("Mod Manager Valid"u8,             _modManager.Valid.ToString());
                table.DrawDataPair("Web Server Enabled"u8,            _httpApi.Enabled.ToString());
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
                        table.DrawDataPair(mod.Name, $"{mod.Index:D5}");
                        table.NextColumn();
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
                    table.DrawDataPair("Is Importing"u8,            importing.ToString());
                    table.DrawDataPair("Importer State"u8,          (importer?.State ?? ImporterState.None).ToString());
                    table.DrawDataPair("Import Window Was Drawn"u8, _importPopup.WasDrawn.ToString());
                    table.DrawDataPair("Import Popup Was Drawn"u8,  _importPopup.PopupWasDrawn.ToString());
                    table.DrawColumn("Import Batches"u8);
                    table.NextColumn();
                    foreach (var (index, batch) in _modImporter.ModBatches.Index())
                    {
                        foreach (var mod in batch)
                            table.DrawDataPair($"{index}", mod);
                    }

                    table.DrawColumn("Addable Mods"u8);
                    table.NextColumn();
                    foreach (var mod in _modImporter.AddableMods)
                    {
                        table.NextColumn();
                        table.DrawColumn(mod.Name);
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
                        table.DrawDataPair(important, "Immediate"u8);

                    foreach (var (idx, onTick) in _framework.OnTick.Index())
                        table.DrawDataPair(onTick, $"{idx + 1} Tick(s) From Now");

                    foreach (var (time, name) in _framework.Delayed)
                    {
                        var span = time - DateTime.UtcNow;
                        table.DrawDataPair(name, $"After {span.Minutes:D2}:{span.Seconds:D2}.{span.Milliseconds / 10:D2} (+ Ticks)");
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
                        table.DrawDataPair(task.Key.ToString()!, $"{task.Value.Item1.Status}");
            }
        }

        using (var tree = Im.Tree.Node("Redraw Service"u8))
        {
            if (tree)
            {
                using var table = Im.Table.Begin("##redraws"u8, 3, TableFlags.RowBackground);
                if (table)
                {
                    table.DrawDataPair("In GPose"u8, $"{_redraws.InGPose}");
                    table.NextColumn();

                    table.DrawDataPair("Target"u8, $"{_redraws.Target}");
                    table.NextColumn();

                    foreach (var (idx, objectIdx) in _redraws.Queue.Index())
                    {
                        var (actualIdx, state) =
                            objectIdx < 0 ? RefTuple.Create(~objectIdx, "Queued"u8) : RefTuple.Create(objectIdx, "Invisible"u8);
                        table.DrawColumn($"Redraw Queue #{idx}");
                        table.DrawColumn($"{actualIdx}");
                        table.DrawColumn(state);
                    }

                    foreach (var (idx, objectIdx) in _redraws.AfterGPoseQueue.Index())
                    {
                        var (actualIdx, state) =
                            objectIdx < 0 ? RefTuple.Create(~objectIdx, "Queued"u8) : RefTuple.Create(objectIdx, "Invisible"u8);
                        table.DrawColumn($"GPose Queue #{idx}");
                        table.DrawColumn($"{actualIdx}");
                        table.DrawColumn(state);
                    }

                    foreach (var (idx, name) in _redraws.GPoseNames.OfType<string>().Index())
                    {
                        table.DrawColumn($"GPose Name #{idx}");
                        table.DrawColumn(name);
                        table.NextColumn();
                    }
                }
            }
        }

        using (var tree = Im.Tree.Node("String Memory"u8))
        {
            if (tree)
            {
                using (Im.Group())
                {
                    Im.Text("Currently Allocated Strings"u8);
                    Im.Text("Total Allocated Strings"u8);
                    Im.Text("Free'd Allocated Strings"u8);
                    Im.Text("Currently Allocated Bytes"u8);
                    Im.Text("Total Allocated Bytes"u8);
                    Im.Text("Free'd Allocated Bytes"u8);
                }

                Im.Line.Same();
                using (ImUtf8.Group())
                {
                    Im.Text($"{PenumbraStringMemory.CurrentStrings}");
                    Im.Text($"{PenumbraStringMemory.AllocatedStrings}");
                    Im.Text($"{PenumbraStringMemory.FreedStrings}");
                    Im.Text($"{PenumbraStringMemory.CurrentBytes}");
                    Im.Text($"{PenumbraStringMemory.AllocatedBytes}");
                    Im.Text($"{PenumbraStringMemory.FreedBytes}");
                }
            }
        }

        _fileWatcherDrawer.Draw();
    }

    private void DrawPerformanceTab()
    {
        if (!Im.Tree.Node("Performance"u8))
            return;

        using (var start = Im.Tree.Node("Startup Performance"u8, TreeNodeFlags.DefaultOpen))
        {
            if (start)
                Im.Line.New();
        }
    }

    private unsafe void DrawActorsDebug()
    {
        if (!Im.Tree.Node("Actors"u8))
            return;

        using (var objectTree = Im.Tree.Node("Object Manager"u8))
        {
            if (objectTree)
            {
                _objects.DrawDebug();

                using var table = Im.Table.Begin("##actors"u8, 8, TableFlags.RowBackground | TableFlags.SizingFixedFit,
                    -Vector2.UnitX);
                if (!table)
                    return;

                DrawSpecial("Current Player"u8,  _actors.GetCurrentPlayer());
                DrawSpecial("Current Inspect"u8, _actors.GetInspectPlayer());
                DrawSpecial("Current Card"u8,    _actors.GetCardPlayer());
                DrawSpecial("Current Glamour"u8, _actors.GetGlamourPlayer());

                foreach (var obj in _objects)
                {
                    table.DrawColumn(obj.Address != nint.Zero ? $"{((GameObject*)obj.Address)->ObjectIndex}" : "NULL"u8);
                    table.NextColumn();
                    Penumbra.Dynamis.DrawPointer(obj.Address);
                    table.NextColumn();
                    if (obj.Address != nint.Zero)
                        Penumbra.Dynamis.DrawPointer((nint)((Character*)obj.Address)->GameObject.GetDrawObject());
                    var identifier = _actors.FromObject(obj, out _, false, true, false);
                    table.DrawColumn(_actors.ToString(identifier));
                    var id = obj.AsObject->ObjectKind is ObjectKind.BattleNpc
                        ? $"{identifier.DataId} | {obj.AsObject->BaseId}"
                        : identifier.DataId.ToString();
                    table.DrawColumn(id);
                    table.NextColumn();
                    Penumbra.Dynamis.DrawPointer(obj.Address != nint.Zero ? *(nint*)obj.Address : nint.Zero);
                    table.DrawColumn(obj.Address != nint.Zero ? $"0x{obj.AsObject->EntityId:X}" : "NULL");
                    table.DrawColumn(obj.Address != nint.Zero
                        ? obj.AsObject->IsCharacter() ? $"Character: {obj.AsCharacter->ObjectKind}" : "No Character"
                        : "NULL");
                }
            }
        }

        using (var shapeTree = Im.Tree.Node("Shape Inspector"u8))
        {
            if (shapeTree)
                _shapeInspector.Draw();
        }

        return;

        void DrawSpecial(ReadOnlySpan<byte> name, ActorIdentifier id)
        {
            if (!id.IsValid)
                return;

            Im.Table.DrawColumn(name);
            Im.Table.DrawColumn(StringU8.Empty);
            Im.Table.DrawColumn(StringU8.Empty);
            Im.Table.DrawColumn(_actors.ToString(id));
            Im.Table.DrawColumn(StringU8.Empty);
            Im.Table.DrawColumn(StringU8.Empty);
            Im.Table.DrawColumn(StringU8.Empty);
            Im.Table.DrawColumn(StringU8.Empty);
        }
    }

    /// <summary>
    /// Draw information about which draw objects correspond to which game objects
    /// and which paths are due to be loaded by which collection.
    /// </summary>
    private unsafe void DrawPathResolverDebug()
    {
        if (!Im.Tree.Node("Path Resolver"u8))
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
                        table.NextColumn();
                        Penumbra.Dynamis.DrawPointer(drawObject.Address);
                        table.DrawColumn($"{gameObjectPtr.Index}");
                        using (ImGuiColor.Text.Push(new Vector4(1, 0, 0, 1), gameObjectPtr.Index != idx))
                        {
                            table.DrawColumn($"{idx}");
                        }

                        table.DrawColumn(child ? "Child"u8 : "Main"u8);
                        table.NextColumn();
                        Penumbra.Dynamis.DrawPointer(gameObjectPtr);
                        using (ImGuiColor.Text.Push(new Vector4(1, 0, 0, 1), _objects[idx] != gameObjectPtr))
                        {
                            table.DrawColumn($"{_objects[idx]}");
                        }

                        table.DrawColumn(gameObjectPtr.Utf8Name.Span);
                        var collection = _collectionResolver.IdentifyCollection(gameObjectPtr.AsObject, true);
                        table.DrawColumn(collection.ModCollection.Identity.Name);
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
                        table.NextColumn();
                        Penumbra.Dynamis.DrawPointer(data.AssociatedGameObject);
                        table.DrawColumn(data.ModCollection.Identity.Name);
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
                    table.DrawColumn("Current Mtrl Data"u8);
                    table.DrawColumn(_subfileHelper.MtrlData.ModCollection.Identity.Name);
                    table.DrawColumn($"0x{_subfileHelper.MtrlData.AssociatedGameObject:X}");
                    table.NextColumn();

                    table.DrawColumn("Current Avfx Data"u8);
                    table.DrawColumn(_subfileHelper.AvfxData.ModCollection.Identity.Name);
                    table.DrawColumn($"0x{_subfileHelper.AvfxData.AssociatedGameObject:X}");
                    table.NextColumn();

                    table.DrawColumn("Current Resources"u8);
                    table.DrawColumn($"{_subfileHelper.Count}");
                    table.NextColumn();
                    table.NextColumn();

                    foreach (var (resource, resolve) in _subfileHelper)
                    {
                        table.DrawColumn($"0x{resource:X}");
                        table.DrawColumn(resolve.ModCollection.Identity.Name);
                        table.DrawColumn($"0x{resolve.AssociatedGameObject:X}");
                        table.DrawColumn($"{((ResourceHandle*)resource)->FileName()}");
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
                        table.DrawColumn($"{((GameObject*)address)->ObjectIndex}");
                        table.DrawColumn($"0x{address:X}");
                        table.DrawColumn($"{identifier}");
                        table.DrawColumn(collection.Identity.Name);
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
                        table.DrawColumn($"Cutscene Actor {idx}");
                        table.DrawColumn(((Actor)actor.Address).StoredName());
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
                    table.DrawColumn("Group Members"u8);
                    table.DrawColumn($"{GroupManager.Instance()->MainGroup.MemberCount}");
                    for (var i = 0; i < 8; ++i)
                    {
                        table.DrawColumn($"Member #{i}");
                        var member = GroupManager.Instance()->MainGroup.GetPartyMemberByIndex(i);
                        table.DrawColumn(member is null ? "NULL"u8 : member->Name);
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

                Im.Text("Agent: "u8);
                Im.Line.NoSpacing();
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
        if (!Im.Tree.Node("Game Data"u8))
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

        Im.Input.Text("File Name"u8,  ref _emoteSearchFile);
        Im.Input.Text("Emote Name"u8, ref _emoteSearchName);
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
                Im.Table.DrawColumn(p.Key);
                Im.Table.DrawColumn(StringU8.Join(", "u8, p.Value.Select(v => v.Name.ToDalamudString().TextValue)));
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

        if (Im.Input.Text("Key"u8, ref _tmbKeyFilter))
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
                    table.NextColumn();
                    var frame = new Vector2(Im.Style.TextHeight);
                    Im.Color.Button("###color"u8, new Vector4(MtrlTab.PseudoSqrtRgb((Vector3)color), 1), 0, frame);
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
        if (!Im.Tree.Header("Shader Replacement Fixer"u8))
            return;

        var enableShaderReplacementFixer = _shaderReplacementFixer.Enabled;
        if (Im.Checkbox("Enable Shader Replacement Fixer"u8, ref enableShaderReplacementFixer))
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
        table.HeaderRow();

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
        var player = _objects[0];
        var name   = player.Valid ? player.StoredName() : "NULL"u8;
        if (!Im.Tree.Header($"Player Model Info: {name}##Draw") || !player.Valid)
            return;

        DrawCopyableAddress("PlayerCharacter"u8, player.Address);

        var model = player.Model;
        if (!model.IsCharacterBase)
            return;

        DrawCopyableAddress("CharacterBase"u8, model);

        using (var t1 = Im.Table.Begin("##table"u8, 2, TableFlags.SizingFixedFit))
        {
            if (t1)
            {
                ImGuiUtil.DrawTableColumn("Flags");
                ImGuiUtil.DrawTableColumn($"{model.AsCharacterBase->StateFlags}");
                ImGuiUtil.DrawTableColumn("Has Model In Slot Loaded");
                ImGuiUtil.DrawTableColumn($"{model.AsCharacterBase->HasModelInSlotLoaded:X8}");
                ImGuiUtil.DrawTableColumn("Has Model Files In Slot Loaded");
                ImGuiUtil.DrawTableColumn($"{model.AsCharacterBase->HasModelFilesInSlotLoaded:X8}");
            }
        }

        using var table = Im.Table.Begin($"##{name}DrawTable", 5, TableFlags.RowBackground | TableFlags.SizingFixedFit);
        if (!table)
            return;

        table.NextColumn();
        table.Header("Slot"u8);
        table.NextColumn();
        table.Header("Imc Ptr"u8);
        table.NextColumn();
        table.Header("Imc File"u8);
        table.NextColumn();
        table.Header("Model Ptr"u8);
        table.NextColumn();
        table.Header("Model File"u8);

        for (var i = 0; i < model.AsCharacterBase->SlotCount; ++i)
        {
            var imc = (ResourceHandle*)model.AsCharacterBase->IMCArray[i];
            table.NextRow();
            table.DrawColumn($"Slot {i}");
            table.NextColumn();
            Penumbra.Dynamis.DrawPointer((nint)imc);
            table.NextColumn();
            if (imc is not null)
                Im.Text(imc->FileName().Span);

            var mdl = (RenderModel*)model.AsCharacterBase->Models[i];
            table.NextColumn();
            Penumbra.Dynamis.DrawPointer((nint)mdl);
            if (mdl is null || mdl->ResourceHandle is null || mdl->ResourceHandle->Category is not ResourceCategory.Chara)
                continue;

            table.DrawColumn(mdl->ResourceHandle->FileName().Span);
        }
    }

    private string   _crcInput = string.Empty;
    private FullPath _crcPath  = FullPath.Empty;

    private void DrawCrcCache()
    {
        var header = Im.Tree.Header("CRC Cache"u8);
        if (!header)
            return;

        if (Im.Input.Text("##crcInput"u8, ref _crcInput, "Input path for CRC..."u8))
            _crcPath = new FullPath(_crcInput);

        using var font = ImRaii.PushFont(UiBuilder.MonoFont);
        Im.Text($"   CRC32: {_crcPath.InternalName.CiCrc32:X8}");
        Im.Text($"CI CRC32: {_crcPath.InternalName.Crc32:X8}");
        Im.Text($"   CRC64: {_crcPath.Crc64:X16}");

        using var table = Im.Table.Begin("table"u8, 2);
        if (!table)
            return;

        table.SetupColumn("Hash"u8, TableColumnFlags.WidthFixed, 18 * UiBuilder.MonoFont.GetCharAdvance('0'));
        table.SetupColumn("Type"u8, TableColumnFlags.WidthFixed, 5 * UiBuilder.MonoFont.GetCharAdvance('0'));
        table.HeaderRow();

        foreach (var (hash, type) in _rsfService.CustomCache)
        {
            table.DrawColumn($"{hash:X16}");
            table.DrawColumn($"{type}");
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
        table.HeaderRow();

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
        var header = Im.Tree.HeaderId("Resource Problems"u8);
        Im.Tooltip.OnHover("Draw resources with unusually high reference count to detect overflows."u8);
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

        if (Im.Input.Text("Path"u8, ref _cloudTesterPath, flags: InputTextFlags.EnterReturnsTrue))
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

    public override void Draw()
        => DrawContent();

    public override bool DrawConditions()
        => _config.DebugMode && _config.Ephemeral.DebugSeparateWindow;

    public override void OnClose()
    {
        _config.Ephemeral.DebugSeparateWindow = false;
        _config.Ephemeral.Save();
    }

    public static unsafe void DrawCopyableAddress(ReadOnlySpan<byte> label, nint address)
    {
        Penumbra.Dynamis.DrawPointer(address);
        Im.Line.SameInner();
        ImUtf8.Text(label);
    }
}

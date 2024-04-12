using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.System.Resource;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ImGuiNET;
using Microsoft.Extensions.DependencyInjection;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Services;
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
using Penumbra.Interop.ResourceLoading;
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
using CharacterUtility = Penumbra.Interop.Services.CharacterUtility;
using ObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;
using ResidentResourceManager = Penumbra.Interop.Services.ResidentResourceManager;
using ImGuiClip = OtterGui.ImGuiClip;
using Penumbra.Api.IpcTester;

namespace Penumbra.UI.Tabs.Debug;

public class Diagnostics(IServiceProvider provider)
{
    public void DrawDiagnostics()
    {
        if (!ImGui.CollapsingHeader("Diagnostics"))
            return;

        using var table = ImRaii.Table("##data", 4, ImGuiTableFlags.RowBg);
        foreach (var type in typeof(ActorManager).Assembly.GetTypes()
                     .Where(t => t is { IsAbstract: false, IsInterface: false } && t.IsAssignableTo(typeof(IAsyncDataContainer))))
        {
            var container = (IAsyncDataContainer)provider.GetRequiredService(type);
            ImGuiUtil.DrawTableColumn(container.Name);
            ImGuiUtil.DrawTableColumn(container.Time.ToString());
            ImGuiUtil.DrawTableColumn(Functions.HumanReadableSize(container.Memory));
            ImGuiUtil.DrawTableColumn(container.TotalCount.ToString());
        }
    }
}

public class DebugTab : Window, ITab
{
    private readonly PerformanceTracker        _performance;
    private readonly Configuration             _config;
    private readonly CollectionManager         _collectionManager;
    private readonly ModManager                _modManager;
    private readonly ValidityChecker           _validityChecker;
    private readonly HttpApi                   _httpApi;
    private readonly ActorManager              _actors;
    private readonly StainService              _stains;
    private readonly CharacterUtility          _characterUtility;
    private readonly ResidentResourceManager   _residentResources;
    private readonly ResourceManagerService    _resourceManager;
    private readonly CollectionResolver        _collectionResolver;
    private readonly DrawObjectState           _drawObjectState;
    private readonly PathState                 _pathState;
    private readonly SubfileHelper             _subfileHelper;
    private readonly IdentifiedCollectionCache _identifiedCollectionCache;
    private readonly CutsceneService           _cutsceneService;
    private readonly ModImportManager          _modImporter;
    private readonly ImportPopup               _importPopup;
    private readonly FrameworkManager          _framework;
    private readonly TextureManager            _textureManager;
    private readonly ShaderReplacementFixer    _shaderReplacementFixer;
    private readonly RedrawService             _redraws;
    private readonly DictEmote                 _emotes;
    private readonly Diagnostics               _diagnostics;
    private readonly ObjectManager             _objects;
    private readonly IClientState              _clientState;
    private readonly IpcTester                 _ipcTester;
    private readonly CrashHandlerPanel         _crashHandlerPanel;

    public DebugTab(PerformanceTracker performance, Configuration config, CollectionManager collectionManager, ObjectManager objects,
        IClientState clientState,
        ValidityChecker validityChecker, ModManager modManager, HttpApi httpApi, ActorManager actors, StainService stains,
        CharacterUtility characterUtility, ResidentResourceManager residentResources,
        ResourceManagerService resourceManager, CollectionResolver collectionResolver,
        DrawObjectState drawObjectState, PathState pathState, SubfileHelper subfileHelper, IdentifiedCollectionCache identifiedCollectionCache,
        CutsceneService cutsceneService, ModImportManager modImporter, ImportPopup importPopup, FrameworkManager framework,
        TextureManager textureManager, ShaderReplacementFixer shaderReplacementFixer, RedrawService redraws, DictEmote emotes,
        Diagnostics diagnostics, IpcTester ipcTester, CrashHandlerPanel crashHandlerPanel)
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
        _characterUtility          = characterUtility;
        _residentResources         = residentResources;
        _resourceManager           = resourceManager;
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
        _objects                   = objects;
        _clientState               = clientState;
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
        ImGui.NewLine();
        _crashHandlerPanel.Draw();
        ImGui.NewLine();
        _diagnostics.DrawDiagnostics();
        DrawPerformanceTab();
        ImGui.NewLine();
        DrawPathResolverDebug();
        ImGui.NewLine();
        DrawActorsDebug();
        ImGui.NewLine();
        DrawCollectionCaches();
        ImGui.NewLine();
        DrawDebugCharacterUtility();
        ImGui.NewLine();
        DrawData();
        ImGui.NewLine();
        DrawDebugTabMetaLists();
        ImGui.NewLine();
        DrawResourceProblems();
        ImGui.NewLine();
        DrawPlayerModelInfo();
        ImGui.NewLine();
        DrawGlobalVariableInfo();
        ImGui.NewLine();
        DrawDebugTabIpc();
        ImGui.NewLine();
    }


    private void DrawCollectionCaches()
    {
        if (!ImGui.CollapsingHeader(
                $"Collections ({_collectionManager.Caches.Count}/{_collectionManager.Storage.Count - 1} Caches)###Collections"))
            return;

        foreach (var collection in _collectionManager.Storage)
        {
            if (collection.HasCache)
            {
                using var color = PushColor(ImGuiCol.Text, ColorId.FolderExpanded.Value());
                using var node  = TreeNode($"{collection.AnonymizedName} (Change Counter {collection.ChangeCounter})");
                if (!node)
                    continue;

                color.Pop();
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
                TreeNode($"{collection.AnonymizedName} (Change Counter {collection.ChangeCounter})",
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
                PrintValue(TutorialService.SelectedCollection, _collectionManager.Active.Current.Name);
                PrintValue("    has Cache",                    _collectionManager.Active.Current.HasCache.ToString());
                PrintValue(TutorialService.DefaultCollection,  _collectionManager.Active.Default.Name);
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
    }

    private void DrawPerformanceTab()
    {
        ImGui.NewLine();
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

        using var table = Table("##actors", 5, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit,
            -Vector2.UnitX);
        if (!table)
            return;

        DrawSpecial("Current Player",  _actors.GetCurrentPlayer());
        DrawSpecial("Current Inspect", _actors.GetInspectPlayer());
        DrawSpecial("Current Card",    _actors.GetCardPlayer());
        DrawSpecial("Current Glamour", _actors.GetGlamourPlayer());

        foreach (var obj in _objects)
        {
            ImGuiUtil.DrawTableColumn($"{((GameObject*)obj.Address)->ObjectIndex}");
            ImGuiUtil.DrawTableColumn($"0x{obj.Address:X}");
            ImGuiUtil.DrawTableColumn(obj.Address == nint.Zero
                ? string.Empty
                : $"0x{(nint)((Character*)obj.Address)->GameObject.GetDrawObject():X}");
            var identifier = _actors.FromObject(obj, out _, false, true, false);
            ImGuiUtil.DrawTableColumn(_actors.ToString(identifier));
            var id = obj.AsObject->ObjectKind == (byte)ObjectKind.BattleNpc
                ? $"{identifier.DataId} | {obj.AsObject->DataID}"
                : identifier.DataId.ToString();
            ImGuiUtil.DrawTableColumn(id);
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
            $"Last Game Object: 0x{_collectionResolver.IdentifyLastGameObjectCollection(true).AssociatedGameObject:X} ({_collectionResolver.IdentifyLastGameObjectCollection(true).ModCollection.Name})");
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
                        ImGui.TextUnformatted($"0x{drawObject:X}");
                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted(gameObject->ObjectIndex.ToString());
                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted(child ? "Child" : "Main");
                        ImGui.TableNextColumn();
                        var (address, name) = ($"0x{gameObjectPtr:X}", new ByteString(gameObject->Name).ToString());
                        ImGui.TextUnformatted(address);
                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted(name);
                        ImGui.TableNextColumn();
                        var collection = _collectionResolver.IdentifyCollection(gameObject, true);
                        ImGui.TextUnformatted(collection.ModCollection.Name);
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
                        ImGui.TextUnformatted(data.ModCollection.Name);
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
                    ImGuiUtil.DrawTableColumn(_subfileHelper.MtrlData.ModCollection.Name);
                    ImGuiUtil.DrawTableColumn($"0x{_subfileHelper.MtrlData.AssociatedGameObject:X}");
                    ImGui.TableNextColumn();

                    ImGuiUtil.DrawTableColumn("Current Avfx Data");
                    ImGuiUtil.DrawTableColumn(_subfileHelper.AvfxData.ModCollection.Name);
                    ImGuiUtil.DrawTableColumn($"0x{_subfileHelper.AvfxData.AssociatedGameObject:X}");
                    ImGui.TableNextColumn();

                    ImGuiUtil.DrawTableColumn("Current Resources");
                    ImGuiUtil.DrawTableColumn(_subfileHelper.Count.ToString());
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();

                    foreach (var (resource, resolve) in _subfileHelper)
                    {
                        ImGuiUtil.DrawTableColumn($"0x{resource:X}");
                        ImGuiUtil.DrawTableColumn(resolve.ModCollection.Name);
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
                        ImGuiUtil.DrawTableColumn(collection.Name);
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
                    ImGuiUtil.DrawTableColumn(GroupManager.Instance()->MemberCount.ToString());
                    for (var i = 0; i < 8; ++i)
                    {
                        ImGuiUtil.DrawTableColumn($"Member #{i}");
                        var member = GroupManager.Instance()->GetPartyMemberByIndex(i);
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
                            ref var c = ref agent->Data->CharacterArraySpan[i];
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
    }

    private void DrawData()
    {
        if (!ImGui.CollapsingHeader("Game Data"))
            return;

        DrawEmotes();
        DrawStainTemplates();
    }

    private string _emoteSearchFile = string.Empty;
    private string _emoteSearchName = string.Empty;

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

    private void DrawStainTemplates()
    {
        using var mainTree = TreeNode("Staining Templates");
        if (!mainTree)
            return;

        foreach (var (key, data) in _stains.StmFile.Entries)
        {
            using var tree = TreeNode($"Template {key}");
            if (!tree)
                continue;

            using var table = Table("##table", 5, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg);
            if (!table)
                continue;

            for (var i = 0; i < StmFile.StainingTemplateEntry.NumElements; ++i)
            {
                var (r, g, b) = data.DiffuseEntries[i];
                ImGuiUtil.DrawTableColumn($"{r:F6} | {g:F6} | {b:F6}");

                (r, g, b) = data.SpecularEntries[i];
                ImGuiUtil.DrawTableColumn($"{r:F6} | {g:F6} | {b:F6}");

                (r, g, b) = data.EmissiveEntries[i];
                ImGuiUtil.DrawTableColumn($"{r:F6} | {g:F6} | {b:F6}");

                var a = data.SpecularPowerEntries[i];
                ImGuiUtil.DrawTableColumn($"{a:F6}");

                a = data.GlossEntries[i];
                ImGuiUtil.DrawTableColumn($"{a:F6}");
            }
        }
    }

    /// <summary>
    /// Draw information about the character utility class from SE,
    /// displaying all files, their sizes, the default files and the default sizes.
    /// </summary>
    private unsafe void DrawDebugCharacterUtility()
    {
        if (!ImGui.CollapsingHeader("Character Utility"))
            return;

        var enableShaderReplacementFixer = _shaderReplacementFixer.Enabled;
        if (ImGui.Checkbox("Enable Shader Replacement Fixer", ref enableShaderReplacementFixer))
            _shaderReplacementFixer.Enabled = enableShaderReplacementFixer;

        if (enableShaderReplacementFixer)
        {
            ImGui.SameLine();
            ImGui.Dummy(ImGuiHelpers.ScaledVector2(20, 0));
            var slowPathCallDeltas = _shaderReplacementFixer.GetAndResetSlowPathCallDeltas();
            ImGui.SameLine();
            ImGui.TextUnformatted($"\u0394 Slow-Path Calls for skin.shpk: {slowPathCallDeltas.Skin}");
            ImGui.SameLine();
            ImGui.TextUnformatted($"characterglass.shpk: {slowPathCallDeltas.CharacterGlass}");
            ImGui.SameLine();
            ImGui.Dummy(ImGuiHelpers.ScaledVector2(20, 0));
            ImGui.SameLine();
            ImGui.TextUnformatted($"Materials with Modded skin.shpk: {_shaderReplacementFixer.ModdedSkinShpkCount}");
            ImGui.SameLine();
            ImGui.TextUnformatted($"characterglass.shpk: {_shaderReplacementFixer.ModdedCharacterGlassShpkCount}");
        }

        using var table = Table("##CharacterUtility", 7, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit,
            -Vector2.UnitX);
        if (!table)
            return;

        for (var idx = 0; idx < CharacterUtility.ReverseIndices.Length; ++idx)
        {
            var intern   = CharacterUtility.ReverseIndices[idx];
            var resource = _characterUtility.Address->Resource(idx);
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"[{idx}]");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"0x{(ulong)resource:X}");
            ImGui.TableNextColumn();
            if (resource == null)
            {
                ImGui.TableNextRow();
                continue;
            }

            UiHelpers.Text(resource);
            ImGui.TableNextColumn();
            var data   = (nint)resource->CsHandle.GetData();
            var length = resource->CsHandle.GetLength();
            if (ImGui.Selectable($"0x{data:X}"))
                if (data != nint.Zero && length > 0)
                    ImGui.SetClipboardText(string.Join("\n",
                        new ReadOnlySpan<byte>((byte*)data, (int)length).ToArray().Select(b => b.ToString("X2"))));

            ImGuiUtil.HoverTooltip("Click to copy bytes to clipboard.");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(length.ToString());

            ImGui.TableNextColumn();
            if (intern.Value != -1)
            {
                ImGui.Selectable($"0x{_characterUtility.DefaultResource(intern).Address:X}");
                if (ImGui.IsItemClicked())
                    ImGui.SetClipboardText(string.Join("\n",
                        new ReadOnlySpan<byte>((byte*)_characterUtility.DefaultResource(intern).Address,
                            _characterUtility.DefaultResource(intern).Size).ToArray().Select(b => b.ToString("X2"))));

                ImGuiUtil.HoverTooltip("Click to copy bytes to clipboard.");

                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{_characterUtility.DefaultResource(intern).Size}");
            }
            else
            {
                ImGui.TableNextColumn();
            }
        }
    }

    private void DrawDebugTabMetaLists()
    {
        if (!ImGui.CollapsingHeader("Metadata Changes"))
            return;

        using var table = Table("##DebugMetaTable", 3, ImGuiTableFlags.SizingFixedFit);
        if (!table)
            return;

        foreach (var list in _characterUtility.Lists)
        {
            ImGuiUtil.DrawTableColumn(list.GlobalMetaIndex.ToString());
            ImGuiUtil.DrawTableColumn(list.Entries.Count.ToString());
            ImGuiUtil.DrawTableColumn(string.Join(", ", list.Entries.Select(e => $"0x{e.Data:X}")));
        }
    }

    /// <summary> Draw information about the resident resource files. </summary>
    private unsafe void DrawDebugResidentResources()
    {
        using var tree = TreeNode("Resident Resources");
        if (!tree)
            return;

        if (_residentResources.Address == null || _residentResources.Address->NumResources == 0)
            return;

        using var table = Table("##ResidentResources", 2, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit,
            -Vector2.UnitX);
        if (!table)
            return;

        for (var i = 0; i < _residentResources.Address->NumResources; ++i)
        {
            var resource = _residentResources.Address->ResourceList[i];
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"0x{(ulong)resource:X}");
            ImGui.TableNextColumn();
            UiHelpers.Text(resource);
        }
    }

    private static void DrawCopyableAddress(string label, nint address)
    {
        using (var _ = PushFont(UiBuilder.MonoFont))
        {
            if (ImGui.Selectable($"0x{address:X16}    {label}"))
                ImGui.SetClipboardText($"{address:X16}");
        }

        ImGuiUtil.HoverTooltip("Click to copy address to clipboard.");
    }

    private static unsafe void DrawCopyableAddress(string label, void* address)
        => DrawCopyableAddress(label, (nint)address);

    /// <summary> Draw information about the models, materials and resources currently loaded by the local player. </summary>
    private unsafe void DrawPlayerModelInfo()
    {
        var player = _clientState.LocalPlayer;
        var name   = player?.Name.ToString() ?? "NULL";
        if (!ImGui.CollapsingHeader($"Player Model Info: {name}##Draw") || player == null)
            return;

        DrawCopyableAddress("PlayerCharacter", player.Address);

        var model = (CharacterBase*)((Character*)player.Address)->GameObject.GetDrawObject();
        if (model == null)
            return;

        DrawCopyableAddress("CharacterBase", model);

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

    /// <summary> Draw information about some game global variables. </summary>
    private unsafe void DrawGlobalVariableInfo()
    {
        var header = ImGui.CollapsingHeader("Global Variables");
        ImGuiUtil.HoverTooltip("Draw information about global variables. Can provide useful starting points for a memory viewer.");
        if (!header)
            return;

        DrawCopyableAddress("CharacterUtility",        _characterUtility.Address);
        DrawCopyableAddress("ResidentResourceManager", _residentResources.Address);
        DrawCopyableAddress("Device",                  Device.Instance());
        DrawDebugResidentResources();
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
}

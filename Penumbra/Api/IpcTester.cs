using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Plugin;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using Penumbra.Mods;
using Dalamud.Utility;
using Penumbra.Api.Enums;
using Penumbra.Api.Helpers;
using Penumbra.String;
using Penumbra.String.Classes;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Manager;
using Penumbra.Services;
using Penumbra.UI;
using Penumbra.Collections.Manager;
using Dalamud.Plugin.Services;
using Penumbra.GameData.Structs;

namespace Penumbra.Api;

public class IpcTester : IDisposable
{
    private readonly PenumbraIpcProviders _ipcProviders;
    private          bool                 _subscribed = true;

    private readonly PluginState      _pluginState;
    private readonly IpcConfiguration _ipcConfiguration;
    private readonly Ui               _ui;
    private readonly Redrawing        _redrawing;
    private readonly GameState        _gameState;
    private readonly Resolve          _resolve;
    private readonly Collections      _collections;
    private readonly Meta             _meta;
    private readonly Mods             _mods;
    private readonly ModSettings      _modSettings;
    private readonly Editing          _editing;
    private readonly Temporary        _temporary;
    private readonly ResourceTree     _resourceTree;

    public IpcTester(Configuration config, DalamudServices dalamud, PenumbraIpcProviders ipcProviders, ModManager modManager,
        CollectionManager collections, TempModManager tempMods, TempCollectionManager tempCollections, SaveService saveService)
    {
        _ipcProviders     = ipcProviders;
        _pluginState      = new PluginState(dalamud.PluginInterface);
        _ipcConfiguration = new IpcConfiguration(dalamud.PluginInterface);
        _ui               = new Ui(dalamud.PluginInterface);
        _redrawing        = new Redrawing(dalamud);
        _gameState        = new GameState(dalamud.PluginInterface);
        _resolve          = new Resolve(dalamud.PluginInterface);
        _collections      = new Collections(dalamud.PluginInterface);
        _meta             = new Meta(dalamud.PluginInterface);
        _mods             = new Mods(dalamud.PluginInterface);
        _modSettings      = new ModSettings(dalamud.PluginInterface);
        _editing          = new Editing(dalamud.PluginInterface);
        _temporary        = new Temporary(dalamud.PluginInterface, modManager, collections, tempMods, tempCollections, saveService, config);
        _resourceTree     = new ResourceTree(dalamud.PluginInterface, dalamud.Objects);
        UnsubscribeEvents();
    }

    public void Draw()
    {
        try
        {
            SubscribeEvents();
            ImGui.TextUnformatted($"API Version: {_ipcProviders.Api.ApiVersion.Breaking}.{_ipcProviders.Api.ApiVersion.Feature:D4}");
            _pluginState.Draw();
            _ipcConfiguration.Draw();
            _ui.Draw();
            _redrawing.Draw();
            _gameState.Draw();
            _resolve.Draw();
            _collections.Draw();
            _meta.Draw();
            _mods.Draw();
            _modSettings.Draw();
            _editing.Draw();
            _temporary.Draw();
            _temporary.DrawCollections();
            _temporary.DrawMods();
            _resourceTree.Draw();
        }
        catch (Exception e)
        {
            Penumbra.Log.Error($"Error during IPC Tests:\n{e}");
        }
    }

    private void SubscribeEvents()
    {
        if (!_subscribed)
        {
            _pluginState.Initialized.Enable();
            _pluginState.Disposed.Enable();
            _pluginState.EnabledChange.Enable();
            _redrawing.Redrawn.Enable();
            _ui.PreSettingsDraw.Enable();
            _ui.PostSettingsDraw.Enable();
            _modSettings.SettingChanged.Enable();
            _gameState.CharacterBaseCreating.Enable();
            _gameState.CharacterBaseCreated.Enable();
            _ipcConfiguration.ModDirectoryChanged.Enable();
            _gameState.GameObjectResourcePathResolved.Enable();
            _mods.DeleteSubscriber.Enable();
            _mods.AddSubscriber.Enable();
            _mods.MoveSubscriber.Enable();
            _subscribed = true;
        }
    }

    public void UnsubscribeEvents()
    {
        if (_subscribed)
        {
            _pluginState.Initialized.Disable();
            _pluginState.Disposed.Disable();
            _pluginState.EnabledChange.Disable();
            _redrawing.Redrawn.Disable();
            _ui.PreSettingsDraw.Disable();
            _ui.PostSettingsDraw.Disable();
            _ui.Tooltip.Disable();
            _ui.Click.Disable();
            _modSettings.SettingChanged.Disable();
            _gameState.CharacterBaseCreating.Disable();
            _gameState.CharacterBaseCreated.Disable();
            _ipcConfiguration.ModDirectoryChanged.Disable();
            _gameState.GameObjectResourcePathResolved.Disable();
            _mods.DeleteSubscriber.Disable();
            _mods.AddSubscriber.Disable();
            _mods.MoveSubscriber.Disable();
            _subscribed = false;
        }
    }

    public void Dispose()
    {
        _pluginState.Initialized.Dispose();
        _pluginState.Disposed.Dispose();
        _pluginState.EnabledChange.Dispose();
        _redrawing.Redrawn.Dispose();
        _ui.PreSettingsDraw.Dispose();
        _ui.PostSettingsDraw.Dispose();
        _ui.Tooltip.Dispose();
        _ui.Click.Dispose();
        _modSettings.SettingChanged.Dispose();
        _gameState.CharacterBaseCreating.Dispose();
        _gameState.CharacterBaseCreated.Dispose();
        _ipcConfiguration.ModDirectoryChanged.Dispose();
        _gameState.GameObjectResourcePathResolved.Dispose();
        _mods.DeleteSubscriber.Dispose();
        _mods.AddSubscriber.Dispose();
        _mods.MoveSubscriber.Dispose();
        _subscribed = false;
    }

    private static void DrawIntro(string label, string info)
    {
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(label);
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(info);
        ImGui.TableNextColumn();
    }


    private class PluginState
    {
        private readonly DalamudPluginInterface _pi;
        public readonly  EventSubscriber        Initialized;
        public readonly  EventSubscriber        Disposed;
        public readonly  EventSubscriber<bool>  EnabledChange;

        private readonly List<DateTimeOffset> _initializedList = new();
        private readonly List<DateTimeOffset> _disposedList    = new();

        private DateTimeOffset _lastEnabledChange = DateTimeOffset.UnixEpoch;
        private bool?          _lastEnabledValue;

        public PluginState(DalamudPluginInterface pi)
        {
            _pi           = pi;
            Initialized   = Ipc.Initialized.Subscriber(pi, AddInitialized);
            Disposed      = Ipc.Disposed.Subscriber(pi, AddDisposed);
            EnabledChange = Ipc.EnabledChange.Subscriber(pi, SetLastEnabled);
        }

        public void Draw()
        {
            using var _ = ImRaii.TreeNode("Plugin State");
            if (!_)
                return;

            using var table = ImRaii.Table(string.Empty, 3, ImGuiTableFlags.SizingFixedFit);
            if (!table)
                return;

            void DrawList(string label, string text, List<DateTimeOffset> list)
            {
                DrawIntro(label, text);
                if (list.Count == 0)
                {
                    ImGui.TextUnformatted("Never");
                }
                else
                {
                    ImGui.TextUnformatted(list[^1].LocalDateTime.ToString(CultureInfo.CurrentCulture));
                    if (list.Count > 1 && ImGui.IsItemHovered())
                        ImGui.SetTooltip(string.Join("\n",
                            list.SkipLast(1).Select(t => t.LocalDateTime.ToString(CultureInfo.CurrentCulture))));
                }
            }

            DrawList(Ipc.Initialized.Label, "Last Initialized", _initializedList);
            DrawList(Ipc.Disposed.Label,    "Last Disposed",    _disposedList);
            DrawIntro(Ipc.ApiVersions.Label, "Current Version");
            var (breaking, features) = Ipc.ApiVersions.Subscriber(_pi).Invoke();
            ImGui.TextUnformatted($"{breaking}.{features:D4}");
            DrawIntro(Ipc.GetEnabledState.Label, "Current State");
            ImGui.TextUnformatted($"{Ipc.GetEnabledState.Subscriber(_pi).Invoke()}");
            DrawIntro(Ipc.EnabledChange.Label, "Last Change");
            ImGui.TextUnformatted(_lastEnabledValue is { } v ? $"{_lastEnabledChange} (to {v})" : "Never");
        }

        private void AddInitialized()
            => _initializedList.Add(DateTimeOffset.UtcNow);

        private void AddDisposed()
            => _disposedList.Add(DateTimeOffset.UtcNow);

        private void SetLastEnabled(bool val)
            => (_lastEnabledChange, _lastEnabledValue) = (DateTimeOffset.Now, val);
    }

    private class IpcConfiguration
    {
        private readonly DalamudPluginInterface        _pi;
        public readonly  EventSubscriber<string, bool> ModDirectoryChanged;

        private string         _currentConfiguration = string.Empty;
        private string         _lastModDirectory     = string.Empty;
        private bool           _lastModDirectoryValid;
        private DateTimeOffset _lastModDirectoryTime = DateTimeOffset.MinValue;

        public IpcConfiguration(DalamudPluginInterface pi)
        {
            _pi                 = pi;
            ModDirectoryChanged = Ipc.ModDirectoryChanged.Subscriber(pi, UpdateModDirectoryChanged);
        }

        public void Draw()
        {
            using var _ = ImRaii.TreeNode("Configuration");
            if (!_)
                return;

            using var table = ImRaii.Table(string.Empty, 3, ImGuiTableFlags.SizingFixedFit);
            if (!table)
                return;

            DrawIntro(Ipc.GetModDirectory.Label, "Current Mod Directory");
            ImGui.TextUnformatted(Ipc.GetModDirectory.Subscriber(_pi).Invoke());
            DrawIntro(Ipc.ModDirectoryChanged.Label, "Last Mod Directory Change");
            ImGui.TextUnformatted(_lastModDirectoryTime > DateTimeOffset.MinValue
                ? $"{_lastModDirectory} ({(_lastModDirectoryValid ? "Valid" : "Invalid")}) at {_lastModDirectoryTime}"
                : "None");
            DrawIntro(Ipc.GetConfiguration.Label, "Configuration");
            if (ImGui.Button("Get"))
            {
                _currentConfiguration = Ipc.GetConfiguration.Subscriber(_pi).Invoke();
                ImGui.OpenPopup("Config Popup");
            }

            DrawConfigPopup();
        }

        private void DrawConfigPopup()
        {
            ImGui.SetNextWindowSize(ImGuiHelpers.ScaledVector2(500, 500));
            using var popup = ImRaii.Popup("Config Popup");
            if (popup)
            {
                using (var font = ImRaii.PushFont(UiBuilder.MonoFont))
                {
                    ImGuiUtil.TextWrapped(_currentConfiguration);
                }

                if (ImGui.Button("Close", -Vector2.UnitX) || !ImGui.IsWindowFocused())
                    ImGui.CloseCurrentPopup();
            }
        }

        private void UpdateModDirectoryChanged(string path, bool valid)
            => (_lastModDirectory, _lastModDirectoryValid, _lastModDirectoryTime) = (path, valid, DateTimeOffset.Now);
    }

    private class Ui
    {
        private readonly DalamudPluginInterface                              _pi;
        public readonly  EventSubscriber<string>                             PreSettingsDraw;
        public readonly  EventSubscriber<string>                             PostSettingsDraw;
        public readonly  EventSubscriber<ChangedItemType, uint>              Tooltip;
        public readonly  EventSubscriber<MouseButton, ChangedItemType, uint> Click;

        private string         _lastDrawnMod        = string.Empty;
        private DateTimeOffset _lastDrawnModTime    = DateTimeOffset.MinValue;
        private bool           _subscribedToTooltip = false;
        private bool           _subscribedToClick   = false;
        private string         _lastClicked         = string.Empty;
        private string         _lastHovered         = string.Empty;
        private TabType        _selectTab           = TabType.None;
        private string         _modName             = string.Empty;
        private PenumbraApiEc  _ec                  = PenumbraApiEc.Success;

        public Ui(DalamudPluginInterface pi)
        {
            _pi              = pi;
            PreSettingsDraw  = Ipc.PreSettingsDraw.Subscriber(pi, UpdateLastDrawnMod);
            PostSettingsDraw = Ipc.PostSettingsDraw.Subscriber(pi, UpdateLastDrawnMod);
            Tooltip          = Ipc.ChangedItemTooltip.Subscriber(pi, AddedTooltip);
            Click            = Ipc.ChangedItemClick.Subscriber(pi, AddedClick);
        }

        public void Draw()
        {
            using var _ = ImRaii.TreeNode("UI");
            if (!_)
                return;

            using (var combo = ImRaii.Combo("Tab to Open at", _selectTab.ToString()))
            {
                if (combo)
                    foreach (var val in Enum.GetValues<TabType>())
                    {
                        if (ImGui.Selectable(val.ToString(), _selectTab == val))
                            _selectTab = val;
                    }
            }

            ImGui.InputTextWithHint("##openMod", "Mod to Open at...", ref _modName, 256);
            using var table = ImRaii.Table(string.Empty, 3, ImGuiTableFlags.SizingFixedFit);
            if (!table)
                return;

            DrawIntro(Ipc.PostSettingsDraw.Label, "Last Drawn Mod");
            ImGui.TextUnformatted(_lastDrawnMod.Length > 0 ? $"{_lastDrawnMod} at {_lastDrawnModTime}" : "None");

            DrawIntro(Ipc.ChangedItemTooltip.Label, "Add Tooltip");
            if (ImGui.Checkbox("##tooltip", ref _subscribedToTooltip))
            {
                if (_subscribedToTooltip)
                    Tooltip.Enable();
                else
                    Tooltip.Disable();
            }

            ImGui.SameLine();
            ImGui.TextUnformatted(_lastHovered);

            DrawIntro(Ipc.ChangedItemClick.Label, "Subscribe Click");
            if (ImGui.Checkbox("##click", ref _subscribedToClick))
            {
                if (_subscribedToClick)
                    Click.Enable();
                else
                    Click.Disable();
            }

            ImGui.SameLine();
            ImGui.TextUnformatted(_lastClicked);
            DrawIntro(Ipc.OpenMainWindow.Label, "Open Mod Window");
            if (ImGui.Button("Open##window"))
                _ec = Ipc.OpenMainWindow.Subscriber(_pi).Invoke(_selectTab, _modName, _modName);

            ImGui.SameLine();
            ImGui.TextUnformatted(_ec.ToString());

            DrawIntro(Ipc.CloseMainWindow.Label, "Close Mod Window");
            if (ImGui.Button("Close##window"))
                Ipc.CloseMainWindow.Subscriber(_pi).Invoke();
        }

        private void UpdateLastDrawnMod(string name)
            => (_lastDrawnMod, _lastDrawnModTime) = (name, DateTimeOffset.Now);

        private void AddedTooltip(ChangedItemType type, uint id)
        {
            _lastHovered = $"{type} {id} at {DateTime.UtcNow.ToLocalTime().ToString(CultureInfo.CurrentCulture)}";
            ImGui.TextUnformatted("IPC Test Successful");
        }

        private void AddedClick(MouseButton button, ChangedItemType type, uint id)
        {
            _lastClicked = $"{button}-click on {type} {id} at {DateTime.UtcNow.ToLocalTime().ToString(CultureInfo.CurrentCulture)}";
        }
    }

    private class Redrawing
    {
        private readonly DalamudServices              _dalamud;
        public readonly  EventSubscriber<IntPtr, int> Redrawn;

        private string _redrawName        = string.Empty;
        private int    _redrawIndex       = 0;
        private string _lastRedrawnString = "None";

        public Redrawing(DalamudServices dalamud)
        {
            _dalamud = dalamud;
            Redrawn  = Ipc.GameObjectRedrawn.Subscriber(_dalamud.PluginInterface, SetLastRedrawn);
        }

        public void Draw()
        {
            using var _ = ImRaii.TreeNode("Redrawing");
            if (!_)
                return;

            using var table = ImRaii.Table(string.Empty, 3, ImGuiTableFlags.SizingFixedFit);
            if (!table)
                return;

            DrawIntro(Ipc.RedrawObjectByName.Label, "Redraw by Name");
            ImGui.SetNextItemWidth(100 * UiHelpers.Scale);
            ImGui.InputTextWithHint("##redrawName", "Name...", ref _redrawName, 32);
            ImGui.SameLine();
            if (ImGui.Button("Redraw##Name"))
                Ipc.RedrawObjectByName.Subscriber(_dalamud.PluginInterface).Invoke(_redrawName, RedrawType.Redraw);

            DrawIntro(Ipc.RedrawObject.Label, "Redraw Player Character");
            if (ImGui.Button("Redraw##pc") && _dalamud.ClientState.LocalPlayer != null)
                Ipc.RedrawObject.Subscriber(_dalamud.PluginInterface).Invoke(_dalamud.ClientState.LocalPlayer, RedrawType.Redraw);

            DrawIntro(Ipc.RedrawObjectByIndex.Label, "Redraw by Index");
            var tmp = _redrawIndex;
            ImGui.SetNextItemWidth(100 * UiHelpers.Scale);
            if (ImGui.DragInt("##redrawIndex", ref tmp, 0.1f, 0, _dalamud.Objects.Length))
                _redrawIndex = Math.Clamp(tmp, 0, _dalamud.Objects.Length);

            ImGui.SameLine();
            if (ImGui.Button("Redraw##Index"))
                Ipc.RedrawObjectByIndex.Subscriber(_dalamud.PluginInterface).Invoke(_redrawIndex, RedrawType.Redraw);

            DrawIntro(Ipc.RedrawAll.Label, "Redraw All");
            if (ImGui.Button("Redraw##All"))
                Ipc.RedrawAll.Subscriber(_dalamud.PluginInterface).Invoke(RedrawType.Redraw);

            DrawIntro(Ipc.GameObjectRedrawn.Label, "Last Redrawn Object:");
            ImGui.TextUnformatted(_lastRedrawnString);
        }

        private void SetLastRedrawn(IntPtr address, int index)
        {
            if (index < 0
             || index > _dalamud.Objects.Length
             || address == IntPtr.Zero
             || _dalamud.Objects[index]?.Address != address)
                _lastRedrawnString = "Invalid";

            _lastRedrawnString = $"{_dalamud.Objects[index]!.Name} (0x{address:X}, {index})";
        }
    }

    private class GameState
    {
        private readonly DalamudPluginInterface                                  _pi;
        public readonly  EventSubscriber<IntPtr, string, IntPtr, IntPtr, IntPtr> CharacterBaseCreating;
        public readonly  EventSubscriber<IntPtr, string, IntPtr>                 CharacterBaseCreated;
        public readonly  EventSubscriber<IntPtr, string, string>                 GameObjectResourcePathResolved;


        private string         _lastCreatedGameObjectName = string.Empty;
        private IntPtr         _lastCreatedDrawObject     = IntPtr.Zero;
        private DateTimeOffset _lastCreatedGameObjectTime = DateTimeOffset.MaxValue;
        private string         _lastResolvedGamePath      = string.Empty;
        private string         _lastResolvedFullPath      = string.Empty;
        private string         _lastResolvedObject        = string.Empty;
        private DateTimeOffset _lastResolvedGamePathTime  = DateTimeOffset.MaxValue;
        private string         _currentDrawObjectString   = string.Empty;
        private IntPtr         _currentDrawObject         = IntPtr.Zero;
        private int            _currentCutsceneActor      = 0;

        public GameState(DalamudPluginInterface pi)
        {
            _pi                            = pi;
            CharacterBaseCreating          = Ipc.CreatingCharacterBase.Subscriber(pi, UpdateLastCreated);
            CharacterBaseCreated           = Ipc.CreatedCharacterBase.Subscriber(pi, UpdateLastCreated2);
            GameObjectResourcePathResolved = Ipc.GameObjectResourcePathResolved.Subscriber(pi, UpdateGameObjectResourcePath);
        }

        public void Draw()
        {
            using var _ = ImRaii.TreeNode("Game State");
            if (!_)
                return;

            if (ImGui.InputTextWithHint("##drawObject", "Draw Object Address..", ref _currentDrawObjectString, 16,
                    ImGuiInputTextFlags.CharsHexadecimal))
                _currentDrawObject = IntPtr.TryParse(_currentDrawObjectString, NumberStyles.HexNumber, CultureInfo.InvariantCulture,
                    out var tmp)
                    ? tmp
                    : IntPtr.Zero;

            ImGui.InputInt("Cutscene Actor", ref _currentCutsceneActor, 0);
            using var table = ImRaii.Table(string.Empty, 3, ImGuiTableFlags.SizingFixedFit);
            if (!table)
                return;

            DrawIntro(Ipc.GetDrawObjectInfo.Label, "Draw Object Info");
            if (_currentDrawObject == IntPtr.Zero)
            {
                ImGui.TextUnformatted("Invalid");
            }
            else
            {
                var (ptr, collection) = Ipc.GetDrawObjectInfo.Subscriber(_pi).Invoke(_currentDrawObject);
                ImGui.TextUnformatted(ptr == IntPtr.Zero ? $"No Actor Associated, {collection}" : $"{ptr:X}, {collection}");
            }

            DrawIntro(Ipc.GetCutsceneParentIndex.Label, "Cutscene Parent");
            ImGui.TextUnformatted(Ipc.GetCutsceneParentIndex.Subscriber(_pi).Invoke(_currentCutsceneActor).ToString());

            DrawIntro(Ipc.CreatingCharacterBase.Label, "Last Drawobject created");
            if (_lastCreatedGameObjectTime < DateTimeOffset.Now)
                ImGui.TextUnformatted(_lastCreatedDrawObject != IntPtr.Zero
                    ? $"0x{_lastCreatedDrawObject:X} for <{_lastCreatedGameObjectName}> at {_lastCreatedGameObjectTime}"
                    : $"NULL for <{_lastCreatedGameObjectName}> at {_lastCreatedGameObjectTime}");

            DrawIntro(Ipc.GameObjectResourcePathResolved.Label, "Last GamePath resolved");
            if (_lastResolvedGamePathTime < DateTimeOffset.Now)
                ImGui.TextUnformatted(
                    $"{_lastResolvedGamePath} -> {_lastResolvedFullPath} for <{_lastResolvedObject}> at {_lastResolvedGamePathTime}");
        }

        private void UpdateLastCreated(IntPtr gameObject, string _, IntPtr _2, IntPtr _3, IntPtr _4)
        {
            _lastCreatedGameObjectName = GetObjectName(gameObject);
            _lastCreatedGameObjectTime = DateTimeOffset.Now;
            _lastCreatedDrawObject     = IntPtr.Zero;
        }

        private void UpdateLastCreated2(IntPtr gameObject, string _, IntPtr drawObject)
        {
            _lastCreatedGameObjectName = GetObjectName(gameObject);
            _lastCreatedGameObjectTime = DateTimeOffset.Now;
            _lastCreatedDrawObject     = drawObject;
        }

        private void UpdateGameObjectResourcePath(IntPtr gameObject, string gamePath, string fullPath)
        {
            _lastResolvedObject       = GetObjectName(gameObject);
            _lastResolvedGamePath     = gamePath;
            _lastResolvedFullPath     = fullPath;
            _lastResolvedGamePathTime = DateTimeOffset.Now;
        }

        private static unsafe string GetObjectName(IntPtr gameObject)
        {
            var obj  = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)gameObject;
            var name = obj != null ? obj->Name : null;
            return name != null && *name != 0 ? new ByteString(name).ToString() : "Unknown";
        }
    }

    private class Resolve
    {
        private readonly DalamudPluginInterface _pi;

        private string _currentResolvePath      = string.Empty;
        private string _currentResolveCharacter = string.Empty;
        private string _currentReversePath      = string.Empty;
        private int    _currentReverseIdx       = 0;

        public Resolve(DalamudPluginInterface pi)
            => _pi = pi;

        public void Draw()
        {
            using var _ = ImRaii.TreeNode("Resolving");
            if (!_)
                return;

            ImGui.InputTextWithHint("##resolvePath",      "Resolve this game path...", ref _currentResolvePath, Utf8GamePath.MaxGamePathLength);
            ImGui.InputTextWithHint("##resolveCharacter", "Character Name (leave blank for default)...", ref _currentResolveCharacter, 32);
            ImGui.InputTextWithHint("##resolveInversePath", "Reverse-resolve this path...", ref _currentReversePath,
                Utf8GamePath.MaxGamePathLength);
            ImGui.InputInt("##resolveIdx", ref _currentReverseIdx, 0, 0);
            using var table = ImRaii.Table(string.Empty, 3, ImGuiTableFlags.SizingFixedFit);
            if (!table)
                return;

            DrawIntro(Ipc.ResolveDefaultPath.Label, "Default Collection Resolve");
            if (_currentResolvePath.Length != 0)
                ImGui.TextUnformatted(Ipc.ResolveDefaultPath.Subscriber(_pi).Invoke(_currentResolvePath));

            DrawIntro(Ipc.ResolveInterfacePath.Label, "Interface Collection Resolve");
            if (_currentResolvePath.Length != 0)
                ImGui.TextUnformatted(Ipc.ResolveInterfacePath.Subscriber(_pi).Invoke(_currentResolvePath));

            DrawIntro(Ipc.ResolvePlayerPath.Label, "Player Collection Resolve");
            if (_currentResolvePath.Length != 0)
                ImGui.TextUnformatted(Ipc.ResolvePlayerPath.Subscriber(_pi).Invoke(_currentResolvePath));

            DrawIntro(Ipc.ResolveCharacterPath.Label, "Character Collection Resolve");
            if (_currentResolvePath.Length != 0 && _currentResolveCharacter.Length != 0)
                ImGui.TextUnformatted(Ipc.ResolveCharacterPath.Subscriber(_pi).Invoke(_currentResolvePath, _currentResolveCharacter));

            DrawIntro(Ipc.ResolveGameObjectPath.Label, "Game Object Collection Resolve");
            if (_currentResolvePath.Length != 0)
                ImGui.TextUnformatted(Ipc.ResolveGameObjectPath.Subscriber(_pi).Invoke(_currentResolvePath, _currentReverseIdx));

            DrawIntro(Ipc.ReverseResolvePath.Label, "Reversed Game Paths");
            if (_currentReversePath.Length > 0)
            {
                var list = Ipc.ReverseResolvePath.Subscriber(_pi).Invoke(_currentReversePath, _currentResolveCharacter);
                if (list.Length > 0)
                {
                    ImGui.TextUnformatted(list[0]);
                    if (list.Length > 1 && ImGui.IsItemHovered())
                        ImGui.SetTooltip(string.Join("\n", list.Skip(1)));
                }
            }

            DrawIntro(Ipc.ReverseResolvePlayerPath.Label, "Reversed Game Paths (Player)");
            if (_currentReversePath.Length > 0)
            {
                var list = Ipc.ReverseResolvePlayerPath.Subscriber(_pi).Invoke(_currentReversePath);
                if (list.Length > 0)
                {
                    ImGui.TextUnformatted(list[0]);
                    if (list.Length > 1 && ImGui.IsItemHovered())
                        ImGui.SetTooltip(string.Join("\n", list.Skip(1)));
                }
            }

            DrawIntro(Ipc.ReverseResolveGameObjectPath.Label, "Reversed Game Paths (Game Object)");
            if (_currentReversePath.Length > 0)
            {
                var list = Ipc.ReverseResolveGameObjectPath.Subscriber(_pi).Invoke(_currentReversePath, _currentReverseIdx);
                if (list.Length > 0)
                {
                    ImGui.TextUnformatted(list[0]);
                    if (list.Length > 1 && ImGui.IsItemHovered())
                        ImGui.SetTooltip(string.Join("\n", list.Skip(1)));
                }
            }

            DrawIntro(Ipc.ResolvePlayerPaths.Label, "Resolved Paths (Player)");
            if (_currentResolvePath.Length > 0 || _currentReversePath.Length > 0)
            {
                var forwardArray = _currentResolvePath.Length > 0
                    ? new[]
                    {
                        _currentResolvePath,
                    }
                    : Array.Empty<string>();
                var reverseArray = _currentReversePath.Length > 0
                    ? new[]
                    {
                        _currentReversePath,
                    }
                    : Array.Empty<string>();
                var ret  = Ipc.ResolvePlayerPaths.Subscriber(_pi).Invoke(forwardArray, reverseArray);
                var text = string.Empty;
                if (ret.Item1.Length > 0)
                {
                    if (ret.Item2.Length > 0)
                        text = $"Forward: {ret.Item1[0]} | Reverse: {string.Join("; ", ret.Item2[0])}.";
                    else
                        text = $"Forward: {ret.Item1[0]}.";
                }
                else if (ret.Item2.Length > 0)
                {
                    text = $"Reverse: {string.Join("; ", ret.Item2[0])}.";
                }

                ImGui.TextUnformatted(text);
            }
        }
    }

    private class Collections
    {
        private readonly DalamudPluginInterface _pi;

        private int               _objectIdx      = 0;
        private string            _collectionName = string.Empty;
        private bool              _allowCreation  = true;
        private bool              _allowDeletion  = true;
        private ApiCollectionType _type           = ApiCollectionType.Current;

        private string                               _characterCollectionName = string.Empty;
        private IList<string>                        _collections             = new List<string>();
        private string                               _changedItemCollection   = string.Empty;
        private IReadOnlyDictionary<string, object?> _changedItems            = new Dictionary<string, object?>();
        private PenumbraApiEc                        _returnCode              = PenumbraApiEc.Success;
        private string?                              _oldCollection           = null;

        public Collections(DalamudPluginInterface pi)
            => _pi = pi;

        public void Draw()
        {
            using var _ = ImRaii.TreeNode("Collections");
            if (!_)
                return;

            ImGuiUtil.GenericEnumCombo("Collection Type", 200, _type, out _type, t => ((CollectionType)t).ToName());
            ImGui.InputInt("Object Index##Collections", ref _objectIdx, 0, 0);
            ImGui.InputText("Collection Name##Collections", ref _collectionName, 64);
            ImGui.Checkbox("Allow Assignment Creation", ref _allowCreation);
            ImGui.SameLine();
            ImGui.Checkbox("Allow Assignment Deletion", ref _allowDeletion);

            using var table = ImRaii.Table(string.Empty, 3, ImGuiTableFlags.SizingFixedFit);
            if (!table)
                return;

            DrawIntro("Last Return Code", _returnCode.ToString());
            if (_oldCollection != null)
                ImGui.TextUnformatted(_oldCollection.Length == 0 ? "Created" : _oldCollection);

            DrawIntro(Ipc.GetCurrentCollectionName.Label, "Current Collection");
            ImGui.TextUnformatted(Ipc.GetCurrentCollectionName.Subscriber(_pi).Invoke());
            DrawIntro(Ipc.GetDefaultCollectionName.Label, "Default Collection");
            ImGui.TextUnformatted(Ipc.GetDefaultCollectionName.Subscriber(_pi).Invoke());
            DrawIntro(Ipc.GetInterfaceCollectionName.Label, "Interface Collection");
            ImGui.TextUnformatted(Ipc.GetInterfaceCollectionName.Subscriber(_pi).Invoke());
            DrawIntro(Ipc.GetCharacterCollectionName.Label, "Character");
            ImGui.SetNextItemWidth(200 * UiHelpers.Scale);
            ImGui.InputTextWithHint("##characterCollectionName", "Character Name...", ref _characterCollectionName, 64);
            var (c, s) = Ipc.GetCharacterCollectionName.Subscriber(_pi).Invoke(_characterCollectionName);
            ImGui.SameLine();
            ImGui.TextUnformatted($"{c}, {(s ? "Custom" : "Default")}");

            DrawIntro(Ipc.GetCollections.Label, "Collections");
            if (ImGui.Button("Get##Collections"))
            {
                _collections = Ipc.GetCollections.Subscriber(_pi).Invoke();
                ImGui.OpenPopup("Collections");
            }

            DrawIntro(Ipc.GetCollectionForType.Label, "Get Special Collection");
            var name = Ipc.GetCollectionForType.Subscriber(_pi).Invoke(_type);
            ImGui.TextUnformatted(name.Length == 0 ? "Unassigned" : name);
            DrawIntro(Ipc.SetCollectionForType.Label, "Set Special Collection");
            if (ImGui.Button("Set##TypeCollection"))
                (_returnCode, _oldCollection) =
                    Ipc.SetCollectionForType.Subscriber(_pi).Invoke(_type, _collectionName, _allowCreation, _allowDeletion);

            DrawIntro(Ipc.GetCollectionForObject.Label, "Get Object Collection");
            (var valid, var individual, name) = Ipc.GetCollectionForObject.Subscriber(_pi).Invoke(_objectIdx);
            ImGui.TextUnformatted(
                $"{(valid ? "Valid" : "Invalid")} Object, {(name.Length == 0 ? "Unassigned" : name)}{(individual ? " (Individual Assignment)" : string.Empty)}");
            DrawIntro(Ipc.SetCollectionForObject.Label, "Set Object Collection");
            if (ImGui.Button("Set##ObjectCollection"))
                (_returnCode, _oldCollection) = Ipc.SetCollectionForObject.Subscriber(_pi)
                    .Invoke(_objectIdx, _collectionName, _allowCreation, _allowDeletion);

            if (_returnCode == PenumbraApiEc.NothingChanged && _oldCollection.IsNullOrEmpty())
                _oldCollection = null;

            DrawIntro(Ipc.GetChangedItems.Label, "Changed Item List");
            ImGui.SetNextItemWidth(200 * UiHelpers.Scale);
            ImGui.InputTextWithHint("##changedCollection", "Collection Name...", ref _changedItemCollection, 64);
            ImGui.SameLine();
            if (ImGui.Button("Get"))
            {
                _changedItems = Ipc.GetChangedItems.Subscriber(_pi).Invoke(_changedItemCollection);
                ImGui.OpenPopup("Changed Item List");
            }

            DrawChangedItemPopup();
            DrawCollectionPopup();
        }

        private void DrawChangedItemPopup()
        {
            ImGui.SetNextWindowSize(ImGuiHelpers.ScaledVector2(500, 500));
            using var p = ImRaii.Popup("Changed Item List");
            if (!p)
                return;

            foreach (var item in _changedItems)
                ImGui.TextUnformatted(item.Key);

            if (ImGui.Button("Close", -Vector2.UnitX) || !ImGui.IsWindowFocused())
                ImGui.CloseCurrentPopup();
        }

        private void DrawCollectionPopup()
        {
            ImGui.SetNextWindowSize(ImGuiHelpers.ScaledVector2(500, 500));
            using var p = ImRaii.Popup("Collections");
            if (!p)
                return;

            foreach (var collection in _collections)
                ImGui.TextUnformatted(collection);

            if (ImGui.Button("Close", -Vector2.UnitX) || !ImGui.IsWindowFocused())
                ImGui.CloseCurrentPopup();
        }
    }

    private class Meta
    {
        private readonly DalamudPluginInterface _pi;

        private string _characterName   = string.Empty;
        private int    _gameObjectIndex = 0;

        public Meta(DalamudPluginInterface pi)
            => _pi = pi;

        public void Draw()
        {
            using var _ = ImRaii.TreeNode("Meta");
            if (!_)
                return;

            ImGui.InputTextWithHint("##characterName", "Character Name...", ref _characterName, 64);
            ImGui.InputInt("##metaIdx", ref _gameObjectIndex, 0, 0);
            using var table = ImRaii.Table(string.Empty, 3, ImGuiTableFlags.SizingFixedFit);
            if (!table)
                return;

            DrawIntro(Ipc.GetMetaManipulations.Label, "Meta Manipulations");
            if (ImGui.Button("Copy to Clipboard"))
            {
                var base64 = Ipc.GetMetaManipulations.Subscriber(_pi).Invoke(_characterName);
                ImGui.SetClipboardText(base64);
            }

            DrawIntro(Ipc.GetPlayerMetaManipulations.Label, "Player Meta Manipulations");
            if (ImGui.Button("Copy to Clipboard##Player"))
            {
                var base64 = Ipc.GetPlayerMetaManipulations.Subscriber(_pi).Invoke();
                ImGui.SetClipboardText(base64);
            }

            DrawIntro(Ipc.GetGameObjectMetaManipulations.Label, "Game Object Manipulations");
            if (ImGui.Button("Copy to Clipboard##GameObject"))
            {
                var base64 = Ipc.GetGameObjectMetaManipulations.Subscriber(_pi).Invoke(_gameObjectIndex);
                ImGui.SetClipboardText(base64);
            }
        }
    }

    private class Mods
    {
        private readonly DalamudPluginInterface _pi;

        private string                  _modDirectory   = string.Empty;
        private string                  _modName        = string.Empty;
        private string                  _pathInput      = string.Empty;
        private string                  _newInstallPath = string.Empty;
        private PenumbraApiEc           _lastReloadEc;
        private PenumbraApiEc           _lastAddEc;
        private PenumbraApiEc           _lastDeleteEc;
        private PenumbraApiEc           _lastSetPathEc;
        private PenumbraApiEc           _lastInstallEc;
        private IList<(string, string)> _mods = new List<(string, string)>();

        public readonly EventSubscriber<string>         DeleteSubscriber;
        public readonly EventSubscriber<string>         AddSubscriber;
        public readonly EventSubscriber<string, string> MoveSubscriber;

        private DateTimeOffset _lastDeletedModTime = DateTimeOffset.UnixEpoch;
        private string         _lastDeletedMod     = string.Empty;
        private DateTimeOffset _lastAddedModTime   = DateTimeOffset.UnixEpoch;
        private string         _lastAddedMod       = string.Empty;
        private DateTimeOffset _lastMovedModTime   = DateTimeOffset.UnixEpoch;
        private string         _lastMovedModFrom   = string.Empty;
        private string         _lastMovedModTo     = string.Empty;

        public Mods(DalamudPluginInterface pi)
        {
            _pi = pi;
            DeleteSubscriber = Ipc.ModDeleted.Subscriber(pi, s =>
            {
                _lastDeletedModTime = DateTimeOffset.UtcNow;
                _lastDeletedMod     = s;
            });
            AddSubscriber = Ipc.ModAdded.Subscriber(pi, s =>
            {
                _lastAddedModTime = DateTimeOffset.UtcNow;
                _lastAddedMod     = s;
            });
            MoveSubscriber = Ipc.ModMoved.Subscriber(pi, (s1, s2) =>
            {
                _lastMovedModTime = DateTimeOffset.UtcNow;
                _lastMovedModFrom = s1;
                _lastMovedModTo   = s2;
            });
        }

        public void Draw()
        {
            using var _ = ImRaii.TreeNode("Mods");
            if (!_)
                return;

            ImGui.InputTextWithHint("##install", "Install File Path...",  ref _newInstallPath, 100);
            ImGui.InputTextWithHint("##modDir",  "Mod Directory Name...", ref _modDirectory,   100);
            ImGui.InputTextWithHint("##modName", "Mod Name...",           ref _modName,        100);
            ImGui.InputTextWithHint("##path",    "New Path...",           ref _pathInput,      100);
            using var table = ImRaii.Table(string.Empty, 3, ImGuiTableFlags.SizingFixedFit);
            if (!table)
                return;

            DrawIntro(Ipc.GetMods.Label, "Mods");
            if (ImGui.Button("Get##Mods"))
            {
                _mods = Ipc.GetMods.Subscriber(_pi).Invoke();
                ImGui.OpenPopup("Mods");
            }

            DrawIntro(Ipc.ReloadMod.Label, "Reload Mod");
            if (ImGui.Button("Reload"))
                _lastReloadEc = Ipc.ReloadMod.Subscriber(_pi).Invoke(_modDirectory, _modName);

            ImGui.SameLine();
            ImGui.TextUnformatted(_lastReloadEc.ToString());

            DrawIntro(Ipc.InstallMod.Label, "Install Mod");
            if (ImGui.Button("Install"))
                _lastInstallEc = Ipc.InstallMod.Subscriber(_pi).Invoke(_newInstallPath);

            ImGui.SameLine();
            ImGui.TextUnformatted(_lastInstallEc.ToString());

            DrawIntro(Ipc.AddMod.Label, "Add Mod");
            if (ImGui.Button("Add"))
                _lastAddEc = Ipc.AddMod.Subscriber(_pi).Invoke(_modDirectory);

            ImGui.SameLine();
            ImGui.TextUnformatted(_lastAddEc.ToString());

            DrawIntro(Ipc.DeleteMod.Label, "Delete Mod");
            if (ImGui.Button("Delete"))
                _lastDeleteEc = Ipc.DeleteMod.Subscriber(_pi).Invoke(_modDirectory, _modName);

            ImGui.SameLine();
            ImGui.TextUnformatted(_lastDeleteEc.ToString());

            DrawIntro(Ipc.GetModPath.Label, "Current Path");
            var (ec, path, def) = Ipc.GetModPath.Subscriber(_pi).Invoke(_modDirectory, _modName);
            ImGui.TextUnformatted($"{path} ({(def ? "Custom" : "Default")}) [{ec}]");

            DrawIntro(Ipc.SetModPath.Label, "Set Path");
            if (ImGui.Button("Set"))
                _lastSetPathEc = Ipc.SetModPath.Subscriber(_pi).Invoke(_modDirectory, _modName, _pathInput);

            ImGui.SameLine();
            ImGui.TextUnformatted(_lastSetPathEc.ToString());

            DrawIntro(Ipc.ModDeleted.Label, "Last Mod Deleted");
            if (_lastDeletedModTime > DateTimeOffset.UnixEpoch)
                ImGui.TextUnformatted($"{_lastDeletedMod} at {_lastDeletedModTime}");

            DrawIntro(Ipc.ModAdded.Label, "Last Mod Added");
            if (_lastAddedModTime > DateTimeOffset.UnixEpoch)
                ImGui.TextUnformatted($"{_lastAddedMod} at {_lastAddedModTime}");

            DrawIntro(Ipc.ModMoved.Label, "Last Mod Moved");
            if (_lastMovedModTime > DateTimeOffset.UnixEpoch)
                ImGui.TextUnformatted($"{_lastMovedModFrom} -> {_lastMovedModTo} at {_lastMovedModTime}");

            DrawModsPopup();
        }

        private void DrawModsPopup()
        {
            ImGui.SetNextWindowSize(ImGuiHelpers.ScaledVector2(500, 500));
            using var p = ImRaii.Popup("Mods");
            if (!p)
                return;

            foreach (var (modDir, modName) in _mods)
                ImGui.TextUnformatted($"{modDir}: {modName}");

            if (ImGui.Button("Close", -Vector2.UnitX) || !ImGui.IsWindowFocused())
                ImGui.CloseCurrentPopup();
        }
    }

    private class ModSettings
    {
        private readonly DalamudPluginInterface                                  _pi;
        public readonly  EventSubscriber<ModSettingChange, string, string, bool> SettingChanged;

        private PenumbraApiEc    _lastSettingsError = PenumbraApiEc.Success;
        private ModSettingChange _lastSettingChangeType;
        private string           _lastSettingChangeCollection = string.Empty;
        private string           _lastSettingChangeMod        = string.Empty;
        private bool             _lastSettingChangeInherited;
        private DateTimeOffset   _lastSettingChange;

        private string                                           _settingsModDirectory     = string.Empty;
        private string                                           _settingsModName          = string.Empty;
        private string                                           _settingsCollection       = string.Empty;
        private bool                                             _settingsAllowInheritance = true;
        private bool                                             _settingsInherit          = false;
        private bool                                             _settingsEnabled          = false;
        private int                                              _settingsPriority         = 0;
        private IDictionary<string, (IList<string>, GroupType)>? _availableSettings;
        private IDictionary<string, IList<string>>?              _currentSettings = null;

        public ModSettings(DalamudPluginInterface pi)
        {
            _pi            = pi;
            SettingChanged = Ipc.ModSettingChanged.Subscriber(pi, UpdateLastModSetting);
        }

        public void Draw()
        {
            using var _ = ImRaii.TreeNode("Mod Settings");
            if (!_)
                return;

            ImGui.InputTextWithHint("##settingsDir",        "Mod Directory Name...", ref _settingsModDirectory, 100);
            ImGui.InputTextWithHint("##settingsName",       "Mod Name...",           ref _settingsModName,      100);
            ImGui.InputTextWithHint("##settingsCollection", "Collection...",         ref _settingsCollection,   100);
            ImGui.Checkbox("Allow Inheritance", ref _settingsAllowInheritance);

            using var table = ImRaii.Table(string.Empty, 3, ImGuiTableFlags.SizingFixedFit);
            if (!table)
                return;

            DrawIntro("Last Error",                _lastSettingsError.ToString());
            DrawIntro(Ipc.ModSettingChanged.Label, "Last Mod Setting Changed");
            ImGui.TextUnformatted(_lastSettingChangeMod.Length > 0
                ? $"{_lastSettingChangeType} of {_lastSettingChangeMod} in {_lastSettingChangeCollection}{(_lastSettingChangeInherited ? " (Inherited)" : string.Empty)} at {_lastSettingChange}"
                : "None");
            DrawIntro(Ipc.GetAvailableModSettings.Label, "Get Available Settings");
            if (ImGui.Button("Get##Available"))
            {
                _availableSettings = Ipc.GetAvailableModSettings.Subscriber(_pi).Invoke(_settingsModDirectory, _settingsModName);
                _lastSettingsError = _availableSettings == null ? PenumbraApiEc.ModMissing : PenumbraApiEc.Success;
            }


            DrawIntro(Ipc.GetCurrentModSettings.Label, "Get Current Settings");
            if (ImGui.Button("Get##Current"))
            {
                var ret = Ipc.GetCurrentModSettings.Subscriber(_pi)
                    .Invoke(_settingsCollection, _settingsModDirectory, _settingsModName, _settingsAllowInheritance);
                _lastSettingsError = ret.Item1;
                if (ret.Item1 == PenumbraApiEc.Success)
                {
                    _settingsEnabled  = ret.Item2?.Item1 ?? false;
                    _settingsInherit  = ret.Item2?.Item4 ?? false;
                    _settingsPriority = ret.Item2?.Item2 ?? 0;
                    _currentSettings  = ret.Item2?.Item3;
                }
                else
                {
                    _currentSettings = null;
                }
            }

            DrawIntro(Ipc.TryInheritMod.Label, "Inherit Mod");
            ImGui.Checkbox("##inherit", ref _settingsInherit);
            ImGui.SameLine();
            if (ImGui.Button("Set##Inherit"))
                _lastSettingsError = Ipc.TryInheritMod.Subscriber(_pi)
                    .Invoke(_settingsCollection, _settingsModDirectory, _settingsModName, _settingsInherit);

            DrawIntro(Ipc.TrySetMod.Label, "Set Enabled");
            ImGui.Checkbox("##enabled", ref _settingsEnabled);
            ImGui.SameLine();
            if (ImGui.Button("Set##Enabled"))
                _lastSettingsError = Ipc.TrySetMod.Subscriber(_pi)
                    .Invoke(_settingsCollection, _settingsModDirectory, _settingsModName, _settingsEnabled);

            DrawIntro(Ipc.TrySetModPriority.Label, "Set Priority");
            ImGui.SetNextItemWidth(200 * UiHelpers.Scale);
            ImGui.DragInt("##Priority", ref _settingsPriority);
            ImGui.SameLine();
            if (ImGui.Button("Set##Priority"))
                _lastSettingsError = Ipc.TrySetModPriority.Subscriber(_pi)
                    .Invoke(_settingsCollection, _settingsModDirectory, _settingsModName, _settingsPriority);

            DrawIntro(Ipc.CopyModSettings.Label, "Copy Mod Settings");
            if (ImGui.Button("Copy Settings"))
                _lastSettingsError = Ipc.CopyModSettings.Subscriber(_pi).Invoke(_settingsCollection, _settingsModDirectory, _settingsModName);

            ImGuiUtil.HoverTooltip("Copy settings from Mod Directory Name to Mod Name (as directory) in collection.");

            DrawIntro(Ipc.TrySetModSetting.Label, "Set Setting(s)");
            if (_availableSettings == null)
                return;

            foreach (var (group, (list, type)) in _availableSettings)
            {
                using var     id      = ImRaii.PushId(group);
                var           preview = list.Count > 0 ? list[0] : string.Empty;
                IList<string> current;
                if (_currentSettings != null && _currentSettings.TryGetValue(group, out current!) && current.Count > 0)
                {
                    preview = current[0];
                }
                else
                {
                    current = new List<string>();
                    if (_currentSettings != null)
                        _currentSettings[group] = current;
                }

                ImGui.SetNextItemWidth(200 * UiHelpers.Scale);
                using (var c = ImRaii.Combo("##group", preview))
                {
                    if (c)
                        foreach (var s in list)
                        {
                            var contained = current.Contains(s);
                            if (ImGui.Checkbox(s, ref contained))
                            {
                                if (contained)
                                    current.Add(s);
                                else
                                    current.Remove(s);
                            }
                        }
                }

                ImGui.SameLine();
                if (ImGui.Button("Set##setting"))
                {
                    if (type == GroupType.Single)
                        _lastSettingsError = Ipc.TrySetModSetting.Subscriber(_pi).Invoke(_settingsCollection,
                            _settingsModDirectory, _settingsModName, group, current.Count > 0 ? current[0] : string.Empty);
                    else
                        _lastSettingsError = Ipc.TrySetModSettings.Subscriber(_pi).Invoke(_settingsCollection,
                            _settingsModDirectory, _settingsModName, group, current.ToArray());
                }

                ImGui.SameLine();
                ImGui.TextUnformatted(group);
            }
        }

        private void UpdateLastModSetting(ModSettingChange type, string collection, string mod, bool inherited)
        {
            _lastSettingChangeType       = type;
            _lastSettingChangeCollection = collection;
            _lastSettingChangeMod        = mod;
            _lastSettingChangeInherited  = inherited;
            _lastSettingChange           = DateTimeOffset.Now;
        }
    }

    private class Editing
    {
        private readonly DalamudPluginInterface _pi;

        private string _inputPath   = string.Empty;
        private string _inputPath2  = string.Empty;
        private string _outputPath  = string.Empty;
        private string _outputPath2 = string.Empty;

        private TextureType _typeSelector;
        private bool        _mipMaps = true;

        private Task? _task1;
        private Task? _task2;

        public Editing(DalamudPluginInterface pi)
            => _pi = pi;

        public void Draw()
        {
            using var _ = ImRaii.TreeNode("Editing");
            if (!_)
                return;

            ImGui.InputTextWithHint("##inputPath",   "Input Texture Path...",    ref _inputPath,   256);
            ImGui.InputTextWithHint("##outputPath",  "Output Texture Path...",   ref _outputPath,  256);
            ImGui.InputTextWithHint("##inputPath2",  "Input Texture Path 2...",  ref _inputPath2,  256);
            ImGui.InputTextWithHint("##outputPath2", "Output Texture Path 2...", ref _outputPath2, 256);
            TypeCombo();
            ImGui.Checkbox("Add MipMaps", ref _mipMaps);

            using var table = ImRaii.Table("...", 3, ImGuiTableFlags.SizingFixedFit);
            if (!table)
                return;

            DrawIntro(Ipc.ConvertTextureFile.Label, "Convert Texture 1");
            if (ImGuiUtil.DrawDisabledButton("Save 1", Vector2.Zero, string.Empty, _task1 is { IsCompleted: false }))
                _task1 = Ipc.ConvertTextureFile.Subscriber(_pi).Invoke(_inputPath, _outputPath, _typeSelector, _mipMaps);
            ImGui.SameLine();
            ImGui.TextUnformatted(_task1 == null ? "Not Initiated" : _task1.Status.ToString());
            if (ImGui.IsItemHovered() && _task1?.Status == TaskStatus.Faulted)
                ImGui.SetTooltip(_task1.Exception?.ToString());

            DrawIntro(Ipc.ConvertTextureFile.Label, "Convert Texture 2");
            if (ImGuiUtil.DrawDisabledButton("Save 2", Vector2.Zero, string.Empty, _task2 is { IsCompleted: false }))
                _task2 = Ipc.ConvertTextureFile.Subscriber(_pi).Invoke(_inputPath2, _outputPath2, _typeSelector, _mipMaps);
            ImGui.SameLine();
            ImGui.TextUnformatted(_task2 == null ? "Not Initiated" : _task2.Status.ToString());
            if (ImGui.IsItemHovered() && _task2?.Status == TaskStatus.Faulted)
                ImGui.SetTooltip(_task2.Exception?.ToString());
        }

        private void TypeCombo()
        {
            using var combo = ImRaii.Combo("Convert To", _typeSelector.ToString());
            if (!combo)
                return;

            foreach (var value in Enum.GetValues<TextureType>())
            {
                if (ImGui.Selectable(value.ToString(), _typeSelector == value))
                    _typeSelector = value;
            }
        }
    }

    private class Temporary
    {
        private readonly DalamudPluginInterface _pi;
        private readonly ModManager             _modManager;
        private readonly CollectionManager      _collections;
        private readonly TempModManager         _tempMods;
        private readonly TempCollectionManager  _tempCollections;
        private readonly SaveService            _saveService;
        private readonly Configuration          _config;

        public Temporary(DalamudPluginInterface pi, ModManager modManager, CollectionManager collections, TempModManager tempMods,
            TempCollectionManager tempCollections, SaveService saveService, Configuration config)
        {
            _pi              = pi;
            _modManager      = modManager;
            _collections     = collections;
            _tempMods        = tempMods;
            _tempCollections = tempCollections;
            _saveService     = saveService;
            _config          = config;
        }

        public string LastCreatedCollectionName = string.Empty;

        private string        _tempCollectionName = string.Empty;
        private string        _tempCharacterName  = string.Empty;
        private string        _tempModName        = string.Empty;
        private string        _tempGamePath       = "test/game/path.mtrl";
        private string        _tempFilePath       = "test/success.mtrl";
        private string        _tempManipulation   = string.Empty;
        private PenumbraApiEc _lastTempError;
        private int           _tempActorIndex = 0;
        private bool          _forceOverwrite;

        public void Draw()
        {
            using var _ = ImRaii.TreeNode("Temporary");
            if (!_)
                return;

            ImGui.InputTextWithHint("##tempCollection",     "Collection Name...",      ref _tempCollectionName, 128);
            ImGui.InputTextWithHint("##tempCollectionChar", "Collection Character...", ref _tempCharacterName,  32);
            ImGui.InputInt("##tempActorIndex", ref _tempActorIndex, 0, 0);
            ImGui.InputTextWithHint("##tempMod",   "Temporary Mod Name...",         ref _tempModName,      32);
            ImGui.InputTextWithHint("##tempGame",  "Game Path...",                  ref _tempGamePath,     256);
            ImGui.InputTextWithHint("##tempFile",  "File Path...",                  ref _tempFilePath,     256);
            ImGui.InputTextWithHint("##tempManip", "Manipulation Base64 String...", ref _tempManipulation, 256);
            ImGui.Checkbox("Force Character Collection Overwrite", ref _forceOverwrite);


            using var table = ImRaii.Table(string.Empty, 3, ImGuiTableFlags.SizingFixedFit);
            if (!table)
                return;

            DrawIntro("Last Error",                        _lastTempError.ToString());
            DrawIntro("Last Created Collection",           LastCreatedCollectionName);
            DrawIntro(Ipc.CreateTemporaryCollection.Label, "Create Temporary Collection");
#pragma warning disable 0612
            if (ImGui.Button("Create##Collection"))
                (_lastTempError, LastCreatedCollectionName) = Ipc.CreateTemporaryCollection.Subscriber(_pi)
                    .Invoke(_tempCollectionName, _tempCharacterName, _forceOverwrite);

            DrawIntro(Ipc.CreateNamedTemporaryCollection.Label, "Create Named Temporary Collection");
            if (ImGui.Button("Create##NamedCollection"))
                _lastTempError = Ipc.CreateNamedTemporaryCollection.Subscriber(_pi).Invoke(_tempCollectionName);

            DrawIntro(Ipc.RemoveTemporaryCollection.Label, "Remove Temporary Collection from Character");
            if (ImGui.Button("Delete##Collection"))
                _lastTempError = Ipc.RemoveTemporaryCollection.Subscriber(_pi).Invoke(_tempCharacterName);
#pragma warning restore 0612
            DrawIntro(Ipc.RemoveTemporaryCollectionByName.Label, "Remove Temporary Collection");
            if (ImGui.Button("Delete##NamedCollection"))
                _lastTempError = Ipc.RemoveTemporaryCollectionByName.Subscriber(_pi).Invoke(_tempCollectionName);

            DrawIntro(Ipc.AssignTemporaryCollection.Label, "Assign Temporary Collection");
            if (ImGui.Button("Assign##NamedCollection"))
                _lastTempError = Ipc.AssignTemporaryCollection.Subscriber(_pi).Invoke(_tempCollectionName, _tempActorIndex, _forceOverwrite);

            DrawIntro(Ipc.AddTemporaryMod.Label, "Add Temporary Mod to specific Collection");
            if (ImGui.Button("Add##Mod"))
                _lastTempError = Ipc.AddTemporaryMod.Subscriber(_pi).Invoke(_tempModName, _tempCollectionName,
                    new Dictionary<string, string> { { _tempGamePath, _tempFilePath } },
                    _tempManipulation.Length > 0 ? _tempManipulation : string.Empty, int.MaxValue);

            DrawIntro(Ipc.CreateTemporaryCollection.Label, "Copy Existing Collection");
            if (ImGuiUtil.DrawDisabledButton("Copy##Collection", Vector2.Zero,
                    "Copies the effective list from the collection named in Temporary Mod Name...",
                    !_collections.Storage.ByName(_tempModName, out var copyCollection))
             && copyCollection is { HasCache: true })
            {
                var files = copyCollection.ResolvedFiles.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value.Path.ToString());
                var manips = Functions.ToCompressedBase64(copyCollection.MetaCache?.Manipulations.ToArray() ?? Array.Empty<MetaManipulation>(),
                    MetaManipulation.CurrentVersion);
                _lastTempError = Ipc.AddTemporaryMod.Subscriber(_pi).Invoke(_tempModName, _tempCollectionName, files, manips, 999);
            }

            DrawIntro(Ipc.AddTemporaryModAll.Label, "Add Temporary Mod to all Collections");
            if (ImGui.Button("Add##All"))
                _lastTempError = Ipc.AddTemporaryModAll.Subscriber(_pi).Invoke(_tempModName,
                    new Dictionary<string, string> { { _tempGamePath, _tempFilePath } },
                    _tempManipulation.Length > 0 ? _tempManipulation : string.Empty, int.MaxValue);

            DrawIntro(Ipc.RemoveTemporaryMod.Label, "Remove Temporary Mod from specific Collection");
            if (ImGui.Button("Remove##Mod"))
                _lastTempError = Ipc.RemoveTemporaryMod.Subscriber(_pi).Invoke(_tempModName, _tempCollectionName, int.MaxValue);

            DrawIntro(Ipc.RemoveTemporaryModAll.Label, "Remove Temporary Mod from all Collections");
            if (ImGui.Button("Remove##ModAll"))
                _lastTempError = Ipc.RemoveTemporaryModAll.Subscriber(_pi).Invoke(_tempModName, int.MaxValue);
        }

        public void DrawCollections()
        {
            using var collTree = ImRaii.TreeNode("Temporary Collections##TempCollections");
            if (!collTree)
                return;

            using var table = ImRaii.Table("##collTree", 5);
            if (!table)
                return;

            foreach (var collection in _tempCollections.Values)
            {
                ImGui.TableNextColumn();
                var character = _tempCollections.Collections.Where(p => p.Collection == collection).Select(p => p.DisplayName)
                        .FirstOrDefault()
                 ?? "Unknown";
                if (ImGui.Button($"Save##{collection.Name}"))
                    TemporaryMod.SaveTempCollection(_config, _saveService, _modManager, collection, character);

                ImGuiUtil.DrawTableColumn(collection.Name);
                ImGuiUtil.DrawTableColumn(collection.ResolvedFiles.Count.ToString());
                ImGuiUtil.DrawTableColumn(collection.MetaCache?.Count.ToString() ?? "0");
                ImGuiUtil.DrawTableColumn(string.Join(", ",
                    _tempCollections.Collections.Where(p => p.Collection == collection).Select(c => c.DisplayName)));
            }
        }

        public void DrawMods()
        {
            using var modTree = ImRaii.TreeNode("Temporary Mods##TempMods");
            if (!modTree)
                return;

            using var table = ImRaii.Table("##modTree", 5);

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
                        foreach (var manip in mod.Default.Manipulations)
                            ImGui.TextUnformatted(manip.ToString());
                    }
                }
            }

            if (table)
            {
                PrintList("All", _tempMods.ModsForAllCollections);
                foreach (var (collection, list) in _tempMods.Mods)
                    PrintList(collection.Name, list);
            }
        }
    }

    private class ResourceTree
    {
        private readonly DalamudPluginInterface _pi;
        private readonly IObjectTable           _objects;
        private readonly Stopwatch              _stopwatch = new();

        private string       _gameObjectIndices = "0";
        private ResourceType _type              = ResourceType.Mtrl;
        private bool         _withUIData        = false;

        private (string, IReadOnlyDictionary<string, string[]>?)[]?                        _lastGameObjectResourcePaths;
        private (string, IReadOnlyDictionary<string, string[]>?)[]?                        _lastPlayerResourcePaths;
        private (string, IReadOnlyDictionary<nint, (string, string, ChangedItemIcon)>?)[]? _lastGameObjectResourcesOfType;
        private (string, IReadOnlyDictionary<nint, (string, string, ChangedItemIcon)>?)[]? _lastPlayerResourcesOfType;
        private TimeSpan                                                                   _lastCallDuration;

        public ResourceTree(DalamudPluginInterface pi, IObjectTable objects)
        {
            _pi      = pi;
            _objects = objects;
        }

        public void Draw()
        {
            using var _ = ImRaii.TreeNode("Resource Tree");
            if (!_)
                return;

            ImGui.InputText("GameObject indices", ref _gameObjectIndices, 511);
            ImGuiUtil.GenericEnumCombo("Resource type", ImGui.CalcItemWidth(), _type, out _type, Enum.GetValues<ResourceType>());
            ImGui.Checkbox("Also get names and icons", ref _withUIData);

            using var table = ImRaii.Table(string.Empty, 3, ImGuiTableFlags.SizingFixedFit);
            if (!table)
                return;

            DrawIntro(Ipc.GetGameObjectResourcePaths.Label, "Get GameObject resource paths");
            if (ImGui.Button("Get##GameObjectResourcePaths"))
            {
                var gameObjects   = GetSelectedGameObjects();
                var subscriber    = Ipc.GetGameObjectResourcePaths.Subscriber(_pi);
                _stopwatch.Restart();
                var resourcePaths = subscriber.Invoke(gameObjects);

                _lastCallDuration            = _stopwatch.Elapsed;
                _lastGameObjectResourcePaths = gameObjects
                    .Select(i => GameObjectToString(i))
                    .Zip(resourcePaths)
                    .ToArray();

                ImGui.OpenPopup(nameof(Ipc.GetGameObjectResourcePaths));
            }

            DrawIntro(Ipc.GetPlayerResourcePaths.Label, "Get local player resource paths");
            if (ImGui.Button("Get##PlayerResourcePaths"))
            {
                var subscriber    = Ipc.GetPlayerResourcePaths.Subscriber(_pi);
                _stopwatch.Restart();
                var resourcePaths = subscriber.Invoke();

                _lastCallDuration        = _stopwatch.Elapsed;
                _lastPlayerResourcePaths = resourcePaths
                    .Select(pair => (GameObjectToString(pair.Key), (IReadOnlyDictionary<string, string[]>?)pair.Value))
                    .ToArray();

                ImGui.OpenPopup(nameof(Ipc.GetPlayerResourcePaths));
            }

            DrawIntro(Ipc.GetGameObjectResourcesOfType.Label, "Get GameObject resources of type");
            if (ImGui.Button("Get##GameObjectResourcesOfType"))
            {
                var gameObjects     = GetSelectedGameObjects();
                var subscriber      = Ipc.GetGameObjectResourcesOfType.Subscriber(_pi);
                _stopwatch.Restart();
                var resourcesOfType = subscriber.Invoke(_type, _withUIData, gameObjects);

                _lastCallDuration              = _stopwatch.Elapsed;
                _lastGameObjectResourcesOfType = gameObjects
                    .Select(i => GameObjectToString(i))
                    .Zip(resourcesOfType)
                    .ToArray();

                ImGui.OpenPopup(nameof(Ipc.GetGameObjectResourcesOfType));
            }

            DrawIntro(Ipc.GetPlayerResourcesOfType.Label, "Get local player resources of type");
            if (ImGui.Button("Get##PlayerResourcesOfType"))
            {
                var subscriber      = Ipc.GetPlayerResourcesOfType.Subscriber(_pi);
                _stopwatch.Restart();
                var resourcesOfType = subscriber.Invoke(_type, _withUIData);

                _lastCallDuration          = _stopwatch.Elapsed;
                _lastPlayerResourcesOfType = resourcesOfType
                    .Select(pair => (GameObjectToString(pair.Key), (IReadOnlyDictionary<nint, (string, string, ChangedItemIcon)>?)pair.Value))
                    .ToArray();

                ImGui.OpenPopup(nameof(Ipc.GetPlayerResourcesOfType));
            }

            DrawPopup(nameof(Ipc.GetGameObjectResourcePaths), ref _lastGameObjectResourcePaths, DrawResourcePaths, _lastCallDuration);
            DrawPopup(nameof(Ipc.GetPlayerResourcePaths), ref _lastPlayerResourcePaths, DrawResourcePaths, _lastCallDuration);

            DrawPopup(nameof(Ipc.GetGameObjectResourcesOfType), ref _lastGameObjectResourcesOfType, DrawResourcesOfType, _lastCallDuration);
            DrawPopup(nameof(Ipc.GetPlayerResourcesOfType), ref _lastPlayerResourcesOfType, DrawResourcesOfType, _lastCallDuration);
        }

        private static void DrawPopup<T>(string popupId, ref T? result, Action<T> drawResult, TimeSpan duration) where T : class
        {
            ImGui.SetNextWindowSize(ImGuiHelpers.ScaledVector2(1000, 500));
            using var popup = ImRaii.Popup(popupId);
            if (!popup)
            {
                result = null;
                return;
            }

            if (result == null)
            {
                ImGui.CloseCurrentPopup();
                return;
            }

            drawResult(result);

            ImGui.TextUnformatted($"Invoked in {duration.TotalMilliseconds} ms");

            if (ImGui.Button("Close", -Vector2.UnitX) || !ImGui.IsWindowFocused())
            {
                result = null;
                ImGui.CloseCurrentPopup();
            }
        }

        private static void DrawWithHeaders<T>((string, T?)[] result, Action<T> drawItem) where T : class
        {
            var firstSeen = new Dictionary<T, string>();
            foreach (var (label, item) in result)
            {
                if (item == null)
                {
                    ImRaii.TreeNode($"{label}: null", ImGuiTreeNodeFlags.Leaf).Dispose();
                    continue;
                }

                if (firstSeen.TryGetValue(item, out var firstLabel))
                {
                    ImRaii.TreeNode($"{label}: same as {firstLabel}", ImGuiTreeNodeFlags.Leaf).Dispose();
                    continue;
                }

                firstSeen.Add(item, label);

                using var header = ImRaii.TreeNode(label);
                if (!header)
                    continue;

                drawItem(item);
            }
        }

        private static void DrawResourcePaths((string, IReadOnlyDictionary<string, string[]>?)[] result)
        {
            DrawWithHeaders(result, paths =>
            {
                using var table = ImRaii.Table(string.Empty, 2, ImGuiTableFlags.SizingFixedFit);
                if (!table)
                    return;

                ImGui.TableSetupColumn("Actual Path", ImGuiTableColumnFlags.WidthStretch, 0.6f);
                ImGui.TableSetupColumn("Game Paths", ImGuiTableColumnFlags.WidthStretch, 0.4f);
                ImGui.TableHeadersRow();

                foreach (var (actualPath, gamePaths) in paths)
                {
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(actualPath);
                    ImGui.TableNextColumn();
                    foreach (var gamePath in gamePaths)
                        ImGui.TextUnformatted(gamePath);
                }
            });
        }

        private void DrawResourcesOfType((string, IReadOnlyDictionary<nint, (string, string, ChangedItemIcon)>?)[] result)
        {
            DrawWithHeaders(result, resources =>
            {
                using var table = ImRaii.Table(string.Empty, _withUIData ? 3 : 2, ImGuiTableFlags.SizingFixedFit);
                if (!table)
                    return;

                ImGui.TableSetupColumn("Resource Handle", ImGuiTableColumnFlags.WidthStretch, 0.15f);
                ImGui.TableSetupColumn("Actual Path", ImGuiTableColumnFlags.WidthStretch, _withUIData ? 0.55f : 0.85f);
                if (_withUIData)
                    ImGui.TableSetupColumn("Icon & Name", ImGuiTableColumnFlags.WidthStretch, 0.3f);
                ImGui.TableHeadersRow();

                foreach (var (resourceHandle, (actualPath, name, icon)) in resources)
                {
                    ImGui.TableNextColumn();
                    TextUnformattedMono($"0x{resourceHandle:X}");
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(actualPath);
                    if (_withUIData)
                    {
                        ImGui.TableNextColumn();
                        TextUnformattedMono(icon.ToString());
                        ImGui.SameLine();
                        ImGui.TextUnformatted(name);
                    }
                }
            });
        }

        private static void TextUnformattedMono(string text)
        {
            using var _ = ImRaii.PushFont(UiBuilder.MonoFont);
            ImGui.TextUnformatted(text);
        }

        private ushort[] GetSelectedGameObjects()
            => _gameObjectIndices.Split(',')
                    .SelectWhere(index => (ushort.TryParse(index.Trim(), out var i), i))
                    .ToArray();

        private unsafe string GameObjectToString(ObjectIndex gameObjectIndex)
        {
            var gameObject = _objects[gameObjectIndex.Index];

            return gameObject != null
                ? $"[{gameObjectIndex}] {gameObject.Name} ({gameObject.ObjectKind})"
                : $"[{gameObjectIndex}] null";
        }
    }
}

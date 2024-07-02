using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Plugin;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Services;
using Penumbra.Api.Enums;
using Penumbra.Api.Helpers;
using Penumbra.Api.IpcSubscribers;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Interop;
using Penumbra.GameData.Structs;

namespace Penumbra.Api.IpcTester;

public class ResourceTreeIpcTester(IDalamudPluginInterface pi, ObjectManager objects) : IUiService
{
    private readonly Stopwatch _stopwatch = new();

    private string       _gameObjectIndices = "0";
    private ResourceType _type              = ResourceType.Mtrl;
    private bool         _withUiData;

    private (string, Dictionary<string, HashSet<string>>?)[]?                          _lastGameObjectResourcePaths;
    private (string, Dictionary<string, HashSet<string>>?)[]?                          _lastPlayerResourcePaths;
    private (string, IReadOnlyDictionary<nint, (string, string, ChangedItemIcon)>?)[]? _lastGameObjectResourcesOfType;
    private (string, IReadOnlyDictionary<nint, (string, string, ChangedItemIcon)>?)[]? _lastPlayerResourcesOfType;
    private (string, ResourceTreeDto?)[]?                                              _lastGameObjectResourceTrees;
    private (string, ResourceTreeDto)[]?                                               _lastPlayerResourceTrees;
    private TimeSpan                                                                   _lastCallDuration;

    public void Draw()
    {
        using var _ = ImRaii.TreeNode("Resource Tree");
        if (!_)
            return;

        ImGui.InputText("GameObject indices", ref _gameObjectIndices, 511);
        ImGuiUtil.GenericEnumCombo("Resource type", ImGui.CalcItemWidth(), _type, out _type, Enum.GetValues<ResourceType>());
        ImGui.Checkbox("Also get names and icons", ref _withUiData);

        using var table = ImRaii.Table(string.Empty, 3, ImGuiTableFlags.SizingFixedFit);
        if (!table)
            return;

        IpcTester.DrawIntro(GetGameObjectResourcePaths.Label, "Get GameObject resource paths");
        if (ImGui.Button("Get##GameObjectResourcePaths"))
        {
            var gameObjects = GetSelectedGameObjects();
            var subscriber  = new GetGameObjectResourcePaths(pi);
            _stopwatch.Restart();
            var resourcePaths = subscriber.Invoke(gameObjects);

            _lastCallDuration = _stopwatch.Elapsed;
            _lastGameObjectResourcePaths = gameObjects
                .Select(i => GameObjectToString(i))
                .Zip(resourcePaths)
                .ToArray();

            ImGui.OpenPopup(nameof(GetGameObjectResourcePaths));
        }

        IpcTester.DrawIntro(GetPlayerResourcePaths.Label, "Get local player resource paths");
        if (ImGui.Button("Get##PlayerResourcePaths"))
        {
            var subscriber = new GetPlayerResourcePaths(pi);
            _stopwatch.Restart();
            var resourcePaths = subscriber.Invoke();

            _lastCallDuration = _stopwatch.Elapsed;
            _lastPlayerResourcePaths = resourcePaths
                .Select(pair => (GameObjectToString(pair.Key), pair.Value))
                .ToArray()!;

            ImGui.OpenPopup(nameof(GetPlayerResourcePaths));
        }

        IpcTester.DrawIntro(GetGameObjectResourcesOfType.Label, "Get GameObject resources of type");
        if (ImGui.Button("Get##GameObjectResourcesOfType"))
        {
            var gameObjects = GetSelectedGameObjects();
            var subscriber  = new GetGameObjectResourcesOfType(pi);
            _stopwatch.Restart();
            var resourcesOfType = subscriber.Invoke(_type, _withUiData, gameObjects);

            _lastCallDuration = _stopwatch.Elapsed;
            _lastGameObjectResourcesOfType = gameObjects
                .Select(i => GameObjectToString(i))
                .Zip(resourcesOfType)
                .ToArray();

            ImGui.OpenPopup(nameof(GetGameObjectResourcesOfType));
        }

        IpcTester.DrawIntro(GetPlayerResourcesOfType.Label, "Get local player resources of type");
        if (ImGui.Button("Get##PlayerResourcesOfType"))
        {
            var subscriber = new GetPlayerResourcesOfType(pi);
            _stopwatch.Restart();
            var resourcesOfType = subscriber.Invoke(_type, _withUiData);

            _lastCallDuration = _stopwatch.Elapsed;
            _lastPlayerResourcesOfType = resourcesOfType
                .Select(pair => (GameObjectToString(pair.Key), (IReadOnlyDictionary<nint, (string, string, ChangedItemIcon)>?)pair.Value))
                .ToArray();

            ImGui.OpenPopup(nameof(GetPlayerResourcesOfType));
        }

        IpcTester.DrawIntro(GetGameObjectResourceTrees.Label, "Get GameObject resource trees");
        if (ImGui.Button("Get##GameObjectResourceTrees"))
        {
            var gameObjects = GetSelectedGameObjects();
            var subscriber  = new GetGameObjectResourceTrees(pi);
            _stopwatch.Restart();
            var trees = subscriber.Invoke(_withUiData, gameObjects);

            _lastCallDuration = _stopwatch.Elapsed;
            _lastGameObjectResourceTrees = gameObjects
                .Select(i => GameObjectToString(i))
                .Zip(trees)
                .ToArray();

            ImGui.OpenPopup(nameof(GetGameObjectResourceTrees));
        }

        IpcTester.DrawIntro(GetPlayerResourceTrees.Label, "Get local player resource trees");
        if (ImGui.Button("Get##PlayerResourceTrees"))
        {
            var subscriber = new GetPlayerResourceTrees(pi);
            _stopwatch.Restart();
            var trees = subscriber.Invoke(_withUiData);

            _lastCallDuration = _stopwatch.Elapsed;
            _lastPlayerResourceTrees = trees
                .Select(pair => (GameObjectToString(pair.Key), pair.Value))
                .ToArray();

            ImGui.OpenPopup(nameof(GetPlayerResourceTrees));
        }

        DrawPopup(nameof(GetGameObjectResourcePaths), ref _lastGameObjectResourcePaths, DrawResourcePaths,
            _lastCallDuration);
        DrawPopup(nameof(GetPlayerResourcePaths), ref _lastPlayerResourcePaths!, DrawResourcePaths, _lastCallDuration);

        DrawPopup(nameof(GetGameObjectResourcesOfType), ref _lastGameObjectResourcesOfType, DrawResourcesOfType,
            _lastCallDuration);
        DrawPopup(nameof(GetPlayerResourcesOfType), ref _lastPlayerResourcesOfType, DrawResourcesOfType,
            _lastCallDuration);

        DrawPopup(nameof(GetGameObjectResourceTrees), ref _lastGameObjectResourceTrees, DrawResourceTrees,
            _lastCallDuration);
        DrawPopup(nameof(GetPlayerResourceTrees), ref _lastPlayerResourceTrees, DrawResourceTrees!, _lastCallDuration);
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

    private static void DrawResourcePaths((string, Dictionary<string, HashSet<string>>?)[] result)
    {
        DrawWithHeaders(result, paths =>
        {
            using var table = ImRaii.Table(string.Empty, 2, ImGuiTableFlags.SizingFixedFit);
            if (!table)
                return;

            ImGui.TableSetupColumn("Actual Path", ImGuiTableColumnFlags.WidthStretch, 0.6f);
            ImGui.TableSetupColumn("Game Paths",  ImGuiTableColumnFlags.WidthStretch, 0.4f);
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
            using var table = ImRaii.Table(string.Empty, _withUiData ? 3 : 2, ImGuiTableFlags.SizingFixedFit);
            if (!table)
                return;

            ImGui.TableSetupColumn("Resource Handle", ImGuiTableColumnFlags.WidthStretch, 0.15f);
            ImGui.TableSetupColumn("Actual Path",     ImGuiTableColumnFlags.WidthStretch, _withUiData ? 0.55f : 0.85f);
            if (_withUiData)
                ImGui.TableSetupColumn("Icon & Name", ImGuiTableColumnFlags.WidthStretch, 0.3f);
            ImGui.TableHeadersRow();

            foreach (var (resourceHandle, (actualPath, name, icon)) in resources)
            {
                ImGui.TableNextColumn();
                TextUnformattedMono($"0x{resourceHandle:X}");
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(actualPath);
                if (_withUiData)
                {
                    ImGui.TableNextColumn();
                    TextUnformattedMono(icon.ToString());
                    ImGui.SameLine();
                    ImGui.TextUnformatted(name);
                }
            }
        });
    }

    private void DrawResourceTrees((string, ResourceTreeDto?)[] result)
    {
        DrawWithHeaders(result, tree =>
        {
            ImGui.TextUnformatted($"Name: {tree.Name}\nRaceCode: {(GenderRace)tree.RaceCode}");

            using var table = ImRaii.Table(string.Empty, _withUiData ? 7 : 5, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.Resizable);
            if (!table)
                return;

            if (_withUiData)
            {
                ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch, 0.5f);
                ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthStretch, 0.1f);
                ImGui.TableSetupColumn("Icon", ImGuiTableColumnFlags.WidthStretch, 0.15f);
            }
            else
            {
                ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthStretch, 0.5f);
            }

            ImGui.TableSetupColumn("Game Path",       ImGuiTableColumnFlags.WidthStretch, 0.5f);
            ImGui.TableSetupColumn("Actual Path",     ImGuiTableColumnFlags.WidthStretch, 0.5f);
            ImGui.TableSetupColumn("Object Address",  ImGuiTableColumnFlags.WidthStretch, 0.2f);
            ImGui.TableSetupColumn("Resource Handle", ImGuiTableColumnFlags.WidthStretch, 0.2f);
            ImGui.TableHeadersRow();

            void DrawNode(ResourceNodeDto node)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                var hasChildren = node.Children.Any();
                using var treeNode = ImRaii.TreeNode(
                    $"{(_withUiData ? node.Name ?? "Unknown" : node.Type)}##{node.ObjectAddress:X8}",
                    hasChildren
                        ? ImGuiTreeNodeFlags.SpanFullWidth
                        : ImGuiTreeNodeFlags.SpanFullWidth | ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen);
                if (_withUiData)
                {
                    ImGui.TableNextColumn();
                    TextUnformattedMono(node.Type.ToString());
                    ImGui.TableNextColumn();
                    TextUnformattedMono(node.Icon.ToString());
                }

                ImGui.TableNextColumn();
                ImGui.TextUnformatted(node.GamePath ?? "Unknown");
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(node.ActualPath);
                ImGui.TableNextColumn();
                TextUnformattedMono($"0x{node.ObjectAddress:X8}");
                ImGui.TableNextColumn();
                TextUnformattedMono($"0x{node.ResourceHandle:X8}");

                if (treeNode)
                    foreach (var child in node.Children)
                        DrawNode(child);
            }

            foreach (var node in tree.Nodes)
                DrawNode(node);
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
        var gameObject = objects[gameObjectIndex];

        return gameObject.Valid
            ? $"[{gameObjectIndex}] {gameObject.Utf8Name} ({(ObjectKind)gameObject.AsObject->ObjectKind})"
            : $"[{gameObjectIndex}] null";
    }
}

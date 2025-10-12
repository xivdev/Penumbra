using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Plugin;
using ImSharp;
using Luna;
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

    private (StringU8, Dictionary<string, HashSet<string>>?)[]?                          _lastGameObjectResourcePaths;
    private (StringU8, Dictionary<string, HashSet<string>>?)[]?                          _lastPlayerResourcePaths;
    private (StringU8, IReadOnlyDictionary<nint, (string, string, ChangedItemIcon)>?)[]? _lastGameObjectResourcesOfType;
    private (StringU8, IReadOnlyDictionary<nint, (string, string, ChangedItemIcon)>?)[]? _lastPlayerResourcesOfType;
    private (StringU8, ResourceTreeDto?)[]?                                              _lastGameObjectResourceTrees;
    private (StringU8, ResourceTreeDto)[]?                                               _lastPlayerResourceTrees;
    private TimeSpan                                                                     _lastCallDuration;

    public void Draw()
    {
        using var _ = Im.Tree.Node("Resource Tree"u8);
        if (!_)
            return;

        Im.Input.Text("GameObject indices"u8, ref _gameObjectIndices);
        EnumCombo<ResourceType>.Instance.Draw("Resource type"u8, ref _type, default, Im.Item.Size.X);
        Im.Checkbox("Also get names and icons"u8, ref _withUiData);

        using var table = Im.Table.Begin(StringU8.Empty, 3, TableFlags.SizingFixedFit);
        if (!table)
            return;

        IpcTester.DrawIntro(GetGameObjectResourcePaths.Label, "Get GameObject resource paths"u8);
        if (Im.Button("Get##GameObjectResourcePaths"u8))
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

            Im.Popup.Open("GetGameObjectResourcePaths"u8);
        }

        IpcTester.DrawIntro(GetPlayerResourcePaths.Label, "Get local player resource paths"u8);
        if (Im.Button("Get##PlayerResourcePaths"u8))
        {
            var subscriber = new GetPlayerResourcePaths(pi);
            _stopwatch.Restart();
            var resourcePaths = subscriber.Invoke();

            _lastCallDuration = _stopwatch.Elapsed;
            _lastPlayerResourcePaths = resourcePaths
                .Select(pair => (GameObjectToString(pair.Key), pair.Value))
                .ToArray()!;

            Im.Popup.Open("GetPlayerResourcePaths"u8);
        }

        IpcTester.DrawIntro(GetGameObjectResourcesOfType.Label, "Get GameObject resources of type"u8);
        if (Im.Button("Get##GameObjectResourcesOfType"u8))
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

            Im.Popup.Open("GetGameObjectResourcesOfType"u8);
        }

        IpcTester.DrawIntro(GetPlayerResourcesOfType.Label, "Get local player resources of type"u8);
        if (Im.Button("Get##PlayerResourcesOfType"u8))
        {
            var subscriber = new GetPlayerResourcesOfType(pi);
            _stopwatch.Restart();
            var resourcesOfType = subscriber.Invoke(_type, _withUiData);

            _lastCallDuration = _stopwatch.Elapsed;
            _lastPlayerResourcesOfType = resourcesOfType
                .Select(pair => (GameObjectToString(pair.Key), (IReadOnlyDictionary<nint, (string, string, ChangedItemIcon)>?)pair.Value))
                .ToArray();

            Im.Popup.Open("GetPlayerResourcesOfType"u8);
        }

        IpcTester.DrawIntro(GetGameObjectResourceTrees.Label, "Get GameObject resource trees"u8);
        if (Im.Button("Get##GameObjectResourceTrees"u8))
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

            Im.Popup.Open("GetGameObjectResourceTrees"u8);
        }

        IpcTester.DrawIntro(GetPlayerResourceTrees.Label, "Get local player resource trees"u8);
        if (Im.Button("Get##PlayerResourceTrees"u8))
        {
            var subscriber = new GetPlayerResourceTrees(pi);
            _stopwatch.Restart();
            var trees = subscriber.Invoke(_withUiData);

            _lastCallDuration = _stopwatch.Elapsed;
            _lastPlayerResourceTrees = trees
                .Select(pair => (GameObjectToString(pair.Key), pair.Value))
                .ToArray();

            Im.Popup.Open("GetPlayerResourceTrees"u8);
        }

        DrawPopup("GetGameObjectResourcePaths"u8, ref _lastGameObjectResourcePaths, DrawResourcePaths,
            _lastCallDuration);
        DrawPopup("GetPlayerResourcePaths"u8, ref _lastPlayerResourcePaths!, DrawResourcePaths, _lastCallDuration);

        DrawPopup("GetGameObjectResourcesOfType"u8, ref _lastGameObjectResourcesOfType, DrawResourcesOfType,
            _lastCallDuration);
        DrawPopup("GetPlayerResourcesOfType"u8, ref _lastPlayerResourcesOfType, DrawResourcesOfType,
            _lastCallDuration);

        DrawPopup("GetGameObjectResourceTrees"u8, ref _lastGameObjectResourceTrees, DrawResourceTrees,
            _lastCallDuration);
        DrawPopup("GetPlayerResourceTrees"u8, ref _lastPlayerResourceTrees, DrawResourceTrees!, _lastCallDuration);
    }

    private static void DrawPopup<T>(ReadOnlySpan<byte> popupId, ref T? result, Action<T> drawResult, TimeSpan duration) where T : class
    {
        Im.Window.SetNextSize(ImEx.ScaledVector(1000, 500));
        using var popup = Im.Popup.Begin(popupId);
        if (!popup)
        {
            result = null;
            return;
        }

        if (result == null)
        {
            Im.Popup.CloseCurrent();
            return;
        }

        drawResult(result);

        Im.Text($"Invoked in {duration.TotalMilliseconds} ms");

        if (Im.Button("Close"u8, -Vector2.UnitX) || !Im.Window.Focused())
        {
            result = null;
            Im.Popup.CloseCurrent();
        }
    }

    private static void DrawWithHeaders<T>((StringU8, T?)[] result, Action<T> drawItem) where T : class
    {
        var firstSeen = new Dictionary<T, StringU8>();
        foreach (var (label, item) in result)
        {
            if (item == null)
            {
                Im.Tree.Node($"{label}: null", TreeNodeFlags.Leaf).Dispose();
                continue;
            }

            if (firstSeen.TryGetValue(item, out var firstLabel))
            {
                Im.Tree.Node($"{label}: same as {firstLabel}", TreeNodeFlags.Leaf).Dispose();
                continue;
            }

            firstSeen.Add(item, label);

            using var header = Im.Tree.Node(label);
            if (!header)
                continue;

            drawItem(item);
        }
    }

    private static void DrawResourcePaths((StringU8, Dictionary<string, HashSet<string>>?)[] result)
    {
        DrawWithHeaders(result, paths =>
        {
            using var table = Im.Table.Begin(StringU8.Empty, 2, TableFlags.SizingFixedFit);
            if (!table)
                return;

            table.SetupColumn("Actual Path"u8, TableColumnFlags.WidthStretch, 0.6f);
            table.SetupColumn("Game Paths"u8,  TableColumnFlags.WidthStretch, 0.4f);
            table.HeaderRow();

            foreach (var (actualPath, gamePaths) in paths)
            {
                table.DrawColumn(actualPath);
                table.NextColumn();
                foreach (var gamePath in gamePaths)
                    Im.Text(gamePath);
            }
        });
    }

    private void DrawResourcesOfType((StringU8, IReadOnlyDictionary<nint, (string, string, ChangedItemIcon)>?)[] result)
    {
        DrawWithHeaders(result, resources =>
        {
            using var table = Im.Table.Begin(StringU8.Empty, _withUiData ? 3 : 2, TableFlags.SizingFixedFit);
            if (!table)
                return;

            table.SetupColumn("Resource Handle"u8, TableColumnFlags.WidthStretch, 0.15f);
            table.SetupColumn("Actual Path"u8,     TableColumnFlags.WidthStretch, _withUiData ? 0.55f : 0.85f);
            if (_withUiData)
                table.SetupColumn("Icon & Name"u8, TableColumnFlags.WidthStretch, 0.3f);
            table.HeaderRow();

            foreach (var (resourceHandle, (actualPath, name, icon)) in resources)
            {
                table.NextColumn();
                Penumbra.Dynamis.DrawPointer(resourceHandle);
                table.DrawColumn(actualPath);
                if (_withUiData)
                {
                    table.NextColumn();
                    ImEx.MonoText($"{icon}");
                    Im.Line.SameInner();
                    Im.Text(name);
                }
            }
        });
    }

    private void DrawResourceTrees((StringU8, ResourceTreeDto?)[] result)
    {
        DrawWithHeaders(result, tree =>
        {
            Im.Text($"Name: {tree.Name}\nRaceCode: {(GenderRace)tree.RaceCode}");

            using var table = Im.Table.Begin(StringU8.Empty, _withUiData ? 7 : 5, TableFlags.SizingFixedFit | TableFlags.Resizable);
            if (!table)
                return;

            if (_withUiData)
            {
                table.SetupColumn("Name"u8, TableColumnFlags.WidthStretch, 0.5f);
                table.SetupColumn("Type"u8, TableColumnFlags.WidthStretch, 0.1f);
                table.SetupColumn("Icon"u8, TableColumnFlags.WidthStretch, 0.15f);
            }
            else
            {
                table.SetupColumn("Type"u8, TableColumnFlags.WidthStretch, 0.5f);
            }

            table.SetupColumn("Game Path"u8,       TableColumnFlags.WidthStretch, 0.5f);
            table.SetupColumn("Actual Path"u8,     TableColumnFlags.WidthStretch, 0.5f);
            table.SetupColumn("Object Address"u8,  TableColumnFlags.WidthStretch, 0.2f);
            table.SetupColumn("Resource Handle"u8, TableColumnFlags.WidthStretch, 0.2f);
            table.HeaderRow();

            foreach (var node in tree.Nodes)
                DrawNode(table, node, _withUiData);
            return;

            static void DrawNode(in Im.TableDisposable table, ResourceNodeDto node, bool uiData)
            {
                table.NextRow();
                table.NextColumn();
                var hasChildren = node.Children.Count > 0;
                using var treeNode = Im.Tree.Node($"{(uiData ? node.Name ?? "Unknown" : node.Type)}##{node.ObjectAddress:X8}",
                    hasChildren
                        ? TreeNodeFlags.SpanFullWidth
                        : TreeNodeFlags.SpanFullWidth | TreeNodeFlags.Leaf | TreeNodeFlags.NoTreePushOnOpen);
                if (uiData)
                {
                    using var mono = Im.Font.PushMono();
                    table.DrawColumn($"{node.Type}");
                    table.DrawColumn($"{node.Icon}");
                }

                table.DrawColumn(node.GamePath ?? "Unknown");
                table.DrawColumn(node.ActualPath);
                table.NextColumn();
                Penumbra.Dynamis.DrawPointer(node.ObjectAddress);
                table.NextColumn();
                Penumbra.Dynamis.DrawPointer(node.ResourceHandle);

                if (treeNode)
                    foreach (var child in node.Children)
                        DrawNode(table, child, uiData);
            }
        });
    }

    private ushort[] GetSelectedGameObjects()
        => _gameObjectIndices.Split(',')
            .SelectWhere(index => (ushort.TryParse(index.Trim(), out var i), i))
            .ToArray();

    private unsafe StringU8 GameObjectToString(ObjectIndex gameObjectIndex)
    {
        var gameObject = objects[gameObjectIndex];

        return gameObject.Valid
            ? new StringU8($"[{gameObjectIndex}] {gameObject.Utf8Name} ({(ObjectKind)gameObject.AsObject->ObjectKind})")
            : new StringU8($"[{gameObjectIndex}] null");
    }
}

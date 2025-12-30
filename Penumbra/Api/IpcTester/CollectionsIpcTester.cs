using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Plugin;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Services;
using Penumbra.Api.Enums;
using Penumbra.Api.Helpers;
using Penumbra.Api.IpcSubscribers;
using Penumbra.Collections.Manager;
using Penumbra.GameData.Data;
using ImGuiClip = OtterGui.ImGuiClip;

namespace Penumbra.Api.IpcTester;

public class CollectionsIpcTester : IUiService, IDisposable
{
    private readonly IDalamudPluginInterface                                                   _pi;
    public readonly  EventSubscriber<ResolvedFileChange, Guid, string, string, string, string> ResolveFileChange;
    
    private ResolvedFileChange _lastResolvedFileChangeType;
    private Guid               _lastResolvedFileChangeCollection  = Guid.Empty;
    private string             _lastResolvedFileChangeMod         = string.Empty;
    private string             _lastResolvedFileChangeGamePath    = string.Empty;
    private string             _lastResolvedFileChangeOldFilePath = string.Empty;
    private string             _lastResolvedFileChangeNewFilePath = string.Empty;

    private int               _objectIdx;
    private string            _collectionIdString = string.Empty;
    private Guid?             _collectionId;
    private bool              _allowCreation = true;
    private bool              _allowDeletion = true;
    private ApiCollectionType _type          = ApiCollectionType.Yourself;

    private Dictionary<Guid, string>          _collections  = [];
    private (string, string)[]                _resolvedFiles= [];
    private (string, ChangedItemType, uint)[] _changedItems = [];
    private PenumbraApiEc                     _returnCode   = PenumbraApiEc.Success;
    private (Guid Id, string Name)?           _oldCollection;

    public CollectionsIpcTester(IDalamudPluginInterface pi)
    {
        _pi = pi;
        ResolveFileChange = ResolvedFileChanged.Subscriber(pi, UpdateLastResolvedChange);
        ResolveFileChange.Disable();
    }

    public void Dispose()
    {
        ResolveFileChange.Dispose();
    }

    public void Draw()
    {
        using var _ = ImRaii.TreeNode("Collections");
        if (!_)
            return;

        ImGuiUtil.GenericEnumCombo("Collection Type", 200, _type, out _type, t => ((CollectionType)t).ToName());
        ImGui.InputInt("Object Index##Collections", ref _objectIdx, 0, 0);
        ImGuiUtil.GuidInput("Collection Id##Collections", "Collection Identifier...", string.Empty, ref _collectionId, ref _collectionIdString);
        ImGui.Checkbox("Allow Assignment Creation", ref _allowCreation);
        ImGui.SameLine();
        ImGui.Checkbox("Allow Assignment Deletion", ref _allowDeletion);

        using var table = ImRaii.Table(string.Empty, 4, ImGuiTableFlags.SizingFixedFit);
        if (!table)
            return;

        IpcTester.DrawIntro("Last Return Code", _returnCode.ToString());

        IpcTester.DrawIntro(ResolvedFileChanged.Label, "Last Resolved File Change");
        ImGui.TextUnformatted(_lastResolvedFileChangeMod.Length > 0
            ? $"{_lastResolvedFileChangeType} of {_lastResolvedFileChangeMod} in {_lastResolvedFileChangeCollection} for game path {_lastResolvedFileChangeGamePath} from {_lastResolvedFileChangeOldFilePath} to {_lastResolvedFileChangeNewFilePath}"
            : "None");

        if (_oldCollection != null)
            ImGui.TextUnformatted(!_oldCollection.HasValue ? "Created" : _oldCollection.ToString());

        IpcTester.DrawIntro(GetCollectionsByIdentifier.Label, "Collection Identifier");
        var collectionList = new GetCollectionsByIdentifier(_pi).Invoke(_collectionIdString);
        if (collectionList.Count == 0)
        {
            DrawCollection(null);
        }
        else
        {
            DrawCollection(collectionList[0]);
            foreach (var pair in collectionList.Skip(1))
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                DrawCollection(pair);
            }
        }

        IpcTester.DrawIntro(GetCollection.Label, "Current Collection");
        DrawCollection(new GetCollection(_pi).Invoke(ApiCollectionType.Current));

        IpcTester.DrawIntro(GetCollection.Label, "Default Collection");
        DrawCollection(new GetCollection(_pi).Invoke(ApiCollectionType.Default));

        IpcTester.DrawIntro(GetCollection.Label, "Interface Collection");
        DrawCollection(new GetCollection(_pi).Invoke(ApiCollectionType.Interface));

        IpcTester.DrawIntro(GetCollection.Label, "Special Collection");
        DrawCollection(new GetCollection(_pi).Invoke(_type));

        IpcTester.DrawIntro(GetCollections.Label, "Collections");
        DrawCollectionPopup();
        if (ImGui.Button("Get##Collections"))
        {
            _collections = new GetCollections(_pi).Invoke();
            ImGui.OpenPopup("Collections");
        }

        IpcTester.DrawIntro(GetCollectionForObject.Label, "Get Object Collection");
        var (valid, individual, effectiveCollection) = new GetCollectionForObject(_pi).Invoke(_objectIdx);
        DrawCollection(effectiveCollection);
        ImGui.SameLine();
        ImGui.TextUnformatted($"({(valid ? "Valid" : "Invalid")} Object{(individual ? ", Individual Assignment)" : ")")}");

        IpcTester.DrawIntro(SetCollection.Label, "Set Special Collection");
        if (ImGui.Button("Set##SpecialCollection"))
            (_returnCode, _oldCollection) =
                new SetCollection(_pi).Invoke(_type, _collectionId.GetValueOrDefault(Guid.Empty), _allowCreation, _allowDeletion);
        ImGui.TableNextColumn();
        if (ImGui.Button("Remove##SpecialCollection"))
            (_returnCode, _oldCollection) = new SetCollection(_pi).Invoke(_type, null, _allowCreation, _allowDeletion);

        IpcTester.DrawIntro(SetCollectionForObject.Label, "Set Object Collection");
        if (ImGui.Button("Set##ObjectCollection"))
            (_returnCode, _oldCollection) = new SetCollectionForObject(_pi).Invoke(_objectIdx, _collectionId.GetValueOrDefault(Guid.Empty),
                _allowCreation, _allowDeletion);
        ImGui.TableNextColumn();
        if (ImGui.Button("Remove##ObjectCollection"))
            (_returnCode, _oldCollection) = new SetCollectionForObject(_pi).Invoke(_objectIdx, null, _allowCreation, _allowDeletion);

        IpcTester.DrawIntro(GetResolvedFilesForCollection.Label, "Resolved Files List");
        DrawResolvedFilesPopup();
        if (ImGui.Button("Get##ResolvedFiles"))
        {
            var files = new GetResolvedFilesForCollection(_pi).Invoke(_collectionId.GetValueOrDefault(Guid.Empty));
            _resolvedFiles = files.Select(kvp => (kvp.Key, kvp.Value)).ToArray();
            ImGui.OpenPopup("Resolved Files List");
        }


        IpcTester.DrawIntro(GetChangedItemsForCollection.Label, "Changed Item List");
        DrawChangedItemPopup();
        if (ImGui.Button("Get##ChangedItems"))
        {
            var items = new GetChangedItemsForCollection(_pi).Invoke(_collectionId.GetValueOrDefault(Guid.Empty));
            _changedItems = items.Select(kvp =>
            {
                var (type, id) = kvp.Value.ToApiObject();
                return (kvp.Key, type, id);
            }).ToArray();
            ImGui.OpenPopup("Changed Item List");
        }
        IpcTester.DrawIntro(RedrawCollectionMembers.Label, "Redraw Collection Members");
        if (ImGui.Button("Redraw##ObjectCollection"))
             new RedrawCollectionMembers(_pi).Invoke(collectionList[0].Id, RedrawType.Redraw);
            
    }

    private void DrawResolvedFilesPopup()
    {
        // calculate the width by getting the max width of the longest string in each item.
        var width = _resolvedFiles.Length > 0
            ? ImGui.CalcTextSize(_resolvedFiles.OrderByDescending(i => i.Item1.Length + i.Item2.Length).Select(i => i.Item1 + i.Item2).First()).X : 500f;
        ImGui.SetNextWindowSize(ImGuiHelpers.ScaledVector2(width, 500));
        using var p = ImRaii.Popup("Resolved Files List");
        if (!p)
            return;

        using (var table = ImRaii.Table("##ResolvedFiles", 2, ImGuiTableFlags.SizingFixedFit))
        {
            if (table)
                ImGuiClip.ClippedDraw(_resolvedFiles, t =>
                {
                    ImGuiUtil.DrawTableColumn(t.Item1);
                    ImGuiUtil.DrawTableColumn(t.Item2);
                }, ImGui.GetTextLineHeightWithSpacing());
        }
        if (ImGui.Button("Close", -Vector2.UnitX) || !ImGui.IsWindowFocused())
            ImGui.CloseCurrentPopup();
    }

    private void DrawChangedItemPopup()
    {
        ImGui.SetNextWindowSize(ImGuiHelpers.ScaledVector2(500, 500));
        using var p = ImRaii.Popup("Changed Item List");
        if (!p)
            return;

        using (var table = ImRaii.Table("##ChangedItems", 3, ImGuiTableFlags.SizingFixedFit))
        {
            if (table)
                ImGuiClip.ClippedDraw(_changedItems, t =>
                {
                    ImGuiUtil.DrawTableColumn(t.Item1);
                    ImGuiUtil.DrawTableColumn(t.Item2.ToString());
                    ImGuiUtil.DrawTableColumn(t.Item3.ToString());
                }, ImGui.GetTextLineHeightWithSpacing());
        }

        if (ImGui.Button("Close", -Vector2.UnitX) || !ImGui.IsWindowFocused())
            ImGui.CloseCurrentPopup();
    }

    private void DrawCollectionPopup()
    {
        ImGui.SetNextWindowSize(ImGuiHelpers.ScaledVector2(500, 500));
        using var p = ImRaii.Popup("Collections");
        if (!p)
            return;

        using (var t = ImRaii.Table("collections", 2, ImGuiTableFlags.SizingFixedFit))
        {
            if (t)
                foreach (var collection in _collections)
                {
                    ImGui.TableNextColumn();
                    DrawCollection((collection.Key, collection.Value));
                }
        }

        if (ImGui.Button("Close", -Vector2.UnitX) || !ImGui.IsWindowFocused())
            ImGui.CloseCurrentPopup();
    }

    private static void DrawCollection((Guid Id, string Name)? collection)
    {
        if (collection == null)
        {
            ImGui.TextUnformatted("<Unassigned>");
            ImGui.TableNextColumn();
            return;
        }

        ImGui.TextUnformatted(collection.Value.Name);
        ImGui.TableNextColumn();
        using (ImRaii.PushFont(UiBuilder.MonoFont))
        {
            ImGuiUtil.CopyOnClickSelectable(collection.Value.Id.ToString());
        }
    }

    private void UpdateLastResolvedChange(ResolvedFileChange type, Guid collection, string mod, string gamePath, string oldFilePath, string newFilePath)
    {
        _lastResolvedFileChangeType = type;
        _lastResolvedFileChangeCollection = collection;
        _lastResolvedFileChangeMod = mod;
        _lastResolvedFileChangeGamePath = gamePath;
        _lastResolvedFileChangeOldFilePath = oldFilePath;
        _lastResolvedFileChangeNewFilePath = newFilePath;
    }
}

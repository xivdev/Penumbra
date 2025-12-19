using Dalamud.Plugin;
using ImSharp;
using Luna;
using Penumbra.Api.Enums;
using Penumbra.Api.IpcSubscribers;
using Penumbra.GameData.Data;

namespace Penumbra.Api.IpcTester;

public class CollectionsIpcTester(IDalamudPluginInterface pi) : IUiService
{
    private int               _objectIdx;
    private Guid?             _collectionId;
    private bool              _allowCreation = true;
    private bool              _allowDeletion = true;
    private ApiCollectionType _type          = ApiCollectionType.Yourself;

    private Dictionary<Guid, string>          _collections  = [];
    private (string, ChangedItemType, uint)[] _changedItems = [];
    private PenumbraApiEc                     _returnCode   = PenumbraApiEc.Success;
    private (Guid Id, string Name)?           _oldCollection;

    public void Draw()
    {
        using var _ = Im.Tree.Node("Collections"u8);
        if (!_)
            return;

        EnumCombo<ApiCollectionType>.Instance.Draw("Collection Type"u8, ref _type, default, 200 * Im.Style.GlobalScale);
        Im.Input.Scalar("Object Index##Collections"u8, ref _objectIdx);
        if (_collectionId.HasValue)
        {
            if (ImEx.GuidInput("Collection Id##Collections"u8, $"{_collectionId.Value}", out var id))
                _collectionId = id;
        }
        else
        {
            if (ImEx.GuidInput("Collection Id##Collections"u8, "Collection Identifier..."u8, out var id))
                _collectionId = id;
        }

        Im.Checkbox("Allow Assignment Creation"u8, ref _allowCreation);
        Im.Line.Same();
        Im.Checkbox("Allow Assignment Deletion"u8, ref _allowDeletion);

        using var table = Im.Table.Begin("table"u8, 4, TableFlags.SizingFixedFit);
        if (!table)
            return;


        table.DrawColumn("Last Return Code"u8);
        table.DrawColumn($"{_returnCode}");
        if (_oldCollection is not null)
            Im.Text(!_oldCollection.HasValue ? "Created" : _oldCollection.ToString()!);

        table.NextRow();
        using (IpcTester.DrawIntro(GetCollectionsByIdentifier.LabelU8, "Collection Identifier"u8))
        {
            var collectionList = new GetCollectionsByIdentifier(pi).Invoke(_collectionId.GetValueOrDefault().ToString());
            if (collectionList.Count == 0)
            {
                DrawCollection(table, null);
            }
            else
            {
                DrawCollection(table, collectionList[0]);
                foreach (var pair in collectionList.Skip(1))
                {
                    table.NextRow();
                    table.NextColumn();
                    table.NextColumn();
                    table.NextColumn();
                    DrawCollection(table, pair);
                }
            }
        }

        using (IpcTester.DrawIntro(GetCollection.LabelU8, "Current Collection"u8))
        {
            DrawCollection(table, new GetCollection(pi).Invoke(ApiCollectionType.Current));
        }

        using (IpcTester.DrawIntro(GetCollection.LabelU8, "Default Collection"u8))
        {
            DrawCollection(table, new GetCollection(pi).Invoke(ApiCollectionType.Default));
        }

        using (IpcTester.DrawIntro(GetCollection.LabelU8, "Interface Collection"u8))
        {
            DrawCollection(table, new GetCollection(pi).Invoke(ApiCollectionType.Interface));
        }

        using (IpcTester.DrawIntro(GetCollection.LabelU8, "Special Collection"u8))
        {
            DrawCollection(table, new GetCollection(pi).Invoke(_type));
        }

        using (IpcTester.DrawIntro(GetCollections.LabelU8, "Collections"u8))
        {
            DrawCollectionPopup();
            table.NextColumn();
            if (Im.SmallButton("Get##Collections"u8))
            {
                _collections = new GetCollections(pi).Invoke();
                Im.Popup.Open("Collections"u8);
            }
        }

        using (IpcTester.DrawIntro(GetCollectionForObject.LabelU8, "Get Object Collection"u8))
        {
            var (valid, individual, effectiveCollection) = new GetCollectionForObject(pi).Invoke(_objectIdx);
            DrawCollection(table, effectiveCollection);
            Im.Line.Same();
            Im.Text($"({(valid ? "Valid" : "Invalid")} Object{(individual ? ", Individual Assignment)" : ")")}");
        }

        using (IpcTester.DrawIntro(SetCollection.LabelU8, "Set Special Collection"u8))
        {
            table.NextColumn();
            if (Im.SmallButton("Set##SpecialCollection"u8))
                (_returnCode, _oldCollection) =
                    new SetCollection(pi).Invoke(_type, _collectionId.GetValueOrDefault(Guid.Empty), _allowCreation, _allowDeletion);
            table.NextColumn();
            if (Im.SmallButton("Remove##SpecialCollection"u8))
                (_returnCode, _oldCollection) = new SetCollection(pi).Invoke(_type, null, _allowCreation, _allowDeletion);
        }

        using (IpcTester.DrawIntro(SetCollectionForObject.LabelU8, "Set Object Collection"u8))
        {
            table.NextColumn();
            if (Im.SmallButton("Set##ObjectCollection"u8))
                (_returnCode, _oldCollection) = new SetCollectionForObject(pi).Invoke(_objectIdx, _collectionId.GetValueOrDefault(Guid.Empty),
                    _allowCreation, _allowDeletion);
            table.NextColumn();
            if (Im.SmallButton("Remove##ObjectCollection"u8))
                (_returnCode, _oldCollection) = new SetCollectionForObject(pi).Invoke(_objectIdx, null, _allowCreation, _allowDeletion);
        }

        using (IpcTester.DrawIntro(GetChangedItemsForCollection.LabelU8, "Changed Item List"u8))
        {
            DrawChangedItemPopup();
            table.NextColumn();
            if (Im.SmallButton("Get##ChangedItems"u8))
            {
                var items = new GetChangedItemsForCollection(pi).Invoke(_collectionId.GetValueOrDefault(Guid.Empty));
                _changedItems = items.Select(kvp =>
                {
                    var (type, id) = kvp.Value.ToApiObject();
                    return (kvp.Key, type, id);
                }).ToArray();
                Im.Popup.Open("Changed Item List"u8);
            }
        }
        IpcTester.DrawIntro(RedrawCollectionMembers.LabelU8, "Redraw Collection Members"u8);
        if (Im.Button("Redraw##ObjectCollection"u8))
             new RedrawCollectionMembers(pi).Invoke(_collectionId.GetValueOrDefault(Guid.Empty), RedrawType.Redraw);
            
    }

    private void DrawChangedItemPopup()
    {
        Im.Window.SetNextSize(ImEx.ScaledVector(500));
        using var p = Im.Popup.Begin("Changed Item List"u8);
        if (!p)
            return;

        using (var table = Im.Table.Begin("##ChangedItems"u8, 3, TableFlags.SizingFixedFit))
        {
            if (table)
            {
                using var clipper = new Im.ListClipper(_changedItems.Length, Im.Style.TextHeightWithSpacing);
                foreach (var item in clipper.Iterate(_changedItems))
                {
                    table.DrawColumn(item.Item1);
                    table.DrawColumn($"{item.Item2}");
                    table.DrawColumn($"{item.Item3}");
                }
            }
        }

        if (Im.Button("Close"u8, -Vector2.UnitX) || !Im.Window.Focused())
            Im.Popup.CloseCurrent();
    }

    private void DrawCollectionPopup()
    {
        Im.Window.SetNextSize(ImEx.ScaledVector(500));
        using var p = Im.Popup.Begin("Collections"u8);
        if (!p)
            return;

        using (var t = Im.Table.Begin("collections"u8, 2, TableFlags.SizingFixedFit))
        {
            if (t)
                foreach (var collection in _collections)
                {
                    t.NextColumn();
                    DrawCollection(t, (collection.Key, collection.Value));
                }
        }

        if (Im.Button("Close"u8, -Vector2.UnitX) || !Im.Window.Focused())
            Im.Popup.CloseCurrent();
    }

    private static void DrawCollection(Im.TableDisposable table, (Guid Id, string Name)? collection)
    {
        table.NextColumn();
        if (collection == null)
        {
            Im.Text("<Unassigned>"u8);
            table.NextColumn();
            return;
        }

        Im.Text(collection.Value.Name);
        table.NextColumn();
        LunaStyle.DrawGuid(collection.Value.Id);
    }
}

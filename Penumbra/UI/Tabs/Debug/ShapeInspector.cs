using Dalamud.Interface;
using ImSharp;
using Penumbra.Collections.Cache;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Interop;
using Penumbra.Interop.PathResolving;
using Penumbra.Meta;
using Penumbra.Meta.Manipulations;

namespace Penumbra.UI.Tabs.Debug;

public class ShapeInspector(ObjectManager objects, CollectionResolver resolver) : Luna.IUiService
{
    private int _objectIndex;

    public void Draw()
    {
        Im.Input.Scalar("Object Index"u8, ref _objectIndex);
        var actor = objects[0];
        if (!actor.IsCharacter)
        {
            Im.Text("No valid character."u8);
            return;
        }

        var human = actor.Model;
        if (!human.IsHuman)
        {
            Im.Text("No valid character."u8);
            return;
        }

        DrawCollectionShapeCache(actor);
        DrawCharacterShapes(human);
        DrawCollectionAttributeCache(actor);
        DrawCharacterAttributes(human);
    }

    private unsafe void DrawCollectionAttributeCache(Actor actor)
    {
        var       data      = resolver.IdentifyCollection(actor.AsObject, true);
        using var treeNode1 = Im.Tree.Node($"Collection Attribute Cache ({data.ModCollection})");
        if (!treeNode1.Success || !data.ModCollection.HasCache)
            return;

        using var table = Im.Table.Begin("##aCache"u8, 2, TableFlags.RowBackground);
        if (!table)
            return;

        table.SetupColumn("Attribute"u8, TableColumnFlags.WidthFixed, 150 * Im.Style.GlobalScale);
        table.SetupColumn("State"u8,     TableColumnFlags.WidthStretch);
        table.HeaderRow();

        foreach (var (attribute, set) in data.ModCollection.MetaCache!.Atr.Data.OrderBy(a => a.Key))
        {
            table.DrawColumn(attribute.AsSpan);
            DrawValues(table, attribute, set);
        }
    }

    private unsafe void DrawCollectionShapeCache(Actor actor)
    {
        var       data      = resolver.IdentifyCollection(actor.AsObject, true);
        using var treeNode1 = Im.Tree.Node($"Collection Shape Cache ({data.ModCollection})");
        if (!treeNode1.Success || !data.ModCollection.HasCache)
            return;

        using var table = Im.Table.Begin("##sCache"u8, 3, TableFlags.RowBackground);
        if (!table)
            return;

        table.SetupColumn("Condition"u8, TableColumnFlags.WidthFixed, 150 * Im.Style.GlobalScale);
        table.SetupColumn("Shape"u8,     TableColumnFlags.WidthFixed, 150 * Im.Style.GlobalScale);
        table.SetupColumn("State"u8,     TableColumnFlags.WidthStretch);
        table.HeaderRow();

        foreach (var condition in Enum.GetValues<ShapeConnectorCondition>())
        {
            foreach (var (shape, set) in data.ModCollection.MetaCache!.Shp.State(condition).OrderBy(shp => shp.Key))
            {
                table.DrawColumn($"{condition}");
                table.DrawColumn(shape.AsSpan);
                DrawValues(table, shape, set);
            }
        }
    }

    private static void DrawValues(in Im.TableDisposable table, in ShapeAttributeString _, ShapeAttributeHashSet set)
    {
        table.NextColumn();
        var disabledColor = Im.Style[ImGuiColor.TextDisabled];
        if (set.All is { } value)
        {
            using var color = ImGuiColor.Text.Push(disabledColor, !value);
            Im.Text("All, "u8);
            Im.Line.Same(0, 0);
        }

        foreach (var slot in ShapeAttributeManager.UsedModels)
        {
            if (set[slot] is not { } value2)
                continue;

            using var color = ImGuiColor.Text.Push(disabledColor, !value2);
            Im.Text($"All {slot.ToNameU8()}, ");
            Im.Line.Same(0, 0);
        }

        foreach (var gr in ShapeAttributeHashSet.GenderRaceValues.Skip(1))
        {
            if (set[gr] is { } value3)
            {
                using var color = ImGuiColor.Text.Push(disabledColor, !value3);
                Im.Text($"All {gr.ToNameU8()}, ");
                Im.Line.Same(0, 0);
            }
            else
            {
                foreach (var slot in ShapeAttributeManager.UsedModels)
                {
                    if (set[slot, gr] is not { } value4)
                        continue;

                    using var color = ImGuiColor.Text.Push(disabledColor, !value4);
                    Im.Text($"All {gr.ToNameU8()} {slot.ToNameU8()}, ");
                    Im.Line.Same(0, 0);
                }
            }
        }

        foreach (var ((slot, id), flags) in set)
        {
            if ((flags & 3) is not 0)
            {
                var enabled = (flags & 1) is 1;

                if (set[slot, GenderRace.Unknown] != enabled)
                {
                    using var color = ImGuiColor.Text.Push(disabledColor, !enabled);
                    Im.Text($"{slot.ToNameU8()} {id.Id:D4}, ");
                    Im.Line.Same(0, 0);
                }
            }
            else
            {
                var currentIndex = BitOperations.TrailingZeroCount(flags) / 2;
                var currentFlags = flags >> (2 * currentIndex);
                while (currentIndex < ShapeAttributeHashSet.GenderRaceValues.Count)
                {
                    var enabled = (currentFlags & 1) is 1;
                    var gr      = ShapeAttributeHashSet.GenderRaceValues[currentIndex];
                    if (set[slot, gr] != enabled)
                    {
                        using var color = ImGuiColor.Text.Push(disabledColor, !enabled);
                        Im.Text($"{gr.ToNameU8()} {slot.ToNameU8()} #{id.Id:D4}, ");
                        Im.Line.Same(0, 0);
                    }

                    currentFlags &= ~0x3u;
                    currentIndex += BitOperations.TrailingZeroCount(currentFlags) / 2;
                    currentFlags =  flags >> (2 * currentIndex);
                }
            }
        }
    }

    private unsafe void DrawCharacterShapes(Model human)
    {
        using var treeNode2 = Im.Tree.Node("Character Model Shapes"u8);
        if (!treeNode2)
            return;

        using var table = Im.Table.Begin("##shapes"u8, 7, TableFlags.RowBackground);
        if (!table)
            return;

        table.SetupColumn("#"u8,       TableColumnFlags.WidthFixed, 25 * Im.Style.GlobalScale);
        table.SetupColumn("Slot"u8,    TableColumnFlags.WidthFixed, 150 * Im.Style.GlobalScale);
        table.SetupColumn("Address"u8, TableColumnFlags.WidthFixed, UiBuilder.MonoFont.GetCharAdvance('0') * 14);
        table.SetupColumn("Mask"u8,    TableColumnFlags.WidthFixed, UiBuilder.MonoFont.GetCharAdvance('0') * 8);
        table.SetupColumn("ID"u8,      TableColumnFlags.WidthFixed, UiBuilder.MonoFont.GetCharAdvance('0') * 4);
        table.SetupColumn("Count"u8,   TableColumnFlags.WidthFixed, 30 * Im.Style.GlobalScale);
        table.SetupColumn("Shapes"u8,  TableColumnFlags.WidthStretch);
        table.HeaderRow();

        var disabledColor = Im.Style[ImGuiColor.TextDisabled];
        for (var i = 0; i < human.AsHuman->SlotCount; ++i)
        {
            table.DrawColumn($"{(uint)i:D2}");
            table.DrawColumn(((HumanSlot)i).ToNameU8());

            table.NextColumn();
            var model = human.AsHuman->Models[i];
            Penumbra.Dynamis.DrawPointer((nint)model);
            if (model is not null)
            {
                var mask = model->EnabledShapeKeyIndexMask;
                table.DrawColumn($"{mask:X8}");
                table.DrawColumn($"{human.GetModelId((HumanSlot)i):D4}");
                table.DrawColumn($"{model->ModelResourceHandle->Shapes.Count}");
                table.NextColumn();
                foreach (var (idx, (shape, flag)) in model->ModelResourceHandle->Shapes.Index())
                {
                    var       disabled = (mask & (1u << flag)) is 0;
                    using var color    = ImGuiColor.Text.Push(disabledColor, disabled);
                    Im.Text(shape.AsSpan());
                    Im.Line.Same(0, 0);
                    Im.Text(",  "u8);
                    if (idx % 8 < 7)
                        Im.Line.Same(0, 0);
                }
            }
            else
            {
                table.NextColumn();
                table.NextColumn();
                table.NextColumn();
                table.NextColumn();
            }
        }
    }

    private unsafe void DrawCharacterAttributes(Model human)
    {
        using var treeNode2 = Im.Tree.Node("Character Model Attributes"u8);
        if (!treeNode2)
            return;

        using var table = Im.Table.Begin("##attributes"u8, 7, TableFlags.RowBackground);
        if (!table)
            return;

        table.SetupColumn("#"u8,          TableColumnFlags.WidthFixed, 25 * Im.Style.GlobalScale);
        table.SetupColumn("Slot"u8,       TableColumnFlags.WidthFixed, 150 * Im.Style.GlobalScale);
        table.SetupColumn("Address"u8,    TableColumnFlags.WidthFixed, UiBuilder.MonoFont.GetCharAdvance('0') * 14);
        table.SetupColumn("Mask"u8,       TableColumnFlags.WidthFixed, UiBuilder.MonoFont.GetCharAdvance('0') * 8);
        table.SetupColumn("ID"u8,         TableColumnFlags.WidthFixed, UiBuilder.MonoFont.GetCharAdvance('0') * 4);
        table.SetupColumn("Count"u8,      TableColumnFlags.WidthFixed, 30 * Im.Style.GlobalScale);
        table.SetupColumn("Attributes"u8, TableColumnFlags.WidthStretch);
        table.HeaderRow();

        var disabledColor = Im.Style[ImGuiColor.TextDisabled];
        for (var i = 0; i < human.AsHuman->SlotCount; ++i)
        {
            table.DrawColumn($"{(uint)i:D2}");
            table.DrawColumn(((HumanSlot)i).ToNameU8());

            table.NextColumn();
            var model = human.AsHuman->Models[i];
            Penumbra.Dynamis.DrawPointer((nint)model);
            if (model is not null)
            {
                var mask = model->EnabledAttributeIndexMask;
                table.DrawColumn($"{mask:X8}");
                table.DrawColumn($"{human.GetModelId((HumanSlot)i):D4}");
                table.DrawColumn($"{model->ModelResourceHandle->Attributes.Count}");
                table.NextColumn();
                foreach (var (idx, (attribute, flag)) in model->ModelResourceHandle->Attributes.Index())
                {
                    var       disabled = (mask & (1u << flag)) is 0;
                    using var color    = ImGuiColor.Text.Push(disabledColor, disabled);
                    Im.Text(attribute.AsSpan());
                    Im.Line.Same(0, 0);
                    Im.Text(",  "u8);
                    if (idx % 8 < 7)
                        Im.Line.Same(0, 0);
                }
            }
            else
            {
                table.NextColumn();
                table.NextColumn();
                table.NextColumn();
                table.NextColumn();
            }
        }
    }
}

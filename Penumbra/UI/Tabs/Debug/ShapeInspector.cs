using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using ImSharp;
using OtterGui.Text;
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
        ImUtf8.InputScalar("Object Index"u8, ref _objectIndex);
        var actor = objects[0];
        if (!actor.IsCharacter)
        {
            ImUtf8.Text("No valid character."u8);
            return;
        }

        var human = actor.Model;
        if (!human.IsHuman)
        {
            ImUtf8.Text("No valid character."u8);
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
        using var treeNode1 = ImUtf8.TreeNode($"Collection Attribute Cache ({data.ModCollection})");
        if (!treeNode1.Success || !data.ModCollection.HasCache)
            return;

        using var table = Im.Table.Begin("##aCache"u8, 2, TableFlags.RowBackground);
        if (!table)
            return;

        table.SetupColumn("Attribute"u8, TableColumnFlags.WidthFixed, 150 * Im.Style.GlobalScale);
        table.SetupColumn("State"u8,     TableColumnFlags.WidthStretch);

        ImGui.TableHeadersRow();
        foreach (var (attribute, set) in data.ModCollection.MetaCache!.Atr.Data.OrderBy(a => a.Key))
        {
            ImUtf8.DrawTableColumn(attribute.AsSpan);
            DrawValues(attribute, set);
        }
    }

    private unsafe void DrawCollectionShapeCache(Actor actor)
    {
        var       data      = resolver.IdentifyCollection(actor.AsObject, true);
        using var treeNode1 = ImUtf8.TreeNode($"Collection Shape Cache ({data.ModCollection})");
        if (!treeNode1.Success || !data.ModCollection.HasCache)
            return;

        using var table = Im.Table.Begin("##sCache"u8, 3, TableFlags.RowBackground);
        if (!table)
            return;

        table.SetupColumn("Condition"u8, TableColumnFlags.WidthFixed, 150 * Im.Style.GlobalScale);
        table.SetupColumn("Shape"u8,     TableColumnFlags.WidthFixed, 150 * Im.Style.GlobalScale);
        table.SetupColumn("State"u8,     TableColumnFlags.WidthStretch);

        ImGui.TableHeadersRow();
        foreach (var condition in Enum.GetValues<ShapeConnectorCondition>())
        {
            foreach (var (shape, set) in data.ModCollection.MetaCache!.Shp.State(condition).OrderBy(shp => shp.Key))
            {
                ImUtf8.DrawTableColumn(condition.ToString());
                ImUtf8.DrawTableColumn(shape.AsSpan);
                DrawValues(shape, set);
            }
        }
    }

    private static void DrawValues(in ShapeAttributeString shapeAttribute, ShapeAttributeHashSet set)
    {
        ImGui.TableNextColumn();
        var disabledColor = Im.Style[ImGuiColor.TextDisabled];
        if (set.All is { } value)
        {
            using var color = ImGuiColor.Text.Push(disabledColor, !value);
            ImUtf8.Text("All, "u8);
            ImGui.SameLine(0, 0);
        }

        foreach (var slot in ShapeAttributeManager.UsedModels)
        {
            if (set[slot] is not { } value2)
                continue;

            using var color = ImGuiColor.Text.Push(disabledColor, !value2);
            ImUtf8.Text($"All {slot.ToName()}, ");
            ImGui.SameLine(0, 0);
        }

        foreach (var gr in ShapeAttributeHashSet.GenderRaceValues.Skip(1))
        {
            if (set[gr] is { } value3)
            {
                using var color = ImGuiColor.Text.Push(disabledColor, !value3);
                ImUtf8.Text($"All {gr.ToName()}, ");
                ImGui.SameLine(0, 0);
            }
            else
            {
                foreach (var slot in ShapeAttributeManager.UsedModels)
                {
                    if (set[slot, gr] is not { } value4)
                        continue;

                    using var color = ImGuiColor.Text.Push(disabledColor, !value4);
                    ImUtf8.Text($"All {gr.ToName()} {slot.ToName()}, ");
                    ImGui.SameLine(0, 0);
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
                    ImUtf8.Text($"{slot.ToName()} {id.Id:D4}, ");
                    ImGui.SameLine(0, 0);
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
                        ImUtf8.Text($"{gr.ToName()} {slot.ToName()} #{id.Id:D4}, ");
                        ImGui.SameLine(0, 0);
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
        using var treeNode2 = ImUtf8.TreeNode("Character Model Shapes"u8);
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

        ImGui.TableHeadersRow();

        var disabledColor = Im.Style[ImGuiColor.TextDisabled];
        for (var i = 0; i < human.AsHuman->SlotCount; ++i)
        {
            ImUtf8.DrawTableColumn($"{(uint)i:D2}");
            ImUtf8.DrawTableColumn(((HumanSlot)i).ToName());

            ImGui.TableNextColumn();
            var model = human.AsHuman->Models[i];
            Penumbra.Dynamis.DrawPointer((nint)model);
            if (model is not null)
            {
                var mask = model->EnabledShapeKeyIndexMask;
                ImUtf8.DrawTableColumn($"{mask:X8}");
                ImUtf8.DrawTableColumn($"{human.GetModelId((HumanSlot)i):D4}");
                ImUtf8.DrawTableColumn($"{model->ModelResourceHandle->Shapes.Count}");
                ImGui.TableNextColumn();
                foreach (var (idx, (shape, flag)) in model->ModelResourceHandle->Shapes.Index())
                {
                    var       disabled = (mask & (1u << flag)) is 0;
                    using var color    = ImGuiColor.Text.Push(disabledColor, disabled);
                    ImUtf8.Text(shape.AsSpan());
                    ImGui.SameLine(0, 0);
                    ImUtf8.Text(",  "u8);
                    if (idx % 8 < 7)
                        ImGui.SameLine(0, 0);
                }
            }
            else
            {
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
            }
        }
    }

    private unsafe void DrawCharacterAttributes(Model human)
    {
        using var treeNode2 = ImUtf8.TreeNode("Character Model Attributes"u8);
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

        ImGui.TableHeadersRow();

        var disabledColor = Im.Style[ImGuiColor.TextDisabled];
        for (var i = 0; i < human.AsHuman->SlotCount; ++i)
        {
            ImUtf8.DrawTableColumn($"{(uint)i:D2}");
            ImUtf8.DrawTableColumn(((HumanSlot)i).ToName());

            ImGui.TableNextColumn();
            var model = human.AsHuman->Models[i];
            Penumbra.Dynamis.DrawPointer((nint)model);
            if (model is not null)
            {
                var mask = model->EnabledAttributeIndexMask;
                ImUtf8.DrawTableColumn($"{mask:X8}");
                ImUtf8.DrawTableColumn($"{human.GetModelId((HumanSlot)i):D4}");
                ImUtf8.DrawTableColumn($"{model->ModelResourceHandle->Attributes.Count}");
                ImGui.TableNextColumn();
                foreach (var (idx, (attribute, flag)) in model->ModelResourceHandle->Attributes.Index())
                {
                    var       disabled = (mask & (1u << flag)) is 0;
                    using var color    = ImGuiColor.Text.Push(disabledColor, disabled);
                    ImUtf8.Text(attribute.AsSpan());
                    ImGui.SameLine(0, 0);
                    ImUtf8.Text(",  "u8);
                    if (idx % 8 < 7)
                        ImGui.SameLine(0, 0);
                }
            }
            else
            {
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
            }
        }
    }
}

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using OtterGui.Extensions;
using OtterGui.Services;
using OtterGui.Text;
using Penumbra.Collections.Cache;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Interop;
using Penumbra.Interop.PathResolving;
using Penumbra.Meta;
using Penumbra.Meta.Manipulations;

namespace Penumbra.UI.Tabs.Debug;

public class ShapeInspector(ObjectManager objects, CollectionResolver resolver) : IUiService
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

        using var table = ImUtf8.Table("##aCache"u8, 2, ImGuiTableFlags.RowBg);
        if (!table)
            return;

        ImUtf8.TableSetupColumn("Attribute"u8, ImGuiTableColumnFlags.WidthFixed, 150 * ImUtf8.GlobalScale);
        ImUtf8.TableSetupColumn("State"u8,     ImGuiTableColumnFlags.WidthStretch);

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

        using var table = ImUtf8.Table("##sCache"u8, 3, ImGuiTableFlags.RowBg);
        if (!table)
            return;

        ImUtf8.TableSetupColumn("Condition"u8, ImGuiTableColumnFlags.WidthFixed, 150 * ImUtf8.GlobalScale);
        ImUtf8.TableSetupColumn("Shape"u8,     ImGuiTableColumnFlags.WidthFixed, 150 * ImUtf8.GlobalScale);
        ImUtf8.TableSetupColumn("State"u8,     ImGuiTableColumnFlags.WidthStretch);

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

        if (set.All is { } value)
        {
            using var color = ImRaii.PushColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.TextDisabled), !value);
            ImUtf8.Text("All, "u8);
            ImGui.SameLine(0, 0);
        }

        foreach (var slot in ShapeAttributeManager.UsedModels)
        {
            if (set[slot] is not { } value2)
                continue;

            using var color = ImRaii.PushColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.TextDisabled), !value2);
            ImUtf8.Text($"All {slot.ToName()}, ");
            ImGui.SameLine(0, 0);
        }

        foreach (var gr in ShapeAttributeHashSet.GenderRaceValues.Skip(1))
        {
            if (set[gr] is { } value3)
            {
                using var color = ImRaii.PushColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.TextDisabled), !value3);
                ImUtf8.Text($"All {gr.ToName()}, ");
                ImGui.SameLine(0, 0);
            }
            else
            {
                foreach (var slot in ShapeAttributeManager.UsedModels)
                {
                    if (set[slot, gr] is not { } value4)
                        continue;

                    using var color = ImRaii.PushColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.TextDisabled), !value4);
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
                    using var color = ImRaii.PushColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.TextDisabled), !enabled);
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
                        using var color = ImRaii.PushColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.TextDisabled), !enabled);
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

        using var table = ImUtf8.Table("##shapes"u8, 7, ImGuiTableFlags.RowBg);
        if (!table)
            return;

        ImUtf8.TableSetupColumn("#"u8,       ImGuiTableColumnFlags.WidthFixed, 25 * ImUtf8.GlobalScale);
        ImUtf8.TableSetupColumn("Slot"u8,    ImGuiTableColumnFlags.WidthFixed, 150 * ImUtf8.GlobalScale);
        ImUtf8.TableSetupColumn("Address"u8, ImGuiTableColumnFlags.WidthFixed, UiBuilder.MonoFont.GetCharAdvance('0') * 14);
        ImUtf8.TableSetupColumn("Mask"u8,    ImGuiTableColumnFlags.WidthFixed, UiBuilder.MonoFont.GetCharAdvance('0') * 8);
        ImUtf8.TableSetupColumn("ID"u8,      ImGuiTableColumnFlags.WidthFixed, UiBuilder.MonoFont.GetCharAdvance('0') * 4);
        ImUtf8.TableSetupColumn("Count"u8,   ImGuiTableColumnFlags.WidthFixed, 30 * ImUtf8.GlobalScale);
        ImUtf8.TableSetupColumn("Shapes"u8,  ImGuiTableColumnFlags.WidthStretch);

        ImGui.TableHeadersRow();

        var disabledColor = ImGui.GetColorU32(ImGuiCol.TextDisabled);
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
                foreach (var ((shape, flag), idx) in model->ModelResourceHandle->Shapes.WithIndex())
                {
                    var       disabled = (mask & (1u << flag)) is 0;
                    using var color    = ImRaii.PushColor(ImGuiCol.Text, disabledColor, disabled);
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

        using var table = ImUtf8.Table("##attributes"u8, 7, ImGuiTableFlags.RowBg);
        if (!table)
            return;

        ImUtf8.TableSetupColumn("#"u8,          ImGuiTableColumnFlags.WidthFixed, 25 * ImUtf8.GlobalScale);
        ImUtf8.TableSetupColumn("Slot"u8,       ImGuiTableColumnFlags.WidthFixed, 150 * ImUtf8.GlobalScale);
        ImUtf8.TableSetupColumn("Address"u8,    ImGuiTableColumnFlags.WidthFixed, UiBuilder.MonoFont.GetCharAdvance('0') * 14);
        ImUtf8.TableSetupColumn("Mask"u8,       ImGuiTableColumnFlags.WidthFixed, UiBuilder.MonoFont.GetCharAdvance('0') * 8);
        ImUtf8.TableSetupColumn("ID"u8,         ImGuiTableColumnFlags.WidthFixed, UiBuilder.MonoFont.GetCharAdvance('0') * 4);
        ImUtf8.TableSetupColumn("Count"u8,      ImGuiTableColumnFlags.WidthFixed, 30 * ImUtf8.GlobalScale);
        ImUtf8.TableSetupColumn("Attributes"u8, ImGuiTableColumnFlags.WidthStretch);

        ImGui.TableHeadersRow();

        var disabledColor = ImGui.GetColorU32(ImGuiCol.TextDisabled);
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
                foreach (var ((attribute, flag), idx) in model->ModelResourceHandle->Attributes.WithIndex())
                {
                    var       disabled = (mask & (1u << flag)) is 0;
                    using var color    = ImRaii.PushColor(ImGuiCol.Text, disabledColor, disabled);
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

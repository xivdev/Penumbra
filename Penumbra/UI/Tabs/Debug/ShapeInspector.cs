using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
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
    }

    private unsafe void DrawCollectionShapeCache(Actor actor)
    {
        var       data      = resolver.IdentifyCollection(actor.AsObject, true);
        using var treeNode1 = ImUtf8.TreeNode($"Collection Shape Cache ({data.ModCollection})");
        if (!treeNode1.Success || !data.ModCollection.HasCache)
            return;

        using var table = ImUtf8.Table("##cacheTable"u8, 3, ImGuiTableFlags.RowBg);
        if (!table)
            return;

        ImUtf8.TableSetupColumn("Condition"u8, ImGuiTableColumnFlags.WidthFixed, 150 * ImUtf8.GlobalScale);
        ImUtf8.TableSetupColumn("Shape"u8,     ImGuiTableColumnFlags.WidthFixed, 150 * ImUtf8.GlobalScale);
        ImUtf8.TableSetupColumn("Enabled"u8,   ImGuiTableColumnFlags.WidthStretch);

        ImGui.TableHeadersRow();
        foreach (var condition in Enum.GetValues<ShapeConnectorCondition>())
        {
            foreach (var (shape, set) in data.ModCollection.MetaCache!.Shp.State(condition))
            {
                ImUtf8.DrawTableColumn(condition.ToString());
                DrawShape(shape, set);
            }
        }
    }

    private static void DrawShape(in ShapeString shape, ShpCache.ShpHashSet set)
    {
        ImUtf8.DrawTableColumn(shape.AsSpan);
        if (set.All)
        {
            ImUtf8.DrawTableColumn("All"u8);
        }
        else
        {
            ImGui.TableNextColumn();
            foreach (var slot in ShapeManager.UsedModels)
            {
                if (!set[slot])
                    continue;

                ImUtf8.Text($"All {slot.ToName()}, ");
                ImGui.SameLine(0, 0);
            }

            foreach (var item in set.Where(i => !set[i.Slot]))
            {
                ImUtf8.Text($"{item.Slot.ToName()} {item.Id.Id:D4}, ");
                ImGui.SameLine(0, 0);
            }
        }
    }

    private unsafe void DrawCharacterShapes(Model human)
    {
        using var treeNode2 = ImUtf8.TreeNode("Character Model Shapes"u8);
        if (!treeNode2)
            return;

        using var table = ImUtf8.Table("##table"u8, 5, ImGuiTableFlags.RowBg);
        if (!table)
            return;

        ImUtf8.TableSetupColumn("#"u8,       ImGuiTableColumnFlags.WidthFixed, 25 * ImUtf8.GlobalScale);
        ImUtf8.TableSetupColumn("Slot"u8,    ImGuiTableColumnFlags.WidthFixed, 150 * ImUtf8.GlobalScale);
        ImUtf8.TableSetupColumn("Address"u8, ImGuiTableColumnFlags.WidthFixed, UiBuilder.MonoFont.GetCharAdvance('0') * 14);
        ImUtf8.TableSetupColumn("Mask"u8,    ImGuiTableColumnFlags.WidthFixed, UiBuilder.MonoFont.GetCharAdvance('0') * 8);
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
                ImGui.TableNextColumn();
                foreach (var (shape, idx) in model->ModelResourceHandle->Shapes)
                {
                    var       disabled = (mask & (1u << idx)) is 0;
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
            }
        }
    }
}

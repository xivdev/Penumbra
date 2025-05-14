using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using OtterGui.Services;
using OtterGui.Text;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Interop;

namespace Penumbra.UI.Tabs.Debug;

public class ShapeInspector(ObjectManager objects) : IUiService
{
    private int _objectIndex = 0;

    public unsafe void Draw()
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

        using var table = ImUtf8.Table("##table"u8, 4, ImGuiTableFlags.RowBg);
        if (!table)
            return;

        ImUtf8.TableSetupColumn("idx"u8,    ImGuiTableColumnFlags.WidthFixed, 25 * ImUtf8.GlobalScale);
        ImUtf8.TableSetupColumn("ptr"u8,    ImGuiTableColumnFlags.WidthFixed, UiBuilder.MonoFont.GetCharAdvance('0') * 14);
        ImUtf8.TableSetupColumn("mask"u8,   ImGuiTableColumnFlags.WidthFixed, UiBuilder.MonoFont.GetCharAdvance('0') * 8);
        ImUtf8.TableSetupColumn("shapes"u8, ImGuiTableColumnFlags.WidthStretch);

        var disabledColor = ImGui.GetColorU32(ImGuiCol.TextDisabled);
        foreach (var slot in Enum.GetValues<HumanSlot>())
        {
            ImUtf8.DrawTableColumn($"{(uint)slot:D2}");
            ImGui.TableNextColumn();
            var model = human.AsHuman->Models[(int)slot];
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
                    if ((idx % 8) < 7)
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

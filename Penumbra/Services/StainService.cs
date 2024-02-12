using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using ImGuiNET;
using OtterGui.Services;
using OtterGui.Widgets;
using Penumbra.GameData.DataContainers;
using Penumbra.GameData.Files;
using Penumbra.UI.AdvancedWindow;

namespace Penumbra.Services;

public class StainService : IService
{
    public sealed class StainTemplateCombo(FilterComboColors stainCombo, StmFile stmFile)
        : FilterComboCache<ushort>(stmFile.Entries.Keys.Prepend((ushort)0), MouseWheelType.None, Penumbra.Log)
    {
        protected override float GetFilterWidth()
        {
            var baseSize = ImGui.CalcTextSize("0000").X + ImGui.GetStyle().ScrollbarSize + ImGui.GetStyle().ItemInnerSpacing.X;
            if (stainCombo.CurrentSelection.Key == 0)
                return baseSize;

            return baseSize + ImGui.GetTextLineHeight() * 3 + ImGui.GetStyle().ItemInnerSpacing.X * 3;
        }

        protected override string ToString(ushort obj)
            => $"{obj,4}";

        protected override void DrawFilter(int currentSelected, float width)
        {
            using var font = ImRaii.PushFont(UiBuilder.DefaultFont);
            base.DrawFilter(currentSelected, width);
        }

        public override bool Draw(string label, string preview, string tooltip, ref int currentSelection, float previewWidth, float itemHeight,
            ImGuiComboFlags flags = ImGuiComboFlags.None)
        {
            using var font = ImRaii.PushFont(UiBuilder.MonoFont);
            using var style = ImRaii.PushStyle(ImGuiStyleVar.ButtonTextAlign, new Vector2(1, 0.5f))
                .Push(ImGuiStyleVar.ItemSpacing, ImGui.GetStyle().ItemSpacing with { X = ImGui.GetStyle().ItemInnerSpacing.X });
            var spaceSize = ImGui.CalcTextSize(" ").X;
            var spaces    = (int)(previewWidth / spaceSize) - 1;
            return base.Draw(label, preview.PadLeft(spaces), tooltip, ref currentSelection, previewWidth, itemHeight, flags);
        }

        protected override bool DrawSelectable(int globalIdx, bool selected)
        {
            var ret       = base.DrawSelectable(globalIdx, selected);
            var selection = stainCombo.CurrentSelection.Key;
            if (selection == 0 || !stmFile.TryGetValue(Items[globalIdx], selection, out var colors))
                return ret;

            ImGui.SameLine();
            var frame = new Vector2(ImGui.GetTextLineHeight());
            ImGui.ColorButton("D", new Vector4(ModEditWindow.PseudoSqrtRgb(colors.Diffuse), 1), 0, frame);
            ImGui.SameLine();
            ImGui.ColorButton("S", new Vector4(ModEditWindow.PseudoSqrtRgb(colors.Specular), 1), 0, frame);
            ImGui.SameLine();
            ImGui.ColorButton("E", new Vector4(ModEditWindow.PseudoSqrtRgb(colors.Emissive), 1), 0, frame);
            return ret;
        }
    }

    public readonly DictStain          StainData;
    public readonly FilterComboColors  StainCombo;
    public readonly StmFile            StmFile;
    public readonly StainTemplateCombo TemplateCombo;

    public StainService(IDataManager dataManager, DictStain stainData)
    {
        StainData = stainData;
        StainCombo = new FilterComboColors(140, MouseWheelType.None,
            () => StainData.Value.Prepend(new KeyValuePair<byte, (string Name, uint Dye, bool Gloss)>(0, ("None", 0, false))).ToList(), 
            Penumbra.Log);
        StmFile       = new StmFile(dataManager);
        TemplateCombo = new StainTemplateCombo(StainCombo, StmFile);
    }
}

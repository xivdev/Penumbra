using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ImGuiNET;
using OtterGui.Widgets;
using Penumbra.GameData.Data;
using Penumbra.GameData.Files;
using Penumbra.UI.AdvancedWindow;
using Penumbra.Util;

namespace Penumbra.Services;

public class StainService : IDisposable
{
    public sealed class StainTemplateCombo : FilterComboCache<ushort>
    {
        private readonly StmFile           _stmFile;
        private readonly FilterComboColors _stainCombo;

        public StainTemplateCombo(FilterComboColors stainCombo, StmFile stmFile)
            : base(stmFile.Entries.Keys.Prepend((ushort)0), Penumbra.Log)
        {
            _stainCombo = stainCombo;
            _stmFile    = stmFile;
        }

        protected override float GetFilterWidth()
        {
            var       baseSize = ImGui.CalcTextSize("0000").X + ImGui.GetStyle().ScrollbarSize + ImGui.GetStyle().ItemInnerSpacing.X;
            if (_stainCombo.CurrentSelection.Key == 0)
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
            using var font  = ImRaii.PushFont(UiBuilder.MonoFont);
            using var style = ImRaii.PushStyle(ImGuiStyleVar.ButtonTextAlign, new Vector2(1, 0.5f))
                .Push(ImGuiStyleVar.ItemSpacing, ImGui.GetStyle().ItemSpacing with { X = ImGui.GetStyle().ItemInnerSpacing.X });
            var spaceSize = ImGui.CalcTextSize(" ").X;
            var spaces    = (int) (previewWidth / spaceSize) - 1;
            return base.Draw(label, preview.PadLeft(spaces), tooltip, ref currentSelection, previewWidth, itemHeight, flags);
        }

        protected override bool DrawSelectable(int globalIdx, bool selected)
        {
            var ret       = base.DrawSelectable(globalIdx, selected);
            var selection = _stainCombo.CurrentSelection.Key;
            if (selection == 0 || !_stmFile.TryGetValue(Items[globalIdx], selection, out var colors))
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

    public readonly StainData          StainData;
    public readonly FilterComboColors  StainCombo;
    public readonly StmFile            StmFile;
    public readonly StainTemplateCombo TemplateCombo;

    public StainService(StartTracker timer, DalamudPluginInterface pluginInterface, IDataManager dataManager, IPluginLog dalamudLog)
    {
        using var t = timer.Measure(StartTimeType.Stains);
        StainData = new StainData(pluginInterface, dataManager, dataManager.Language, dalamudLog);
        StainCombo = new FilterComboColors(140,
            () => StainData.Data.Prepend(new KeyValuePair<byte, (string Name, uint Dye, bool Gloss)>(0, ("None", 0, false))).ToList(),
            Penumbra.Log);
        StmFile       = new StmFile(dataManager);
        TemplateCombo = new StainTemplateCombo(StainCombo, StmFile);
        Penumbra.Log.Verbose($"[{nameof(StainService)}] Created.");
    }

    public void Dispose()
    {
        StainData.Dispose();
        Penumbra.Log.Verbose($"[{nameof(StainService)}] Disposed.");
    }
}

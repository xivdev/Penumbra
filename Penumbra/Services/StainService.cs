using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using ImGuiNET;
using OtterGui.Services;
using OtterGui.Widgets;
using Penumbra.GameData.DataContainers;
using Penumbra.GameData.Files;
using Penumbra.GameData.Files.StainMapStructs;
using Penumbra.Interop.Services;
using Penumbra.Interop.Structs;
using Penumbra.UI.AdvancedWindow.Materials;

namespace Penumbra.Services;

public class StainService : IService
{
    public sealed class StainTemplateCombo<TDyePack>(FilterComboColors[] stainCombos, StmFile<TDyePack> stmFile)
        : FilterComboCache<ushort>(stmFile.Entries.Keys.Prepend((ushort)0), MouseWheelType.None, Penumbra.Log)
        where TDyePack : unmanaged, IDyePack
    {
        // FIXME There might be a better way to handle that.
        public int CurrentDyeChannel = 0;

        protected override float GetFilterWidth()
        {
            var baseSize = ImGui.CalcTextSize("0000").X + ImGui.GetStyle().ScrollbarSize + ImGui.GetStyle().ItemInnerSpacing.X;
            if (stainCombos[CurrentDyeChannel].CurrentSelection.Key == 0)
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
            var selection = stainCombos[CurrentDyeChannel].CurrentSelection.Key;
            if (selection == 0 || !stmFile.TryGetValue(Items[globalIdx], selection, out var colors))
                return ret;

            ImGui.SameLine();
            var frame = new Vector2(ImGui.GetTextLineHeight());
            ImGui.ColorButton("D", new Vector4(MtrlTab.PseudoSqrtRgb((Vector3)colors.DiffuseColor), 1), 0, frame);
            ImGui.SameLine();
            ImGui.ColorButton("S", new Vector4(MtrlTab.PseudoSqrtRgb((Vector3)colors.SpecularColor), 1), 0, frame);
            ImGui.SameLine();
            ImGui.ColorButton("E", new Vector4(MtrlTab.PseudoSqrtRgb((Vector3)colors.EmissiveColor), 1), 0, frame);
            return ret;
        }
    }

    public const int ChannelCount = 2;

    public readonly DictStain                         StainData;
    public readonly FilterComboColors                 StainCombo1;
    public readonly FilterComboColors                 StainCombo2; // FIXME is there a better way to handle this?
    public readonly StmFile<LegacyDyePack>            LegacyStmFile;
    public readonly StmFile<DyePack>                  GudStmFile;
    public readonly StainTemplateCombo<LegacyDyePack> LegacyTemplateCombo;
    public readonly StainTemplateCombo<DyePack>       GudTemplateCombo;

    public unsafe StainService(IDataManager dataManager, CharacterUtility characterUtility, DictStain stainData)
    {
        StainData   = stainData;
        StainCombo1 = CreateStainCombo();
        StainCombo2 = CreateStainCombo();

        if (characterUtility.Address == null)
        {
            LegacyStmFile = LoadStmFile<LegacyDyePack>(null, dataManager);
            GudStmFile    = LoadStmFile<DyePack>(null, dataManager);
        }
        else
        {
            LegacyStmFile = LoadStmFile<LegacyDyePack>(characterUtility.Address->LegacyStmResource, dataManager);
            GudStmFile    = LoadStmFile<DyePack>(characterUtility.Address->GudStmResource, dataManager);
        }


        FilterComboColors[] stainCombos = [StainCombo1, StainCombo2];

        LegacyTemplateCombo = new StainTemplateCombo<LegacyDyePack>(stainCombos, LegacyStmFile);
        GudTemplateCombo    = new StainTemplateCombo<DyePack>(stainCombos, GudStmFile);
    }

    /// <summary> Retrieves the <see cref="FilterComboColors"/> instance for the given channel. Indexing is zero-based. </summary>
    public FilterComboColors GetStainCombo(int channel)
        => channel switch
        {
            0 => StainCombo1,
            1 => StainCombo2,
            _ => throw new ArgumentOutOfRangeException(nameof(channel), channel,
                $"Unsupported dye channel {channel} (supported values are 0 and 1)"),
        };

    /// <summary> Loads a STM file. Opportunistically attempts to re-use the file already read by the game, with Lumina fallback. </summary>
    private static unsafe StmFile<TDyePack> LoadStmFile<TDyePack>(ResourceHandle* stmResourceHandle, IDataManager dataManager)
        where TDyePack : unmanaged, IDyePack
    {
        if (stmResourceHandle != null)
        {
            var stmData = stmResourceHandle->CsHandle.GetDataSpan();
            if (stmData.Length > 0)
            {
                Penumbra.Log.Debug($"[StainService] Loading StmFile<{typeof(TDyePack)}> from ResourceHandle 0x{(nint)stmResourceHandle:X}");
                return new StmFile<TDyePack>(stmData);
            }
        }

        Penumbra.Log.Debug($"[StainService] Loading StmFile<{typeof(TDyePack)}> from Lumina");
        return new StmFile<TDyePack>(dataManager);
    }

    private FilterComboColors CreateStainCombo()
        => new(140, MouseWheelType.None,
            () => StainData.Value.Prepend(new KeyValuePair<byte, (string Name, uint Dye, bool Gloss)>(0, ("None", 0, false))).ToList(),
            Penumbra.Log);
}

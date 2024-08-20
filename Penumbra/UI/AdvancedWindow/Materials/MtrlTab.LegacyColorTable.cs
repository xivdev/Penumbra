using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using OtterGui.Text;
using Penumbra.GameData.Files.MaterialStructs;
using Penumbra.GameData.Files.StainMapStructs;
using Penumbra.Services;

namespace Penumbra.UI.AdvancedWindow.Materials;

public partial class MtrlTab
{
    private bool DrawLegacyColorTable<TRow, TDyeRow>(IColorTable<TRow> table, IColorDyeTable<TDyeRow>? dyeTable, bool disabled, MtrlTabUiState uiState)
        where TRow : unmanaged, ILegacyColorRow where TDyeRow : unmanaged, ILegacyColorDyeRow
    {
        DrawColorTablePairSelector(table, disabled, uiState);
        return DrawLegacyColorTablePairEditor(table, dyeTable, disabled, uiState);
    }

    private bool DrawLegacyColorTablePairEditor<TRow, TDyeRow>(IColorTable<TRow> table, IColorDyeTable<TDyeRow>? dyeTable, bool disabled, MtrlTabUiState uiState)
        where TRow : unmanaged, ILegacyColorRow where TDyeRow : unmanaged, ILegacyColorDyeRow
    {
        var retA        = false;
        var retB        = false;
        var rowAIdx     = uiState.ColorTableSelectedPair << 1;
        var rowBIdx     = rowAIdx | 1;
        var dyeA        = dyeTable?[uiState.ColorTableSelectedPair << 1] ?? default;
        var dyeB        = dyeTable?[(uiState.ColorTableSelectedPair << 1) | 1] ?? default;
        var previewDyeA = _stainService.GetStainCombo(dyeA.Channel).CurrentSelection.Key;
        var previewDyeB = _stainService.GetStainCombo(dyeB.Channel).CurrentSelection.Key;
        var dyePackA    = _stainService.LegacyStmFile.GetValueOrNull(dyeA.Template, previewDyeA);
        var dyePackB    = _stainService.LegacyStmFile.GetValueOrNull(dyeB.Template, previewDyeB);
        using (var columns = ImUtf8.Columns(2, "ColorTable"u8))
        {
            using (ImUtf8.PushId("RowHeaderA"u8))
            {
                retA |= DrawRowHeader(rowAIdx, disabled);
            }
            columns.Next();
            using (ImUtf8.PushId("RowHeaderB"u8))
            {
                retB |= DrawRowHeader(rowBIdx, disabled);
            }
        }

        DrawHeader("  Colors"u8);
        using (var columns = ImUtf8.Columns(2, "ColorTable"u8))
        {
            using var dis = ImRaii.Disabled(disabled);
            using (ImUtf8.PushId("ColorsA"u8))
            {
                retA |= DrawColors(table, dyeTable, dyePackA, rowAIdx);
            }

            columns.Next();
            using (ImUtf8.PushId("ColorsB"u8))
            {
                retB |= DrawColors(table, dyeTable, dyePackB, rowBIdx);
            }
        }

        DrawHeader("  Specular Parameters"u8);
        using (var columns = ImUtf8.Columns(2, "ColorTable"u8))
        {
            using var dis = ImRaii.Disabled(disabled);
            using (ImUtf8.PushId("SpecularA"u8))
            {
                retA |= DrawLegacySpecular(table, dyeTable, dyePackA, rowAIdx);
            }

            columns.Next();
            using (ImUtf8.PushId("SpecularB"u8))
            {
                retB |= DrawLegacySpecular(table, dyeTable, dyePackB, rowBIdx);
            }
        }

        DrawHeader("  Material Template"u8);
        using (var columns = ImUtf8.Columns(2, "ColorTable"u8))
        {
            using var dis = ImRaii.Disabled(disabled);
            using (ImUtf8.PushId("TemplateA"u8))
            {
                retA |= DrawTemplateTile(table, rowAIdx);
            }

            columns.Next();
            using (ImUtf8.PushId("TemplateB"u8))
            {
                retB |= DrawTemplateTile(table, rowBIdx);
            }
        }

        if (dyeTable != null)
        {
            DrawHeader("  Dye Properties"u8);
            using var columns = ImUtf8.Columns(2, "ColorTable"u8);
            using var dis = ImRaii.Disabled(disabled);
            using (ImUtf8.PushId("DyeA"u8))
            {
                retA |= DrawLegacyDye(dyeTable, dyePackA, rowAIdx);
            }

            columns.Next();
            using (ImUtf8.PushId("DyeB"u8))
            {
                retB |= DrawLegacyDye(dyeTable, dyePackB, rowBIdx);
            }
        }

        if (retA)
            UpdateColorTableRowPreview(rowAIdx);
        if (retB)
            UpdateColorTableRowPreview(rowBIdx);

        return retA | retB;
    }

    private static bool DrawLegacySpecular<TRow, TDyeRow>(IColorTable<TRow> table, IColorDyeTable<TDyeRow>? dyeTable, LegacyDyePack? dyePack, int rowIdx)
        where TRow : unmanaged, ILegacyColorRow where TDyeRow : unmanaged, ILegacyColorDyeRow
    {
        var scalarSize  = ColorTableScalarSize * UiHelpers.Scale;
        var subColWidth = CalculateSubColumnWidth(2) + ImGui.GetStyle().ItemSpacing.X;
        var dyeOffset = subColWidth
          - ImGui.GetStyle().ItemSpacing.X * 2.0f
          - ImGui.GetStyle().ItemInnerSpacing.X
          - ImGui.GetFrameHeight()
          - scalarSize;

        var     ret = false;
        ref var row = ref table[rowIdx];
        var     dye = dyeTable?[rowIdx] ?? default;

        ImGui.SetNextItemWidth(scalarSize);
        ret |= CtDragScalar("Specular Strength"u8, default, (float)row.SpecularMask * 100.0f, "%.0f%%"u8, 0.0f, HalfMaxValue * 100.0f,
            1.0f,
            v => table[rowIdx].SpecularMask = (Half)(v * 0.01f));
        if (dyeTable != null)
        {
            ImGui.SameLine(dyeOffset);
            ret |= CtApplyStainCheckbox("##dyeSpecularMask"u8, "Apply Specular Strength on Dye"u8, dye.SpecularMask,
                b => dyeTable[rowIdx].SpecularMask = b);
            ImUtf8.SameLineInner();
            ImGui.SetNextItemWidth(scalarSize);
            CtDragScalar("##dyePreviewSpecularMask"u8, "Dye Preview for Specular Strength"u8, (float?)dyePack?.SpecularMask * 100.0f, "%.0f%%"u8);
        }

        ImGui.SameLine(subColWidth);
        ImGui.SetNextItemWidth(scalarSize);
        var shininessMin = ImGui.GetIO().KeyCtrl ? 0.0f : HalfEpsilon;
        ret |= CtDragHalf("Gloss"u8, default, row.Shininess, "%.1f"u8, shininessMin, HalfMaxValue,
            Math.Max(0.1f, (float)row.Shininess * 0.025f),
            v => table[rowIdx].Shininess = v);
        if (dyeTable != null)
        {
            ImGui.SameLine(subColWidth + dyeOffset);
            ret |= CtApplyStainCheckbox("##dyeShininess"u8, "Apply Gloss on Dye"u8, dye.Shininess,
                b => dyeTable[rowIdx].Shininess = b);
            ImUtf8.SameLineInner();
            ImGui.SetNextItemWidth(scalarSize);
            CtDragHalf("##dyePreviewShininess"u8, "Dye Preview for Gloss"u8, dyePack?.Shininess, "%.1f"u8);
        }

        return ret;
    }

    private bool DrawLegacyDye<TDyeRow>(IColorDyeTable<TDyeRow> dyeTable, LegacyDyePack? dyePack, int rowIdx)
        where TDyeRow : unmanaged, ILegacyColorDyeRow
    {
        var scalarSize       = ColorTableScalarSize * UiHelpers.Scale;
        var applyButtonWidth = ImUtf8.CalcTextSize("Apply Preview Dye"u8).X + ImGui.GetStyle().FramePadding.X * 2.0f;
        var subColWidth      = CalculateSubColumnWidth(2, applyButtonWidth);

        var     ret = false;
        ref var dye = ref dyeTable[rowIdx];

        ImGui.SetNextItemWidth(scalarSize);
        if (dyeTable is ColorDyeTable rwDyeTable)
        {
            ret |= CtDragScalar("Dye Channel"u8, default, dye.Channel + 1, "%d"u8, 1, StainService.ChannelCount, 0.1f,
                value => rwDyeTable[rowIdx].Channel = (byte)(Math.Clamp(value, 1, StainService.ChannelCount) - 1));
        }
        else
            CtDragScalar<int>("Dye Channel"u8, default, dye.Channel + 1, "%d"u8);
        ImGui.SameLine(subColWidth);
        ImGui.SetNextItemWidth(scalarSize);
        if (_stainService.LegacyTemplateCombo.Draw("##dyeTemplate", dye.Template.ToString(), string.Empty,
                scalarSize + ImGui.GetStyle().ScrollbarSize / 2, ImGui.GetTextLineHeightWithSpacing(), ImGuiComboFlags.NoArrowButton))
        {
            dye.Template = _stainService.LegacyTemplateCombo.CurrentSelection;
            ret          = true;
        }

        ImUtf8.SameLineInner();
        ImUtf8.Text("Dye Template"u8);
        ImGui.SameLine(ImGui.GetContentRegionAvail().X - applyButtonWidth + ImGui.GetStyle().ItemSpacing.X);
        using var dis = ImRaii.Disabled(!dyePack.HasValue);
        if (ImUtf8.Button("Apply Preview Dye"u8))
            ret |= Mtrl.ApplyDyeToRow(_stainService.LegacyStmFile, [
                _stainService.StainCombo1.CurrentSelection.Key,
                _stainService.StainCombo2.CurrentSelection.Key,
            ], rowIdx);

        return ret;
    }
}

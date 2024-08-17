using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using OtterGui;
using OtterGui.Text;
using Penumbra.GameData.Files.MaterialStructs;
using Penumbra.GameData.Files.StainMapStructs;
using Penumbra.Services;

namespace Penumbra.UI.AdvancedWindow.Materials;

public partial class MtrlTab
{
    private const float LegacyColorTableFloatSize      = 65.0f;
    private const float LegacyColorTablePercentageSize = 50.0f;
    private const float LegacyColorTableIntegerSize    = 40.0f;
    private const float LegacyColorTableByteSize       = 25.0f;

    private bool DrawLegacyColorTable<TRow, TDyeRow>(IColorTable<TRow> table, IColorDyeTable<TDyeRow>? dyeTable, bool disabled)
        where TRow : unmanaged, ILegacyColorRow where TDyeRow : unmanaged, ILegacyColorDyeRow
    {
        using var imTable = ImUtf8.Table("##ColorTable"u8, dyeTable != null ? 10 : 8,
            ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV);
        if (!imTable)
            return false;

        DrawLegacyColorTableHeader(dyeTable != null);

        var ret = false;
        for (var i = 0; i < ColorTable.NumRows; ++i)
        {
            if (DrawLegacyColorTableRow(table, dyeTable, i, disabled))
            {
                UpdateColorTableRowPreview(i);
                ret = true;
            }

            ImGui.TableNextRow();
        }

        return ret;
    }

    private static void DrawLegacyColorTableHeader(bool hasDyeTable)
    {
        ImGui.TableNextColumn();
        ImUtf8.TableHeader(default(ReadOnlySpan<byte>));
        ImGui.TableNextColumn();
        ImUtf8.TableHeader("Row"u8);
        ImGui.TableNextColumn();
        ImUtf8.TableHeader("Diffuse"u8);
        ImGui.TableNextColumn();
        ImUtf8.TableHeader("Specular"u8);
        ImGui.TableNextColumn();
        ImUtf8.TableHeader("Emissive"u8);
        ImGui.TableNextColumn();
        ImUtf8.TableHeader("Gloss"u8);
        ImGui.TableNextColumn();
        ImUtf8.TableHeader("Tile"u8);
        ImGui.TableNextColumn();
        ImUtf8.TableHeader("Repeat / Skew"u8);
        if (hasDyeTable)
        {
            ImGui.TableNextColumn();
            ImUtf8.TableHeader("Dye"u8);
            ImGui.TableNextColumn();
            ImUtf8.TableHeader("Dye Preview"u8);
        }
    }

    private bool DrawLegacyColorTableRow<TRow, TDyeRow>(IColorTable<TRow> table, IColorDyeTable<TDyeRow>? dyeTable, int rowIdx, bool disabled)
        where TRow : unmanaged, ILegacyColorRow where TDyeRow : unmanaged, ILegacyColorDyeRow
    {
        using var id        = ImRaii.PushId(rowIdx);
        ref var   row       = ref table[rowIdx];
        var       dye       = dyeTable?[rowIdx] ?? default;
        var       floatSize = LegacyColorTableFloatSize * UiHelpers.Scale;
        var       pctSize   = LegacyColorTablePercentageSize * UiHelpers.Scale;
        var       intSize   = LegacyColorTableIntegerSize * UiHelpers.Scale;
        var       byteSize  = LegacyColorTableByteSize * UiHelpers.Scale;
        ImGui.TableNextColumn();
        ColorTableCopyClipboardButton(rowIdx);
        ImUtf8.SameLineInner();
        var ret = ColorTablePasteFromClipboardButton(rowIdx, disabled);
        ImUtf8.SameLineInner();
        ColorTableRowHighlightButton(rowIdx, disabled);

        ImGui.TableNextColumn();
        using (ImRaii.PushFont(UiBuilder.MonoFont))
        {
            ImUtf8.Text($"{(rowIdx >> 1) + 1,2:D}{"AB"[rowIdx & 1]}");
        }

        ImGui.TableNextColumn();
        using var dis = ImRaii.Disabled(disabled);
        ret |= CtColorPicker("##Diffuse"u8, "Diffuse Color"u8, row.DiffuseColor,
            c => table[rowIdx].DiffuseColor = c);
        if (dyeTable != null)
        {
            ImUtf8.SameLineInner();
            ret |= CtApplyStainCheckbox("##dyeDiffuse"u8, "Apply Diffuse Color on Dye"u8, dye.DiffuseColor,
                b => dyeTable[rowIdx].DiffuseColor = b);
        }

        ImGui.TableNextColumn();
        ret |= CtColorPicker("##Specular"u8, "Specular Color"u8, row.SpecularColor,
            c => table[rowIdx].SpecularColor = c);
        if (dyeTable != null)
        {
            ImUtf8.SameLineInner();
            ret |= CtApplyStainCheckbox("##dyeSpecular"u8, "Apply Specular Color on Dye"u8, dye.SpecularColor,
                b => dyeTable[rowIdx].SpecularColor = b);
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(pctSize);
        ret |= CtDragScalar("##SpecularMask"u8, "Specular Strength"u8, (float)row.SpecularMask * 100.0f, "%.0f%%"u8, 0f, HalfMaxValue * 100.0f, 1.0f,
            v => table[rowIdx].SpecularMask = (Half)(v * 0.01f));
        if (dyeTable != null)
        {
            ImUtf8.SameLineInner();
            ret |= CtApplyStainCheckbox("##dyeSpecularMask"u8, "Apply Specular Strength on Dye"u8, dye.SpecularMask,
                b => dyeTable[rowIdx].SpecularMask = b);
        }

        ImGui.TableNextColumn();
        ret |= CtColorPicker("##Emissive"u8, "Emissive Color"u8, row.EmissiveColor,
            c => table[rowIdx].EmissiveColor = c);
        if (dyeTable != null)
        {
            ImUtf8.SameLineInner();
            ret |= CtApplyStainCheckbox("##dyeEmissive"u8, "Apply Emissive Color on Dye"u8, dye.EmissiveColor,
                b => dyeTable[rowIdx].EmissiveColor = b);
        }

        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(floatSize);
        var glossStrengthMin = ImGui.GetIO().KeyCtrl ? 0.0f : HalfEpsilon;
        ret |= CtDragHalf("##Shininess"u8, "Gloss Strength"u8, row.Shininess, "%.1f"u8, glossStrengthMin, HalfMaxValue,
            Math.Max(0.1f, (float)row.Shininess * 0.025f),
            v => table[rowIdx].Shininess = v);

        if (dyeTable != null)
        {
            ImUtf8.SameLineInner();
            ret |= CtApplyStainCheckbox("##dyeShininess"u8, "Apply Gloss Strength on Dye"u8, dye.Shininess,
                b => dyeTable[rowIdx].Shininess = b);
        }

        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(intSize);
        ret |= CtTileIndexPicker("##TileIndex"u8, "Tile Index"u8, row.TileIndex, true,
            value => table[rowIdx].TileIndex = value);
        ImUtf8.SameLineInner();
        ImGui.SetNextItemWidth(pctSize);
        if (table is ColorTable rwTable)
        {
            ret |= CtDragScalar("##TileAlpha"u8, "Tile Opacity"u8, (float)row.TileAlpha * 100.0f, "%.0f%%"u8, 0f, HalfMaxValue * 100.0f, 1.0f,
                v => rwTable[rowIdx].TileAlpha = (Half)(v * 0.01f));
        }
        else
            CtDragScalar<float>("##TileAlpha"u8, "Tile Opacity"u8, (float)row.TileAlpha * 100.0f, "%.0f%%"u8);

        ImGui.TableNextColumn();
        ret |= CtTileTransformMatrix(row.TileTransform, floatSize, false,
            m => table[rowIdx].TileTransform = m);

        if (dyeTable != null)
        {
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(byteSize);
            if (dyeTable is ColorDyeTable rwDyeTable)
            {
                ret |= CtDragScalar("##DyeChannel"u8, "Dye Channel"u8, dye.Channel + 1, "%d"u8, 1, StainService.ChannelCount, 0.25f,
                    value => rwDyeTable[rowIdx].Channel = (byte)(Math.Clamp(value, 1, StainService.ChannelCount) - 1));
            }
            else
                CtDragScalar<int>("##DyeChannel"u8, "Dye Channel"u8, dye.Channel + 1, "%d"u8);
            ImUtf8.SameLineInner();
            _stainService.LegacyTemplateCombo.CurrentDyeChannel = dye.Channel;
            if (_stainService.LegacyTemplateCombo.Draw("##dyeTemplate", dye.Template.ToString(), string.Empty, intSize
                  + ImGui.GetStyle().ScrollbarSize / 2, ImGui.GetTextLineHeightWithSpacing(), ImGuiComboFlags.NoArrowButton))
            {
                dyeTable[rowIdx].Template = _stainService.LegacyTemplateCombo.CurrentSelection;
                ret                       = true;
            }

            ImGuiUtil.HoverTooltip("Dye Template", ImGuiHoveredFlags.AllowWhenDisabled);

            ImGui.TableNextColumn();
            ret |= DrawLegacyDyePreview(rowIdx, disabled, dye, floatSize);
        }

        return ret;
    }

    private bool DrawLegacyDyePreview<TDyeRow>(int rowIdx, bool disabled, TDyeRow dye, float floatSize)
        where TDyeRow : unmanaged, ILegacyColorDyeRow
    {
        var stain = _stainService.StainCombo1.CurrentSelection.Key;
        if (stain == 0 || !_stainService.LegacyStmFile.TryGetValue(dye.Template, stain, out var values))
            return false;

        using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, ImGui.GetStyle().ItemSpacing / 2);

        var ret = ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.PaintBrush.ToIconString(), new Vector2(ImGui.GetFrameHeight()),
            "Apply the selected dye to this row.", disabled, true);

        ret = ret && Mtrl.ApplyDyeToRow(_stainService.LegacyStmFile, [stain], rowIdx);

        ImGui.SameLine();
        DrawLegacyDyePreview(values, floatSize);

        return ret;
    }

    private bool DrawLegacyDyePreview(int rowIdx, bool disabled, ColorDyeTableRow dye, float floatSize)
    {
        var stain = _stainService.GetStainCombo(dye.Channel).CurrentSelection.Key;
        if (stain == 0 || !_stainService.LegacyStmFile.TryGetValue(dye.Template, stain, out var values))
            return false;

        using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, ImGui.GetStyle().ItemSpacing / 2);

        var ret = ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.PaintBrush.ToIconString(), new Vector2(ImGui.GetFrameHeight()),
            "Apply the selected dye to this row.", disabled, true);

        ret = ret
         && Mtrl.ApplyDyeToRow(_stainService.LegacyStmFile, [
                _stainService.StainCombo1.CurrentSelection.Key,
                _stainService.StainCombo2.CurrentSelection.Key,
            ], rowIdx);

        ImGui.SameLine();
        DrawLegacyDyePreview(values, floatSize);

        return ret;
    }

    private static void DrawLegacyDyePreview(LegacyDyePack values, float floatSize)
    {
        CtColorPicker("##diffusePreview"u8, default, values.DiffuseColor, "D"u8);
        ImUtf8.SameLineInner();
        CtColorPicker("##specularPreview"u8, default, values.SpecularColor, "S"u8);
        ImUtf8.SameLineInner();
        CtColorPicker("##emissivePreview"u8, default, values.EmissiveColor, "E"u8);
        ImUtf8.SameLineInner();
        using var dis = ImRaii.Disabled();
        ImGui.SetNextItemWidth(floatSize);
        var shininess = (float)values.Shininess;
        ImGui.DragFloat("##shininessPreview", ref shininess, 0, shininess, shininess, "%.1f G");
        ImUtf8.SameLineInner();
        ImGui.SetNextItemWidth(floatSize);
        var specularMask = (float)values.SpecularMask * 100.0f;
        ImGui.DragFloat("##specularMaskPreview", ref specularMask, 0, specularMask, specularMask, "%.0f%% S");
    }
}

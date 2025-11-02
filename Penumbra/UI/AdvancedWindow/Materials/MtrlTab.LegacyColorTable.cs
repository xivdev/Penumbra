using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using ImSharp;
using OtterGui;
using OtterGui.Text;
using Penumbra.GameData.Files.MaterialStructs;
using Penumbra.GameData.Files.StainMapStructs;
using Penumbra.Services;
using TableFlags = ImSharp.TableFlags;

namespace Penumbra.UI.AdvancedWindow.Materials;

public partial class MtrlTab
{
    private const float LegacyColorTableFloatSize      = 65.0f;
    private const float LegacyColorTablePercentageSize = 50.0f;
    private const float LegacyColorTableIntegerSize    = 40.0f;
    private const float LegacyColorTableByteSize       = 25.0f;

    private bool DrawLegacyColorTable(LegacyColorTable table, LegacyColorDyeTable? dyeTable, bool disabled)
    {
        using var imTable = Im.Table.Begin("##ColorTable"u8, dyeTable is not null ? 10 : 8,
            TableFlags.SizingFixedFit | TableFlags.RowBackground | TableFlags.BordersInnerVertical);
        if (!imTable)
            return false;

        DrawLegacyColorTableHeader(dyeTable is not null);

        var ret = false;
        for (var i = 0; i < LegacyColorTable.NumRows; ++i)
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

    private bool DrawLegacyColorTable(ColorTable table, ColorDyeTable? dyeTable, bool disabled)
    {
        using var imTable = Im.Table.Begin("##ColorTable"u8, dyeTable is not null ? 10 : 8,
            TableFlags.SizingFixedFit | TableFlags.RowBackground | TableFlags.BordersInnerVertical);
        if (!imTable)
            return false;

        DrawLegacyColorTableHeader(dyeTable is not null);

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
        ImUtf8.TableHeader(""u8);
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

    private bool DrawLegacyColorTableRow(LegacyColorTable table, LegacyColorDyeTable? dyeTable, int rowIdx, bool disabled)
    {
        using var id        = ImRaii.PushId(rowIdx);
        ref var   row       = ref table[rowIdx];
        var       dye       = dyeTable != null ? dyeTable[rowIdx] : default;
        var       floatSize = LegacyColorTableFloatSize * Im.Style.GlobalScale;
        var       pctSize   = LegacyColorTablePercentageSize * Im.Style.GlobalScale;
        var       intSize   = LegacyColorTableIntegerSize * Im.Style.GlobalScale;
        ImGui.TableNextColumn();
        ColorTableCopyClipboardButton(rowIdx);
        Im.Line.SameInner();
        var ret = ColorTablePasteFromClipboardButton(rowIdx, disabled);
        Im.Line.SameInner();
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
            Im.Line.SameInner();
            ret |= CtApplyStainCheckbox("##dyeDiffuse"u8, "Apply Diffuse Color on Dye"u8, dye.DiffuseColor,
                b => dyeTable[rowIdx].DiffuseColor = b);
        }

        ImGui.TableNextColumn();
        ret |= CtColorPicker("##Specular"u8, "Specular Color"u8, row.SpecularColor,
            c => table[rowIdx].SpecularColor = c);
        if (dyeTable != null)
        {
            Im.Line.SameInner();
            ret |= CtApplyStainCheckbox("##dyeSpecular"u8, "Apply Specular Color on Dye"u8, dye.SpecularColor,
                b => dyeTable[rowIdx].SpecularColor = b);
        }

        Im.Line.Same();
        Im.Item.SetNextWidth(pctSize);
        ret |= CtDragScalar("##SpecularMask"u8, "Specular Strength"u8, (float)row.SpecularMask * 100.0f, "%.0f%%"u8, 0f, HalfMaxValue * 100.0f,
            1.0f,
            v => table[rowIdx].SpecularMask = (Half)(v * 0.01f));
        if (dyeTable != null)
        {
            Im.Line.SameInner();
            ret |= CtApplyStainCheckbox("##dyeSpecularMask"u8, "Apply Specular Strength on Dye"u8, dye.SpecularMask,
                b => dyeTable[rowIdx].SpecularMask = b);
        }

        ImGui.TableNextColumn();
        ret |= CtColorPicker("##Emissive"u8, "Emissive Color"u8, row.EmissiveColor,
            c => table[rowIdx].EmissiveColor = c);
        if (dyeTable != null)
        {
            Im.Line.SameInner();
            ret |= CtApplyStainCheckbox("##dyeEmissive"u8, "Apply Emissive Color on Dye"u8, dye.EmissiveColor,
                b => dyeTable[rowIdx].EmissiveColor = b);
        }

        ImGui.TableNextColumn();
        Im.Item.SetNextWidth(floatSize);
        var glossStrengthMin = ImGui.GetIO().KeyCtrl ? 0.0f : HalfEpsilon;
        ret |= CtDragHalf("##Shininess"u8, "Gloss Strength"u8, row.Shininess, "%.1f"u8, glossStrengthMin, HalfMaxValue,
            Math.Max(0.1f, (float)row.Shininess * 0.025f),
            v => table[rowIdx].Shininess = v);

        if (dyeTable != null)
        {
            Im.Line.SameInner();
            ret |= CtApplyStainCheckbox("##dyeShininess"u8, "Apply Gloss Strength on Dye"u8, dye.Shininess,
                b => dyeTable[rowIdx].Shininess = b);
        }

        ImGui.TableNextColumn();
        Im.Item.SetNextWidth(intSize);
        ret |= CtTileIndexPicker("##TileIndex"u8, "Tile Index"u8, row.TileIndex, true,
            value => table[rowIdx].TileIndex = value);

        ImGui.TableNextColumn();
        ret |= CtTileTransformMatrix(row.TileTransform, floatSize, false,
            m => table[rowIdx].TileTransform = m);

        if (dyeTable != null)
        {
            ImGui.TableNextColumn();
            if (_stainService.LegacyTemplateCombo.Draw("##dyeTemplate", dye.Template.ToString(), string.Empty, intSize
                  + Im.Style.ScrollbarSize / 2, Im.Style.TextHeightWithSpacing, ImGuiComboFlags.NoArrowButton))
            {
                dyeTable[rowIdx].Template = _stainService.LegacyTemplateCombo.CurrentSelection.UShort;
                ret                       = true;
            }

            Im.Tooltip.OnHover("Dye Template"u8, HoveredFlags.AllowWhenDisabled);

            ImGui.TableNextColumn();
            ret |= DrawLegacyDyePreview(rowIdx, disabled, dye, floatSize);
        }

        return ret;
    }

    private bool DrawLegacyColorTableRow(ColorTable table, ColorDyeTable? dyeTable, int rowIdx, bool disabled)
    {
        using var id        = ImRaii.PushId(rowIdx);
        ref var   row       = ref table[rowIdx];
        var       dye       = dyeTable?[rowIdx] ?? default;
        var       floatSize = LegacyColorTableFloatSize * Im.Style.GlobalScale;
        var       pctSize   = LegacyColorTablePercentageSize * Im.Style.GlobalScale;
        var       intSize   = LegacyColorTableIntegerSize * Im.Style.GlobalScale;
        var       byteSize  = LegacyColorTableByteSize * Im.Style.GlobalScale;
        ImGui.TableNextColumn();
        ColorTableCopyClipboardButton(rowIdx);
        Im.Line.SameInner();
        var ret = ColorTablePasteFromClipboardButton(rowIdx, disabled);
        Im.Line.SameInner();
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
            Im.Line.SameInner();
            ret |= CtApplyStainCheckbox("##dyeDiffuse"u8, "Apply Diffuse Color on Dye"u8, dye.DiffuseColor,
                b => dyeTable[rowIdx].DiffuseColor = b);
        }

        ImGui.TableNextColumn();
        ret |= CtColorPicker("##Specular"u8, "Specular Color"u8, row.SpecularColor,
            c => table[rowIdx].SpecularColor = c);
        if (dyeTable != null)
        {
            Im.Line.SameInner();
            ret |= CtApplyStainCheckbox("##dyeSpecular"u8, "Apply Specular Color on Dye"u8, dye.SpecularColor,
                b => dyeTable[rowIdx].SpecularColor = b);
        }

        Im.Line.Same();
        Im.Item.SetNextWidth(pctSize);
        ret |= CtDragScalar("##SpecularMask"u8, "Specular Strength"u8, (float)row.Scalar7 * 100.0f, "%.0f%%"u8, 0f, HalfMaxValue * 100.0f, 1.0f,
            v => table[rowIdx].Scalar7 = (Half)(v * 0.01f));
        if (dyeTable != null)
        {
            Im.Line.SameInner();
            ret |= CtApplyStainCheckbox("##dyeSpecularMask"u8, "Apply Specular Strength on Dye"u8, dye.Metalness,
                b => dyeTable[rowIdx].Metalness = b);
        }

        ImGui.TableNextColumn();
        ret |= CtColorPicker("##Emissive"u8, "Emissive Color"u8, row.EmissiveColor,
            c => table[rowIdx].EmissiveColor = c);
        if (dyeTable != null)
        {
            Im.Line.SameInner();
            ret |= CtApplyStainCheckbox("##dyeEmissive"u8, "Apply Emissive Color on Dye"u8, dye.EmissiveColor,
                b => dyeTable[rowIdx].EmissiveColor = b);
        }

        ImGui.TableNextColumn();
        Im.Item.SetNextWidth(floatSize);
        var glossStrengthMin = ImGui.GetIO().KeyCtrl ? 0.0f : HalfEpsilon;
        ret |= CtDragHalf("##Shininess"u8, "Gloss Strength"u8, row.Scalar3, "%.1f"u8, glossStrengthMin, HalfMaxValue,
            Math.Max(0.1f, (float)row.Scalar3 * 0.025f),
            v => table[rowIdx].Scalar3 = v);

        if (dyeTable != null)
        {
            Im.Line.SameInner();
            ret |= CtApplyStainCheckbox("##dyeShininess"u8, "Apply Gloss Strength on Dye"u8, dye.Scalar3,
                b => dyeTable[rowIdx].Scalar3 = b);
        }

        ImGui.TableNextColumn();
        Im.Item.SetNextWidth(intSize);
        ret |= CtTileIndexPicker("##TileIndex"u8, "Tile Index"u8, row.TileIndex, true,
            value => table[rowIdx].TileIndex = value);
        Im.Line.SameInner();
        Im.Item.SetNextWidth(pctSize);
        ret |= CtDragScalar("##TileAlpha"u8, "Tile Opacity"u8, (float)row.TileAlpha * 100.0f, "%.0f%%"u8, 0f, HalfMaxValue * 100.0f, 1.0f,
            v => table[rowIdx].TileAlpha = (Half)(v * 0.01f));

        ImGui.TableNextColumn();
        ret |= CtTileTransformMatrix(row.TileTransform, floatSize, false,
            m => table[rowIdx].TileTransform = m);

        if (dyeTable != null)
        {
            ImGui.TableNextColumn();
            Im.Item.SetNextWidth(byteSize);
            ret |= CtDragScalar("##DyeChannel"u8, "Dye Channel"u8, dye.Channel + 1, "%hhd"u8, 1, StainService.ChannelCount, 0.25f,
                value => dyeTable[rowIdx].Channel = (byte)(Math.Clamp(value, 1, StainService.ChannelCount) - 1));
            Im.Line.SameInner();
            _stainService.LegacyTemplateCombo.CurrentDyeChannel = dye.Channel;
            if (_stainService.LegacyTemplateCombo.Draw("##dyeTemplate", dye.Template.ToString(), string.Empty, intSize
                  + Im.Style.ScrollbarSize / 2, Im.Style.TextHeightWithSpacing, ImGuiComboFlags.NoArrowButton))
            {
                dyeTable[rowIdx].Template = _stainService.LegacyTemplateCombo.CurrentSelection.UShort;
                ret                       = true;
            }

            Im.Tooltip.OnHover("Dye Template"u8, HoveredFlags.AllowWhenDisabled);

            ImGui.TableNextColumn();
            ret |= DrawLegacyDyePreview(rowIdx, disabled, dye, floatSize);
        }

        return ret;
    }

    private bool DrawLegacyDyePreview(int rowIdx, bool disabled, LegacyColorDyeTableRow dye, float floatSize)
    {
        var stain = _stainService.StainCombo1.CurrentSelection.Key;
        if (stain == 0 || !_stainService.LegacyStmFile.TryGetValue(dye.Template, stain, out var values))
            return false;

        using var style = ImStyleDouble.ItemSpacing.Push(Im.Style.ItemSpacing / 2);

        var ret = ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.PaintBrush.ToIconString(), new Vector2(Im.Style.FrameHeight),
            "Apply the selected dye to this row.", disabled, true);

        ret = ret && Mtrl.ApplyDyeToRow(_stainService.LegacyStmFile, [stain], rowIdx);

        Im.Line.Same();
        DrawLegacyDyePreview(values, floatSize);

        return ret;
    }

    private bool DrawLegacyDyePreview(int rowIdx, bool disabled, ColorDyeTableRow dye, float floatSize)
    {
        var stain = _stainService.GetStainCombo(dye.Channel).CurrentSelection.Key;
        if (stain == 0 || !_stainService.LegacyStmFile.TryGetValue(dye.Template, stain, out var values))
            return false;

        using var style = ImStyleDouble.ItemSpacing.Push(Im.Style.ItemSpacing / 2);

        var ret = ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.PaintBrush.ToIconString(), new Vector2(Im.Style.FrameHeight),
            "Apply the selected dye to this row.", disabled, true);

        ret = ret
         && Mtrl.ApplyDyeToRow(_stainService.LegacyStmFile, [
                _stainService.StainCombo1.CurrentSelection.Key,
                _stainService.StainCombo2.CurrentSelection.Key,
            ], rowIdx);

        Im.Line.Same();
        DrawLegacyDyePreview(values, floatSize);

        return ret;
    }

    private static void DrawLegacyDyePreview(LegacyDyePack values, float floatSize)
    {
        CtColorPicker("##diffusePreview"u8, default, values.DiffuseColor, "D"u8);
        Im.Line.SameInner();
        CtColorPicker("##specularPreview"u8, default, values.SpecularColor, "S"u8);
        Im.Line.SameInner();
        CtColorPicker("##emissivePreview"u8, default, values.EmissiveColor, "E"u8);
        Im.Line.SameInner();
        using var dis = ImRaii.Disabled();
        Im.Item.SetNextWidth(floatSize);
        var shininess = (float)values.Shininess;
        ImGui.DragFloat("##shininessPreview", ref shininess, 0, shininess, shininess, "%.1f G");
        Im.Line.SameInner();
        Im.Item.SetNextWidth(floatSize);
        var specularMask = (float)values.SpecularMask * 100.0f;
        ImGui.DragFloat("##specularMaskPreview", ref specularMask, 0, specularMask, specularMask, "%.0f%% S");
    }
}

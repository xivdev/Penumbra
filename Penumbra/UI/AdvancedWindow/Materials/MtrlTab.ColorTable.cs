using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using ImSharp;
using Luna;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Text;
using Penumbra.GameData.Files.MaterialStructs;
using Penumbra.GameData.Files.StainMapStructs;
using Penumbra.Services;

namespace Penumbra.UI.AdvancedWindow.Materials;

public partial class MtrlTab
{
    private const float ColorTableScalarSize = 65.0f;

    private int _colorTableSelectedPair;

    private bool DrawColorTable(ColorTable table, ColorDyeTable? dyeTable, bool disabled)
    {
        DrawColorTablePairSelector(table, disabled);
        return DrawColorTablePairEditor(table, dyeTable, disabled);
    }

    private void DrawColorTablePairSelector(ColorTable table, bool disabled)
    {
        var itemSpacing      = Im.Style.ItemSpacing.X;
        var itemInnerSpacing = Im.Style.ItemInnerSpacing.X;
        var framePadding     = Im.Style.FramePadding;
        var buttonWidth      = (Im.ContentRegion.Available.X - itemSpacing * 7.0f) * 0.125f;
        var frameHeight      = Im.Style.FrameHeight;
        var highlighterSize  = ImEx.Icon.CalculateSize(FontAwesomeIcon.Crosshairs.Icon()) + framePadding * 2.0f;

        using var font      = Im.Font.PushMono();
        using var alignment = ImStyleDouble.ButtonTextAlign.Push(new Vector2(0, 0.5f));

        // This depends on the font being pushed for "proper" alignment of the pair indices in the buttons.
        var spaceWidth   = Im.Font.Mono.GetCharacterAdvance(' ');
        var spacePadding = (int)MathF.Ceiling((highlighterSize.X + framePadding.X + itemInnerSpacing) / spaceWidth);

        for (var i = 0; i < ColorTable.NumRows >> 1; i += 8)
        {
            for (var j = 0; j < 8; ++j)
            {
                var pairIndex = i + j;
                using (ImGuiColor.Button.Push(Im.Style[ImGuiColor.ButtonActive], pairIndex == _colorTableSelectedPair))
                {
                    if (Im.Button($"#{pairIndex + 1}".PadLeft(3 + spacePadding),
                            new Vector2(buttonWidth, Im.Style.FrameHeightWithSpacing + frameHeight)))
                        _colorTableSelectedPair = pairIndex;
                }

                var rcMin = Im.Item.UpperLeftCorner + framePadding;
                var rcMax = Im.Item.LowerRightCorner - framePadding;
                CtBlendRect(
                    rcMin with { X = rcMax.X - frameHeight * 3 - itemInnerSpacing * 2 },
                    rcMax with { X = rcMax.X - (frameHeight + itemInnerSpacing) * 2 },
                    ImGuiUtil.ColorConvertFloat3ToU32(PseudoSqrtRgb((Vector3)table[pairIndex << 1].DiffuseColor)),
                    ImGuiUtil.ColorConvertFloat3ToU32(PseudoSqrtRgb((Vector3)table[(pairIndex << 1) | 1].DiffuseColor))
                );
                CtBlendRect(
                    rcMin with { X = rcMax.X - frameHeight * 2 - itemInnerSpacing },
                    rcMax with { X = rcMax.X - frameHeight - itemInnerSpacing },
                    ImGuiUtil.ColorConvertFloat3ToU32(PseudoSqrtRgb((Vector3)table[pairIndex << 1].SpecularColor)),
                    ImGuiUtil.ColorConvertFloat3ToU32(PseudoSqrtRgb((Vector3)table[(pairIndex << 1) | 1].SpecularColor))
                );
                CtBlendRect(
                    rcMin with { X = rcMax.X - frameHeight }, rcMax,
                    ImGuiUtil.ColorConvertFloat3ToU32(PseudoSqrtRgb((Vector3)table[pairIndex << 1].EmissiveColor)),
                    ImGuiUtil.ColorConvertFloat3ToU32(PseudoSqrtRgb((Vector3)table[(pairIndex << 1) | 1].EmissiveColor))
                );
                if (j < 7)
                    Im.Line.Same();

                var cursor = Im.Cursor.ScreenPosition;
                Im.Cursor.ScreenPosition = rcMin with { Y = float.Lerp(rcMin.Y, rcMax.Y, 0.5f) - highlighterSize.Y * 0.5f };
                font.Pop();
                ColorTablePairHighlightButton(pairIndex, disabled);
                font.Push(Im.Font.Mono);
                Im.Cursor.ScreenPosition = cursor;
            }
        }
    }

    private bool DrawColorTablePairEditor(ColorTable table, ColorDyeTable? dyeTable, bool disabled)
    {
        var retA        = false;
        var retB        = false;
        var rowAIdx     = _colorTableSelectedPair << 1;
        var rowBIdx     = rowAIdx | 1;
        var dyeA        = dyeTable?[_colorTableSelectedPair << 1] ?? default;
        var dyeB        = dyeTable?[(_colorTableSelectedPair << 1) | 1] ?? default;
        var previewDyeA = _stainService.GetStainCombo(dyeA.Channel).CurrentSelection.Key;
        var previewDyeB = _stainService.GetStainCombo(dyeB.Channel).CurrentSelection.Key;
        var dyePackA    = _stainService.GudStmFile.GetValueOrNull(dyeA.Template, previewDyeA);
        var dyePackB    = _stainService.GudStmFile.GetValueOrNull(dyeB.Template, previewDyeB);
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

        DrawHeader("  Physical Parameters"u8);
        using (var columns = ImUtf8.Columns(2, "ColorTable"u8))
        {
            using var dis = ImRaii.Disabled(disabled);
            using (ImUtf8.PushId("PbrA"u8))
            {
                retA |= DrawPbr(table, dyeTable, dyePackA, rowAIdx);
            }

            columns.Next();
            using (ImUtf8.PushId("PbrB"u8))
            {
                retB |= DrawPbr(table, dyeTable, dyePackB, rowBIdx);
            }
        }

        DrawHeader("  Sheen Layer Parameters"u8);
        using (var columns = ImUtf8.Columns(2, "ColorTable"u8))
        {
            using var dis = ImRaii.Disabled(disabled);
            using (ImUtf8.PushId("SheenA"u8))
            {
                retA |= DrawSheen(table, dyeTable, dyePackA, rowAIdx);
            }

            columns.Next();
            using (ImUtf8.PushId("SheenB"u8))
            {
                retB |= DrawSheen(table, dyeTable, dyePackB, rowBIdx);
            }
        }

        DrawHeader("  Pair Blending"u8);
        using (var columns = ImUtf8.Columns(2, "ColorTable"u8))
        {
            using var dis = ImRaii.Disabled(disabled);
            using (ImUtf8.PushId("BlendingA"u8))
            {
                retA |= DrawBlending(table, dyeTable, dyePackA, rowAIdx);
            }

            columns.Next();
            using (ImUtf8.PushId("BlendingB"u8))
            {
                retB |= DrawBlending(table, dyeTable, dyePackB, rowBIdx);
            }
        }

        DrawHeader("  Material Template"u8);
        using (var columns = ImUtf8.Columns(2, "ColorTable"u8))
        {
            using var dis = ImRaii.Disabled(disabled);
            using (ImUtf8.PushId("TemplateA"u8))
            {
                retA |= DrawTemplate(table, dyeTable, dyePackA, rowAIdx);
            }

            columns.Next();
            using (ImUtf8.PushId("TemplateB"u8))
            {
                retB |= DrawTemplate(table, dyeTable, dyePackB, rowBIdx);
            }
        }

        if (dyeTable != null)
        {
            DrawHeader("  Dye Properties"u8);
            using var columns = ImUtf8.Columns(2, "ColorTable"u8);
            using var dis     = ImRaii.Disabled(disabled);
            using (ImUtf8.PushId("DyeA"u8))
            {
                retA |= DrawDye(dyeTable, dyePackA, rowAIdx);
            }

            columns.Next();
            using (ImUtf8.PushId("DyeB"u8))
            {
                retB |= DrawDye(dyeTable, dyePackB, rowBIdx);
            }
        }

        DrawHeader("  Further Content"u8);
        using (var columns = ImUtf8.Columns(2, "ColorTable"u8))
        {
            using var dis = ImRaii.Disabled(disabled);
            using (ImUtf8.PushId("FurtherA"u8))
            {
                retA |= DrawFurther(table, dyeTable, dyePackA, rowAIdx);
            }

            columns.Next();
            using (ImUtf8.PushId("FurtherB"u8))
            {
                retB |= DrawFurther(table, dyeTable, dyePackB, rowBIdx);
            }
        }

        if (retA)
            UpdateColorTableRowPreview(rowAIdx);
        if (retB)
            UpdateColorTableRowPreview(rowBIdx);

        return retA | retB;
    }

    /// <remarks> Padding styles do not seem to apply to this component. It is recommended to prepend two spaces. </remarks>
    private static void DrawHeader(ReadOnlySpan<byte> label)
    {
        var       headerColor = Im.Style[ImGuiColor.Header];
        using var _           = ImGuiColor.HeaderHovered.Push(headerColor).Push(ImGuiColor.HeaderActive, headerColor);
        ImUtf8.CollapsingHeader(label, ImGuiTreeNodeFlags.Leaf);
    }

    private bool DrawRowHeader(int rowIdx, bool disabled)
    {
        ColorTableCopyClipboardButton(rowIdx);
        Im.Line.SameInner();
        var ret = ColorTablePasteFromClipboardButton(rowIdx, disabled);
        Im.Line.SameInner();
        ColorTableRowHighlightButton(rowIdx, disabled);

        Im.Line.Same();
        CenteredTextInRest($"Row {(rowIdx >> 1) + 1}{"AB"[rowIdx & 1]}");

        return ret;
    }

    private static bool DrawColors(ColorTable table, ColorDyeTable? dyeTable, DyePack? dyePack, int rowIdx)
    {
        var dyeOffset = Im.ContentRegion.Available.X
          + Im.Style.ItemSpacing.X
          - Im.Style.ItemInnerSpacing.X
          - Im.Style.FrameHeight * 2.0f;

        var     ret = false;
        ref var row = ref table[rowIdx];
        var     dye = dyeTable?[rowIdx] ?? default;

        ret |= CtColorPicker("Diffuse Color"u8, default, row.DiffuseColor,
            c => table[rowIdx].DiffuseColor = c);
        if (dyeTable != null)
        {
            Im.Line.Same(dyeOffset);
            ret |= CtApplyStainCheckbox("##dyeDiffuseColor"u8, "Apply Diffuse Color on Dye"u8, dye.DiffuseColor,
                b => dyeTable[rowIdx].DiffuseColor = b);
            Im.Line.SameInner();
            CtColorPicker("##dyePreviewDiffuseColor"u8, "Dye Preview for Diffuse Color"u8, dyePack?.DiffuseColor);
        }

        ret |= CtColorPicker("Specular Color"u8, default, row.SpecularColor,
            c => table[rowIdx].SpecularColor = c);
        if (dyeTable != null)
        {
            Im.Line.Same(dyeOffset);
            ret |= CtApplyStainCheckbox("##dyeSpecularColor"u8, "Apply Specular Color on Dye"u8, dye.SpecularColor,
                b => dyeTable[rowIdx].SpecularColor = b);
            Im.Line.SameInner();
            CtColorPicker("##dyePreviewSpecularColor"u8, "Dye Preview for Specular Color"u8, dyePack?.SpecularColor);
        }

        ret |= CtColorPicker("Emissive Color"u8, default, row.EmissiveColor,
            c => table[rowIdx].EmissiveColor = c);
        if (dyeTable != null)
        {
            Im.Line.Same(dyeOffset);
            ret |= CtApplyStainCheckbox("##dyeEmissiveColor"u8, "Apply Emissive Color on Dye"u8, dye.EmissiveColor,
                b => dyeTable[rowIdx].EmissiveColor = b);
            Im.Line.SameInner();
            CtColorPicker("##dyePreviewEmissiveColor"u8, "Dye Preview for Emissive Color"u8, dyePack?.EmissiveColor);
        }

        return ret;
    }

    private static bool DrawBlending(ColorTable table, ColorDyeTable? dyeTable, DyePack? dyePack, int rowIdx)
    {
        var scalarSize = ColorTableScalarSize * Im.Style.GlobalScale;
        var dyeOffset = Im.ContentRegion.Available.X
          + Im.Style.ItemSpacing.X
          - Im.Style.ItemInnerSpacing.X
          - Im.Style.FrameHeight
          - scalarSize;

        var isRowB = (rowIdx & 1) != 0;

        var     ret = false;
        ref var row = ref table[rowIdx];
        var     dye = dyeTable?[rowIdx] ?? default;

        Im.Item.SetNextWidth(scalarSize);
        ret |= CtDragHalf(isRowB ? "Field #19"u8 : "Anisotropy Degree"u8, default, row.Anisotropy, "%.2f"u8, 0.0f, HalfMaxValue, 0.1f,
            v => table[rowIdx].Anisotropy = v);
        if (dyeTable != null)
        {
            Im.Line.Same(dyeOffset);
            ret |= CtApplyStainCheckbox("##dyeAnisotropy"u8, isRowB ? "Apply Field #19 on Dye"u8 : "Apply Anisotropy Degree on Dye"u8,
                dye.Anisotropy,
                b => dyeTable[rowIdx].Anisotropy = b);
            Im.Line.SameInner();
            Im.Item.SetNextWidth(scalarSize);
            CtDragHalf("##dyePreviewAnisotropy"u8, isRowB ? "Dye Preview for Field #19"u8 : "Dye Preview for Anisotropy Degree"u8,
                dyePack?.Anisotropy,               "%.2f"u8);
        }

        return ret;
    }

    private bool DrawTemplate(ColorTable table, ColorDyeTable? dyeTable, DyePack? dyePack, int rowIdx)
    {
        var scalarSize  = ColorTableScalarSize * Im.Style.GlobalScale;
        var itemSpacing = Im.Style.ItemSpacing.X;
        var dyeOffset   = Im.ContentRegion.Available.X - Im.Style.ItemInnerSpacing.X - Im.Style.FrameHeight - scalarSize - 64.0f;
        var subColWidth = CalculateSubColumnWidth(2);

        var     ret = false;
        ref var row = ref table[rowIdx];
        var     dye = dyeTable?[rowIdx] ?? default;

        Im.Item.SetNextWidth(scalarSize);
        ret |= CtDragScalar("Shader ID"u8, default, row.ShaderId, "%d"u8, (ushort)0, (ushort)255, 0.25f,
            v => table[rowIdx].ShaderId = v);

        ImGui.Dummy(new Vector2(Im.Style.TextHeight / 2));

        Im.Item.SetNextWidth(scalarSize + itemSpacing + 64.0f);
        ret |= CtSphereMapIndexPicker("###SphereMapIndex"u8, default, row.SphereMapIndex, false,
            v => table[rowIdx].SphereMapIndex = v);
        Im.Line.SameInner();
        ImUtf8.Text("Sphere Map"u8);
        if (dyeTable != null)
        {
            var textRectMin = ImGui.GetItemRectMin();
            var textRectMax = ImGui.GetItemRectMax();
            Im.Line.Same(dyeOffset);
            var cursor = ImGui.GetCursorScreenPos();
            ImGui.SetCursorScreenPos(cursor with { Y = float.Lerp(textRectMin.Y, textRectMax.Y, 0.5f) - Im.Style.FrameHeight * 0.5f });
            ret |= CtApplyStainCheckbox("##dyeSphereMapIndex"u8, "Apply Sphere Map on Dye"u8, dye.SphereMapIndex,
                b => dyeTable[rowIdx].SphereMapIndex = b);
            Im.Line.SameInner();
            ImGui.SetCursorScreenPos(ImGui.GetCursorScreenPos() with { Y = cursor.Y });
            Im.Item.SetNextWidth(scalarSize + itemSpacing + 64.0f);
            using var dis = ImRaii.Disabled();
            CtSphereMapIndexPicker("###SphereMapIndexDye"u8, "Dye Preview for Sphere Map"u8, dyePack?.SphereMapIndex ?? ushort.MaxValue, false,
                Nop);
        }

        ImGui.Dummy(new Vector2(64.0f, 0.0f));
        Im.Line.Same();
        Im.Item.SetNextWidth(scalarSize);
        ret |= CtDragScalar("Sphere Map Intensity"u8, default, (float)row.SphereMapMask * 100.0f, "%.0f%%"u8, HalfMinValue * 100.0f,
            HalfMaxValue * 100.0f,                    1.0f,
            v => table[rowIdx].SphereMapMask = (Half)(v * 0.01f));
        if (dyeTable != null)
        {
            Im.Line.Same(dyeOffset);
            ret |= CtApplyStainCheckbox("##dyeSphereMapMask"u8, "Apply Sphere Map Intensity on Dye"u8, dye.SphereMapMask,
                b => dyeTable[rowIdx].SphereMapMask = b);
            Im.Line.SameInner();
            Im.Item.SetNextWidth(scalarSize);
            CtDragScalar("##dyeSphereMapMask"u8, "Dye Preview for Sphere Map Intensity"u8, (float?)dyePack?.SphereMapMask * 100.0f, "%.0f%%"u8);
        }

        ImGui.Dummy(new Vector2(Im.Style.TextHeight / 2));

        var leftLineHeight  = 64.0f + Im.Style.FramePadding.Y * 2.0f;
        var rightLineHeight = 3.0f * Im.Style.FrameHeight + 2.0f * Im.Style.ItemSpacing.Y;
        var lineHeight      = Math.Max(leftLineHeight, rightLineHeight);
        var cursorPos       = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(cursorPos + new Vector2(0.0f, (lineHeight - leftLineHeight) * 0.5f));
        Im.Item.SetNextWidth(scalarSize + (itemSpacing + 64.0f) * 2.0f);
        ret |= CtTileIndexPicker("###TileIndex"u8, default, row.TileIndex, false,
            v => table[rowIdx].TileIndex = v);
        Im.Line.SameInner();
        ImUtf8.Text("Tile"u8);

        Im.Line.Same(subColWidth);
        ImGui.SetCursorScreenPos(ImGui.GetCursorScreenPos() with { Y = cursorPos.Y + (lineHeight - rightLineHeight) * 0.5f });
        using (ImUtf8.Child("###TileProperties"u8,
                   new Vector2(Im.ContentRegion.Available.X, float.Lerp(rightLineHeight, lineHeight, 0.5f))))
        {
            ImGui.Dummy(new Vector2(scalarSize, 0.0f));
            Im.Line.SameInner();
            Im.Item.SetNextWidth(scalarSize);
            ret |= CtDragScalar("Tile Opacity"u8, default, (float)row.TileAlpha * 100.0f, "%.0f%%"u8, 0.0f, HalfMaxValue * 100.0f, 1.0f,
                v => table[rowIdx].TileAlpha = (Half)(v * 0.01f));

            ret |= CtTileTransformMatrix(row.TileTransform, scalarSize, true,
                m => table[rowIdx].TileTransform = m);
            Im.Line.SameInner();
            ImGui.SetCursorScreenPos(ImGui.GetCursorScreenPos()
              - new Vector2(0.0f, (Im.Style.FrameHeight + Im.Style.ItemSpacing.Y) * 0.5f));
            ImUtf8.Text("Tile Transform"u8);
        }

        return ret;
    }

    private static bool DrawPbr(ColorTable table, ColorDyeTable? dyeTable, DyePack? dyePack, int rowIdx)
    {
        var scalarSize  = ColorTableScalarSize * Im.Style.GlobalScale;
        var subColWidth = CalculateSubColumnWidth(2) + Im.Style.ItemSpacing.X;
        var dyeOffset = subColWidth
          - Im.Style.ItemSpacing.X * 2.0f
          - Im.Style.ItemInnerSpacing.X
          - Im.Style.FrameHeight
          - scalarSize;

        var     ret = false;
        ref var row = ref table[rowIdx];
        var     dye = dyeTable?[rowIdx] ?? default;

        Im.Item.SetNextWidth(scalarSize);
        ret |= CtDragScalar("Roughness"u8, default, (float)row.Roughness * 100.0f, "%.0f%%"u8, HalfMinValue * 100.0f, HalfMaxValue * 100.0f,
            1.0f,
            v => table[rowIdx].Roughness = (Half)(v * 0.01f));
        if (dyeTable != null)
        {
            Im.Line.Same(dyeOffset);
            ret |= CtApplyStainCheckbox("##dyeRoughness"u8, "Apply Roughness on Dye"u8, dye.Roughness,
                b => dyeTable[rowIdx].Roughness = b);
            Im.Line.SameInner();
            Im.Item.SetNextWidth(scalarSize);
            CtDragScalar("##dyePreviewRoughness"u8, "Dye Preview for Roughness"u8, (float?)dyePack?.Roughness * 100.0f, "%.0f%%"u8);
        }

        Im.Line.Same(subColWidth);
        Im.Item.SetNextWidth(scalarSize);
        ret |= CtDragScalar("Metalness"u8, default, (float)row.Metalness * 100.0f, "%.0f%%"u8, HalfMinValue * 100.0f, HalfMaxValue * 100.0f,
            1.0f,
            v => table[rowIdx].Metalness = (Half)(v * 0.01f));
        if (dyeTable != null)
        {
            Im.Line.Same(subColWidth + dyeOffset);
            ret |= CtApplyStainCheckbox("##dyeMetalness"u8, "Apply Metalness on Dye"u8, dye.Metalness,
                b => dyeTable[rowIdx].Metalness = b);
            Im.Line.SameInner();
            Im.Item.SetNextWidth(scalarSize);
            CtDragScalar("##dyePreviewMetalness"u8, "Dye Preview for Metalness"u8, (float?)dyePack?.Metalness * 100.0f, "%.0f%%"u8);
        }

        return ret;
    }

    private static bool DrawSheen(ColorTable table, ColorDyeTable? dyeTable, DyePack? dyePack, int rowIdx)
    {
        var scalarSize  = ColorTableScalarSize * Im.Style.GlobalScale;
        var subColWidth = CalculateSubColumnWidth(2) + Im.Style.ItemSpacing.X;
        var dyeOffset = subColWidth
          - Im.Style.ItemSpacing.X * 2.0f
          - Im.Style.ItemInnerSpacing.X
          - Im.Style.FrameHeight
          - scalarSize;

        var     ret = false;
        ref var row = ref table[rowIdx];
        var     dye = dyeTable?[rowIdx] ?? default;

        Im.Item.SetNextWidth(scalarSize);
        ret |= CtDragScalar("Sheen"u8, default, (float)row.SheenRate * 100.0f, "%.0f%%"u8, HalfMinValue * 100.0f, HalfMaxValue * 100.0f, 1.0f,
            v => table[rowIdx].SheenRate = (Half)(v * 0.01f));
        if (dyeTable != null)
        {
            Im.Line.Same(dyeOffset);
            ret |= CtApplyStainCheckbox("##dyeSheenRate"u8, "Apply Sheen on Dye"u8, dye.SheenRate,
                b => dyeTable[rowIdx].SheenRate = b);
            Im.Line.SameInner();
            Im.Item.SetNextWidth(scalarSize);
            CtDragScalar("##dyePreviewSheenRate"u8, "Dye Preview for Sheen"u8, (float?)dyePack?.SheenRate * 100.0f, "%.0f%%"u8);
        }

        Im.Line.Same(subColWidth);
        Im.Item.SetNextWidth(scalarSize);
        ret |= CtDragScalar("Sheen Tint"u8, default, (float)row.SheenTintRate * 100.0f, "%.0f%%"u8, HalfMinValue * 100.0f,
            HalfMaxValue * 100.0f,          1.0f,
            v => table[rowIdx].SheenTintRate = (Half)(v * 0.01f));
        if (dyeTable != null)
        {
            Im.Line.Same(subColWidth + dyeOffset);
            ret |= CtApplyStainCheckbox("##dyeSheenTintRate"u8, "Apply Sheen Tint on Dye"u8, dye.SheenTintRate,
                b => dyeTable[rowIdx].SheenTintRate = b);
            Im.Line.SameInner();
            Im.Item.SetNextWidth(scalarSize);
            CtDragScalar("##dyePreviewSheenTintRate"u8, "Dye Preview for Sheen Tint"u8, (float?)dyePack?.SheenTintRate * 100.0f, "%.0f%%"u8);
        }

        Im.Item.SetNextWidth(scalarSize);
        ret |= CtDragScalar("Sheen Roughness"u8, default, 100.0f / (float)row.SheenAperture, "%.0f%%"u8, 100.0f / HalfMaxValue,
            100.0f / HalfEpsilon,                1.0f,
            v => table[rowIdx].SheenAperture = (Half)(100.0f / v));
        if (dyeTable != null)
        {
            Im.Line.Same(dyeOffset);
            ret |= CtApplyStainCheckbox("##dyeSheenRoughness"u8, "Apply Sheen Roughness on Dye"u8, dye.SheenAperture,
                b => dyeTable[rowIdx].SheenAperture = b);
            Im.Line.SameInner();
            Im.Item.SetNextWidth(scalarSize);
            CtDragScalar("##dyePreviewSheenRoughness"u8, "Dye Preview for Sheen Roughness"u8, 100.0f / (float?)dyePack?.SheenAperture,
                "%.0f%%"u8);
        }

        return ret;
    }

    private static bool DrawFurther(ColorTable table, ColorDyeTable? dyeTable, DyePack? dyePack, int rowIdx)
    {
        var scalarSize  = ColorTableScalarSize * Im.Style.GlobalScale;
        var subColWidth = CalculateSubColumnWidth(2) + Im.Style.ItemSpacing.X;
        var dyeOffset = subColWidth
          - Im.Style.ItemSpacing.X * 2.0f
          - Im.Style.ItemInnerSpacing.X
          - Im.Style.FrameHeight
          - scalarSize;

        var     ret = false;
        ref var row = ref table[rowIdx];
        var     dye = dyeTable?[rowIdx] ?? default;

        Im.Item.SetNextWidth(scalarSize);
        ret |= CtDragHalf("Field #11"u8, default, row.Scalar11, "%.2f"u8, HalfMinValue, HalfMaxValue, 0.1f,
            v => table[rowIdx].Scalar11 = v);
        if (dyeTable != null)
        {
            Im.Line.Same(dyeOffset);
            ret |= CtApplyStainCheckbox("##dyeScalar11"u8, "Apply Field #11 on Dye"u8, dye.Scalar3,
                b => dyeTable[rowIdx].Scalar3 = b);
            Im.Line.SameInner();
            Im.Item.SetNextWidth(scalarSize);
            CtDragHalf("##dyePreviewScalar11"u8, "Dye Preview for Field #11"u8, dyePack?.Scalar3, "%.2f"u8);
        }

        ImGui.Dummy(new Vector2(Im.Style.TextHeight / 2));

        Im.Item.SetNextWidth(scalarSize);
        ret |= CtDragHalf("Field #3"u8, default, row.Scalar3, "%.2f"u8, HalfMinValue, HalfMaxValue, 0.1f,
            v => table[rowIdx].Scalar3 = v);

        Im.Line.Same(subColWidth);
        Im.Item.SetNextWidth(scalarSize);
        ret |= CtDragHalf("Field #7"u8, default, row.Scalar7, "%.2f"u8, HalfMinValue, HalfMaxValue, 0.1f,
            v => table[rowIdx].Scalar7 = v);

        Im.Item.SetNextWidth(scalarSize);
        ret |= CtDragHalf("Field #15"u8, default, row.Scalar15, "%.2f"u8, HalfMinValue, HalfMaxValue, 0.1f,
            v => table[rowIdx].Scalar15 = v);

        Im.Line.Same(subColWidth);
        Im.Item.SetNextWidth(scalarSize);
        ret |= CtDragHalf("Field #17"u8, default, row.Scalar17, "%.2f"u8, HalfMinValue, HalfMaxValue, 0.1f,
            v => table[rowIdx].Scalar17 = v);

        Im.Item.SetNextWidth(scalarSize);
        ret |= CtDragHalf("Field #20"u8, default, row.Scalar20, "%.2f"u8, HalfMinValue, HalfMaxValue, 0.1f,
            v => table[rowIdx].Scalar20 = v);

        Im.Line.Same(subColWidth);
        Im.Item.SetNextWidth(scalarSize);
        ret |= CtDragHalf("Field #22"u8, default, row.Scalar22, "%.2f"u8, HalfMinValue, HalfMaxValue, 0.1f,
            v => table[rowIdx].Scalar22 = v);

        Im.Item.SetNextWidth(scalarSize);
        ret |= CtDragHalf("Field #23"u8, default, row.Scalar23, "%.2f"u8, HalfMinValue, HalfMaxValue, 0.1f,
            v => table[rowIdx].Scalar23 = v);

        return ret;
    }

    private bool DrawDye(ColorDyeTable dyeTable, DyePack? dyePack, int rowIdx)
    {
        var scalarSize       = ColorTableScalarSize * Im.Style.GlobalScale;
        var applyButtonWidth = ImUtf8.CalcTextSize("Apply Preview Dye"u8).X + Im.Style.FramePadding.X * 2.0f;
        var subColWidth      = CalculateSubColumnWidth(2, applyButtonWidth);

        var     ret = false;
        ref var dye = ref dyeTable[rowIdx];

        Im.Item.SetNextWidth(scalarSize);
        ret |= CtDragScalar("Dye Channel"u8, default, dye.Channel + 1, "%d"u8, 1, StainService.ChannelCount, 0.1f,
            value => dyeTable[rowIdx].Channel = (byte)(Math.Clamp(value, 1, StainService.ChannelCount) - 1));
        Im.Line.Same(subColWidth);
        Im.Item.SetNextWidth(scalarSize);
        _stainService.GudTemplateCombo.CurrentDyeChannel = dye.Channel;
        if (_stainService.GudTemplateCombo.Draw("##dyeTemplate", dye.Template.ToString(), string.Empty,
                scalarSize + Im.Style.ScrollbarSize / 2, Im.Style.TextHeightWithSpacing, ImGuiComboFlags.NoArrowButton))
        {
            dye.Template = _stainService.GudTemplateCombo.CurrentSelection.UShort;
            ret          = true;
        }

        Im.Line.SameInner();
        ImUtf8.Text("Dye Template"u8);
        Im.Line.Same(Im.ContentRegion.Available.X - applyButtonWidth + Im.Style.ItemSpacing.X);
        using var dis = ImRaii.Disabled(!dyePack.HasValue);
        if (ImUtf8.Button("Apply Preview Dye"u8))
            ret |= Mtrl.ApplyDyeToRow(_stainService.GudStmFile, [
                _stainService.StainCombo1.CurrentSelection.Key,
                _stainService.StainCombo2.CurrentSelection.Key,
            ], rowIdx);

        return ret;
    }

    private static void CenteredTextInRest(string text)
        => AlignedTextInRest(text, 0.5f);

    private static void AlignedTextInRest(string text, float alignment)
    {
        var width = ImGui.CalcTextSize(text).X;
        ImGui.SetCursorScreenPos(ImGui.GetCursorScreenPos() + new Vector2((Im.ContentRegion.Available.X - width) * alignment, 0.0f));
        Im.Text(text);
    }

    private static float CalculateSubColumnWidth(int numSubColumns, float reservedSpace = 0.0f)
    {
        var itemSpacing = Im.Style.ItemSpacing.X;
        return (Im.ContentRegion.Available.X - reservedSpace - itemSpacing * (numSubColumns - 1)) / numSubColumns + itemSpacing;
    }
}

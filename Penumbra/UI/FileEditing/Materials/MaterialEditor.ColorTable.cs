using ImSharp;
using Luna;
using Penumbra.GameData.Files.MaterialStructs;
using Penumbra.GameData.Files.StainMapStructs;
using Penumbra.GameData.Gui;
using Penumbra.GameData.Structs;
using Penumbra.Services;

namespace Penumbra.UI.FileEditing.Materials;

public partial class MaterialEditor
{
    private const float ColorTableScalarSize = 65.0f;

    private static readonly float ExposureMax          = MathF.Log2((float)Half.MaxValue) * 0.5f;
    private static readonly float ExposureMinThreshold = -MathF.Ceiling(ExposureMax * 10.0f) * 0.1f;
    private static readonly float ExposureMinInfDummy  = ExposureMinThreshold - 0.025f;

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
        var highlighterSize  = ImEx.Icon.CalculateSize(LunaStyle.OnHoverIcon) + framePadding * 2.0f;

        using var alignment = ImStyleDouble.ButtonTextAlign.Push(new Vector2(0, 0.5f));

        var buttonSize = new Vector2(buttonWidth, Im.Style.FrameHeightWithSpacing + frameHeight);
        for (var i = 0; i < ColorTable.NumRows >> 1; i += 8)
        {
            for (var j = 0; j < 8; ++j)
            {
                var       pairIndex = i + j;
                using var id        = Im.Id.Push(pairIndex);
                using (ImGuiColor.Button.Push(Im.Style[ImGuiColor.ButtonActive], pairIndex == _colorTableSelectedPair))
                {
                    if (Im.Button(StringU8.Empty, buttonSize))
                        _colorTableSelectedPair = pairIndex;
                }

                var rcMin = Im.Item.UpperLeftCorner + framePadding;
                var rcMax = Im.Item.LowerRightCorner - framePadding;
                CtBlendRect(
                    rcMin with { X = rcMax.X - frameHeight * 3 - itemInnerSpacing * 2 },
                    rcMax with { X = rcMax.X - (frameHeight + itemInnerSpacing) * 2 },
                    PseudoSqrtRgb((Vector3)table[pairIndex << 1].DiffuseColor),
                    PseudoSqrtRgb((Vector3)table[(pairIndex << 1) | 1].DiffuseColor)
                );
                CtBlendRect(
                    rcMin with { X = rcMax.X - frameHeight * 2 - itemInnerSpacing },
                    rcMax with { X = rcMax.X - frameHeight - itemInnerSpacing },
                    PseudoSqrtRgb((Vector3)table[pairIndex << 1].SpecularColor),
                    PseudoSqrtRgb((Vector3)table[(pairIndex << 1) | 1].SpecularColor)
                );
                CtBlendRect(
                    rcMin with { X = rcMax.X - frameHeight }, rcMax,
                    PseudoSqrtRgb((Vector3)table[pairIndex << 1].EmissiveColor),
                    PseudoSqrtRgb((Vector3)table[(pairIndex << 1) | 1].EmissiveColor)
                );
                if (j < 7)
                    Im.Line.Same();

                var cursor    = Im.Cursor.ScreenPosition;
                var buttonPos = rcMin with { Y = float.Lerp(rcMin.Y, rcMax.Y, 0.5f) - highlighterSize.Y * 0.5f };
                Im.Cursor.ScreenPosition = buttonPos;
                ColorTablePairHighlightButton(pairIndex, disabled);
                Im.Cursor.ScreenPosition = buttonPos + new Vector2(Im.Style.FrameHeight + Im.Style.ItemInnerSpacing.X, Im.Style.FramePadding.Y);
                using var font = Im.Font.PushMono();
                Im.Text($"#{pairIndex + 1:D2}");
                Im.Cursor.ScreenPosition = cursor;
            }
        }
    }

    private bool DrawColorTablePairEditor(ColorTable table, ColorDyeTable? dyeTable, bool disabled)
    {
        bool retA;
        bool retB;
        var  rowAIdx     = _colorTableSelectedPair << 1;
        var  rowBIdx     = rowAIdx | 1;
        var  dyeA        = dyeTable?[_colorTableSelectedPair << 1] ?? default;
        var  dyeB        = dyeTable?[(_colorTableSelectedPair << 1) | 1] ?? default;
        var  previewDyeA = _stainService.GetStainCombo(dyeA.Channel).CurrentSelection.Id;
        var  previewDyeB = _stainService.GetStainCombo(dyeB.Channel).CurrentSelection.Id;
        var  dyePackA    = _stainService.GudStmFile.GetValueOrNull(dyeA.Template, previewDyeA);
        var  dyePackB    = _stainService.GudStmFile.GetValueOrNull(dyeB.Template, previewDyeB);

        var numSimpleRows  = dyeTable is not null ? 14 : 13;
        var numDummies     = dyeTable is not null ? 9 : 8;
        var numRowSpacings = numSimpleRows + numDummies + 1;
        var taspHeight     = TextureArraySlicePickers.MaximumTextureSize + 2f * Im.Style.FramePadding.Y;
        var tileHeight     = MathF.Max(taspHeight, 3f * Im.Style.FrameHeight + 2f * Im.Style.ItemSpacing.Y);
        var totalHeight = numSimpleRows * Im.Style.FrameHeight
          + numDummies * (Im.Style.TextHeight / 2)
          + numRowSpacings * Im.Style.ItemSpacing.Y
          + taspHeight
          + tileHeight
          + 2f * Im.Style.WindowPadding.Y;

        using (Im.Child.Begin("RowA"u8, new Vector2((Im.ContentRegion.Available.X - Im.Style.ItemSpacing.X) * 0.5f, totalHeight), true,
                   WindowFlags.NoScrollbar))
        {
            retA = DrawColorRowEditor(table, dyeTable, in dyePackA, rowAIdx, disabled);
        }

        Im.Line.Same();
        using (Im.Child.Begin("RowB"u8, Im.ContentRegion.Available with { Y = totalHeight }, true, WindowFlags.NoScrollbar))
        {
            retB = DrawColorRowEditor(table, dyeTable, in dyePackB, rowBIdx, disabled);
        }

        if (retA)
            UpdateColorTableRowPreview(rowAIdx);
        if (retB)
            UpdateColorTableRowPreview(rowBIdx);

        return retA | retB;
    }

    private bool DrawColorRowEditor(ColorTable table, ColorDyeTable? dyeTable, in DyePack? dyePack, int rowIdx, bool disabled)
    {
        var ret = false;
        using (Im.Id.Push("RowHeader"u8))
        {
            ret |= DrawRowHeader(rowIdx, disabled);
        }

        using var dis = Im.Disabled(disabled);

        Im.Dummy(new Vector2(Im.Style.TextHeight / 2));
        using (Im.Id.Push("Colors"u8))
        {
            ret |= DrawColors(table, dyeTable, in dyePack, rowIdx);
        }

        Im.Dummy(new Vector2(Im.Style.TextHeight / 2));
        using (Im.Id.Push("Pbr"u8))
        {
            ret |= DrawPbr(table, dyeTable, in dyePack, rowIdx);
        }

        Im.Dummy(new Vector2(Im.Style.TextHeight / 2));
        using (Im.Id.Push("Sheen"u8))
        {
            ret |= DrawSheen(table, dyeTable, in dyePack, rowIdx);
        }

        Im.Dummy(new Vector2(Im.Style.TextHeight / 2));
        using (Im.Id.Push("Extra"u8))
        {
            ret |= DrawExtra(table, dyeTable, in dyePack, rowIdx);
        }

        Im.Dummy(new Vector2(Im.Style.TextHeight / 2));
        using (Im.Id.Push("Template"u8))
        {
            ret |= DrawTemplate(table, dyeTable, in dyePack, rowIdx);
        }

        if (dyeTable is not null)
        {
            Im.Dummy(new Vector2(Im.Style.TextHeight / 2));
            using var id = Im.Id.Push("Dye"u8);
            ret |= DrawDye(dyeTable, in dyePack, rowIdx);
        }

        Im.Dummy(new Vector2(Im.Style.TextHeight / 2));
        using (Im.Id.Push("Further"u8))
        {
            ret |= DrawFurther(table, rowIdx);
        }

        return ret;
    }

    private bool DrawRowHeader(int rowIdx, bool disabled)
    {
        ColorTableCopyClipboardButton(rowIdx);
        Im.Line.SameInner();
        var ret = ColorTablePasteFromClipboardButton(rowIdx, disabled);
        Im.Line.SameInner();
        ColorTableRowHighlightButton(rowIdx, disabled);

        Im.Line.Same();
        var titleMin    = Im.Cursor.ScreenPosition;
        var titleRect   = new Rectangle(titleMin, titleMin + Im.ContentRegion.Available with { Y = Im.Style.FrameHeight });
        var windowShape = Im.Window.DrawList.Shape;
        windowShape.RectangleFilled(in titleRect, ImGuiColor.Header, Im.Style.FrameRounding);
        if (Im.Style.FrameBorderThickness > 0.0f)
            windowShape.Rectangle(in titleRect, ImGuiColor.Border, Im.Style.FrameRounding, thickness: Im.Style.FrameBorderThickness);
        CenteredTextInRest($"Row {(rowIdx >> 1) + 1}{"AB"[rowIdx & 1]}");

        return ret;
    }

    private static bool DrawColors(ColorTable table, ColorDyeTable? dyeTable, in DyePack? dyePack, int rowIdx)
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
        if (dyeTable is not null)
        {
            Im.Line.Same(dyeOffset);
            ret |= CtApplyStainCheckbox("##dyeSpecularColor"u8, "Apply Specular Color on Dye"u8, dye.SpecularColor,
                b => dyeTable[rowIdx].SpecularColor = b);
            Im.Line.SameInner();
            CtColorPicker("##dyePreviewSpecularColor"u8, "Dye Preview for Specular Color"u8, dyePack?.SpecularColor);
        }

        ret |= CtColorPicker("Emissive Color"u8, default, row.EmissiveColor,
            c => table[rowIdx].EmissiveColor = c);
        if (dyeTable is not null)
        {
            Im.Line.Same(dyeOffset);
            ret |= CtApplyStainCheckbox("##dyeEmissiveColor"u8, "Apply Emissive Color on Dye"u8, dye.EmissiveColor,
                b => dyeTable[rowIdx].EmissiveColor = b);
            Im.Line.SameInner();
            CtColorPicker("##dyePreviewEmissiveColor"u8, "Dye Preview for Emissive Color"u8, dyePack?.EmissiveColor);
        }

        return ret;
    }

    private bool DrawTemplate(ColorTable table, ColorDyeTable? dyeTable, in DyePack? dyePack, int rowIdx)
    {
        var scalarSize  = ColorTableScalarSize * Im.Style.GlobalScale;
        var itemSpacing = Im.Style.ItemSpacing.X;
        var dyeOffset = Im.ContentRegion.Available.X
          - Im.Style.ItemInnerSpacing.X
          - Im.Style.FrameHeight
          - scalarSize
          - TextureArraySlicePickers.MaximumTextureSize;
        var subColWidth = CalculateSubColumnWidth(2) + Im.Style.ItemSpacing.X;

        var     ret = false;
        ref var row = ref table[rowIdx];
        var     dye = dyeTable?[rowIdx] ?? default;

        Im.Item.SetNextWidth(scalarSize);
        ret |= CtDragScalar("Shader ID"u8, default, row.ShaderId, "%d"u8, (ushort)0, (ushort)255, 0.25f,
            v => table[rowIdx].ShaderId = v);

        Im.Line.Same(subColWidth);
        Im.Item.SetNextWidth(scalarSize);
        ret |= CtDragScalar("Scroll Parameters"u8, default, (ushort)row.Scalar23, "%d"u8, (ushort)0, (ushort)2, 0.25f,
            v => table[rowIdx].Scalar23 = (Half)v);

        Im.Dummy(new Vector2(Im.Style.TextHeight / 2));

        Im.Item.SetNextWidth(scalarSize + itemSpacing + TextureArraySlicePickers.MaximumTextureSize);
        ret |= CtSphereMapIndexPicker("###SphereMapIndex"u8, default, row.SphereMapIndex, false,
            v => table[rowIdx].SphereMapIndex = v);
        Im.Line.SameInner();
        Im.Text("Sphere Map"u8);
        if (dyeTable is not null)
        {
            var textRectMin = Im.Item.UpperLeftCorner;
            var textRectMax = Im.Item.LowerRightCorner;
            Im.Line.Same(dyeOffset);
            var cursor = Im.Cursor.ScreenPosition;
            Im.Cursor.ScreenPosition = cursor with { Y = float.Lerp(textRectMin.Y, textRectMax.Y, 0.5f) - Im.Style.FrameHeight * 0.5f };
            ret |= CtApplyStainCheckbox("##dyeSphereMapIndex"u8, "Apply Sphere Map on Dye"u8, dye.SphereMapIndex,
                b => dyeTable[rowIdx].SphereMapIndex = b);
            Im.Line.SameInner();
            Im.Cursor.ScreenPosition = Im.Cursor.ScreenPosition with { Y = cursor.Y };
            Im.Item.SetNextWidth(scalarSize + itemSpacing + TextureArraySlicePickers.MaximumTextureSize);
            using var dis = Im.Disabled();
            CtSphereMapIndexPicker("###SphereMapIndexDye"u8, "Dye Preview for Sphere Map"u8, dyePack?.SphereMapIndex ?? ushort.MaxValue, false,
                Nop);
        }

        Im.Dummy(new Vector2(TextureArraySlicePickers.MaximumTextureSize, 0.0f));
        Im.Line.Same();
        Im.Item.SetNextWidth(scalarSize);
        ret |= CtDragScalar("Sphere Map Intensity"u8, default, (float)row.SphereMapMask * 100.0f, "%.0f%%"u8, HalfMinValue * 100.0f,
            HalfMaxValue * 100.0f,                    1.0f,
            v => table[rowIdx].SphereMapMask = (Half)(v * 0.01f));
        if (dyeTable is not null)
        {
            Im.Line.Same(dyeOffset);
            ret |= CtApplyStainCheckbox("##dyeSphereMapMask"u8, "Apply Sphere Map Intensity on Dye"u8, dye.SphereMapMask,
                b => dyeTable[rowIdx].SphereMapMask = b);
            Im.Line.SameInner();
            Im.Item.SetNextWidth(scalarSize);
            CtDragScalar("##dyeSphereMapMask"u8, "Dye Preview for Sphere Map Intensity"u8, (float?)dyePack?.SphereMapMask * 100.0f, "%.0f%%"u8);
        }

        Im.Dummy(new Vector2(Im.Style.TextHeight / 2));

        var leftLineHeight  = TextureArraySlicePickers.MaximumTextureSize + Im.Style.FramePadding.Y * 2.0f;
        var rightLineHeight = 3.0f * Im.Style.FrameHeight + 2.0f * Im.Style.ItemSpacing.Y;
        var lineHeight      = Math.Max(leftLineHeight, rightLineHeight);
        var cursorPos       = Im.Cursor.ScreenPosition;
        Im.Cursor.ScreenPosition = cursorPos + new Vector2(0.0f, (lineHeight - leftLineHeight) * 0.5f);
        Im.Item.SetNextWidth(scalarSize + (itemSpacing + TextureArraySlicePickers.MaximumTextureSize) * 2.0f);
        ret |= CtTileIndexPicker("###TileIndex"u8, default, row.TileIndex, false,
            v => table[rowIdx].TileIndex = v);
        Im.Line.SameInner();
        Im.Text("Tile"u8);

        Im.Line.Same(subColWidth);
        Im.Cursor.ScreenPosition = Im.Cursor.ScreenPosition with { Y = cursorPos.Y + (lineHeight - rightLineHeight) * 0.5f };
        using (Im.Child.Begin("###TileProperties"u8, Im.ContentRegion.Available with { Y = float.Lerp(rightLineHeight, lineHeight, 0.5f) }))
        {
            Im.Dummy(new Vector2(scalarSize, 0.0f));
            Im.Line.SameInner();
            Im.Item.SetNextWidth(scalarSize);
            ret |= CtDragScalar("Tile Opacity"u8, default, (float)row.TileAlpha * 100.0f, "%.0f%%"u8, 0.0f, HalfMaxValue * 100.0f, 1.0f,
                v => table[rowIdx].TileAlpha = (Half)(v * 0.01f));

            ret |= CtTileTransformMatrix(row.TileTransform, scalarSize, true,
                m => table[rowIdx].TileTransform = m);
            Im.Line.SameInner();
            Im.Cursor.ScreenPosition -= new Vector2(0.0f, (Im.Style.FrameHeight + Im.Style.ItemSpacing.Y) * 0.5f);
            Im.Text("Tile Transform"u8);
        }

        return ret;
    }

    private static bool DrawPbr(ColorTable table, ColorDyeTable? dyeTable, in DyePack? dyePack, int rowIdx)
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
        if (dyeTable is not null)
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
        if (dyeTable is not null)
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

    private static bool DrawSheen(ColorTable table, ColorDyeTable? dyeTable, in DyePack? dyePack, int rowIdx)
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
        if (dyeTable is not null)
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
        if (dyeTable is not null)
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
        if (dyeTable is not null)
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

    private static bool DrawExtra(ColorTable table, ColorDyeTable? dyeTable, in DyePack? dyePack, int rowIdx)
    {
        var scalarSize  = ColorTableScalarSize * Im.Style.GlobalScale;
        var subColWidth = CalculateSubColumnWidth(2) + Im.Style.ItemSpacing.X;
        var dyeOffset = subColWidth
          - Im.Style.ItemSpacing.X * 2.0f
          - Im.Style.ItemInnerSpacing.X
          - Im.Style.FrameHeight
          - scalarSize;

        var     ret    = false;
        ref var row    = ref table[rowIdx];
        var     dye    = dyeTable?[rowIdx] ?? default;
        var     isRowB = (rowIdx & 1) is not 0;

        Im.Item.SetNextWidth(scalarSize);
        var rawExposureValue = (float)row.Exposure;
        var exposureValue    = rawExposureValue is 0.0f ? ExposureMinInfDummy : MathF.Log2(rawExposureValue) * 0.5f;
        ret |= CtDragScalar("Exposure Value"u8, default, exposureValue, rawExposureValue is 0.0f ? "-∞"u8 : "%.1f"u8, ExposureMinInfDummy,
            ExposureMax,                        0.025f,
            v => table[rowIdx].Exposure =
                (Half)(v < ExposureMinThreshold ? 0.0f : MathF.Pow(2.0f, Math.Clamp(v, -ExposureMax, ExposureMax) * 2.0f)));
        if (dyeTable is not null)
        {
            Im.Line.Same(dyeOffset);
            ret |= CtApplyStainCheckbox("##dyeExposure"u8, "Apply Exposure Value on Dye"u8, dye.Exposure,
                b => dyeTable[rowIdx].Exposure = b);
            Im.Line.SameInner();
            Im.Item.SetNextWidth(scalarSize);
            var dyeExposureValue = (float?)dyePack?.Exposure switch
            {
                null      => new float?(),
                0.0f      => ExposureMinInfDummy,
                { } value => MathF.Log2(value) * 0.5f,
            };
            CtDragScalar("##dyePreviewExposure"u8, "Dye Preview for Exposure Value"u8, dyeExposureValue,
                dyePack?.Exposure == Half.Zero ? "-∞"u8 : "%.1f"u8);
        }

        Im.Line.Same(subColWidth);
        Im.Item.SetNextWidth(scalarSize);
        ret |= CtDragHalf(isRowB ? "Anisotropy (Unused)"u8 : "Anisotropy Degree"u8, default, row.Anisotropy, "%.1f"u8, 0.0f,
            HalfMaxValue,                                                           0.025f,  v => table[rowIdx].Anisotropy = v);
        if (dyeTable is not null)
        {
            Im.Line.Same(subColWidth + dyeOffset);
            ret |= CtApplyStainCheckbox("##dyeAnisotropy"u8, "Apply Anisotropy Degree on Dye"u8, dye.Anisotropy,
                b => dyeTable[rowIdx].Anisotropy = b);
            Im.Line.SameInner();
            Im.Item.SetNextWidth(scalarSize);
            CtDragHalf("##dyePreviewAnisotropy"u8, "Dye Preview for Anisotropy Degree"u8, dyePack?.Anisotropy, "%.1f"u8);
        }

        return ret;
    }

    private static bool DrawFurther(ColorTable table, int rowIdx)
    {
        var scalarSize  = ColorTableScalarSize * Im.Style.GlobalScale;
        var subColWidth = CalculateSubColumnWidth(2) + Im.Style.ItemSpacing.X;

        var     ret = false;
        ref var row = ref table[rowIdx];

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

        return ret;
    }

    private bool DrawDye(ColorDyeTable dyeTable, in DyePack? dyePack, int rowIdx)
    {
        var scalarSize       = ColorTableScalarSize * Im.Style.GlobalScale;
        var applyButtonWidth = Im.Font.CalculateSize("Apply Preview Dye"u8).X + Im.Style.FramePadding.X * 2.0f;
        var subColWidth      = CalculateSubColumnWidth(2, applyButtonWidth);

        var     ret = false;
        ref var dye = ref dyeTable[rowIdx];

        Im.Item.SetNextWidth(scalarSize);
        ret |= CtDragScalar("Dye Channel"u8, default, dye.Channel + 1, "%d"u8, 1, StainService.GetUiChannelCount(_config.Editing), 0.1f,
            value => dyeTable[rowIdx].Channel = (byte)(Math.Clamp(value, 1, StainService.ChannelCount) - 1));
        Im.Line.Same(subColWidth);
        Im.Item.SetNextWidth(scalarSize);
        if (_stainService.GudTemplateCombo.Draw("##dyeTemplate"u8, dye.Template, dye.Channel, StringU8.Empty, out var newSelection,
                scalarSize + Im.Style.ScrollbarSize / 2, Im.Style.TextHeightWithSpacing, ComboFlags.NoArrowButton))
        {
            dye.Template = (ushort)newSelection;
            ret          = true;
        }

        Im.Line.SameInner();
        Im.Text("Dye Template"u8);
        Im.Line.Same(Im.ContentRegion.Available.X - applyButtonWidth + Im.Style.ItemSpacing.X);
        using var dis = Im.Disabled(!dyePack.HasValue);
        if (Im.Button("Apply Preview Dye"u8))
        {
            Span<StainId> stainIds = stackalloc StainId[StainService.ChannelCount];
            _stainService.GetCurrentSelection(stainIds);
            ret |= Mtrl.ApplyDyeToRow(_stainService.GudStmFile, stainIds, rowIdx);
        }

        return ret;
    }

    private static void CenteredTextInRest(Utf8StringHandler<TextStringHandlerBuffer> text)
        => AlignedTextInRest(ref text, 0.5f);

    private static void AlignedTextInRest(ref Utf8StringHandler<TextStringHandlerBuffer> text, float alignment)
    {
        var width = Im.Font.CalculateSize(text).X;
        Im.Cursor.ScreenPosition += new Vector2((Im.ContentRegion.Available.X - width) * alignment, 0.0f);
        Im.Text(text);
    }

    private static float CalculateSubColumnWidth(int numSubColumns, float reservedSpace = 0.0f)
    {
        var itemSpacing = Im.Style.ItemSpacing.X;
        return (Im.ContentRegion.Available.X - reservedSpace - itemSpacing * (numSubColumns - 1)) / numSubColumns + itemSpacing;
    }
}

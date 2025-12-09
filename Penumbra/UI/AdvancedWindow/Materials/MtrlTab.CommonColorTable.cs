using Dalamud.Interface;
using ImSharp;
using Luna;
using Penumbra.GameData.Files;
using Penumbra.GameData.Files.MaterialStructs;
using Penumbra.GameData.Structs;

namespace Penumbra.UI.AdvancedWindow.Materials;

public partial class MtrlTab
{
    private static readonly float HalfMinValue = (float)Half.MinValue;
    private static readonly float HalfMaxValue = (float)Half.MaxValue;
    private static readonly float HalfEpsilon  = (float)Half.Epsilon;

    private static (Vector2 Scale, float Rotation, float Shear)? _pinnedTileTransform;

    private bool DrawColorTableSection(bool disabled)
    {
        if (!_shpkLoading && !TextureIds.Contains(ShpkFile.TableSamplerId) || Mtrl.Table == null)
            return false;

        Im.Dummy(new Vector2(Im.Style.TextHeight / 2));
        if (!Im.Tree.Header("Color Table"u8, TreeNodeFlags.DefaultOpen))
            return false;

        ColorTableCopyAllClipboardButton();
        Im.Line.Same();
        var ret = ColorTablePasteAllClipboardButton(disabled);
        if (!disabled)
        {
            Im.Line.Same();
            Im.FrameDummy();
            Im.Line.Same();
            ret |= ColorTableDyeableCheckbox();
        }

        if (Mtrl.DyeTable is not null)
        {
            Im.Line.Same();
            Im.FrameDummy();
            Im.Line.Same();
            ret |= DrawPreviewDye(disabled);
        }

        ret |= Mtrl.Table switch
        {
            LegacyColorTable legacyTable => DrawLegacyColorTable(legacyTable, Mtrl.DyeTable as LegacyColorDyeTable, disabled),
            ColorTable table when Mtrl.ShaderPackage.Name is "characterlegacy.shpk" => DrawLegacyColorTable(table,
                Mtrl.DyeTable as ColorDyeTable, disabled),
            ColorTable table => DrawColorTable(table, Mtrl.DyeTable as ColorDyeTable, disabled),
            _                => false,
        };

        return ret;
    }

    private void ColorTableCopyAllClipboardButton()
    {
        if (Mtrl.Table == null)
            return;

        if (!Im.Button("Export All Rows to Clipboard"u8, ImEx.ScaledVector(200, 0)))
            return;

        try
        {
            var data1 = Mtrl.Table.AsBytes();
            var data2 = Mtrl.DyeTable != null ? Mtrl.DyeTable.AsBytes() : [];

            var array = new byte[data1.Length + data2.Length];
            data1.TryCopyTo(array);
            data2.TryCopyTo(array.AsSpan(data1.Length));

            var text = Convert.ToBase64String(array);
            Im.Clipboard.Set(text);
        }
        catch
        {
            // ignored
        }
    }

    private bool DrawPreviewDye(bool disabled)
    {
        var (dyeId1, (name1, dyeColor1, gloss1)) = _stainService.StainCombo1.CurrentSelection;
        var (dyeId2, (name2, dyeColor2, gloss2)) = _stainService.StainCombo2.CurrentSelection;
        var tt = dyeId1 is 0 && dyeId2 is 0
            ? "Select a preview dye first."u8
            : "Apply all preview values corresponding to the dye template and chosen dye where dyeing is enabled."u8;
        if (ImEx.Button("Apply Preview Dye"u8, default, tt, disabled || dyeId1 is 0 && dyeId2 is 0))
        {
            var ret = false;
            if (Mtrl.DyeTable != null)
            {
                ret |= Mtrl.ApplyDye(_stainService.LegacyStmFile, [dyeId1, dyeId2]);
                ret |= Mtrl.ApplyDye(_stainService.GudStmFile,    [dyeId1, dyeId2]);
            }

            UpdateColorTablePreview();

            return ret;
        }

        Im.Line.Same();
        var label = dyeId1 is 0 ? "Preview Dye 1###previewDye1" : $"{name1} (Preview 1)###previewDye1";
        if (_stainService.StainCombo1.Draw(label, dyeColor1, string.Empty, true, gloss1))
            UpdateColorTablePreview();
        Im.Line.Same();
        label = dyeId2 is 0 ? "Preview Dye 2###previewDye2" : $"{name2} (Preview 2)###previewDye2";
        if (_stainService.StainCombo2.Draw(label, dyeColor2, string.Empty, true, gloss2))
            UpdateColorTablePreview();
        return false;
    }

    private bool ColorTablePasteAllClipboardButton(bool disabled)
    {
        if (Mtrl.Table is null)
            return false;

        if (!ImEx.Button("Import All Rows from Clipboard"u8, ImEx.ScaledVector(200, 0), disabled))
            return false;

        try
        {
            var text     = Im.Clipboard.GetUtf16();
            var data     = Convert.FromBase64String(text);
            var table    = Mtrl.Table.AsBytes();
            var dyeTable = Mtrl.DyeTable != null ? Mtrl.DyeTable.AsBytes() : [];
            if (data.Length != table.Length && data.Length != table.Length + dyeTable.Length)
                return false;

            data.AsSpan(0, table.Length).TryCopyTo(table);
            data.AsSpan(table.Length).TryCopyTo(dyeTable);

            UpdateColorTablePreview();

            return true;
        }
        catch
        {
            return false;
        }
    }

    [SkipLocalsInit]
    private void ColorTableCopyClipboardButton(int rowIdx)
    {
        if (Mtrl.Table == null)
            return;

        if (!ImEx.Icon.Button(LunaStyle.ToClipboardIcon, "Export this row to your clipboard."u8))
            return;

        try
        {
            var data1 = Mtrl.Table.RowAsBytes(rowIdx);
            var data2 = Mtrl.DyeTable is not null ? Mtrl.DyeTable.RowAsBytes(rowIdx) : [];

            var array = new byte[data1.Length + data2.Length];
            data1.TryCopyTo(array);
            data2.TryCopyTo(array.AsSpan(data1.Length));

            var text = Convert.ToBase64String(array);
            Im.Clipboard.Set(text);
        }
        catch
        {
            // ignored
        }
    }

    private bool ColorTableDyeableCheckbox()
    {
        var dyeable = Mtrl.DyeTable is not null;
        var ret     = Im.Checkbox("Dyeable"u8, ref dyeable);

        if (ret)
        {
            Mtrl.DyeTable = dyeable
                ? Mtrl.Table switch
                {
                    ColorTable       => new ColorDyeTable(),
                    LegacyColorTable => new LegacyColorDyeTable(),
                    _                => null,
                }
                : null;
            UpdateColorTablePreview();
        }

        return ret;
    }

    private bool ColorTablePasteFromClipboardButton(int rowIdx, bool disabled)
    {
        if (Mtrl.Table is null)
            return false;

        if (ImEx.Icon.Button(LunaStyle.FromClipboardIcon,
                "Import an exported row from your clipboard onto this row.\n\nRight-Click for more options."u8, disabled))
            try
            {
                var text   = Im.Clipboard.GetUtf16();
                var data   = Convert.FromBase64String(text);
                var row    = Mtrl.Table.RowAsBytes(rowIdx);
                var dyeRow = Mtrl.DyeTable is not null ? Mtrl.DyeTable.RowAsBytes(rowIdx) : [];
                if (data.Length != row.Length && data.Length != row.Length + dyeRow.Length)
                    return false;

                data.AsSpan(0, row.Length).TryCopyTo(row);
                data.AsSpan(row.Length).TryCopyTo(dyeRow);

                UpdateColorTableRowPreview(rowIdx);

                return true;
            }
            catch
            {
                return false;
            }

        return ColorTablePasteFromClipboardContext(rowIdx, disabled);
    }

    private unsafe bool ColorTablePasteFromClipboardContext(int rowIdx, bool disabled)
    {
        if (!disabled && Im.Item.RightClicked())
            Im.Popup.Open("context"u8);

        using var context = Im.Popup.Begin("context"u8);
        if (!context)
            return false;

        using var _ = Im.Disabled(disabled);

        IColorTable.ValueTypes    copy    = 0;
        IColorDyeTable.ValueTypes dyeCopy = 0;
        if (Im.Selectable("Import Colors Only"u8))
        {
            copy    = IColorTable.ValueTypes.Colors;
            dyeCopy = IColorDyeTable.ValueTypes.Colors;
        }

        if (Im.Selectable("Import Other Values Only"u8))
        {
            copy    = ~IColorTable.ValueTypes.Colors;
            dyeCopy = ~IColorDyeTable.ValueTypes.Colors;
        }

        if (copy == 0)
            return false;

        try
        {
            var text   = Im.Clipboard.GetUtf16();
            var data   = Convert.FromBase64String(text);
            var row    = Mtrl.Table!.RowAsHalves(rowIdx);
            var halves = new Span<Half>(Unsafe.AsPointer(ref data[0]), row.Length);
            var dyeRow = Mtrl.DyeTable != null ? Mtrl.DyeTable.RowAsBytes(rowIdx) : [];
            if (!Mtrl.Table.MergeSpecificValues(row, halves, copy))
                return false;

            Mtrl.DyeTable?.MergeSpecificValues(dyeRow, data.AsSpan(row.Length * 2), dyeCopy);

            UpdateColorTableRowPreview(rowIdx);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void ColorTablePairHighlightButton(int pairIdx, bool disabled)
    {
        ImEx.Icon.Button(LunaStyle.OnHoverIcon,
            "Highlight this pair of rows on your character, if possible.\n\nHighlight colors can be configured in Penumbra's settings."u8,
            disabled || _colorTablePreviewers.Count is 0);

        if (Im.Item.Hovered())
            HighlightColorTablePair(pairIdx);
        else if (_highlightedColorTablePair == pairIdx)
            CancelColorTableHighlight();
    }

    private void ColorTableRowHighlightButton(int rowIdx, bool disabled)
    {
        ImEx.Icon.Button(LunaStyle.OnHoverIcon,
            "Highlight this row on your character, if possible.\n\nHighlight colors can be configured in Penumbra's settings."u8,
            disabled || _colorTablePreviewers.Count is 0);

        if (Im.Item.Hovered())
            HighlightColorTableRow(rowIdx);
        else if (_highlightedColorTableRow == rowIdx)
            CancelColorTableHighlight();
    }

    private static void CtBlendRect(Vector2 rcMin, Vector2 rcMax, Rgba32 topColor, Rgba32 bottomColor)
    {
        var frameRounding  = Im.Style.FrameRounding;
        var frameThickness = Im.Style.FrameBorderThickness;
        var borderColor    = ImGuiColor.Border.Get();
        var drawList       = Im.Window.DrawList.Shape;
        if (topColor == bottomColor)
        {
            drawList.RectangleFilled(rcMin, rcMax, topColor, frameRounding);
        }
        else
        {
            drawList.RectangleFilled(
                rcMin, rcMax with { Y = float.Lerp(rcMin.Y, rcMax.Y, 1.0f / 3) },
                topColor, frameRounding, ImDrawFlagsRectangle.RoundCornersTop);
            drawList.RectangleMulticolor(
                rcMin with { Y = float.Lerp(rcMin.Y, rcMax.Y, 1.0f / 3) },
                rcMax with { Y = float.Lerp(rcMin.Y, rcMax.Y, 2.0f / 3) },
                topColor, topColor, bottomColor, bottomColor);
            drawList.RectangleFilled(
                rcMin with { Y = float.Lerp(rcMin.Y, rcMax.Y, 2.0f / 3) }, rcMax,
                bottomColor, frameRounding, ImDrawFlagsRectangle.RoundCornersTop);
        }

        drawList.Rectangle(rcMin, rcMax, borderColor.Color, frameRounding, default, frameThickness);
    }

    private static bool CtColorPicker(ReadOnlySpan<byte> label, ReadOnlySpan<byte> description, HalfColor current, Action<HalfColor> setter,
        ReadOnlySpan<byte> letter = default)
    {
        var ret       = false;
        var inputSqrt = PseudoSqrtRgb((Vector3)current);
        var tmp       = inputSqrt;
        if (Im.Color.Editor(label, ref tmp,
                ColorEditorFlags.NoInputs
              | ColorEditorFlags.DisplayRgb
              | ColorEditorFlags.InputRgb
              | ColorEditorFlags.NoTooltip
              | ColorEditorFlags.Hdr)
         && tmp != inputSqrt)
        {
            setter((HalfColor)PseudoSquareRgb(tmp));
            ret = true;
        }

        if (letter.Length > 0 && Im.Item.Visible)
        {
            var textSize  = Im.Font.CalculateSize(letter);
            var center    = Im.Item.UpperLeftCorner + (Im.Item.Size - textSize) / 2;
            var textColor = inputSqrt.LengthSquared() < 0.25f ? 0x80FFFFFFu : 0x80000000u;
            Im.Window.DrawList.Text(center, textColor, letter);
        }

        Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, description);

        return ret;
    }

    private static void CtColorPicker(ReadOnlySpan<byte> label, ReadOnlySpan<byte> description, HalfColor? current,
        ReadOnlySpan<byte> letter = default)
    {
        if (current.HasValue)
        {
            CtColorPicker(label, description, current.Value, Nop, letter);
        }
        else
        {
            var tmp = Vector4.Zero;
            Im.Color.Editor(label, ref tmp,
                ColorEditorFlags.NoInputs
              | ColorEditorFlags.DisplayRgb
              | ColorEditorFlags.InputRgb
              | ColorEditorFlags.NoTooltip
              | ColorEditorFlags.Hdr
              | ColorEditorFlags.AlphaPreview);

            if (letter.Length > 0 && Im.Item.Visible)
            {
                var textSize = Im.Font.CalculateSize(letter);
                var center   = Im.Item.UpperLeftCorner + (Im.Item.Size - textSize) / 2;
                Im.Window.DrawList.Text(center, 0x80000000u, letter);
            }

            Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, description);
        }
    }

    private static bool CtApplyStainCheckbox(ReadOnlySpan<byte> label, ReadOnlySpan<byte> description, bool current, Action<bool> setter)
    {
        var tmp    = current;
        var result = ImEx.IconCheckbox(label, FontAwesomeIcon.FillDrip.Icon(), ref tmp);
        Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, description);
        if (!result || tmp == current)
            return false;

        setter(tmp);
        return true;
    }

    private static bool CtDragHalf(ReadOnlySpan<byte> label, ReadOnlySpan<byte> description, Half value, ReadOnlySpan<byte> format, float min,
        float max, float speed, Action<Half> setter)
    {
        var tmp    = (float)value;
        var result = Im.Drag(label, ref tmp, format, min, max, speed);
        Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, description);
        if (!result)
            return false;

        var newValue = (Half)tmp;
        if (newValue == value)
            return false;

        setter(newValue);
        return true;
    }

    private static bool CtDragHalf(ReadOnlySpan<byte> label, ReadOnlySpan<byte> description, ref Half value, ReadOnlySpan<byte> format,
        float min, float max, float speed)
    {
        var tmp    = (float)value;
        var result = Im.Drag(label, ref tmp, format, min, max, speed);
        Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, description);
        if (!result)
            return false;

        var newValue = (Half)tmp;
        if (newValue == value)
            return false;

        value = newValue;
        return true;
    }

    private static void CtDragHalf(ReadOnlySpan<byte> label, ReadOnlySpan<byte> description, Half? value, ReadOnlySpan<byte> format)
    {
        using var _              = Im.Disabled();
        var       valueOrDefault = value ?? Half.Zero;
        var       floatValue     = (float)valueOrDefault;
        CtDragHalf(label, description, valueOrDefault, value.HasValue ? format : "-"u8, floatValue, floatValue, 0.0f, Nop);
    }

    private static bool CtDragScalar<T>(ReadOnlySpan<byte> label, ReadOnlySpan<byte> description, T value, ReadOnlySpan<byte> format, T min,
        T max, float speed, Action<T> setter) where T : unmanaged, INumber<T>
    {
        var tmp    = value;
        var result = Im.Drag(label, ref tmp, format, min, max, speed);
        Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, description);
        if (!result || tmp == value)
            return false;

        setter(tmp);
        return true;
    }

    private static bool CtDragScalar<T>(ReadOnlySpan<byte> label, ReadOnlySpan<byte> description, ref T value, ReadOnlySpan<byte> format, T min,
        T max, float speed) where T : unmanaged, INumber<T>
    {
        var tmp    = value;
        var result = Im.Drag(label, ref tmp, format, min, max, speed);
        Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, description);
        if (!result || tmp == value)
            return false;

        value = tmp;
        return true;
    }

    private static void CtDragScalar<T>(ReadOnlySpan<byte> label, ReadOnlySpan<byte> description, T? value, ReadOnlySpan<byte> format)
        where T : unmanaged, INumber<T>
    {
        using var _              = Im.Disabled();
        var       valueOrDefault = value ?? T.Zero;
        CtDragScalar(label, description, valueOrDefault, value.HasValue ? format : "-"u8, valueOrDefault, valueOrDefault, 0.0f, Nop);
    }

    private bool CtTileIndexPicker(ReadOnlySpan<byte> label, ReadOnlySpan<byte> description, ushort value, bool compact, Action<ushort> setter)
    {
        if (!_materialTemplatePickers.DrawTileIndexPicker(label, description, ref value, compact))
            return false;

        setter(value);
        return true;
    }

    private bool CtSphereMapIndexPicker(ReadOnlySpan<byte> label, ReadOnlySpan<byte> description, ushort value, bool compact,
        Action<ushort> setter)
    {
        if (!_materialTemplatePickers.DrawSphereMapIndexPicker(label, description, ref value, compact))
            return false;

        setter(value);
        return true;
    }

    private bool CtTileTransformMatrix(HalfMatrix2x2 value, float floatSize, bool twoRowLayout, Action<HalfMatrix2x2> setter)
    {
        var ret = false;
        if (_config.EditRawTileTransforms)
        {
            var tmp = value;
            Im.Item.SetNextWidth(floatSize);
            ret |= CtDragHalf("##TileTransformUU"u8, "Tile Repeat U"u8, ref tmp.UU, "%.2f"u8, HalfMinValue, HalfMaxValue, 0.1f);
            Im.Line.SameInner();
            Im.Item.SetNextWidth(floatSize);
            ret |= CtDragHalf("##TileTransformVV"u8, "Tile Repeat V"u8, ref tmp.VV, "%.2f"u8, HalfMinValue, HalfMaxValue, 0.1f);
            if (!twoRowLayout)
                Im.Line.SameInner();
            Im.Item.SetNextWidth(floatSize);
            ret |= CtDragHalf("##TileTransformUV"u8, "Tile Skew U"u8, ref tmp.UV, "%.2f"u8, HalfMinValue, HalfMaxValue, 0.1f);
            Im.Line.SameInner();
            Im.Item.SetNextWidth(floatSize);
            ret |= CtDragHalf("##TileTransformVU"u8, "Tile Skew V"u8, ref tmp.VU, "%.2f"u8, HalfMinValue, HalfMaxValue, 0.1f);
            if (!ret || tmp == value)
                return false;

            setter(tmp);
        }
        else
        {
            value.Decompose(out var scale, out var rotation, out var shear);
            rotation *= 180.0f / MathF.PI;
            shear    *= 180.0f / MathF.PI;
            Im.Item.SetNextWidth(floatSize);
            var scaleXChanged = CtDragScalar("##TileScaleU"u8, "Tile Scale U"u8, ref scale.X, "%.2f"u8, HalfMinValue, HalfMaxValue, 0.1f);
            var activated     = Im.Item.Activated;
            var deactivated   = Im.Item.Deactivated;
            Im.Line.SameInner();
            Im.Item.SetNextWidth(floatSize);
            var scaleYChanged = CtDragScalar("##TileScaleV"u8, "Tile Scale V"u8, ref scale.Y, "%.2f"u8, HalfMinValue, HalfMaxValue, 0.1f);
            activated   |= Im.Item.Activated;
            deactivated |= Im.Item.Deactivated;
            if (!twoRowLayout)
                Im.Line.SameInner();
            Im.Item.SetNextWidth(floatSize);
            var rotationChanged = CtDragScalar("##TileRotation"u8, "Tile Rotation"u8, ref rotation, "%.0f°"u8, -180.0f, 180.0f, 1.0f);
            activated   |= Im.Item.Activated;
            deactivated |= Im.Item.Deactivated;
            Im.Line.SameInner();
            Im.Item.SetNextWidth(floatSize);
            var shearChanged = CtDragScalar("##TileShear"u8, "Tile Shear"u8, ref shear, "%.0f°"u8, -90.0f, 90.0f, 1.0f);
            activated   |= Im.Item.Activated;
            deactivated |= Im.Item.Deactivated;
            if (deactivated)
                _pinnedTileTransform = null;
            else if (activated)
                _pinnedTileTransform = (scale, rotation, shear);
            ret = scaleXChanged | scaleYChanged | rotationChanged | shearChanged;
            if (!ret)
                return false;

            if (_pinnedTileTransform.HasValue)
            {
                var (pinScale, pinRotation, pinShear) = _pinnedTileTransform.Value;
                if (!scaleXChanged)
                    scale.X = pinScale.X;
                if (!scaleYChanged)
                    scale.Y = pinScale.Y;
                if (!rotationChanged)
                    rotation = pinRotation;
                if (!shearChanged)
                    shear = pinShear;
            }

            var newValue = HalfMatrix2x2.Compose(scale, rotation * MathF.PI / 180.0f, shear * MathF.PI / 180.0f);
            if (newValue == value)
                return false;

            setter(newValue);
        }

        return true;
    }

    /// <remarks> For use as setter of read-only fields. </remarks>
    private static void Nop<T>(T _)
    { }

    // Functions to deal with squared RGB values without making negatives useless.

    internal static float PseudoSquareRgb(float x)
        => x < 0.0f ? -(x * x) : x * x;

    internal static Vector3 PseudoSquareRgb(Vector3 vec)
        => new(PseudoSquareRgb(vec.X), PseudoSquareRgb(vec.Y), PseudoSquareRgb(vec.Z));

    internal static Vector4 PseudoSquareRgb(Vector4 vec)
        => new(PseudoSquareRgb(vec.X), PseudoSquareRgb(vec.Y), PseudoSquareRgb(vec.Z), vec.W);

    internal static float PseudoSqrtRgb(float x)
        => x < 0.0f ? -MathF.Sqrt(-x) : MathF.Sqrt(x);

    public static Vector3 PseudoSqrtRgb(Vector3 vec)
        => new(PseudoSqrtRgb(vec.X), PseudoSqrtRgb(vec.Y), PseudoSqrtRgb(vec.Z));

    internal static Vector4 PseudoSqrtRgb(Vector4 vec)
        => new(PseudoSqrtRgb(vec.X), PseudoSqrtRgb(vec.Y), PseudoSqrtRgb(vec.Z), vec.W);
}

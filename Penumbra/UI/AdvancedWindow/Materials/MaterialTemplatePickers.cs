using ImSharp;
using Luna;
using OtterGui.Text.Widget.Editors;
using Penumbra.Interop.Services;
using Penumbra.Interop.Structs;

namespace Penumbra.UI.AdvancedWindow.Materials;

public sealed unsafe class MaterialTemplatePickers : IUiService
{
    private const float MaximumTextureSize = 64.0f;

    private readonly TextureArraySlicer _textureArraySlicer;
    private readonly CharacterUtility   _characterUtility;

    public readonly IEditor<byte> TileIndexPicker;
    public readonly IEditor<byte> SphereMapIndexPicker;

    public MaterialTemplatePickers(TextureArraySlicer textureArraySlicer, CharacterUtility characterUtility)
    {
        _textureArraySlicer = textureArraySlicer;
        _characterUtility   = characterUtility;

        TileIndexPicker      = new Editor(DrawTileIndexPicker).AsByteEditor();
        SphereMapIndexPicker = new Editor(DrawSphereMapIndexPicker).AsByteEditor();
    }

    public bool DrawTileIndexPicker(ReadOnlySpan<byte> label, ReadOnlySpan<byte> description, ref ushort value, bool compact)
        => _characterUtility.Address != null
         && DrawTextureArrayIndexPicker(label, description, ref value, compact, [
                _characterUtility.Address->TileOrbArrayTexResource,
                _characterUtility.Address->TileNormArrayTexResource,
            ]);

    public bool DrawSphereMapIndexPicker(ReadOnlySpan<byte> label, ReadOnlySpan<byte> description, ref ushort value, bool compact)
        => _characterUtility.Address != null
         && DrawTextureArrayIndexPicker(label, description, ref value, compact, [
                _characterUtility.Address->SphereDArrayTexResource,
            ]);

    public bool DrawTextureArrayIndexPicker(ReadOnlySpan<byte> label, ReadOnlySpan<byte> description, ref ushort value, bool compact,
        ReadOnlySpan<FFXIVClientStructs.Interop.Pointer<TextureResourceHandle>> textureRHs)
    {
        TextureResourceHandle* firstNonNullTextureRh = null;
        foreach (var texture in textureRHs)
        {
            if (texture.Value != null && texture.Value->CsHandle.Texture != null)
            {
                firstNonNullTextureRh = texture;
                break;
            }
        }

        var firstNonNullTexture = firstNonNullTextureRh != null ? firstNonNullTextureRh->CsHandle.Texture : null;

        var textureSize = firstNonNullTexture != null
            ? new Vector2(firstNonNullTexture->ActualWidth, firstNonNullTexture->ActualHeight).Contain(new Vector2(MaximumTextureSize))
            : Vector2.Zero;
        var count = firstNonNullTexture != null ? firstNonNullTexture->ArraySize : 0;

        var ret = false;

        var framePadding = Im.Style.FramePadding;
        var itemSpacing  = Im.Style.ItemSpacing;
        using (Im.Font.PushMono())
        {
            var spaceSize = Im.Font.Mono.GetCharacterAdvance(' ');
            var spaces = (int)((Im.Item.CalculateWidth()
                  - framePadding.X * 2.0f
                  - (compact ? 0.0f : (textureSize.X + itemSpacing.X) * textureRHs.Length))
              / spaceSize);
            using var padding = ImStyleDouble.FramePadding.Push(
                framePadding + new Vector2(0.0f, Math.Max(textureSize.Y - Im.Style.FrameHeight + itemSpacing.Y, 0.0f) * 0.5f), !compact);
            using var combo = Im.Combo.Begin(label, (value is ushort.MaxValue ? "-" : value.ToString()).PadLeft(spaces),
                ComboFlags.NoArrowButton | ComboFlags.HeightLarge);
            if (combo.Success && firstNonNullTextureRh != null)
            {
                var lineHeight = Math.Max(Im.Style.TextHeightWithSpacing, framePadding.Y * 2.0f + textureSize.Y);
                var itemWidth = Math.Max(Im.ContentRegion.Available.X,
                    Im.Font.CalculateSize("MMM"u8).X + (itemSpacing.X + textureSize.X) * textureRHs.Length + framePadding.X * 2.0f);
                using var center  = ImStyleDouble.SelectableTextAlign.Push(new Vector2(0, 0.5f));
                using var clipper = new Im.ListClipper(count, lineHeight);
                foreach (var index in clipper)
                {
                    if (Im.Selectable($"{index,3}", index == value, size: new Vector2(itemWidth, lineHeight)))
                    {
                        ret   = value != index;
                        value = (ushort)index;
                    }

                    var rectMin = Im.Item.UpperLeftCorner;
                    var rectMax = Im.Item.LowerRightCorner;
                    var textureRegionStart = new Vector2(
                        rectMax.X - framePadding.X - textureSize.X * textureRHs.Length - itemSpacing.X * (textureRHs.Length - 1),
                        rectMin.Y + framePadding.Y);
                    var maxSize = textureSize with { Y = rectMax.Y - framePadding.Y - textureRegionStart.Y };
                    DrawTextureSlices(textureRegionStart, maxSize, itemSpacing.X, textureRHs, (byte)index);
                }
            }
        }

        if (!compact && value is not ushort.MaxValue)
        {
            var cbRectMin = Im.Item.UpperLeftCorner;
            var cbRectMax = Im.Item.LowerRightCorner;
            var cbTextureRegionStart =
                new Vector2(cbRectMax.X - framePadding.X - textureSize.X * textureRHs.Length - itemSpacing.X * (textureRHs.Length - 1),
                    cbRectMin.Y + framePadding.Y);
            var cbMaxSize = textureSize with { Y = cbRectMax.Y - framePadding.Y - cbTextureRegionStart.Y };
            DrawTextureSlices(cbTextureRegionStart, cbMaxSize, itemSpacing.X, textureRHs, (byte)value);
        }

        if (Im.Item.Hovered(HoveredFlags.AllowWhenDisabled) && (description.Length > 0 || compact && value is not ushort.MaxValue))
        {
            using var disabled = Im.Enabled();
            using var tt       = Im.Tooltip.Begin();
            if (description.Length > 0)
                Im.Text(description);
            if (compact && value != ushort.MaxValue)
            {
                Im.Dummy(textureSize with { X = textureSize.X * textureRHs.Length + itemSpacing.X * (textureRHs.Length - 1) });
                var rectMin = Im.Item.UpperLeftCorner;
                DrawTextureSlices(rectMin, textureSize, itemSpacing.X, textureRHs, (byte)value);
            }
        }

        return ret;
    }

    public void DrawTextureSlices(Vector2 regionStart, Vector2 itemSize, float itemSpacing,
        ReadOnlySpan<FFXIVClientStructs.Interop.Pointer<TextureResourceHandle>> textureRHs, byte sliceIndex)
    {
        for (var j = 0; j < textureRHs.Length; ++j)
        {
            if (textureRHs[j].Value == null)
                continue;

            var texture = textureRHs[j].Value->CsHandle.Texture;
            if (texture == null)
                continue;

            var handle = _textureArraySlicer.GetImGuiHandle(texture, sliceIndex);
            if (handle.IsNull)
                continue;

            var position = regionStart with { X = regionStart.X + (itemSize.X + itemSpacing) * j };
            var size     = new Vector2(texture->ActualWidth, texture->ActualHeight).Contain(itemSize);
            position += (itemSize - size) * 0.5f;
            var uvSize = Rectangle.FromSize(texture->ActualWidth / (float)texture->AllocatedWidth,
                texture->ActualHeight / (float)texture->AllocatedHeight);
            Im.Window.DrawList.Image(handle, Rectangle.FromSize(position, size), uvSize);
        }
    }

    private delegate bool DrawEditor(ReadOnlySpan<byte> label, ReadOnlySpan<byte> description, ref ushort value, bool compact);

    private sealed class Editor(DrawEditor draw) : IEditor<float>
    {
        public bool Draw(Span<float> values, bool disabled)
        {
            var helper = Editors.PrepareMultiComponent(values.Length);
            var ret    = false;

            for (var valueIdx = 0; valueIdx < values.Length; ++valueIdx)
            {
                helper.SetupComponent(valueIdx);

                var value = ushort.CreateSaturating(MathF.Round(values[valueIdx]));
                if (disabled)
                {
                    using var _ = Im.Disabled();
                    draw(helper.Id, default, ref value, true);
                }
                else
                {
                    if (draw(helper.Id, default, ref value, true))
                    {
                        values[valueIdx] = value;
                        ret              = true;
                    }
                }
            }

            return ret;
        }
    }
}

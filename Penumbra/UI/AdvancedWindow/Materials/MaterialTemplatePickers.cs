using Dalamud.Interface;
using FFXIVClientStructs.Interop;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Services;
using OtterGui.Text;
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

    public bool DrawTextureArrayIndexPicker(ReadOnlySpan<byte> label, ReadOnlySpan<byte> description, ref ushort value, bool compact, ReadOnlySpan<Pointer<TextureResourceHandle>> textureRHs)
    {
        TextureResourceHandle* firstNonNullTextureRH = null;
        foreach (var texture in textureRHs)
        {
            if (texture.Value != null && texture.Value->CsHandle.Texture != null)
            {
                firstNonNullTextureRH = texture;
                break;
            }
        }
        var firstNonNullTexture = firstNonNullTextureRH != null ? firstNonNullTextureRH->CsHandle.Texture : null;

        var textureSize = firstNonNullTexture != null ? new Vector2(firstNonNullTexture->ActualWidth, firstNonNullTexture->ActualHeight).Contain(new Vector2(MaximumTextureSize)) : Vector2.Zero;
        var count       = firstNonNullTexture != null ? firstNonNullTexture->ArraySize : 0;

        var ret = false;

        var framePadding = ImGui.GetStyle().FramePadding;
        var itemSpacing  = ImGui.GetStyle().ItemSpacing;
        using (var font = ImRaii.PushFont(UiBuilder.MonoFont))
        {
            var spaceSize     = ImUtf8.CalcTextSize(" "u8).X;
            var spaces        = (int)((ImGui.CalcItemWidth() - framePadding.X * 2.0f - (compact ? 0.0f : (textureSize.X + itemSpacing.X) * textureRHs.Length)) / spaceSize);
            using var padding = ImRaii.PushStyle(ImGuiStyleVar.FramePadding, framePadding + new Vector2(0.0f, Math.Max(textureSize.Y - ImGui.GetFrameHeight() + itemSpacing.Y, 0.0f) * 0.5f), !compact);
            using var combo   = ImUtf8.Combo(label, (value == ushort.MaxValue ? "-" : value.ToString()).PadLeft(spaces), ImGuiComboFlags.NoArrowButton | ImGuiComboFlags.HeightLarge);
            if (combo.Success && firstNonNullTextureRH != null)
            {
                var lineHeight    = Math.Max(ImGui.GetTextLineHeightWithSpacing(), framePadding.Y * 2.0f + textureSize.Y);
                var itemWidth     = Math.Max(ImGui.GetContentRegionAvail().X, ImUtf8.CalcTextSize("MMM"u8).X + (itemSpacing.X + textureSize.X) * textureRHs.Length + framePadding.X * 2.0f);
                using var center  = ImRaii.PushStyle(ImGuiStyleVar.SelectableTextAlign, new Vector2(0, 0.5f));
                using var clipper = ImUtf8.ListClipper(count, lineHeight);
                while (clipper.Step())
                {
                    for (var i = clipper.DisplayStart; i < clipper.DisplayEnd && i < count; i++)
                    {
                        if (ImUtf8.Selectable($"{i,3}", i == value, size: new(itemWidth, lineHeight)))
                        {
                            ret   = value != i;
                            value = (ushort)i;
                        }
                        var rectMin = ImGui.GetItemRectMin();
                        var rectMax = ImGui.GetItemRectMax();
                        var textureRegionStart = new Vector2(
                            rectMax.X - framePadding.X - textureSize.X * textureRHs.Length - itemSpacing.X * (textureRHs.Length - 1),
                            rectMin.Y + framePadding.Y);
                        var maxSize = new Vector2(textureSize.X, rectMax.Y - framePadding.Y - textureRegionStart.Y);
                        DrawTextureSlices(textureRegionStart, maxSize, itemSpacing.X, textureRHs, (byte)i);
                    }
                }
            }
        }
        if (!compact && value != ushort.MaxValue)
        {
            var cbRectMin            = ImGui.GetItemRectMin();
            var cbRectMax            = ImGui.GetItemRectMax();
            var cbTextureRegionStart = new Vector2(cbRectMax.X - framePadding.X - textureSize.X * textureRHs.Length - itemSpacing.X * (textureRHs.Length - 1), cbRectMin.Y + framePadding.Y);
            var cbMaxSize            = new Vector2(textureSize.X, cbRectMax.Y - framePadding.Y - cbTextureRegionStart.Y);
            DrawTextureSlices(cbTextureRegionStart, cbMaxSize, itemSpacing.X, textureRHs, (byte)value);
        }
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled) && (description.Length > 0 || compact && value != ushort.MaxValue))
        {
            using var disabled = ImRaii.Enabled();
            using var tt       = ImUtf8.Tooltip();
            if (description.Length > 0)
                ImUtf8.Text(description);
            if (compact && value != ushort.MaxValue)
            {
                ImGui.Dummy(new Vector2(textureSize.X * textureRHs.Length + itemSpacing.X * (textureRHs.Length - 1), textureSize.Y));
                var rectMin = ImGui.GetItemRectMin();
                var rectMax = ImGui.GetItemRectMax();
                DrawTextureSlices(rectMin, textureSize, itemSpacing.X, textureRHs, (byte)value);
            }
        }

        return ret;
    }

    public void DrawTextureSlices(Vector2 regionStart, Vector2 itemSize, float itemSpacing, ReadOnlySpan<Pointer<TextureResourceHandle>> textureRHs, byte sliceIndex)
    {
        for (var j = 0; j < textureRHs.Length; ++j)
        {
            if (textureRHs[j].Value == null)
                continue;
            var texture = textureRHs[j].Value->CsHandle.Texture;
            if (texture == null)
                continue;
            var handle = _textureArraySlicer.GetImGuiHandle(texture, sliceIndex);
            if (handle == 0)
                continue;

            var position =  regionStart with { X = regionStart.X + (itemSize.X + itemSpacing) * j };
            var size     =  new Vector2(texture->ActualWidth, texture->ActualHeight).Contain(itemSize);
            position     += (itemSize - size) * 0.5f;
            ImGui.GetWindowDrawList().AddImage(handle, position, position + size, Vector2.Zero,
                new Vector2(texture->ActualWidth / (float)texture->AllocatedWidth, texture->ActualHeight / (float)texture->AllocatedHeight));
        }
    }

    private delegate bool DrawEditor(ReadOnlySpan<byte> label, ReadOnlySpan<byte> description, ref ushort value, bool compact);

    private sealed class Editor(DrawEditor draw) : IEditor<float>
    {
        public bool Draw(Span<float> values, bool disabled)
        {
            var helper = Editors.PrepareMultiComponent(values.Length);
            var ret = false;

            for (var valueIdx = 0; valueIdx < values.Length; ++valueIdx)
            {
                helper.SetupComponent(valueIdx);

                var value = ushort.CreateSaturating(MathF.Round(values[valueIdx]));
                if (disabled)
                {
                    using var _ = ImRaii.Disabled();
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

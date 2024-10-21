using Dalamud.Interface;
using FFXIVClientStructs.Interop;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Services;
using OtterGui.Text;
using OtterGui.Text.Widget.Editors;
using OtterGui.Widgets;
using Penumbra.Interop.Services;
using Penumbra.Interop.Structs;

namespace Penumbra.UI.AdvancedWindow.Materials;

public sealed unsafe class MaterialTemplatePickers : IUiService
{
    private const float MaximumTextureSize = 64.0f;

    public readonly FilterComboSlices TileCombo;
    public readonly FilterComboSlices SphereMapCombo;

    public readonly IEditor<byte> TileIndexPicker;
    public readonly IEditor<byte> SphereMapIndexPicker;

    public MaterialTemplatePickers(TextureArraySlicer textureArraySlicer, CharacterUtility characterUtility)
    {
        TileCombo      = CreateCombo(new TileList(characterUtility), textureArraySlicer);
        SphereMapCombo = CreateCombo(new SphereMapList(characterUtility), textureArraySlicer);

        TileIndexPicker      = new Editor(TileCombo).AsByteEditor();
        SphereMapIndexPicker = new Editor(SphereMapCombo).AsByteEditor();
    }

    private static FilterComboSlices CreateCombo(ArraySliceList list, TextureArraySlicer textureArraySlicer)
        => new(list, textureArraySlicer, list.GetTextures);

    public sealed class FilterComboSlices(IReadOnlyList<int> list, TextureArraySlicer textureArraySlicer,
        Func<ReadOnlyMemory<Pointer<TextureResourceHandle>>> getTextures) : FilterComboBase<int>(list, false, Penumbra.Log)
    {
        public bool Compact;

        private ReadOnlyMemory<Pointer<TextureResourceHandle>> _textures;
        private TextureResourceHandle*                         _firstNonNullTexture;

        private Vector2 _textureSize;
        private Vector2 _framePadding;
        private Vector2 _itemSpacing;
        private int     _spaces;
        private float   _itemHeight;
        private bool    _mustPopFont;
        private bool    _mustPopPadding;

        public bool Draw(string label, string tooltip, ref int currentSelection)
            => Draw(label, currentSelection < 0 ? "-" : currentSelection.ToString(), tooltip, ref currentSelection, float.NaN, float.NaN, ImGuiComboFlags.NoArrowButton);

        public override bool Draw(string label, string preview, string tooltip, ref int currentSelection, float previewWidth, float itemHeight, ImGuiComboFlags flags = ImGuiComboFlags.None)
        {
            if (float.IsNaN(previewWidth))
                previewWidth = ImGui.CalcItemWidth();

            _framePadding = ImGui.GetStyle().FramePadding;
            _itemSpacing  = ImGui.GetStyle().ItemSpacing;

            _textures            = getTextures();
            _firstNonNullTexture = ArraySliceList.GetFirstNonNullTexture(_textures.Span);

            _textureSize = _firstNonNullTexture switch
            {
                null => Vector2.Zero,
                _    => new Vector2(_firstNonNullTexture->CsHandle.Texture->ActualWidth, _firstNonNullTexture->CsHandle.Texture->ActualHeight).Contain(new Vector2(MaximumTextureSize)),
            };

            if (float.IsNaN(itemHeight))
                itemHeight = Math.Max(ImGui.GetTextLineHeightWithSpacing(), _framePadding.Y * 2.0f + _textureSize.Y);

            return base.Draw(label, preview, tooltip, ref currentSelection, previewWidth, itemHeight, flags);
        }

        protected override void DrawCombo(string label, string preview, string tooltip, int currentSelected, float previewWidth, float itemHeight, ImGuiComboFlags flags)
        {
            ImGui.PushFont(UiBuilder.MonoFont);
            _mustPopFont = true;
            try
            {
                var spaceSize = ImUtf8.CalcTextSize(" "u8).X;
                _spaces       = (int)((ImGui.CalcItemWidth() - _framePadding.X * 2.0f - (Compact ? 0.0f : (_textureSize.X + _itemSpacing.X) * _textures.Length)) / spaceSize);

                if (Compact)
                    _mustPopPadding = false;
                else
                {
                    ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, _framePadding + new Vector2(0.0f, Math.Max(_textureSize.Y - ImGui.GetFrameHeight() + _itemSpacing.Y, 0.0f) * 0.5f));
                    _mustPopPadding = true;
                }

                try
                {
                    base.DrawCombo(label, preview.PadLeft(_spaces), tooltip, currentSelected, previewWidth, itemHeight, flags);
                }
                finally
                {
                    PopPadding();
                }
            }
            finally
            {
                PopFont();
            }

            currentSelected = NewSelection ?? currentSelected;
            if (!Compact && currentSelected >= 0)
            {
                var cbRectMin            = ImGui.GetItemRectMin();
                var cbRectMax            = ImGui.GetItemRectMax();
                var cbTextureRegionStart = new Vector2(cbRectMax.X - _framePadding.X - _textureSize.X * _textures.Length - _itemSpacing.X * (_textures.Length - 1), cbRectMin.Y + _framePadding.Y);
                var cbMaxSize            = new Vector2(_textureSize.X, cbRectMax.Y - _framePadding.Y - cbTextureRegionStart.Y);
                DrawTextureSlices(cbTextureRegionStart, cbMaxSize, _itemSpacing.X, _textures.Span, (byte)currentSelected);
            }
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled) && Compact && currentSelected >= 0)
            {
                using var disabled = ImRaii.Enabled();
                using var tt       = ImUtf8.Tooltip();
                ImGui.Dummy(new Vector2(_textureSize.X * _textures.Length + _itemSpacing.X * (_textures.Length - 1), _textureSize.Y));
                var rectMin = ImGui.GetItemRectMin();
                var rectMax = ImGui.GetItemRectMax();
                DrawTextureSlices(rectMin, _textureSize, _itemSpacing.X, _textures.Span, (byte)currentSelected);
            }
        }

        protected override void PostCombo(float previewWidth)
        {
            PopPadding();
            PopFont();
        }

        protected override float GetFilterWidth()
            => MathF.Max(base.GetFilterWidth(), ImUtf8.CalcTextSize("MMM"u8).X + (_itemSpacing.X + _textureSize.X) * _textures.Length + _framePadding.X * 2.0f + ImGui.GetStyle().ScrollbarSize);

        protected override void DrawList(float width, float itemHeight)
        {
            using var font   = ImRaii.PushFont(UiBuilder.MonoFont);
            using var center = ImRaii.PushStyle(ImGuiStyleVar.SelectableTextAlign, new Vector2(0, 0.5f));

            _itemHeight = itemHeight;

            base.DrawList(width, itemHeight);
        }

        protected override bool DrawSelectable(int globalIdx, bool selected)
        {
            var ret     = ImUtf8.Selectable($"{globalIdx,3}", selected, size: new Vector2(ImGui.GetWindowWidth() - ImGui.GetStyle().ScrollbarSize - _framePadding.X, _itemHeight));
            var rectMin = ImGui.GetItemRectMin();
            var rectMax = ImGui.GetItemRectMax();
            var textureRegionStart = new Vector2(
                rectMax.X - _framePadding.X - _textureSize.X * _textures.Length - _itemSpacing.X * (_textures.Length - 1),
                rectMin.Y + _framePadding.Y);
            var maxSize = new Vector2(_textureSize.X, rectMax.Y - _framePadding.Y - textureRegionStart.Y);
            DrawTextureSlices(textureRegionStart, maxSize, _itemSpacing.X, _textures.Span, (byte)globalIdx);
            return ret;
        }

        private void PopPadding()
        {
            if (!_mustPopPadding)
                return;

            ImGui.PopStyleVar();
            _mustPopPadding = false;
        }

        private void PopFont()
        {
            if (!_mustPopFont)
                return;

            ImGui.PopFont();
            _mustPopFont = false;
        }

        private void DrawTextureSlices(Vector2 regionStart, Vector2 itemSize, float itemSpacing,
            ReadOnlySpan<Pointer<TextureResourceHandle>> textures, byte sliceIndex)
        {
            for (var j = 0; j < textures.Length; ++j)
            {
                if (textures[j].Value == null)
                    continue;
                var texture = textures[j].Value->CsHandle.Texture;
                if (texture == null)
                    continue;
                var handle = textureArraySlicer.GetImGuiHandle(texture, sliceIndex);
                if (handle == 0)
                    continue;

                var position = regionStart with { X = regionStart.X + (itemSize.X + itemSpacing) * j };
                var size = new Vector2(texture->ActualWidth, texture->ActualHeight).Contain(itemSize);
                position += (itemSize - size) * 0.5f;
                ImGui.GetWindowDrawList().AddImage(handle, position, position + size, Vector2.Zero,
                    new Vector2(texture->ActualWidth / (float)texture->AllocatedWidth, texture->ActualHeight / (float)texture->AllocatedHeight));
            }
        }
    }

    private abstract class ArraySliceList(int textureCapacity) : IReadOnlyList<int>
    {
        private readonly Pointer<TextureResourceHandle>[] _textures = new Pointer<TextureResourceHandle>[textureCapacity];

        public int Count
            => GetFirstNonNullTexture(GetTextures().Span) switch
            {
                null        => 0,
                var texture => texture->CsHandle.Texture->ArraySize,
            };

        public int this[int index]
            => index;

        public IEnumerator<int> GetEnumerator()
            => Enumerable.Range(0, Count).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => Enumerable.Range(0, Count).GetEnumerator();

        protected abstract int GetTextures([Out] Span<Pointer<TextureResourceHandle>> textures);

        public ReadOnlyMemory<Pointer<TextureResourceHandle>> GetTextures()
        {
            var textureCount = GetTextures(_textures);
            return _textures.AsMemory(0, textureCount);
        }

        public static TextureResourceHandle* GetFirstNonNullTexture(ReadOnlySpan<Pointer<TextureResourceHandle>> textures)
        {
            foreach (var texture in textures)
            {
                if (texture.Value != null && texture.Value->CsHandle.Texture != null)
                    return texture;
            }

            return null;
        }
    }

    private sealed class TileList(CharacterUtility characterUtility) : ArraySliceList(2)
    {
        protected override int GetTextures([Out] Span<Pointer<TextureResourceHandle>> textures)
        {
            var characterUtilityData = characterUtility.Address;
            if (characterUtilityData == null)
                return 0;

            textures[0] = characterUtilityData->TileOrbArrayTexResource;
            textures[1] = characterUtilityData->TileNormArrayTexResource;
            return 2;
        }
    }

    private sealed class SphereMapList(CharacterUtility characterUtility) : ArraySliceList(1)
    {
        protected override int GetTextures([Out] Span<Pointer<TextureResourceHandle>> textures)
        {
            var characterUtilityData = characterUtility.Address;
            if (characterUtilityData == null)
                return 0;

            textures[0] = characterUtilityData->SphereDArrayTexResource;
            return 1;
        }
    }

    private sealed class Editor(FilterComboSlices combo) : IEditor<float>
    {
        public bool Draw(Span<float> values, bool disabled)
        {
            var helper = Editors.PrepareMultiComponent(values.Length);
            var ret = false;

            combo.Compact = true;
            for (var valueIdx = 0; valueIdx < values.Length; ++valueIdx)
            {
                helper.SetupComponent(valueIdx);

                var value = int.CreateSaturating(MathF.Round(values[valueIdx]));
                if (disabled)
                {
                    using var _ = ImRaii.Disabled();
                    combo.Draw($"###{valueIdx}", string.Empty, ref value);
                }
                else
                {
                    if (combo.Draw($"###{valueIdx}", string.Empty, ref value))
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

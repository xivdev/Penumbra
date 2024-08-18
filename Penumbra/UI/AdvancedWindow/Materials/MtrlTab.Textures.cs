using Dalamud.Interface;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Text;
using Penumbra.GameData;
using Penumbra.GameData.Files.MaterialStructs;
using Penumbra.String.Classes;
using static Penumbra.GameData.Files.MaterialStructs.SamplerFlags;
using static Penumbra.GameData.Files.ShpkFile;

namespace Penumbra.UI.AdvancedWindow.Materials;

public partial class MtrlTab
{
    public readonly List<(string Label, int TextureIndex, int SamplerIndex, string Description, bool MonoFont)> Textures = new(4);

    public readonly HashSet<int>  UnfoldedTextures = new(4);
    public readonly HashSet<uint> SamplerIds       = new(16);
    public          float         TextureLabelWidth;

    private void UpdateTextures()
    {
        Textures.Clear();
        SamplerIds.Clear();
        if (_associatedShpk == null)
        {
            SamplerIds.UnionWith(Mtrl.ShaderPackage.Samplers.Select(sampler => sampler.SamplerId));
            if (Mtrl.Table != null)
                SamplerIds.Add(TableSamplerId);

            foreach (var (sampler, index) in Mtrl.ShaderPackage.Samplers.WithIndex())
                Textures.Add(($"0x{sampler.SamplerId:X8}", sampler.TextureIndex, index, string.Empty, true));
        }
        else
        {
            foreach (var index in _vertexShaders)
                SamplerIds.UnionWith(_associatedShpk.VertexShaders[index].Samplers.Select(sampler => sampler.Id));
            foreach (var index in _pixelShaders)
                SamplerIds.UnionWith(_associatedShpk.PixelShaders[index].Samplers.Select(sampler => sampler.Id));
            if (!_shadersKnown)
            {
                SamplerIds.UnionWith(Mtrl.ShaderPackage.Samplers.Select(sampler => sampler.SamplerId));
                if (Mtrl.Table != null)
                    SamplerIds.Add(TableSamplerId);
            }

            foreach (var samplerId in SamplerIds)
            {
                var shpkSampler = _associatedShpk.GetSamplerById(samplerId);
                if (shpkSampler is not { Slot: 2 })
                    continue;

                var dkData     = TryGetShpkDevkitData<DevkitSampler>("Samplers", samplerId, true);
                var hasDkLabel = !string.IsNullOrEmpty(dkData?.Label);

                var sampler = Mtrl.GetOrAddSampler(samplerId, dkData?.DefaultTexture ?? string.Empty, out var samplerIndex);
                Textures.Add((hasDkLabel ? dkData!.Label : shpkSampler.Value.Name, sampler.TextureIndex, samplerIndex,
                    dkData?.Description ?? string.Empty, !hasDkLabel));
            }

            if (SamplerIds.Contains(TableSamplerId))
                Mtrl.Table ??= new ColorTable();
        }

        Textures.Sort((x, y) => string.CompareOrdinal(x.Label, y.Label));

        TextureLabelWidth = 50f * UiHelpers.Scale;

        float helpWidth;
        using (var _ = ImRaii.PushFont(UiBuilder.IconFont))
        {
            helpWidth = ImGui.GetStyle().ItemSpacing.X + ImGui.CalcTextSize(FontAwesomeIcon.InfoCircle.ToIconString()).X;
        }

        foreach (var (label, _, _, description, monoFont) in Textures)
        {
            if (!monoFont)
                TextureLabelWidth = Math.Max(TextureLabelWidth, ImGui.CalcTextSize(label).X + (description.Length > 0 ? helpWidth : 0.0f));
        }

        using (var _ = ImRaii.PushFont(UiBuilder.MonoFont))
        {
            foreach (var (label, _, _, description, monoFont) in Textures)
            {
                if (monoFont)
                    TextureLabelWidth = Math.Max(TextureLabelWidth,
                        ImGui.CalcTextSize(label).X + (description.Length > 0 ? helpWidth : 0.0f));
            }
        }

        TextureLabelWidth = TextureLabelWidth / UiHelpers.Scale + 4;
    }

    private static ReadOnlySpan<byte> TextureAddressModeTooltip(TextureAddressMode addressMode)
        => addressMode switch
        {
            TextureAddressMode.Wrap =>
                "Tile the texture at every UV integer junction.\n\nFor example, for U values between 0 and 3, the texture is repeated three times."u8,
            TextureAddressMode.Mirror =>
                "Flip the texture at every UV integer junction.\n\nFor U values between 0 and 1, for example, the texture is addressed normally; between 1 and 2, the texture is mirrored; between 2 and 3, the texture is normal again; and so on."u8,
            TextureAddressMode.Clamp =>
                "Texture coordinates outside the range [0.0, 1.0] are set to the texture color at 0.0 or 1.0, respectively."u8,
            TextureAddressMode.Border => "Texture coordinates outside the range [0.0, 1.0] are set to the border color (generally black)."u8,
            _                         => ""u8,
        };

    private bool DrawTextureSection(bool disabled)
    {
        if (Textures.Count == 0)
            return false;

        ImGui.Dummy(new Vector2(ImGui.GetTextLineHeight() / 2));
        if (!ImGui.CollapsingHeader("Textures and Samplers", ImGuiTreeNodeFlags.DefaultOpen))
            return false;

        var       frameHeight = ImGui.GetFrameHeight();
        var       ret         = false;
        using var table       = ImRaii.Table("##Textures", 3);

        ImGui.TableSetupColumn(string.Empty, ImGuiTableColumnFlags.WidthFixed, frameHeight);
        ImGui.TableSetupColumn("Path",       ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Name",       ImGuiTableColumnFlags.WidthFixed, TextureLabelWidth * UiHelpers.Scale);
        foreach (var (label, textureI, samplerI, description, monoFont) in Textures)
        {
            using var _        = ImRaii.PushId(samplerI);
            var       tmp      = Mtrl.Textures[textureI].Path;
            var       unfolded = UnfoldedTextures.Contains(samplerI);
            ImGui.TableNextColumn();
            if (ImGuiUtil.DrawDisabledButton((unfolded ? FontAwesomeIcon.CaretDown : FontAwesomeIcon.CaretRight).ToIconString(),
                    new Vector2(frameHeight),
                    "Settings for this texture and the associated sampler", false, true))
            {
                unfolded = !unfolded;
                if (unfolded)
                    UnfoldedTextures.Add(samplerI);
                else
                    UnfoldedTextures.Remove(samplerI);
            }

            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            if (ImGui.InputText(string.Empty, ref tmp, Utf8GamePath.MaxGamePathLength,
                    disabled ? ImGuiInputTextFlags.ReadOnly : ImGuiInputTextFlags.None)
             && tmp.Length > 0
             && tmp != Mtrl.Textures[textureI].Path)
            {
                ret                          = true;
                Mtrl.Textures[textureI].Path = tmp;
            }

            ImGui.TableNextColumn();
            using (var font = ImRaii.PushFont(UiBuilder.MonoFont, monoFont))
            {
                ImGui.AlignTextToFramePadding();
                if (description.Length > 0)
                    ImGuiUtil.LabeledHelpMarker(label, description);
                else
                    ImGui.TextUnformatted(label);
            }

            if (unfolded)
            {
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                ret |= DrawMaterialSampler(disabled, textureI, samplerI);
                ImGui.TableNextColumn();
            }
        }

        return ret;
    }

    private static bool ComboTextureAddressMode(ReadOnlySpan<byte> label, ref TextureAddressMode value)
    {
        using var c = ImUtf8.Combo(label, value.ToString());
        if (!c)
            return false;

        var ret = false;
        foreach (var mode in Enum.GetValues<TextureAddressMode>())
        {
            if (ImGui.Selectable(mode.ToString(), mode == value))
            {
                value = mode;
                ret   = true;
            }

            ImUtf8.SelectableHelpMarker(TextureAddressModeTooltip(mode));
        }

        return ret;
    }

    private bool DrawMaterialSampler(bool disabled, int textureIdx, int samplerIdx)
    {
        var     ret     = false;
        ref var texture = ref Mtrl.Textures[textureIdx];
        ref var sampler = ref Mtrl.ShaderPackage.Samplers[samplerIdx];

        var dx11 = texture.DX11;
        if (ImUtf8.Checkbox("Prepend -- to the file name on DirectX 11"u8, ref dx11))
        {
            texture.DX11 = dx11;
            ret          = true;
        }

        ref var samplerFlags = ref Wrap(ref sampler.Flags);

        ImGui.SetNextItemWidth(UiHelpers.Scale * 100.0f);
        var addressMode = samplerFlags.UAddressMode;
        if (ComboTextureAddressMode("##UAddressMode"u8, ref addressMode))
        {
            samplerFlags.UAddressMode = addressMode;
            ret                       = true;
            SetSamplerFlags(sampler.SamplerId, sampler.Flags);
        }

        ImGui.SameLine();
        ImUtf8.LabeledHelpMarker("U Address Mode"u8, "Method to use for resolving a U texture coordinate that is outside the 0 to 1 range.");

        ImGui.SetNextItemWidth(UiHelpers.Scale * 100.0f);
        addressMode = samplerFlags.VAddressMode;
        if (ComboTextureAddressMode("##VAddressMode"u8, ref addressMode))
        {
            samplerFlags.VAddressMode = addressMode;
            ret                       = true;
            SetSamplerFlags(sampler.SamplerId, sampler.Flags);
        }

        ImGui.SameLine();
        ImUtf8.LabeledHelpMarker("V Address Mode"u8, "Method to use for resolving a V texture coordinate that is outside the 0 to 1 range.");

        var lodBias = samplerFlags.LodBias;
        ImGui.SetNextItemWidth(UiHelpers.Scale * 100.0f);
        if (ImUtf8.DragScalar("##LoDBias"u8, ref lodBias, -8.0f, 7.984375f, 0.1f))
        {
            samplerFlags.LodBias = lodBias;
            ret                  = true;
            SetSamplerFlags(sampler.SamplerId, sampler.Flags);
        }

        ImGui.SameLine();
        ImUtf8.LabeledHelpMarker("Level of Detail Bias"u8,
            "Offset from the calculated mipmap level.\n\nHigher means that the texture will start to lose detail nearer.\nLower means that the texture will keep its detail until farther.");

        var minLod = samplerFlags.MinLod;
        ImGui.SetNextItemWidth(UiHelpers.Scale * 100.0f);
        if (ImUtf8.DragScalar("##MinLoD"u8, ref minLod, 0, 15, 0.1f))
        {
            samplerFlags.MinLod = minLod;
            ret                 = true;
            SetSamplerFlags(sampler.SamplerId, sampler.Flags);
        }

        ImGui.SameLine();
        ImUtf8.LabeledHelpMarker("Minimum Level of Detail"u8,
            "Most detailed mipmap level to use.\n\n0 is the full-sized texture, 1 is the half-sized texture, 2 is the quarter-sized texture, and so on.\n15 will forcibly reduce the texture to its smallest mipmap.");

        using var t = ImUtf8.TreeNode("Advanced Settings"u8);
        if (!t)
            return ret;

        ImGui.SetNextItemWidth(UiHelpers.Scale * 100.0f);
        if (ImUtf8.InputScalar("Texture Flags"u8, ref texture.Flags, "%04X"u8,
                flags: disabled ? ImGuiInputTextFlags.ReadOnly : ImGuiInputTextFlags.None))
            ret = true;

        ImGui.SetNextItemWidth(UiHelpers.Scale * 100.0f);
        if (ImUtf8.InputScalar("Sampler Flags"u8, ref sampler.Flags, "%08X"u8,
                flags: ImGuiInputTextFlags.CharsHexadecimal | (disabled ? ImGuiInputTextFlags.ReadOnly : ImGuiInputTextFlags.None)))
        {
            ret = true;
            SetSamplerFlags(sampler.SamplerId, sampler.Flags);
        }

        return ret;
    }
}

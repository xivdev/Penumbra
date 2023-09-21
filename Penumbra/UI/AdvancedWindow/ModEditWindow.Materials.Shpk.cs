using Dalamud.Interface;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using Penumbra.GameData;
using Penumbra.String.Classes;

namespace Penumbra.UI.AdvancedWindow;

public partial class ModEditWindow
{
    private readonly FileDialogService _fileDialog;

    // strings path/to/the.exe | grep --fixed-strings '.shpk' | sort -u | sed -e 's#^shader/sm5/shpk/##'
    // Apricot shader packages are unlisted because
    // 1. they cause performance/memory issues when calculating the effective shader set
    // 2. they probably aren't intended for use with materials anyway
    private static readonly IReadOnlyList<string> StandardShaderPackages = new[]
    {
        "3dui.shpk",
        // "apricot_decal_dummy.shpk",
        // "apricot_decal_ring.shpk",
        // "apricot_decal.shpk",
        // "apricot_lightmodel.shpk",
        // "apricot_model_dummy.shpk",
        // "apricot_model_morph.shpk",
        // "apricot_model.shpk",
        // "apricot_powder_dummy.shpk",
        // "apricot_powder.shpk",
        // "apricot_shape_dummy.shpk",
        // "apricot_shape.shpk",
        "bgcolorchange.shpk",
        "bgcrestchange.shpk",
        "bgdecal.shpk",
        "bg.shpk",
        "bguvscroll.shpk",
        "channeling.shpk",
        "characterglass.shpk",
        "character.shpk",
        "cloud.shpk",
        "createviewposition.shpk",
        "crystal.shpk",
        "directionallighting.shpk",
        "directionalshadow.shpk",
        "grass.shpk",
        "hair.shpk",
        "iris.shpk",
        "lightshaft.shpk",
        "linelighting.shpk",
        "planelighting.shpk",
        "pointlighting.shpk",
        "river.shpk",
        "shadowmask.shpk",
        "skin.shpk",
        "spotlighting.shpk",
        "verticalfog.shpk",
        "water.shpk",
        "weather.shpk",
    };

    private enum TextureAddressMode : uint
    {
        Wrap   = 0,
        Mirror = 1,
        Clamp  = 2,
        Border = 3,
    }

    private static readonly IReadOnlyList<string> TextureAddressModeTooltips = new[]
    {
        "Tile the texture at every UV integer junction.\n\nFor example, for U values between 0 and 3, the texture is repeated three times.",
        "Flip the texture at every UV integer junction.\n\nFor U values between 0 and 1, for example, the texture is addressed normally; between 1 and 2, the texture is mirrored; between 2 and 3, the texture is normal again; and so on.",
        "Texture coordinates outside the range [0.0, 1.0] are set to the texture color at 0.0 or 1.0, respectively.",
        "Texture coordinates outside the range [0.0, 1.0] are set to the border color (generally black).",
    };

    private static bool DrawPackageNameInput(MtrlTab tab, bool disabled)
    {
        if (disabled)
        {
            ImGui.TextUnformatted("Shader Package: " + tab.Mtrl.ShaderPackage.Name);
            return false;
        }

        var ret = false;
        ImGui.SetNextItemWidth(UiHelpers.Scale * 250.0f);
        using var c = ImRaii.Combo("Shader Package", tab.Mtrl.ShaderPackage.Name);
        if (c)
            foreach (var value in tab.GetShpkNames())
            {
                if (ImGui.Selectable(value, value == tab.Mtrl.ShaderPackage.Name))
                {
                    tab.Mtrl.ShaderPackage.Name = value;
                    ret                         = true;
                    tab.AssociatedShpk          = null;
                    tab.LoadedShpkPath          = FullPath.Empty;
                    tab.LoadShpk(tab.FindAssociatedShpk(out _, out _));
                }
            }

        return ret;
    }

    private static bool DrawShaderFlagsInput(MtrlTab tab, bool disabled)
    {
        var shpkFlags = (int)tab.Mtrl.ShaderPackage.Flags;
        ImGui.SetNextItemWidth(UiHelpers.Scale * 250.0f);
        if (!ImGui.InputInt("Shader Flags", ref shpkFlags, 0, 0,
                ImGuiInputTextFlags.CharsHexadecimal | (disabled ? ImGuiInputTextFlags.ReadOnly : ImGuiInputTextFlags.None)))
            return false;

        tab.Mtrl.ShaderPackage.Flags = (uint)shpkFlags;
        tab.SetShaderPackageFlags((uint)shpkFlags);
        return true;
    }

    /// <summary>
    /// Show the currently associated shpk file, if any, and the buttons to associate
    /// a specific shpk from your drive, the modded shpk by path or the default shpk.
    /// </summary>
    private void DrawCustomAssociations(MtrlTab tab)
    {
        const string tooltip = "Click to copy file path to clipboard.";
        var text = tab.AssociatedShpk == null
            ? "Associated .shpk file: None"
            : $"Associated .shpk file: {tab.LoadedShpkPathName}";
        var devkitText = tab.AssociatedShpkDevkit == null
            ? "Associated dev-kit file: None"
            : $"Associated dev-kit file: {tab.LoadedShpkDevkitPathName}";
        var baseDevkitText = tab.AssociatedBaseDevkit == null
            ? "Base dev-kit file: None"
            : $"Base dev-kit file: {tab.LoadedBaseDevkitPathName}";

        ImGui.Dummy(new Vector2(ImGui.GetTextLineHeight() / 2));

        ImGuiUtil.CopyOnClickSelectable(text,           tab.LoadedShpkPathName,       tooltip);
        ImGuiUtil.CopyOnClickSelectable(devkitText,     tab.LoadedShpkDevkitPathName, tooltip);
        ImGuiUtil.CopyOnClickSelectable(baseDevkitText, tab.LoadedBaseDevkitPathName, tooltip);

        if (ImGui.Button("Associate Custom .shpk File"))
            _fileDialog.OpenFilePicker("Associate Custom .shpk File...", ".shpk", (success, name) =>
            {
                if (success)
                    tab.LoadShpk(new FullPath(name[0]));
            }, 1, _mod!.ModPath.FullName, false);

        var moddedPath = tab.FindAssociatedShpk(out var defaultPath, out var gamePath);
        ImGui.SameLine();
        if (ImGuiUtil.DrawDisabledButton("Associate Default .shpk File", Vector2.Zero, moddedPath.ToPath(),
                moddedPath.Equals(tab.LoadedShpkPath)))
            tab.LoadShpk(moddedPath);

        if (!gamePath.Path.Equals(moddedPath.InternalName))
        {
            ImGui.SameLine();
            if (ImGuiUtil.DrawDisabledButton("Associate Unmodded .shpk File", Vector2.Zero, defaultPath,
                    gamePath.Path.Equals(tab.LoadedShpkPath.InternalName)))
                tab.LoadShpk(new FullPath(gamePath));
        }

        ImGui.Dummy(new Vector2(ImGui.GetTextLineHeight() / 2));
    }

    private static bool DrawMaterialShaderKeys(MtrlTab tab, bool disabled)
    {
        if (tab.ShaderKeys.Count == 0)
            return false;

        var ret = false;
        foreach (var (label, index, description, monoFont, values) in tab.ShaderKeys)
        {
            using var font         = ImRaii.PushFont(UiBuilder.MonoFont, monoFont);
            ref var   key          = ref tab.Mtrl.ShaderPackage.ShaderKeys[index];
            var       shpkKey      = tab.AssociatedShpk?.GetMaterialKeyById(key.Category);
            var       currentValue = key.Value;
            var (currentLabel, _, currentDescription) =
                values.FirstOrNull(v => v.Value == currentValue) ?? ($"0x{currentValue:X8}", currentValue, string.Empty);
            if (!disabled && shpkKey.HasValue)
            {
                ImGui.SetNextItemWidth(UiHelpers.Scale * 250.0f);
                using (var c = ImRaii.Combo($"##{key.Category:X8}", currentLabel))
                {
                    if (c)
                        foreach (var (valueLabel, value, valueDescription) in values)
                        {
                            if (ImGui.Selectable(valueLabel, value == currentValue))
                            {
                                key.Value = value;
                                ret       = true;
                                tab.Update();
                            }

                            if (valueDescription.Length > 0)
                                ImGuiUtil.SelectableHelpMarker(valueDescription);
                        }
                }

                ImGui.SameLine();
                if (description.Length > 0)
                    ImGuiUtil.LabeledHelpMarker(label, description);
                else
                    ImGui.TextUnformatted(label);
            }
            else if (description.Length > 0 || currentDescription.Length > 0)
            {
                ImGuiUtil.LabeledHelpMarker($"{label}: {currentLabel}",
                    description + (description.Length > 0 && currentDescription.Length > 0 ? "\n\n" : string.Empty) + currentDescription);
            }
            else
            {
                ImGui.TextUnformatted($"{label}: {currentLabel}");
            }
        }

        return ret;
    }

    private static void DrawMaterialShaders(MtrlTab tab)
    {
        if (tab.AssociatedShpk == null)
            return;

        ImRaii.TreeNode(tab.VertexShadersString, ImGuiTreeNodeFlags.Leaf).Dispose();
        ImRaii.TreeNode(tab.PixelShadersString,  ImGuiTreeNodeFlags.Leaf).Dispose();

        if (tab.ShaderComment.Length > 0)
        {
            ImGui.Dummy(new Vector2(ImGui.GetTextLineHeight() / 2));
            ImGui.TextUnformatted(tab.ShaderComment);
        }
    }

    private static bool DrawMaterialConstants(MtrlTab tab, bool disabled)
    {
        if (tab.Constants.Count == 0)
            return false;

        ImGui.Dummy(new Vector2(ImGui.GetTextLineHeight() / 2));
        if (!ImGui.CollapsingHeader("Material Constants"))
            return false;

        using var _ = ImRaii.PushId("MaterialConstants");

        var ret = false;
        foreach (var (header, group) in tab.Constants)
        {
            using var t = ImRaii.TreeNode(header, ImGuiTreeNodeFlags.DefaultOpen);
            if (!t)
                continue;

            foreach (var (label, constantIndex, slice, description, monoFont, editor) in group)
            {
                var constant = tab.Mtrl.ShaderPackage.Constants[constantIndex];
                var buffer   = tab.Mtrl.GetConstantValues(constant);
                if (buffer.Length > 0)
                {
                    using var id = ImRaii.PushId($"##{constant.Id:X8}:{slice.Start}");
                    ImGui.SetNextItemWidth(250.0f);
                    if (editor.Draw(buffer[slice], disabled))
                    {
                        ret = true;
                        tab.SetMaterialParameter(constant.Id, slice.Start, buffer[slice]);
                    }

                    ImGui.SameLine();
                    using var font = ImRaii.PushFont(UiBuilder.MonoFont, monoFont);
                    if (description.Length > 0)
                        ImGuiUtil.LabeledHelpMarker(label, description);
                    else
                        ImGui.TextUnformatted(label);
                }
            }
        }

        return ret;
    }

    private static bool DrawMaterialSampler(MtrlTab tab, bool disabled, int textureIdx, int samplerIdx)
    {
        var     ret     = false;
        ref var texture = ref tab.Mtrl.Textures[textureIdx];
        ref var sampler = ref tab.Mtrl.ShaderPackage.Samplers[samplerIdx];

        // FIXME this probably doesn't belong here
        static unsafe bool InputHexUInt16(string label, ref ushort v, ImGuiInputTextFlags flags)
        {
            fixed (ushort* v2 = &v)
            {
                return ImGui.InputScalar(label, ImGuiDataType.U16, (nint)v2, IntPtr.Zero, IntPtr.Zero, "%04X", flags);
            }
        }

        static bool ComboTextureAddressMode(string label, ref uint samplerFlags, int bitOffset)
        {
            var       current = (TextureAddressMode)((samplerFlags >> bitOffset) & 0x3u);
            using var c       = ImRaii.Combo(label, current.ToString());
            if (!c)
                return false;

            var ret = false;
            foreach (var value in Enum.GetValues<TextureAddressMode>())
            {
                if (ImGui.Selectable(value.ToString(), value == current))
                {
                    samplerFlags = (samplerFlags & ~(0x3u << bitOffset)) | ((uint)value << bitOffset);
                    ret          = true;
                }

                ImGuiUtil.SelectableHelpMarker(TextureAddressModeTooltips[(int)value]);
            }

            return ret;
        }

        var dx11 = texture.DX11;
        if (ImGui.Checkbox("Prepend -- to the file name on DirectX 11", ref dx11))
        {
            texture.DX11 = dx11;
            ret          = true;
        }

        ImGui.SetNextItemWidth(UiHelpers.Scale * 100.0f);
        if (ComboTextureAddressMode("##UAddressMode", ref sampler.Flags, 2))
        {
            ret = true;
            tab.SetSamplerFlags(sampler.SamplerId, sampler.Flags);
        }

        ImGui.SameLine();
        ImGuiUtil.LabeledHelpMarker("U Address Mode", "Method to use for resolving a U texture coordinate that is outside the 0 to 1 range.");

        ImGui.SetNextItemWidth(UiHelpers.Scale * 100.0f);
        if (ComboTextureAddressMode("##VAddressMode", ref sampler.Flags, 0))
        {
            ret = true;
            tab.SetSamplerFlags(sampler.SamplerId, sampler.Flags);
        }

        ImGui.SameLine();
        ImGuiUtil.LabeledHelpMarker("V Address Mode", "Method to use for resolving a V texture coordinate that is outside the 0 to 1 range.");

        var lodBias = ((int)(sampler.Flags << 12) >> 22) / 64.0f;
        ImGui.SetNextItemWidth(UiHelpers.Scale * 100.0f);
        if (ImGui.DragFloat("##LoDBias", ref lodBias, 0.1f, -8.0f, 7.984375f))
        {
            sampler.Flags = (uint)((sampler.Flags & ~0x000FFC00)
              | ((uint)((int)Math.Round(Math.Clamp(lodBias, -8.0f, 7.984375f) * 64.0f) & 0x3FF) << 10));
            ret = true;
            tab.SetSamplerFlags(sampler.SamplerId, sampler.Flags);
        }

        ImGui.SameLine();
        ImGuiUtil.LabeledHelpMarker("Level of Detail Bias",
            "Offset from the calculated mipmap level.\n\nHigher means that the texture will start to lose detail nearer.\nLower means that the texture will keep its detail until farther.");

        var minLod = (int)((sampler.Flags >> 20) & 0xF);
        ImGui.SetNextItemWidth(UiHelpers.Scale * 100.0f);
        if (ImGui.DragInt("##MinLoD", ref minLod, 0.1f, 0, 15))
        {
            sampler.Flags = (uint)((sampler.Flags & ~0x00F00000) | ((uint)Math.Clamp(minLod, 0, 15) << 20));
            ret           = true;
            tab.SetSamplerFlags(sampler.SamplerId, sampler.Flags);
        }

        ImGui.SameLine();
        ImGuiUtil.LabeledHelpMarker("Minimum Level of Detail",
            "Most detailed mipmap level to use.\n\n0 is the full-sized texture, 1 is the half-sized texture, 2 is the quarter-sized texture, and so on.\n15 will forcibly reduce the texture to its smallest mipmap.");

        using var t = ImRaii.TreeNode("Advanced Settings");
        if (!t)
            return ret;

        ImGui.SetNextItemWidth(UiHelpers.Scale * 100.0f);
        if (InputHexUInt16("Texture Flags", ref texture.Flags,
                disabled ? ImGuiInputTextFlags.ReadOnly : ImGuiInputTextFlags.None))
            ret = true;

        var samplerFlags = (int)sampler.Flags;
        ImGui.SetNextItemWidth(UiHelpers.Scale * 100.0f);
        if (ImGui.InputInt("Sampler Flags", ref samplerFlags, 0, 0,
                ImGuiInputTextFlags.CharsHexadecimal | (disabled ? ImGuiInputTextFlags.ReadOnly : ImGuiInputTextFlags.None)))
        {
            sampler.Flags = (uint)samplerFlags;
            ret           = true;
            tab.SetSamplerFlags(sampler.SamplerId, (uint)samplerFlags);
        }

        return ret;
    }

    private bool DrawMaterialShader(MtrlTab tab, bool disabled)
    {
        var ret = false;
        if (ImGui.CollapsingHeader(tab.ShaderHeader))
        {
            ret |= DrawPackageNameInput(tab, disabled);
            ret |= DrawShaderFlagsInput(tab, disabled);
            DrawCustomAssociations(tab);
            ret |= DrawMaterialShaderKeys(tab, disabled);
            DrawMaterialShaders(tab);
        }

        if (tab.AssociatedShpkDevkit == null)
        {
            ImGui.Dummy(new Vector2(ImGui.GetTextLineHeight() / 2));
            GC.KeepAlive(tab);

            var textColor = ImGui.GetColorU32(ImGuiCol.Text);
            var textColorWarning =
                (textColor & 0xFF000000u)
              | ((textColor & 0x00FEFEFE) >> 1)
              | (tab.AssociatedShpk == null ? 0x80u : 0x8080u); // Half red or yellow

            using var c = ImRaii.PushColor(ImGuiCol.Text, textColorWarning);

            ImGui.TextUnformatted(tab.AssociatedShpk == null
                ? "Unable to find a suitable .shpk file for cross-references. Some functionality will be missing."
                : "No dev-kit file found for this material's shaders. Please install one for optimal editing experience, such as actual constant names instead of hexadecimal identifiers.");
        }

        return ret;
    }

    private static string? MaterialParamName(bool componentOnly, int offset)
    {
        if (offset < 0)
            return null;

        return (componentOnly, offset & 0x3) switch
        {
            (true, 0)  => "x",
            (true, 1)  => "y",
            (true, 2)  => "z",
            (true, 3)  => "w",
            (false, 0) => $"[{offset >> 2:D2}].x",
            (false, 1) => $"[{offset >> 2:D2}].y",
            (false, 2) => $"[{offset >> 2:D2}].z",
            (false, 3) => $"[{offset >> 2:D2}].w",
            _          => null,
        };
    }

    private static string VectorSwizzle(int firstComponent, int lastComponent)
        => (firstComponent, lastComponent) switch
        {
            (0, 4) => "     ",
            (0, 0) => ".x   ",
            (0, 1) => ".xy  ",
            (0, 2) => ".xyz ",
            (0, 3) => "     ",
            (1, 1) => ".y   ",
            (1, 2) => ".yz  ",
            (1, 3) => ".yzw ",
            (2, 2) => ".z   ",
            (2, 3) => ".zw  ",
            (3, 3) => ".w   ",
            _      => string.Empty,
        };

    private static (string? Name, bool ComponentOnly) MaterialParamRangeName(string prefix, int valueOffset, int valueLength)
    {
        if (valueLength == 0 || valueOffset < 0)
            return (null, false);

        var firstVector    = valueOffset >> 2;
        var lastVector     = (valueOffset + valueLength - 1) >> 2;
        var firstComponent = valueOffset & 0x3;
        var lastComponent  = (valueOffset + valueLength - 1) & 0x3;
        if (firstVector == lastVector)
            return ($"{prefix}[{firstVector}]{VectorSwizzle(firstComponent, lastComponent)}", true);

        var sb = new StringBuilder(128);
        sb.Append($"{prefix}[{firstVector}]{VectorSwizzle(firstComponent, 3).TrimEnd()}");
        for (var i = firstVector + 1; i < lastVector; ++i)
            sb.Append($", [{i}]");

        sb.Append($", [{lastVector}]{VectorSwizzle(0, lastComponent)}");
        return (sb.ToString(), false);
    }
}

using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiFileDialog;
using ImGuiNET;
using Lumina.Data.Parsing;
using OtterGui;
using OtterGui.Raii;
using Penumbra.GameData;
using Penumbra.GameData.Files;
using Penumbra.String.Classes;

namespace Penumbra.UI.AdvancedWindow;

public partial class ModEditWindow
{
    private readonly FileDialogService _fileDialog;

    private bool DrawPackageNameInput(MtrlTab tab, bool disabled)
    {
        var ret = false;
        ImGui.SetNextItemWidth(UiHelpers.Scale * 150.0f);
        if (ImGui.InputText("Shader Package Name", ref tab.Mtrl.ShaderPackage.Name, 63,
                disabled ? ImGuiInputTextFlags.ReadOnly : ImGuiInputTextFlags.None))
        {
            ret                = true;
            tab.AssociatedShpk = null;
            tab.LoadedShpkPath = FullPath.Empty;
        }

        if (ImGui.IsItemDeactivatedAfterEdit())
            tab.LoadShpk(tab.FindAssociatedShpk(out _, out _));

        return ret;
    }

    private static bool DrawShaderFlagsInput(MtrlFile file, bool disabled)
    {
        var ret       = false;
        var shpkFlags = (int)file.ShaderPackage.Flags;
        ImGui.SetNextItemWidth(UiHelpers.Scale * 150.0f);
        if (ImGui.InputInt("Shader Package Flags", ref shpkFlags, 0, 0,
                ImGuiInputTextFlags.CharsHexadecimal | (disabled ? ImGuiInputTextFlags.ReadOnly : ImGuiInputTextFlags.None)))
        {
            file.ShaderPackage.Flags = (uint)shpkFlags;
            ret                      = true;
        }

        return ret;
    }

    /// <summary>
    /// Show the currently associated shpk file, if any, and the buttons to associate
    /// a specific shpk from your drive, the modded shpk by path or the default shpk.
    /// </summary>
    private void DrawCustomAssociations(MtrlTab tab)
    {
        var text = tab.AssociatedShpk == null
            ? "Associated .shpk file: None"
            : $"Associated .shpk file: {tab.LoadedShpkPathName}";

        ImGui.Dummy(new Vector2(ImGui.GetTextLineHeight() / 2));

        if (ImGui.Selectable(text))
            ImGui.SetClipboardText(tab.LoadedShpkPathName);

        ImGuiUtil.HoverTooltip("Click to copy file path to clipboard.");

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


    private static bool DrawShaderKey(MtrlTab tab, bool disabled, ref int idx)
    {
        var       ret = false;
        using var t2  = ImRaii.TreeNode(tab.ShaderKeyLabels[idx], disabled ? ImGuiTreeNodeFlags.Leaf : 0);
        if (!t2 || disabled)
            return ret;

        var key     = tab.Mtrl.ShaderPackage.ShaderKeys[idx];
        var shpkKey = tab.AssociatedShpk?.GetMaterialKeyById(key.Category);
        if (shpkKey.HasValue)
        {
            ImGui.SetNextItemWidth(UiHelpers.Scale * 150.0f);
            using var c = ImRaii.Combo("Value", $"0x{key.Value:X8}");
            if (c)
                foreach (var value in shpkKey.Value.Values)
                {
                    if (ImGui.Selectable($"0x{value:X8}", value == key.Value))
                    {
                        tab.Mtrl.ShaderPackage.ShaderKeys[idx].Value = value;
                        ret                                          = true;
                        tab.UpdateShaderKeyLabels();
                    }
                }
        }

        if (ImGui.Button("Remove Key"))
        {
            tab.Mtrl.ShaderPackage.ShaderKeys = tab.Mtrl.ShaderPackage.ShaderKeys.RemoveItems(idx--);
            ret                               = true;
            tab.UpdateShaderKeyLabels();
        }

        return ret;
    }

    private static bool DrawNewShaderKey(MtrlTab tab)
    {
        ImGui.SetNextItemWidth(UiHelpers.Scale * 150.0f);
        var ret = false;
        using (var c = ImRaii.Combo("##NewConstantId", $"ID: 0x{tab.NewKeyId:X8}"))
        {
            if (c)
                foreach (var idx in tab.MissingShaderKeyIndices)
                {
                    var key = tab.AssociatedShpk!.MaterialKeys[idx];

                    if (ImGui.Selectable($"ID: 0x{key.Id:X8}", key.Id == tab.NewKeyId))
                    {
                        tab.NewKeyDefault = key.DefaultValue;
                        tab.NewKeyId      = key.Id;
                        ret               = true;
                        tab.UpdateShaderKeyLabels();
                    }
                }
        }

        ImGui.SameLine();
        if (ImGui.Button("Add Key"))
        {
            tab.Mtrl.ShaderPackage.ShaderKeys = tab.Mtrl.ShaderPackage.ShaderKeys.AddItem(new ShaderKey
            {
                Category = tab.NewKeyId,
                Value    = tab.NewKeyDefault,
            });
            ret = true;
            tab.UpdateShaderKeyLabels();
        }

        return ret;
    }

    private static bool DrawMaterialShaderKeys(MtrlTab tab, bool disabled)
    {
        if (tab.Mtrl.ShaderPackage.ShaderKeys.Length <= 0
         && (disabled || tab.AssociatedShpk == null || tab.AssociatedShpk.MaterialKeys.Length <= 0))
            return false;

        using var t = ImRaii.TreeNode("Shader Keys");
        if (!t)
            return false;

        var ret = false;
        for (var idx = 0; idx < tab.Mtrl.ShaderPackage.ShaderKeys.Length; ++idx)
            ret |= DrawShaderKey(tab, disabled, ref idx);

        if (!disabled && tab.AssociatedShpk != null && tab.MissingShaderKeyIndices.Count != 0)
            ret |= DrawNewShaderKey(tab);

        return ret;
    }

    private static void DrawMaterialShaders(MtrlTab tab)
    {
        if (tab.AssociatedShpk == null)
            return;

        ImRaii.TreeNode(tab.VertexShaders, ImGuiTreeNodeFlags.Leaf).Dispose();
        ImRaii.TreeNode(tab.PixelShaders,  ImGuiTreeNodeFlags.Leaf).Dispose();
    }


    private static bool DrawMaterialConstantValues(MtrlTab tab, bool disabled, ref int idx)
    {
        var (name, componentOnly, paramValueOffset) = tab.MaterialConstants[idx];
        using var font = ImRaii.PushFont(UiBuilder.MonoFont);
        using var t2   = ImRaii.TreeNode(name);
        if (!t2)
            return false;

        font.Dispose();

        var constant = tab.Mtrl.ShaderPackage.Constants[idx];
        var ret      = false;
        var values   = tab.Mtrl.GetConstantValues(constant);
        if (values.Length > 0)
        {
            var valueOffset = constant.ByteOffset >> 2;

            for (var valueIdx = 0; valueIdx < values.Length; ++valueIdx)
            {
                var paramName = MaterialParamName(componentOnly, paramValueOffset + valueIdx) ?? $"#{valueIdx}";
                ImGui.SetNextItemWidth(UiHelpers.Scale * 150.0f);
                if (ImGui.InputFloat($"{paramName} (at 0x{(valueOffset + valueIdx) << 2:X4})", ref values[valueIdx], 0.0f, 0.0f, "%.3f",
                        disabled ? ImGuiInputTextFlags.ReadOnly : ImGuiInputTextFlags.None))
                {
                    ret = true;
                    tab.UpdateConstantLabels();
                }
            }
        }
        else
        {
            ImRaii.TreeNode($"Offset: 0x{constant.ByteOffset:X4}", ImGuiTreeNodeFlags.Leaf).Dispose();
            ImRaii.TreeNode($"Size: 0x{constant.ByteSize:X4}",     ImGuiTreeNodeFlags.Leaf).Dispose();
        }

        if (!disabled
         && !tab.HasMalformedMaterialConstants
         && tab.OrphanedMaterialValues.Count == 0
         && tab.AliasedMaterialValueCount == 0
         && ImGui.Button("Remove Constant"))
        {
            tab.Mtrl.ShaderPackage.ShaderValues =
                tab.Mtrl.ShaderPackage.ShaderValues.RemoveItems(constant.ByteOffset >> 2, constant.ByteSize >> 2);
            tab.Mtrl.ShaderPackage.Constants = tab.Mtrl.ShaderPackage.Constants.RemoveItems(idx--);
            for (var i = 0; i < tab.Mtrl.ShaderPackage.Constants.Length; ++i)
            {
                if (tab.Mtrl.ShaderPackage.Constants[i].ByteOffset >= constant.ByteOffset)
                    tab.Mtrl.ShaderPackage.Constants[i].ByteOffset -= constant.ByteSize;
            }

            ret = true;
            tab.UpdateConstantLabels();
        }

        return ret;
    }

    private static bool DrawMaterialOrphans(MtrlTab tab, bool disabled)
    {
        using var t2 = ImRaii.TreeNode($"Orphan Values ({tab.OrphanedMaterialValues.Count})");
        if (!t2)
            return false;

        var ret = false;
        foreach (var idx in tab.OrphanedMaterialValues)
        {
            ImGui.SetNextItemWidth(ImGui.GetFontSize() * 10.0f);
            if (ImGui.InputFloat($"#{idx} (at 0x{idx << 2:X4})",
                    ref tab.Mtrl.ShaderPackage.ShaderValues[idx], 0.0f, 0.0f, "%.3f",
                    disabled ? ImGuiInputTextFlags.ReadOnly : ImGuiInputTextFlags.None))
            {
                ret = true;
                tab.UpdateConstantLabels();
            }
        }

        return ret;
    }

    private static bool DrawNewMaterialParam(MtrlTab tab)
    {
        ImGui.SetNextItemWidth(UiHelpers.Scale * 450.0f);
        using (var font = ImRaii.PushFont(UiBuilder.MonoFont))
        {
            using var c = ImRaii.Combo("##NewConstantId", tab.MissingMaterialConstants[tab.NewConstantIdx].Name);
            if (c)
                foreach (var (constant, idx) in tab.MissingMaterialConstants.WithIndex())
                {
                    if (ImGui.Selectable(constant.Name, constant.Id == tab.NewConstantId))
                    {
                        tab.NewConstantIdx = idx;
                        tab.NewConstantId  = constant.Id;
                    }
                }
        }

        ImGui.SameLine();
        if (ImGui.Button("Add Constant"))
        {
            var (_, _, byteSize) = tab.MissingMaterialConstants[tab.NewConstantIdx];
            tab.Mtrl.ShaderPackage.Constants = tab.Mtrl.ShaderPackage.Constants.AddItem(new MtrlFile.Constant
            {
                Id         = tab.NewConstantId,
                ByteOffset = (ushort)(tab.Mtrl.ShaderPackage.ShaderValues.Length << 2),
                ByteSize   = byteSize,
            });
            tab.Mtrl.ShaderPackage.ShaderValues = tab.Mtrl.ShaderPackage.ShaderValues.AddItem(0.0f, byteSize >> 2);
            tab.UpdateConstantLabels();
            return true;
        }

        return false;
    }

    private static bool DrawMaterialConstants(MtrlTab tab, bool disabled)
    {
        if (tab.Mtrl.ShaderPackage.Constants.Length == 0
         && tab.Mtrl.ShaderPackage.ShaderValues.Length == 0
         && (disabled || tab.AssociatedShpk == null || tab.AssociatedShpk.MaterialParams.Length == 0))
            return false;

        using var font = ImRaii.PushFont(UiBuilder.MonoFont);
        using var t    = ImRaii.TreeNode(tab.MaterialConstantLabel);
        if (!t)
            return false;

        font.Dispose();
        var ret = false;
        for (var idx = 0; idx < tab.Mtrl.ShaderPackage.Constants.Length; ++idx)
            ret |= DrawMaterialConstantValues(tab, disabled, ref idx);

        if (tab.OrphanedMaterialValues.Count > 0)
            ret |= DrawMaterialOrphans(tab, disabled);
        else if (!disabled && !tab.HasMalformedMaterialConstants && tab.MissingMaterialConstants.Count > 0)
            ret |= DrawNewMaterialParam(tab);

        return ret;
    }

    private static bool DrawMaterialSampler(MtrlTab tab, bool disabled, ref int idx)
    {
        var (label, filename) = tab.Samplers[idx];
        using var tree = ImRaii.TreeNode(label);
        if (!tree)
            return false;

        ImRaii.TreeNode(filename, ImGuiTreeNodeFlags.Leaf).Dispose();
        var ret     = false;
        var sampler = tab.Mtrl.ShaderPackage.Samplers[idx];

        // FIXME this probably doesn't belong here
        static unsafe bool InputHexUInt16(string label, ref ushort v, ImGuiInputTextFlags flags)
        {
            fixed (ushort* v2 = &v)
            {
                return ImGui.InputScalar(label, ImGuiDataType.U16, (nint)v2, IntPtr.Zero, IntPtr.Zero, "%04X", flags);
            }
        }

        ImGui.SetNextItemWidth(UiHelpers.Scale * 150.0f);
        if (InputHexUInt16("Texture Flags", ref tab.Mtrl.Textures[sampler.TextureIndex].Flags,
                disabled ? ImGuiInputTextFlags.ReadOnly : ImGuiInputTextFlags.None))
            ret = true;

        var samplerFlags = (int)sampler.Flags;
        ImGui.SetNextItemWidth(UiHelpers.Scale * 150.0f);
        if (ImGui.InputInt("Sampler Flags", ref samplerFlags, 0, 0,
                ImGuiInputTextFlags.CharsHexadecimal | (disabled ? ImGuiInputTextFlags.ReadOnly : ImGuiInputTextFlags.None)))
        {
            tab.Mtrl.ShaderPackage.Samplers[idx].Flags = (uint)samplerFlags;
            ret                                        = true;
        }

        if (!disabled
         && tab.OrphanedSamplers.Count == 0
         && tab.AliasedSamplerCount == 0
         && ImGui.Button("Remove Sampler"))
        {
            tab.Mtrl.Textures               = tab.Mtrl.Textures.RemoveItems(sampler.TextureIndex);
            tab.Mtrl.ShaderPackage.Samplers = tab.Mtrl.ShaderPackage.Samplers.RemoveItems(idx--);
            for (var i = 0; i < tab.Mtrl.ShaderPackage.Samplers.Length; ++i)
            {
                if (tab.Mtrl.ShaderPackage.Samplers[i].TextureIndex >= sampler.TextureIndex)
                    --tab.Mtrl.ShaderPackage.Samplers[i].TextureIndex;
            }

            ret = true;
            tab.UpdateSamplers();
            tab.UpdateTextureLabels();
        }

        return ret;
    }

    private static bool DrawMaterialNewSampler(MtrlTab tab)
    {
        var (name, id) = tab.MissingSamplers[tab.NewSamplerIdx];
        ImGui.SetNextItemWidth(UiHelpers.Scale * 450.0f);
        using (var c = ImRaii.Combo("##NewSamplerId", $"{name} (ID: 0x{id:X8})"))
        {
            if (c)
                foreach (var (sampler, idx) in tab.MissingSamplers.WithIndex())
                {
                    if (ImGui.Selectable($"{sampler.Name} (ID: 0x{sampler.Id:X8})", sampler.Id == tab.NewSamplerId))
                    {
                        tab.NewSamplerIdx = idx;
                        tab.NewSamplerId  = sampler.Id;
                    }
                }
        }

        ImGui.SameLine();
        if (!ImGui.Button("Add Sampler"))
            return false;

        tab.Mtrl.ShaderPackage.Samplers = tab.Mtrl.ShaderPackage.Samplers.AddItem(new Sampler
        {
            SamplerId    = tab.NewSamplerId,
            TextureIndex = (byte)tab.Mtrl.Textures.Length,
            Flags        = 0,
        });
        tab.Mtrl.Textures = tab.Mtrl.Textures.AddItem(new MtrlFile.Texture
        {
            Path  = string.Empty,
            Flags = 0,
        });
        tab.UpdateSamplers();
        tab.UpdateTextureLabels();
        return true;
    }

    private static bool DrawMaterialSamplers(MtrlTab tab, bool disabled)
    {
        if (tab.Mtrl.ShaderPackage.Samplers.Length == 0
         && tab.Mtrl.Textures.Length == 0
         && (disabled || (tab.AssociatedShpk?.Samplers.All(sampler => sampler.Slot != 2) ?? false)))
            return false;

        using var t = ImRaii.TreeNode("Samplers");
        if (!t)
            return false;

        var ret = false;
        for (var idx = 0; idx < tab.Mtrl.ShaderPackage.Samplers.Length; ++idx)
            ret |= DrawMaterialSampler(tab, disabled, ref idx);

        if (tab.OrphanedSamplers.Count > 0)
        {
            using var t2 = ImRaii.TreeNode($"Orphan Textures ({tab.OrphanedSamplers.Count})");
            if (t2)
                foreach (var idx in tab.OrphanedSamplers)
                {
                    ImRaii.TreeNode($"#{idx}: {Path.GetFileName(tab.Mtrl.Textures[idx].Path)} - {tab.Mtrl.Textures[idx].Flags:X4}",
                            ImGuiTreeNodeFlags.Leaf)
                        .Dispose();
                }
        }
        else if (!disabled && tab.MissingSamplers.Count > 0 && tab.AliasedSamplerCount == 0 && tab.Mtrl.Textures.Length < 255)
        {
            ret |= DrawMaterialNewSampler(tab);
        }

        return ret;
    }

    private bool DrawMaterialShaderResources(MtrlTab tab, bool disabled)
    {
        var ret = false;
        if (!ImGui.CollapsingHeader("Advanced Shader Resources"))
            return ret;

        ret |= DrawPackageNameInput(tab, disabled);
        ret |= DrawShaderFlagsInput(tab.Mtrl, disabled);
        DrawCustomAssociations(tab);
        ret |= DrawMaterialShaderKeys(tab, disabled);
        DrawMaterialShaders(tab);
        ret |= DrawMaterialConstants(tab, disabled);
        ret |= DrawMaterialSamplers(tab, disabled);
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

    private static (string? Name, bool ComponentOnly) MaterialParamRangeName(string prefix, int valueOffset, int valueLength)
    {
        static string VectorSwizzle(int firstComponent, int lastComponent)
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

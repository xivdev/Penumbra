using Dalamud.Interface.Internal.Notifications;
using Dalamud.Interface;
using ImGuiNET;
using Lumina.Misc;
using OtterGui.Raii;
using OtterGui;
using OtterGui.Classes;
using Penumbra.GameData;
using Penumbra.GameData.Data;
using Penumbra.GameData.Files;
using Penumbra.String;
using static Penumbra.GameData.Files.ShpkFile;

namespace Penumbra.UI.AdvancedWindow;

public partial class ModEditWindow
{
    private static readonly ByteString DisassemblyLabel = ByteString.FromSpanUnsafe("##disassembly"u8, true, true, true);

    private readonly FileEditor<ShpkTab> _shaderPackageTab;

    private static bool DrawShaderPackagePanel(ShpkTab file, bool disabled)
    {
        DrawShaderPackageSummary(file);

        var ret = false;
        ImGui.Dummy(new Vector2(ImGui.GetTextLineHeight() / 2));
        ret |= DrawShaderPackageShaderArray(file, "Vertex Shader", file.Shpk.VertexShaders, disabled);

        ImGui.Dummy(new Vector2(ImGui.GetTextLineHeight() / 2));
        ret |= DrawShaderPackageShaderArray(file, "Pixel Shader", file.Shpk.PixelShaders, disabled);

        ImGui.Dummy(new Vector2(ImGui.GetTextLineHeight() / 2));
        ret |= DrawShaderPackageMaterialParamLayout(file, disabled);

        ImGui.Dummy(new Vector2(ImGui.GetTextLineHeight() / 2));
        ret |= DrawShaderPackageResources(file, disabled);

        ImGui.Dummy(new Vector2(ImGui.GetTextLineHeight() / 2));
        DrawShaderPackageSelection(file);

        ImGui.Dummy(new Vector2(ImGui.GetTextLineHeight() / 2));
        DrawOtherShaderPackageDetails(file);

        file.FileDialog.Draw();

        ret |= file.Shpk.IsChanged();

        return !disabled && ret;
    }

    private static void DrawShaderPackageSummary(ShpkTab tab)
    {
        ImGui.TextUnformatted(tab.Header);
        if (!tab.Shpk.Disassembled)
        {
            var textColor        = ImGui.GetColorU32(ImGuiCol.Text);
            var textColorWarning = (textColor & 0xFF000000u) | ((textColor & 0x00FEFEFE) >> 1) | 0x80u; // Half red

            using var c = ImRaii.PushColor(ImGuiCol.Text, textColorWarning);

            ImGui.TextUnformatted("Your system doesn't support disassembling shaders. Some functionality will be missing.");
        }
    }

    private static void DrawShaderExportButton(ShpkTab tab, string objectName, Shader shader, int idx)
    {
        if (!ImGui.Button($"Export Shader Program Blob ({shader.Blob.Length} bytes)"))
            return;

        var defaultName = objectName[0] switch
        {
            'V' => $"vs{idx}",
            'P' => $"ps{idx}",
            _   => throw new NotImplementedException(),
        };

        var blob = shader.Blob;
        tab.FileDialog.OpenSavePicker($"Export {objectName} #{idx} Program Blob to...", tab.Extension, defaultName, tab.Extension,
            (success, name) =>
            {
                if (!success)
                    return;

                try
                {
                    File.WriteAllBytes(name, blob);
                }
                catch (Exception e)
                {
                    Penumbra.Messager.NotificationMessage(e, $"Could not export {defaultName}{tab.Extension} to {name}.",
                        NotificationType.Error, false);
                    return;
                }

                Penumbra.Messager.NotificationMessage(
                    $"Shader Program Blob {defaultName}{tab.Extension} exported successfully to {Path.GetFileName(name)}.",
                    NotificationType.Success, false);
            }, null, false);
    }

    private static void DrawShaderImportButton(ShpkTab tab, string objectName, Shader[] shaders, int idx)
    {
        if (!ImGui.Button("Replace Shader Program Blob"))
            return;

        tab.FileDialog.OpenFilePicker($"Replace {objectName} #{idx} Program Blob...", "Shader Program Blobs{.o,.cso,.dxbc,.dxil}",
            (success, name) =>
            {
                if (!success)
                    return;

                try
                {
                    shaders[idx].Blob = File.ReadAllBytes(name[0]);
                }
                catch (Exception e)
                {
                    Penumbra.Messager.NotificationMessage(e, $"Could not import {name}.", NotificationType.Error, false);
                    return;
                }

                try
                {
                    shaders[idx].UpdateResources(tab.Shpk);
                    tab.Shpk.UpdateResources();
                }
                catch (Exception e)
                {
                    tab.Shpk.SetInvalid();
                    Penumbra.Messager.NotificationMessage(e, $"Failed to update resources after importing {name}.", NotificationType.Error,
                        false);
                    return;
                }

                tab.Shpk.SetChanged();
            }, 1, null, false);
    }

    private static unsafe void DrawRawDisassembly(Shader shader)
    {
        using var t2 = ImRaii.TreeNode("Raw Program Disassembly");
        if (!t2)
            return;

        using var font = ImRaii.PushFont(UiBuilder.MonoFont);
        var       size = new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetTextLineHeight() * 20);
        ImGuiNative.igInputTextMultiline(DisassemblyLabel.Path, shader.Disassembly!.RawDisassembly.Path,
            (uint)shader.Disassembly!.RawDisassembly.Length + 1, size,
            ImGuiInputTextFlags.ReadOnly, null, null);
    }

    private static bool DrawShaderPackageShaderArray(ShpkTab tab, string objectName, Shader[] shaders, bool disabled)
    {
        if (shaders.Length == 0 || !ImGui.CollapsingHeader($"{objectName}s"))
            return false;

        var ret = false;
        for (var idx = 0; idx < shaders.Length; ++idx)
        {
            var       shader = shaders[idx];
            using var t      = ImRaii.TreeNode($"{objectName} #{idx}");
            if (!t)
                continue;

            DrawShaderExportButton(tab, objectName, shader, idx);
            if (!disabled && tab.Shpk.Disassembled)
            {
                ImGui.SameLine();
                DrawShaderImportButton(tab, objectName, shaders, idx);
            }

            ret |= DrawShaderPackageResourceArray("Constant Buffers",       "slot", true,  shader.Constants, true);
            ret |= DrawShaderPackageResourceArray("Samplers",               "slot", false, shader.Samplers,  true);
            ret |= DrawShaderPackageResourceArray("Unordered Access Views", "slot", true,  shader.Uavs,      true);

            if (shader.AdditionalHeader.Length > 0)
            {
                using var t2 = ImRaii.TreeNode($"Additional Header (Size: {shader.AdditionalHeader.Length})###AdditionalHeader");
                if (t2)
                    ImGuiUtil.TextWrapped(string.Join(' ', shader.AdditionalHeader.Select(c => $"{c:X2}")));
            }

            if (tab.Shpk.Disassembled)
                DrawRawDisassembly(shader);
        }

        return ret;
    }

    private static bool DrawShaderPackageResource(string slotLabel, bool withSize, ref Resource resource, bool disabled)
    {
        var ret = false;
        if (!disabled)
        {
            ImGui.SetNextItemWidth(UiHelpers.Scale * 150.0f);
            if (ImGuiUtil.InputUInt16($"{char.ToUpper(slotLabel[0])}{slotLabel[1..].ToLower()}", ref resource.Slot, ImGuiInputTextFlags.None))
                ret = true;
        }

        if (resource.Used == null)
            return ret;

        var usedString = UsedComponentString(withSize, resource);
        if (usedString.Length > 0)
            ImRaii.TreeNode($"Used: {usedString}", ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet).Dispose();
        else
            ImRaii.TreeNode("Unused", ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet).Dispose();

        return ret;
    }

    private static bool DrawShaderPackageResourceArray(string arrayName, string slotLabel, bool withSize, Resource[] resources, bool disabled)
    {
        if (resources.Length == 0)
            return false;

        using var t = ImRaii.TreeNode(arrayName);
        if (!t)
            return false;

        var ret = false;
        for (var idx = 0; idx < resources.Length; ++idx)
        {
            ref var buf = ref resources[idx];
            var name = $"#{idx}: {buf.Name} (ID: 0x{buf.Id:X8}), {slotLabel}: {buf.Slot}"
              + (withSize ? $", size: {buf.Size} registers###{idx}: {buf.Name} (ID: 0x{buf.Id:X8})" : string.Empty);
            using var font = ImRaii.PushFont(UiBuilder.MonoFont);
            using var t2   = ImRaii.TreeNode(name, !disabled || buf.Used != null ? 0 : ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet);
            font.Dispose();
            if (t2)
                ret |= DrawShaderPackageResource(slotLabel, withSize, ref buf, disabled);
        }

        return ret;
    }

    private static bool DrawMaterialParamLayoutHeader(string label)
    {
        using var font = ImRaii.PushFont(UiBuilder.MonoFont);
        var pos = ImGui.GetCursorScreenPos()
          + new Vector2(ImGui.CalcTextSize(label).X + 3 * ImGui.GetStyle().ItemInnerSpacing.X + ImGui.GetFrameHeight(),
                ImGui.GetStyle().FramePadding.Y);

        var ret = ImGui.CollapsingHeader(label);
        ImGui.GetWindowDrawList()
            .AddText(UiBuilder.DefaultFont, UiBuilder.DefaultFont.FontSize, pos, ImGui.GetColorU32(ImGuiCol.Text), "Layout");
        return ret;
    }

    private static bool DrawMaterialParamLayoutBufferSize(ShpkFile file, Resource? materialParams)
    {
        var isSizeWellDefined = (file.MaterialParamsSize & 0xF) == 0
         && (!materialParams.HasValue || file.MaterialParamsSize == materialParams.Value.Size << 4);
        if (isSizeWellDefined)
            return true;

        ImGui.TextUnformatted(materialParams.HasValue
            ? $"Buffer size mismatch: {file.MaterialParamsSize} bytes ≠ {materialParams.Value.Size} registers ({materialParams.Value.Size << 4} bytes)"
            : $"Buffer size mismatch: {file.MaterialParamsSize} bytes, not a multiple of 16");
        return false;
    }

    private static bool DrawShaderPackageMaterialMatrix(ShpkTab tab, bool disabled)
    {
        ImGui.TextUnformatted(tab.Shpk.Disassembled
            ? "Parameter positions (continuations are grayed out, unused values are red):"
            : "Parameter positions (continuations are grayed out):");

        using var table = ImRaii.Table("##MaterialParamLayout", 5,
            ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg);
        if (!table)
            return false;

        ImGui.TableSetupColumn(string.Empty, ImGuiTableColumnFlags.WidthFixed, 25 * UiHelpers.Scale);
        ImGui.TableSetupColumn("x",          ImGuiTableColumnFlags.WidthFixed, 100 * UiHelpers.Scale);
        ImGui.TableSetupColumn("y",          ImGuiTableColumnFlags.WidthFixed, 100 * UiHelpers.Scale);
        ImGui.TableSetupColumn("z",          ImGuiTableColumnFlags.WidthFixed, 100 * UiHelpers.Scale);
        ImGui.TableSetupColumn("w",          ImGuiTableColumnFlags.WidthFixed, 100 * UiHelpers.Scale);
        ImGui.TableHeadersRow();

        var textColorStart       = ImGui.GetColorU32(ImGuiCol.Text);
        var textColorCont        = (textColorStart & 0x00FFFFFFu) | ((textColorStart & 0xFE000000u) >> 1);        // Half opacity
        var textColorUnusedStart = (textColorStart & 0xFF000000u) | ((textColorStart & 0x00FEFEFE) >> 1) | 0x80u; // Half red
        var textColorUnusedCont  = (textColorUnusedStart & 0x00FFFFFFu) | ((textColorUnusedStart & 0xFE000000u) >> 1);

        var ret = false;
        for (var i = 0; i < tab.Matrix.GetLength(0); ++i)
        {
            ImGui.TableNextColumn();
            ImGui.TableHeader($"  [{i}]");
            for (var j = 0; j < 4; ++j)
            {
                var (name, tooltip, idx, colorType) = tab.Matrix[i, j];
                var color = colorType switch
                {
                    ShpkTab.ColorType.Unused                                => textColorUnusedStart,
                    ShpkTab.ColorType.Used                                  => textColorStart,
                    ShpkTab.ColorType.Continuation                          => textColorUnusedCont,
                    ShpkTab.ColorType.Continuation | ShpkTab.ColorType.Used => textColorCont,
                    _                                                       => textColorStart,
                };
                using var _         = ImRaii.PushId(i * 4 + j);
                var       deletable = !disabled && idx >= 0;
                using (var font = ImRaii.PushFont(UiBuilder.MonoFont, tooltip.Length > 0))
                {
                    using (var c = ImRaii.PushColor(ImGuiCol.Text, color))
                    {
                        ImGui.TableNextColumn();
                        ImGui.Selectable(name);
                        if (deletable && ImGui.IsItemClicked(ImGuiMouseButton.Right) && ImGui.GetIO().KeyCtrl)
                        {
                            tab.Shpk.MaterialParams = tab.Shpk.MaterialParams.RemoveItems(idx);
                            ret                     = true;
                            tab.Update();
                        }
                    }

                    ImGuiUtil.HoverTooltip(tooltip);
                }

                if (deletable)
                    ImGuiUtil.HoverTooltip("\nControl + Right-Click to remove.");
            }
        }

        return ret;
    }

    private static void DrawShaderPackageMisalignedParameters(ShpkTab tab)
    {
        using var t = ImRaii.TreeNode("Misaligned / Overflowing Parameters");
        if (!t)
            return;

        using var _ = ImRaii.PushFont(UiBuilder.MonoFont);
        foreach (var name in tab.MalformedParameters)
            ImRaii.TreeNode(name, ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet).Dispose();
    }

    private static void DrawShaderPackageStartCombo(ShpkTab tab)
    {
        using var s = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, ImGui.GetStyle().ItemInnerSpacing);
        using (var _ = ImRaii.PushFont(UiBuilder.MonoFont))
        {
            ImGui.SetNextItemWidth(UiHelpers.Scale * 400);
            using var c = ImRaii.Combo("##Start", tab.Orphans[tab.NewMaterialParamStart].Name);
            if (c)
                foreach (var (start, idx) in tab.Orphans.WithIndex())
                {
                    if (ImGui.Selectable(start.Name, idx == tab.NewMaterialParamStart))
                        tab.UpdateOrphanStart(idx);
                }
        }

        ImGui.SameLine();
        ImGui.TextUnformatted("Start");
    }

    private static void DrawShaderPackageEndCombo(ShpkTab tab)
    {
        using var s = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, ImGui.GetStyle().ItemInnerSpacing);
        using (var _ = ImRaii.PushFont(UiBuilder.MonoFont))
        {
            ImGui.SetNextItemWidth(UiHelpers.Scale * 400);
            using var c = ImRaii.Combo("##End", tab.Orphans[tab.NewMaterialParamEnd].Name);
            if (c)
            {
                var current = tab.Orphans[tab.NewMaterialParamStart].Index;
                for (var i = tab.NewMaterialParamStart; i < tab.Orphans.Count; ++i)
                {
                    var next = tab.Orphans[i];
                    if (current++ != next.Index)
                        break;

                    if (ImGui.Selectable(next.Name, i == tab.NewMaterialParamEnd))
                        tab.NewMaterialParamEnd = i;
                }
            }
        }

        ImGui.SameLine();
        ImGui.TextUnformatted("End");
    }

    private static bool DrawShaderPackageNewParameter(ShpkTab tab)
    {
        if (tab.Orphans.Count == 0)
            return false;

        DrawShaderPackageStartCombo(tab);
        DrawShaderPackageEndCombo(tab);

        ImGui.SetNextItemWidth(UiHelpers.Scale * 400);
        if (ImGui.InputText("Name", ref tab.NewMaterialParamName, 63))
            tab.NewMaterialParamId = Crc32.Get(tab.NewMaterialParamName, 0xFFFFFFFFu);

        var tooltip = tab.UsedIds.Contains(tab.NewMaterialParamId)
            ? "The ID is already in use. Please choose a different name."
            : string.Empty;
        if (!ImGuiUtil.DrawDisabledButton($"Add ID 0x{tab.NewMaterialParamId:X8}", new Vector2(400 * UiHelpers.Scale, ImGui.GetFrameHeight()),
                tooltip,
                tooltip.Length > 0))
            return false;

        tab.Shpk.MaterialParams = tab.Shpk.MaterialParams.AddItem(new MaterialParam
        {
            Id         = tab.NewMaterialParamId,
            ByteOffset = (ushort)(tab.Orphans[tab.NewMaterialParamStart].Index << 2),
            ByteSize   = (ushort)((tab.NewMaterialParamEnd - tab.NewMaterialParamStart + 1) << 2),
        });
        tab.Update();
        return true;
    }

    private static bool DrawShaderPackageMaterialParamLayout(ShpkTab tab, bool disabled)
    {
        var ret = false;

        var materialParams = tab.Shpk.GetConstantById(MaterialParamsConstantId);
        if (!DrawMaterialParamLayoutHeader(materialParams?.Name ?? "Material Parameter"))
            return false;

        var sizeWellDefined = DrawMaterialParamLayoutBufferSize(tab.Shpk, materialParams);

        ret |= DrawShaderPackageMaterialMatrix(tab, disabled);

        if (tab.MalformedParameters.Count > 0)
            DrawShaderPackageMisalignedParameters(tab);
        else if (!disabled && sizeWellDefined)
            ret |= DrawShaderPackageNewParameter(tab);

        return ret;
    }

    private static bool DrawShaderPackageResources(ShpkTab tab, bool disabled)
    {
        var ret = false;

        if (!ImGui.CollapsingHeader("Shader Resources"))
            return false;

        ret |= DrawShaderPackageResourceArray("Constant Buffers",       "type", true,  tab.Shpk.Constants, disabled);
        ret |= DrawShaderPackageResourceArray("Samplers",               "type", false, tab.Shpk.Samplers,  disabled);
        ret |= DrawShaderPackageResourceArray("Unordered Access Views", "type", false, tab.Shpk.Uavs,      disabled);

        return ret;
    }

    private static void DrawKeyArray(string arrayName, bool withId, IReadOnlyCollection<Key> keys)
    {
        if (keys.Count == 0)
            return;

        using var t = ImRaii.TreeNode(arrayName);
        if (!t)
            return;

        using var font = ImRaii.PushFont(UiBuilder.MonoFont);
        foreach (var (key, idx) in keys.WithIndex())
        {
            using var t2 = ImRaii.TreeNode(withId ? $"#{idx}: ID: 0x{key.Id:X8}" : $"#{idx}");
            if (t2)
            {
                ImRaii.TreeNode($"Default Value: 0x{key.DefaultValue:X8}", ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet).Dispose();
                ImRaii.TreeNode($"Known Values: {string.Join(", ", Array.ConvertAll(key.Values, value => $"0x{value:X8}"))}",
                    ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet).Dispose();
            }
        }
    }

    private static void DrawShaderPackageNodes(ShpkTab tab)
    {
        if (tab.Shpk.Nodes.Length <= 0)
            return;

        using var t = ImRaii.TreeNode($"Nodes ({tab.Shpk.Nodes.Length})###Nodes");
        if (!t)
            return;

        foreach (var (node, idx) in tab.Shpk.Nodes.WithIndex())
        {
            using var font = ImRaii.PushFont(UiBuilder.MonoFont);
            using var t2   = ImRaii.TreeNode($"#{idx:D4}: Selector: 0x{node.Selector:X8}");
            if (!t2)
                continue;

            foreach (var (key, keyIdx) in node.SystemKeys.WithIndex())
            {
                ImRaii.TreeNode($"System Key 0x{tab.Shpk.SystemKeys[keyIdx].Id:X8} = 0x{key:X8}",
                    ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet).Dispose();
            }

            foreach (var (key, keyIdx) in node.SceneKeys.WithIndex())
            {
                ImRaii.TreeNode($"Scene Key 0x{tab.Shpk.SceneKeys[keyIdx].Id:X8} = 0x{key:X8}",
                    ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet).Dispose();
            }

            foreach (var (key, keyIdx) in node.MaterialKeys.WithIndex())
            {
                ImRaii.TreeNode($"Material Key 0x{tab.Shpk.MaterialKeys[keyIdx].Id:X8} = 0x{key:X8}",
                    ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet).Dispose();
            }

            foreach (var (key, keyIdx) in node.SubViewKeys.WithIndex())
                ImRaii.TreeNode($"Sub-View Key #{keyIdx} = 0x{key:X8}", ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet).Dispose();

            ImRaii.TreeNode($"Pass Indices: {string.Join(' ', node.PassIndices.Select(c => $"{c:X2}"))}",
                ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet).Dispose();
            foreach (var (pass, passIdx) in node.Passes.WithIndex())
            {
                ImRaii.TreeNode($"Pass #{passIdx}: ID: 0x{pass.Id:X8}, Vertex Shader #{pass.VertexShader}, Pixel Shader #{pass.PixelShader}",
                        ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet)
                    .Dispose();
            }
        }
    }

    private static void DrawShaderPackageSelection(ShpkTab tab)
    {
        if (!ImGui.CollapsingHeader("Shader Selection"))
            return;

        DrawKeyArray("System Keys",   true,  tab.Shpk.SystemKeys);
        DrawKeyArray("Scene Keys",    true,  tab.Shpk.SceneKeys);
        DrawKeyArray("Material Keys", true,  tab.Shpk.MaterialKeys);
        DrawKeyArray("Sub-View Keys", false, tab.Shpk.SubViewKeys);

        DrawShaderPackageNodes(tab);
        using var t = ImRaii.TreeNode($"Node Selectors ({tab.Shpk.NodeSelectors.Count})###NodeSelectors");
        if (t)
        {
            using var font = ImRaii.PushFont(UiBuilder.MonoFont);
            foreach (var selector in tab.Shpk.NodeSelectors)
            {
                ImRaii.TreeNode($"#{selector.Value:D4}: Selector: 0x{selector.Key:X8}", ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet)
                    .Dispose();
            }
        }
    }

    private static void DrawOtherShaderPackageDetails(ShpkTab tab)
    {
        if (!ImGui.CollapsingHeader("Further Content"))
            return;

        ImRaii.TreeNode($"Version: 0x{tab.Shpk.Version:X8}", ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet).Dispose();

        if (tab.Shpk.AdditionalData.Length > 0)
        {
            using var t = ImRaii.TreeNode($"Additional Data (Size: {tab.Shpk.AdditionalData.Length})###AdditionalData");
            if (t)
                ImGuiUtil.TextWrapped(string.Join(' ', tab.Shpk.AdditionalData.Select(c => $"{c:X2}")));
        }
    }

    private static string UsedComponentString(bool withSize, in Resource resource)
    {
        var sb = new StringBuilder(256);
        if (withSize)
        {
            foreach (var (components, i) in (resource.Used ?? Array.Empty<DisassembledShader.VectorComponents>()).WithIndex())
            {
                switch (components)
                {
                    case 0: break;
                    case DisassembledShader.VectorComponents.All:
                        sb.Append($"[{i}], ");
                        break;
                    default:
                        sb.Append($"[{i}].");
                        foreach (var c in components.ToString().Where(char.IsUpper))
                            sb.Append(char.ToLower(c));

                        sb.Append(", ");
                        break;
                }
            }

            switch (resource.UsedDynamically ?? 0)
            {
                case 0: break;
                case DisassembledShader.VectorComponents.All:
                    sb.Append("[*], ");
                    break;
                default:
                    sb.Append("[*].");
                    foreach (var c in resource.UsedDynamically!.Value.ToString().Where(char.IsUpper))
                        sb.Append(char.ToLower(c));

                    sb.Append(", ");
                    break;
            }
        }
        else
        {
            var components = (resource.Used is { Length: > 0 } ? resource.Used[0] : 0) | (resource.UsedDynamically ?? 0);
            if ((components & DisassembledShader.VectorComponents.X) != 0)
                sb.Append("Red, ");

            if ((components & DisassembledShader.VectorComponents.Y) != 0)
                sb.Append("Green, ");

            if ((components & DisassembledShader.VectorComponents.Z) != 0)
                sb.Append("Blue, ");

            if ((components & DisassembledShader.VectorComponents.W) != 0)
                sb.Append("Alpha, ");
        }

        return sb.Length == 0 ? string.Empty : sb.ToString(0, sb.Length - 2);
    }
}

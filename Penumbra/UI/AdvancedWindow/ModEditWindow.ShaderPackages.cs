using Dalamud.Interface;
using Dalamud.Interface.ImGuiNotification;
using ImGuiNET;
using OtterGui.Raii;
using OtterGui;
using OtterGui.Classes;
using Penumbra.GameData;
using Penumbra.GameData.Files;
using Penumbra.GameData.Interop;
using Penumbra.String;
using static Penumbra.GameData.Files.ShpkFile;
using OtterGui.Widgets;
using OtterGui.Text;
using Penumbra.GameData.Structs;

namespace Penumbra.UI.AdvancedWindow;

public partial class ModEditWindow
{
    private static readonly CiByteString DisassemblyLabel = CiByteString.FromSpanUnsafe("##disassembly"u8, true, true, true);

    private readonly FileEditor<ShpkTab> _shaderPackageTab;

    private static bool DrawShaderPackagePanel(ShpkTab file, bool disabled)
    {
        DrawShaderPackageSummary(file);

        ImGui.Dummy(new Vector2(ImGui.GetTextLineHeight() / 2));
        DrawShaderPackageFilterSection(file);

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

        ret |= file.Shpk.IsChanged();

        return !disabled && ret;
    }

    private static void DrawShaderPackageSummary(ShpkTab tab)
    {
        if (tab.Shpk.IsLegacy)
            ImUtf8.Text("This legacy shader package will not work in the current version of the game. Do not attempt to load it.",
                ImGuiUtil.HalfBlendText(0x80u)); // Half red
        ImUtf8.Text(tab.Header);
        if (!tab.Shpk.Disassembled)
            ImUtf8.Text("Your system doesn't support disassembling shaders. Some functionality will be missing.",
                ImGuiUtil.HalfBlendText(0x80u)); // Half red
    }

    private static void DrawShaderExportButton(ShpkTab tab, string objectName, Shader shader, int idx)
    {
        if (!ImUtf8.Button($"Export Shader Program Blob ({shader.Blob.Length} bytes)"))
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
        if (!ImUtf8.Button("Replace Shader Program Blob"u8))
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
                    tab.UpdateFilteredUsed();
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
        using var tree = ImUtf8.TreeNode("Raw Program Disassembly"u8);
        if (!tree)
            return;

        using var font = ImRaii.PushFont(UiBuilder.MonoFont);
        var       size = new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetTextLineHeight() * 20);
        ImGuiNative.igInputTextMultiline(DisassemblyLabel.Path, shader.Disassembly!.RawDisassembly.Path,
            (uint)shader.Disassembly!.RawDisassembly.Length + 1, size,
            ImGuiInputTextFlags.ReadOnly, null, null);
    }

    private static void DrawShaderUsage(ShpkTab tab, Shader shader)
    {
        using (var node = ImUtf8.TreeNode("Used with Shader Keys"u8))
        {
            if (node)
            {
                foreach (var (key, keyIdx) in shader.SystemValues!.WithIndex())
                {
                    ImUtf8.TreeNode(
                        $"Used with System Key {tab.TryResolveName(tab.Shpk.SystemKeys[keyIdx].Id)} \u2208 {{ {tab.NameSetToString(key)} }}",
                        ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet).Dispose();
                }

                foreach (var (key, keyIdx) in shader.SceneValues!.WithIndex())
                {
                    ImUtf8.TreeNode(
                        $"Used with Scene Key {tab.TryResolveName(tab.Shpk.SceneKeys[keyIdx].Id)} \u2208 {{ {tab.NameSetToString(key)} }}",
                        ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet).Dispose();
                }

                foreach (var (key, keyIdx) in shader.MaterialValues!.WithIndex())
                {
                    ImUtf8.TreeNode(
                        $"Used with Material Key {tab.TryResolveName(tab.Shpk.MaterialKeys[keyIdx].Id)} \u2208 {{ {tab.NameSetToString(key)} }}",
                        ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet).Dispose();
                }

                foreach (var (key, keyIdx) in shader.SubViewValues!.WithIndex())
                {
                    ImUtf8.TreeNode($"Used with Sub-View Key #{keyIdx} \u2208 {{ {tab.NameSetToString(key)} }}",
                        ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet).Dispose();
                }
            }
        }

        ImUtf8.TreeNode($"Used in Passes: {tab.NameSetToString(shader.Passes)}", ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet).Dispose();
    }

    private static void DrawShaderPackageFilterSection(ShpkTab tab)
    {
        if (!ImUtf8.CollapsingHeader(tab.FilterPopCount == tab.FilterMaximumPopCount ? "Filters###Filters"u8 : "Filters (ACTIVE)###Filters"u8))
            return;

        foreach (var (key, keyIdx) in tab.Shpk.SystemKeys.WithIndex())
            DrawShaderPackageFilterSet(tab, $"System Key {tab.TryResolveName(key.Id)}", ref tab.FilterSystemValues[keyIdx]);

        foreach (var (key, keyIdx) in tab.Shpk.SceneKeys.WithIndex())
            DrawShaderPackageFilterSet(tab, $"Scene Key {tab.TryResolveName(key.Id)}", ref tab.FilterSceneValues[keyIdx]);

        foreach (var (key, keyIdx) in tab.Shpk.MaterialKeys.WithIndex())
            DrawShaderPackageFilterSet(tab, $"Material Key {tab.TryResolveName(key.Id)}", ref tab.FilterMaterialValues[keyIdx]);

        foreach (var (_, keyIdx) in tab.Shpk.SubViewKeys.WithIndex())
            DrawShaderPackageFilterSet(tab, $"Sub-View Key #{keyIdx}", ref tab.FilterSubViewValues[keyIdx]);

        DrawShaderPackageFilterSet(tab, "Passes", ref tab.FilterPasses);
    }

    private static void DrawShaderPackageFilterSet(ShpkTab tab, string label, ref SharedSet<uint, uint> values)
    {
        if (values.PossibleValues == null)
        {
            ImUtf8.TreeNode(label, ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet).Dispose();
            return;
        }

        using var node = ImUtf8.TreeNode(label);
        if (!node)
            return;

        foreach (var value in values.PossibleValues)
        {
            var contains = values.Contains(value);
            if (!ImUtf8.Checkbox($"{tab.TryResolveName(value)}", ref contains))
                continue;

            if (contains)
            {
                if (values.AddExisting(value))
                {
                    ++tab.FilterPopCount;
                    tab.UpdateFilteredUsed();
                }
            }
            else
            {
                if (values.Remove(value))
                {
                    --tab.FilterPopCount;
                    tab.UpdateFilteredUsed();
                }
            }
        }
    }

    private static bool DrawShaderPackageShaderArray(ShpkTab tab, string objectName, Shader[] shaders, bool disabled)
    {
        if (shaders.Length == 0 || !ImUtf8.CollapsingHeader($"{objectName}s"))
            return false;

        var ret = false;
        for (var idx = 0; idx < shaders.Length; ++idx)
        {
            var shader = shaders[idx];
            if (!tab.IsFilterMatch(shader))
                continue;

            using var t = ImUtf8.TreeNode($"{objectName} #{idx}");
            if (!t)
                continue;

            DrawShaderExportButton(tab, objectName, shader, idx);
            if (!disabled && tab.Shpk.Disassembled)
            {
                ImGui.SameLine();
                DrawShaderImportButton(tab, objectName, shaders, idx);
            }

            ret |= DrawShaderPackageResourceArray("Constant Buffers", "slot", true,  shader.Constants, false, true);
            ret |= DrawShaderPackageResourceArray("Samplers",         "slot", false, shader.Samplers,  false, true);
            if (!tab.Shpk.IsLegacy)
                ret |= DrawShaderPackageResourceArray("Textures", "slot", false, shader.Textures, false, true);
            ret |= DrawShaderPackageResourceArray("Unordered Access Views", "slot", true, shader.Uavs, false, true);

            if (shader.DeclaredInputs != 0)
                ImUtf8.TreeNode($"Declared Inputs: {shader.DeclaredInputs}", ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet).Dispose();
            if (shader.UsedInputs != 0)
                ImUtf8.TreeNode($"Used Inputs: {shader.UsedInputs}", ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet).Dispose();

            if (shader.AdditionalHeader.Length > 8)
            {
                using var t2 = ImUtf8.TreeNode($"Additional Header (Size: {shader.AdditionalHeader.Length})###AdditionalHeader");
                if (t2)
                    Widget.DrawHexViewer(shader.AdditionalHeader);
            }

            if (tab.Shpk.Disassembled)
                DrawRawDisassembly(shader);

            DrawShaderUsage(tab, shader);
        }

        return ret;
    }

    private static bool DrawShaderPackageResource(string slotLabel, bool withSize, ref Resource resource, bool hasFilter, bool disabled)
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

        var usedString = UsedComponentString(withSize, false, resource);
        if (usedString.Length > 0)
        {
            ImUtf8.TreeNode(hasFilter ? $"Globally Used: {usedString}" : $"Used: {usedString}",
                ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet).Dispose();
            if (hasFilter)
            {
                var filteredUsedString = UsedComponentString(withSize, true, resource);
                if (filteredUsedString.Length > 0)
                    ImUtf8.TreeNode($"Used within Filters: {filteredUsedString}", ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet)
                        .Dispose();
                else
                    ImUtf8.TreeNode("Unused within Filters"u8, ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet).Dispose();
            }
        }
        else
        {
            ImUtf8.TreeNode(hasFilter ? "Globally Unused"u8 : "Unused"u8, ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet).Dispose();
        }

        return ret;
    }

    private static bool DrawShaderPackageResourceArray(string arrayName, string slotLabel, bool withSize, Resource[] resources, bool hasFilter,
        bool disabled)
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
            using var t2   = ImUtf8.TreeNode(name, !disabled || buf.Used != null ? 0 : ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet);
            font.Pop();
            if (t2)
                ret |= DrawShaderPackageResource(slotLabel, withSize, ref buf, hasFilter, disabled);
        }

        return ret;
    }

    private static bool DrawMaterialParamLayoutHeader(string label)
    {
        using var font = ImRaii.PushFont(UiBuilder.MonoFont);
        var pos = ImGui.GetCursorScreenPos()
          + new Vector2(ImGui.CalcTextSize(label).X + 3 * ImGui.GetStyle().ItemInnerSpacing.X + ImGui.GetFrameHeight(),
                ImGui.GetStyle().FramePadding.Y);

        var ret = ImUtf8.CollapsingHeader(label);
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

        ImUtf8.Text(materialParams.HasValue
            ? $"Buffer size mismatch: {file.MaterialParamsSize} bytes ≠ {materialParams.Value.Size} registers ({materialParams.Value.Size << 4} bytes)"
            : $"Buffer size mismatch: {file.MaterialParamsSize} bytes, not a multiple of 16");
        return false;
    }

    private static bool DrawShaderPackageMaterialMatrix(ShpkTab tab, bool disabled)
    {
        ImUtf8.Text(tab.Shpk.Disassembled
            ? "Parameter positions (continuations are grayed out, globally unused values are red, unused values within filters are yellow):"
            : "Parameter positions (continuations are grayed out):");

        using var table = ImRaii.Table("##MaterialParamLayout", 5,
            ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg);
        if (!table)
            return false;

        ImGui.TableSetupColumn(string.Empty, ImGuiTableColumnFlags.WidthFixed, 40 * UiHelpers.Scale);
        ImGui.TableSetupColumn("x",          ImGuiTableColumnFlags.WidthFixed, 250 * UiHelpers.Scale);
        ImGui.TableSetupColumn("y",          ImGuiTableColumnFlags.WidthFixed, 250 * UiHelpers.Scale);
        ImGui.TableSetupColumn("z",          ImGuiTableColumnFlags.WidthFixed, 250 * UiHelpers.Scale);
        ImGui.TableSetupColumn("w",          ImGuiTableColumnFlags.WidthFixed, 250 * UiHelpers.Scale);
        ImGui.TableHeadersRow();

        var textColorStart = ImGui.GetColorU32(ImGuiCol.Text);

        var ret = false;
        for (var i = 0; i < tab.Matrix.GetLength(0); ++i)
        {
            ImGui.TableNextColumn();
            ImGui.TableHeader($"  [{i}]");
            for (var j = 0; j < 4; ++j)
            {
                var (name, tooltip, idx, colorType) = tab.Matrix[i, j];
                var color = textColorStart;
                if (!colorType.HasFlag(ShpkTab.ColorType.Used))
                    color = ImGuiUtil.HalfBlend(color, 0x80u); // Half red
                else if (!colorType.HasFlag(ShpkTab.ColorType.FilteredUsed))
                    color = ImGuiUtil.HalfBlend(color, 0x8080u); // Half yellow
                if (colorType.HasFlag(ShpkTab.ColorType.Continuation))
                    color = ImGuiUtil.HalfTransparent(color); // Half opacity
                using var _         = ImRaii.PushId(i * 4 + j);
                var       deletable = !disabled && idx >= 0;
                using (ImRaii.PushFont(UiBuilder.MonoFont, tooltip.Length > 0))
                {
                    using (ImRaii.PushColor(ImGuiCol.Text, color))
                    {
                        ImGui.TableNextColumn();
                        ImUtf8.Selectable(name);
                        if (deletable && ImGui.IsItemClicked(ImGuiMouseButton.Right) && ImGui.GetIO().KeyCtrl)
                        {
                            tab.Shpk.MaterialParams = tab.Shpk.MaterialParams.RemoveItems(idx);
                            ret                     = true;
                            tab.Update();
                        }
                    }

                    ImUtf8.HoverTooltip(tooltip);
                }

                if (deletable)
                    ImUtf8.HoverTooltip("\nControl + Right-Click to remove."u8);
            }
        }

        return ret;
    }

    private static void DrawShaderPackageMaterialDevkitExport(ShpkTab tab)
    {
        if (!ImUtf8.Button("Export globally unused parameters as material dev-kit file"u8))
            return;

        tab.FileDialog.OpenSavePicker("Export material dev-kit file", ".json", $"{Path.GetFileNameWithoutExtension(tab.FilePath)}.json",
            ".json", DoSave, null, false);
        return;

        void DoSave(bool success, string path)
        {
            if (!success)
                return;

            try
            {
                File.WriteAllText(path, tab.ExportDevkit().ToString());
            }
            catch (Exception e)
            {
                Penumbra.Messager.NotificationMessage(e, $"Could not export dev-kit for {Path.GetFileName(tab.FilePath)} to {path}.",
                    NotificationType.Error, false);
                return;
            }

            Penumbra.Messager.NotificationMessage(
                $"Material dev-kit file for {Path.GetFileName(tab.FilePath)} exported successfully to {Path.GetFileName(path)}.",
                NotificationType.Success, false);
        }
    }

    private static void DrawShaderPackageMisalignedParameters(ShpkTab tab)
    {
        using var t = ImUtf8.TreeNode("Misaligned / Overflowing Parameters"u8);
        if (!t)
            return;

        using var _ = ImRaii.PushFont(UiBuilder.MonoFont);
        foreach (var name in tab.MalformedParameters)
            ImUtf8.TreeNode(name, ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet).Dispose();
    }

    private static void DrawShaderPackageStartCombo(ShpkTab tab)
    {
        using var s = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, ImGui.GetStyle().ItemInnerSpacing);
        using (ImRaii.PushFont(UiBuilder.MonoFont))
        {
            ImGui.SetNextItemWidth(UiHelpers.Scale * 400);
            using var c = ImUtf8.Combo("##Start", tab.Orphans[tab.NewMaterialParamStart].Name);
            if (c)
                foreach (var (start, idx) in tab.Orphans.WithIndex())
                {
                    if (ImGui.Selectable(start.Name, idx == tab.NewMaterialParamStart))
                        tab.UpdateOrphanStart(idx);
                }
        }

        ImGui.SameLine();
        ImUtf8.Text("Start"u8);
    }

    private static void DrawShaderPackageEndCombo(ShpkTab tab)
    {
        using var s = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, ImGui.GetStyle().ItemInnerSpacing);
        using (var _ = ImRaii.PushFont(UiBuilder.MonoFont))
        {
            ImGui.SetNextItemWidth(UiHelpers.Scale * 400);
            using var c = ImUtf8.Combo("##End", tab.Orphans[tab.NewMaterialParamEnd].Name);
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
        ImUtf8.Text("End"u8);
    }

    private static bool DrawShaderPackageNewParameter(ShpkTab tab)
    {
        if (tab.Orphans.Count == 0)
            return false;

        DrawShaderPackageStartCombo(tab);
        DrawShaderPackageEndCombo(tab);

        ImGui.SetNextItemWidth(UiHelpers.Scale * 400);
        var newName = tab.NewMaterialParamName.Value!;
        if (ImUtf8.InputText("Name", ref newName))
            tab.NewMaterialParamName = newName;

        var tooltip = tab.UsedIds.Contains(tab.NewMaterialParamName.Crc32)
            ? "The ID is already in use. Please choose a different name."u8
            : ""u8;
        if (!ImUtf8.ButtonEx($"Add {tab.NewMaterialParamName} (0x{tab.NewMaterialParamName.Crc32:X8})", tooltip,
                new Vector2(400 * UiHelpers.Scale, ImGui.GetFrameHeight()), tooltip.Length > 0))
            return false;

        tab.Shpk.MaterialParams = tab.Shpk.MaterialParams.AddItem(new MaterialParam
        {
            Id         = tab.NewMaterialParamName.Crc32,
            ByteOffset = (ushort)(tab.Orphans[tab.NewMaterialParamStart].Index << 2),
            ByteSize   = (ushort)((tab.NewMaterialParamEnd - tab.NewMaterialParamStart + 1) << 2),
        });
        tab.AddNameToCache(tab.NewMaterialParamName);
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

        if (tab.Shpk.Disassembled)
            DrawShaderPackageMaterialDevkitExport(tab);

        return ret;
    }

    private static bool DrawShaderPackageResources(ShpkTab tab, bool disabled)
    {
        var ret = false;

        if (!ImUtf8.CollapsingHeader("Shader Resources"u8))
            return false;

        var hasFilters = tab.FilterPopCount != tab.FilterMaximumPopCount;
        ret |= DrawShaderPackageResourceArray("Constant Buffers", "type", true,  tab.Shpk.Constants, hasFilters, disabled);
        ret |= DrawShaderPackageResourceArray("Samplers",         "type", false, tab.Shpk.Samplers,  hasFilters, disabled);
        if (!tab.Shpk.IsLegacy)
            ret |= DrawShaderPackageResourceArray("Textures", "type", false, tab.Shpk.Textures, hasFilters, disabled);
        ret |= DrawShaderPackageResourceArray("Unordered Access Views", "type", false, tab.Shpk.Uavs, hasFilters, disabled);

        return ret;
    }

    private static void DrawKeyArray(ShpkTab tab, string arrayName, bool withId, IReadOnlyCollection<Key> keys)
    {
        if (keys.Count == 0)
            return;

        using var t = ImUtf8.TreeNode(arrayName);
        if (!t)
            return;

        using var font = ImRaii.PushFont(UiBuilder.MonoFont);
        foreach (var (key, idx) in keys.WithIndex())
        {
            using var t2 = ImUtf8.TreeNode(withId ? $"#{idx}: {tab.TryResolveName(key.Id)} (0x{key.Id:X8})" : $"#{idx}");
            if (t2)
            {
                ImUtf8.TreeNode($"Default Value: {tab.TryResolveName(key.DefaultValue)} (0x{key.DefaultValue:X8})",
                    ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet).Dispose();
                ImUtf8.TreeNode($"Known Values: {tab.NameSetToString(key.Values, true)}", ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet)
                    .Dispose();
            }
        }
    }

    private static void DrawShaderPackageNodes(ShpkTab tab)
    {
        if (tab.Shpk.Nodes.Length <= 0)
            return;

        using var t = ImUtf8.TreeNode($"Nodes ({tab.Shpk.Nodes.Length})###Nodes");
        if (!t)
            return;

        using var font = ImRaii.PushFont(UiBuilder.MonoFont);

        foreach (var (node, idx) in tab.Shpk.Nodes.WithIndex())
        {
            if (!tab.IsFilterMatch(node))
                continue;

            using var t2 = ImUtf8.TreeNode($"#{idx:D4}: Selector: 0x{node.Selector:X8}");
            if (!t2)
                continue;

            foreach (var (key, keyIdx) in node.SystemKeys.WithIndex())
            {
                ImUtf8.TreeNode(
                    $"System Key {tab.TryResolveName(tab.Shpk.SystemKeys[keyIdx].Id)} = {tab.TryResolveName(key)} / \u2208 {{ {tab.NameSetToString(node.SystemValues![keyIdx])} }}",
                    ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet).Dispose();
            }

            foreach (var (key, keyIdx) in node.SceneKeys.WithIndex())
            {
                ImUtf8.TreeNode(
                    $"Scene Key {tab.TryResolveName(tab.Shpk.SceneKeys[keyIdx].Id)} = {tab.TryResolveName(key)} / \u2208 {{ {tab.NameSetToString(node.SceneValues![keyIdx])} }}",
                    ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet).Dispose();
            }

            foreach (var (key, keyIdx) in node.MaterialKeys.WithIndex())
            {
                ImUtf8.TreeNode(
                    $"Material Key {tab.TryResolveName(tab.Shpk.MaterialKeys[keyIdx].Id)} = {tab.TryResolveName(key)} / \u2208 {{ {tab.NameSetToString(node.MaterialValues![keyIdx])} }}",
                    ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet).Dispose();
            }

            foreach (var (key, keyIdx) in node.SubViewKeys.WithIndex())
            {
                ImUtf8.TreeNode(
                    $"Sub-View Key #{keyIdx} = {tab.TryResolveName(key)} / \u2208 {{ {tab.NameSetToString(node.SubViewValues![keyIdx])} }}",
                    ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet).Dispose();
            }

            ImUtf8.TreeNode($"Pass Indices: {string.Join(' ', node.PassIndices.Select(c => $"{c:X2}"))}",
                ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet).Dispose();
            foreach (var (pass, passIdx) in node.Passes.WithIndex())
            {
                ImUtf8.TreeNode(
                        $"Pass #{passIdx}: ID: {tab.TryResolveName(pass.Id)}, Vertex Shader #{pass.VertexShader}, Pixel Shader #{pass.PixelShader}",
                        ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet)
                    .Dispose();
            }
        }
    }

    private static void DrawShaderPackageSelection(ShpkTab tab)
    {
        if (!ImUtf8.CollapsingHeader("Shader Selection"u8))
            return;

        DrawKeyArray(tab, "System Keys",   true,  tab.Shpk.SystemKeys);
        DrawKeyArray(tab, "Scene Keys",    true,  tab.Shpk.SceneKeys);
        DrawKeyArray(tab, "Material Keys", true,  tab.Shpk.MaterialKeys);
        DrawKeyArray(tab, "Sub-View Keys", false, tab.Shpk.SubViewKeys);

        DrawShaderPackageNodes(tab);
        using var t = ImUtf8.TreeNode($"Node Selectors ({tab.Shpk.NodeSelectors.Count})###NodeSelectors");
        if (t)
        {
            using var font = ImRaii.PushFont(UiBuilder.MonoFont);
            foreach (var selector in tab.Shpk.NodeSelectors)
            {
                ImUtf8.TreeNode($"#{selector.Value:D4}: Selector: 0x{selector.Key:X8}", ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet)
                    .Dispose();
            }
        }
    }

    private static void DrawOtherShaderPackageDetails(ShpkTab tab)
    {
        if (!ImUtf8.CollapsingHeader("Further Content"u8))
            return;

        ImUtf8.TreeNode($"Version: 0x{tab.Shpk.Version:X8}", ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet).Dispose();

        if (tab.Shpk.AdditionalData.Length > 0)
        {
            using var t = ImUtf8.TreeNode($"Additional Data (Size: {tab.Shpk.AdditionalData.Length})###AdditionalData");
            if (t)
                Widget.DrawHexViewer(tab.Shpk.AdditionalData);
        }
    }

    private static string UsedComponentString(bool withSize, bool filtered, in Resource resource)
    {
        var used            = filtered ? resource.FilteredUsed : resource.Used;
        var usedDynamically = filtered ? resource.FilteredUsedDynamically : resource.UsedDynamically;
        var sb              = new StringBuilder(256);
        if (withSize)
        {
            foreach (var (components, i) in (used ?? Array.Empty<DisassembledShader.VectorComponents>()).WithIndex())
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

            switch (usedDynamically ?? 0)
            {
                case 0: break;
                case DisassembledShader.VectorComponents.All:
                    sb.Append("[*], ");
                    break;
                default:
                    sb.Append("[*].");
                    foreach (var c in usedDynamically!.Value.ToString().Where(char.IsUpper))
                        sb.Append(char.ToLower(c));

                    sb.Append(", ");
                    break;
            }
        }
        else
        {
            var components = (used is { Length: > 0 } ? used[0] : 0) | (usedDynamically ?? 0);
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

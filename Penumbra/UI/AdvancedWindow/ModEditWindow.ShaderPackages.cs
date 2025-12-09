using Dalamud.Interface.ImGuiNotification;
using ImSharp;
using Luna;
using Penumbra.GameData;
using Penumbra.GameData.Files;
using Penumbra.GameData.Interop;
using static Penumbra.GameData.Files.ShpkFile;
using Penumbra.GameData.Structs;

namespace Penumbra.UI.AdvancedWindow;

public partial class ModEditWindow
{
    private static readonly StringU8 DisassemblyLabel = new("##disassembly"u8);

    private readonly FileEditor<ShpkTab> _shaderPackageTab;

    private static bool DrawShaderPackagePanel(ShpkTab file, bool disabled)
    {
        var dummyHeight = new Vector2(Im.Style.TextHeight / 2);
        DrawShaderPackageSummary(file);

        Im.Dummy(dummyHeight);
        DrawShaderPackageFilterSection(file);

        var ret = false;
        Im.Dummy(dummyHeight);
        ret |= DrawShaderPackageShaderArray(file, "Vertex Shader", file.Shpk.VertexShaders, disabled);

        Im.Dummy(dummyHeight);
        ret |= DrawShaderPackageShaderArray(file, "Pixel Shader", file.Shpk.PixelShaders, disabled);

        Im.Dummy(dummyHeight);
        ret |= DrawShaderPackageMaterialParamLayout(file, disabled);

        Im.Dummy(dummyHeight);
        ret |= DrawShaderPackageResources(file, disabled);

        Im.Dummy(dummyHeight);
        DrawShaderPackageSelection(file);

        Im.Dummy(dummyHeight);
        DrawOtherShaderPackageDetails(file);

        ret |= file.Shpk.IsChanged();

        return !disabled && ret;
    }

    private static void DrawShaderPackageSummary(ShpkTab tab)
    {
        if (tab.Shpk.IsLegacy)
            Im.Text("This legacy shader package will not work in the current version of the game. Do not attempt to load it."u8,
                ImGuiColor.Text.Get().HalfBlend(new Rgba32(0x80)));
        Im.Text(tab.Header);
        if (!tab.Shpk.Disassembled)
            Im.Text("Your system doesn't support disassembling shaders. Some functionality will be missing."u8,
                ImGuiColor.Text.Get().HalfBlend(new Rgba32(0x80))); // Half red
    }

    private static void DrawShaderExportButton(ShpkTab tab, string objectName, Shader shader, int idx)
    {
        if (!Im.Button($"Export Shader Program Blob ({shader.Blob.Length} bytes)"))
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
        if (!Im.Button("Replace Shader Program Blob"u8))
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
        using var tree = Im.Tree.Node("Raw Program Disassembly"u8);
        if (!tree)
            return;

        using var font = Im.Font.PushMono();
        var       size = Im.ContentRegion.Available with { Y = Im.Style.TextHeight * 20 };
        Im.Input.MultiLine(DisassemblyLabel,
            new Span<byte>(shader.Disassembly!.RawDisassembly.Path, shader.Disassembly.RawDisassembly.Length + 1), out ulong _, size,
            InputTextFlags.ReadOnly);
    }

    private static void DrawShaderUsage(ShpkTab tab, Shader shader)
    {
        using (var node = Im.Tree.Node("Used with Shader Keys"u8))
        {
            if (node)
            {
                foreach (var (keyIdx, key) in shader.SystemValues!.Index())
                {
                    Im.Tree.Leaf(
                        $"Used with System Key {tab.TryResolveName(tab.Shpk.SystemKeys[keyIdx].Id)} \u2208 {{ {tab.NameSetToString(key)} }}");
                }

                foreach (var (keyIdx, key) in shader.SceneValues!.Index())
                {
                    Im.Tree.Leaf(
                        $"Used with Scene Key {tab.TryResolveName(tab.Shpk.SceneKeys[keyIdx].Id)} \u2208 {{ {tab.NameSetToString(key)} }}");
                }

                foreach (var (keyIdx, key) in shader.MaterialValues!.Index())
                {
                    Im.Tree.Leaf(
                        $"Used with Material Key {tab.TryResolveName(tab.Shpk.MaterialKeys[keyIdx].Id)} \u2208 {{ {tab.NameSetToString(key)} }}");
                }

                foreach (var (keyIdx, key) in shader.SubViewValues!.Index())
                    Im.Tree.Leaf($"Used with Sub-View Key #{keyIdx} \u2208 {{ {tab.NameSetToString(key)} }}");
            }
        }

        Im.Tree.Leaf($"Used in Passes: {tab.NameSetToString(shader.Passes)}");
    }

    private static void DrawShaderPackageFilterSection(ShpkTab tab)
    {
        if (!Im.Tree.Header(tab.FilterPopCount == tab.FilterMaximumPopCount ? "Filters###Filters"u8 : "Filters (ACTIVE)###Filters"u8))
            return;

        foreach (var (keyIdx, key) in tab.Shpk.SystemKeys.Index())
            DrawShaderPackageFilterSet(tab, $"System Key {tab.TryResolveName(key.Id)}", ref tab.FilterSystemValues[keyIdx]);

        foreach (var (keyIdx, key) in tab.Shpk.SceneKeys.Index())
            DrawShaderPackageFilterSet(tab, $"Scene Key {tab.TryResolveName(key.Id)}", ref tab.FilterSceneValues[keyIdx]);

        foreach (var (keyIdx, key) in tab.Shpk.MaterialKeys.Index())
            DrawShaderPackageFilterSet(tab, $"Material Key {tab.TryResolveName(key.Id)}", ref tab.FilterMaterialValues[keyIdx]);

        foreach (var (keyIdx, _) in tab.Shpk.SubViewKeys.Index())
            DrawShaderPackageFilterSet(tab, $"Sub-View Key #{keyIdx}", ref tab.FilterSubViewValues[keyIdx]);

        DrawShaderPackageFilterSet(tab, "Passes"u8, ref tab.FilterPasses);
    }

    private static void DrawShaderPackageFilterSet(ShpkTab tab, Utf8StringHandler<LabelStringHandlerBuffer> label,
        ref SharedSet<uint, uint> values)
    {
        if (values.PossibleValues is null)
        {
            Im.Tree.Leaf(label);
            return;
        }

        using var node = Im.Tree.Node(label);
        if (!node)
            return;

        foreach (var value in values.PossibleValues)
        {
            var contains = values.Contains(value);
            if (!Im.Checkbox($"{tab.TryResolveName(value)}", ref contains))
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
        if (shaders.Length is 0 || !Im.Tree.Header($"{objectName}s"))
            return false;

        var ret = false;
        for (var idx = 0; idx < shaders.Length; ++idx)
        {
            var shader = shaders[idx];
            if (!tab.IsFilterMatch(shader))
                continue;

            using var t = Im.Tree.Node($"{objectName} #{idx}");
            if (!t)
                continue;

            DrawShaderExportButton(tab, objectName, shader, idx);
            if (!disabled && tab.Shpk.Disassembled)
            {
                Im.Line.Same();
                DrawShaderImportButton(tab, objectName, shaders, idx);
            }

            ret |= DrawShaderPackageResourceArray("Constant Buffers"u8, "slot", true,  shader.Constants, false, true);
            ret |= DrawShaderPackageResourceArray("Samplers"u8,         "slot", false, shader.Samplers,  false, true);
            if (!tab.Shpk.IsLegacy)
                ret |= DrawShaderPackageResourceArray("Textures"u8, "slot", false, shader.Textures, false, true);
            ret |= DrawShaderPackageResourceArray("Unordered Access Views"u8, "slot", true, shader.Uavs, false, true);

            if (shader.DeclaredInputs is not 0)
                Im.Tree.Leaf($"Declared Inputs: {shader.DeclaredInputs}");
            if (shader.UsedInputs is not 0)
                Im.Tree.Leaf($"Used Inputs: {shader.UsedInputs}");

            if (shader.AdditionalHeader.Length > 8)
            {
                using var t2 = Im.Tree.Node($"Additional Header (Size: {shader.AdditionalHeader.Length})###AdditionalHeader");
                if (t2)
                    ImEx.HexViewer(shader.AdditionalHeader);
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
            Im.Item.SetNextWidth(Im.Style.GlobalScale * 150.0f);
            if (Im.Input.Scalar($"{char.ToUpper(slotLabel[0])}{slotLabel[1..].ToLower()}", ref resource.Slot))
                ret = true;
        }

        if (resource.Used is null)
            return ret;

        var usedString = UsedComponentString(withSize, false, resource);
        if (usedString.Length > 0)
        {
            Im.Tree.Leaf(hasFilter ? $"Globally Used: {usedString}" : $"Used: {usedString}");
            if (hasFilter)
            {
                var filteredUsedString = UsedComponentString(withSize, true, resource);
                if (filteredUsedString.Length > 0)
                    Im.Tree.Leaf($"Used within Filters: {filteredUsedString}");
                else
                    Im.Tree.Leaf("Unused within Filters"u8);
            }
        }
        else
        {
            Im.Tree.Leaf(hasFilter ? "Globally Unused"u8 : "Unused"u8);
        }

        return ret;
    }

    private static bool DrawShaderPackageResourceArray(ReadOnlySpan<byte> arrayName, string slotLabel, bool withSize, Resource[] resources,
        bool hasFilter,
        bool disabled)
    {
        if (resources.Length is 0)
            return false;

        using var t = Im.Tree.Node(arrayName);
        if (!t)
            return false;

        var ret = false;
        for (var idx = 0; idx < resources.Length; ++idx)
        {
            ref var buf = ref resources[idx];
            var name = $"#{idx}: {buf.Name} (ID: 0x{buf.Id:X8}), {slotLabel}: {buf.Slot}"
              + (withSize ? $", size: {buf.Size} registers###{idx}: {buf.Name} (ID: 0x{buf.Id:X8})" : string.Empty);
            using var font = Im.Font.PushMono();
            using var t2   = Im.Tree.Node(name, !disabled || buf.Used is not null ? 0 : TreeNodeFlags.Leaf | TreeNodeFlags.Bullet);
            font.Pop();
            if (t2)
                ret |= DrawShaderPackageResource(slotLabel, withSize, ref buf, hasFilter, disabled);
        }

        return ret;
    }

    private static bool DrawMaterialParamLayoutHeader(Utf8StringHandler<LabelStringHandlerBuffer> label)
    {
        using var font = Im.Font.PushMono();
        var pos = Im.Cursor.ScreenPosition
          + new Vector2(Im.Font.CalculateSize(ref label).X + 3 * Im.Style.ItemInnerSpacing.X + Im.Style.FrameHeight,
                Im.Style.FramePadding.Y);

        var ret = Im.Tree.Header(label);
        Im.Window.DrawList.Text(Im.Font.Default, Im.Font.Default.Size, pos, ImGuiColor.Text.Get().Color, "Layout"u8);
        return ret;
    }

    private static bool DrawMaterialParamLayoutBufferSize(ShpkFile file, Resource? materialParams)
    {
        var isSizeWellDefined = (file.MaterialParamsSize & 0xF) is 0
         && (!materialParams.HasValue || file.MaterialParamsSize == materialParams.Value.Size << 4);
        if (isSizeWellDefined)
            return true;

        Im.Text(materialParams.HasValue
            ? $"Buffer size mismatch: {file.MaterialParamsSize} bytes ≠ {materialParams.Value.Size} registers ({materialParams.Value.Size << 4} bytes)"
            : $"Buffer size mismatch: {file.MaterialParamsSize} bytes, not a multiple of 16");
        return false;
    }

    private static bool DrawShaderPackageMaterialMatrix(ShpkTab tab, bool disabled)
    {
        Im.Text(tab.Shpk.Disassembled
            ? "Parameter positions (continuations are grayed out, globally unused values are red, unused values within filters are yellow):"u8
            : "Parameter positions (continuations are grayed out):"u8);

        using var table = Im.Table.Begin("##MaterialParamLayout"u8, 5,
            TableFlags.SizingFixedFit | TableFlags.RowBackground);
        if (!table)
            return false;

        table.SetupColumn(StringU8.Empty, TableColumnFlags.WidthFixed, 40 * Im.Style.GlobalScale);
        table.SetupColumn("x"u8,          TableColumnFlags.WidthFixed, 250 * Im.Style.GlobalScale);
        table.SetupColumn("y"u8,          TableColumnFlags.WidthFixed, 250 * Im.Style.GlobalScale);
        table.SetupColumn("z"u8,          TableColumnFlags.WidthFixed, 250 * Im.Style.GlobalScale);
        table.SetupColumn("w"u8,          TableColumnFlags.WidthFixed, 250 * Im.Style.GlobalScale);
        table.HeaderRow();

        var textColorStart = ImGuiColor.Text.Get();

        var ret = false;
        for (var i = 0; i < tab.Matrix.GetLength(0); ++i)
        {
            table.NextColumn();
            table.Header($"  [{i}]");
            for (var j = 0; j < 4; ++j)
            {
                var (name, tooltip, idx, colorType) = tab.Matrix[i, j];
                var color = textColorStart;
                if (!colorType.HasFlag(ShpkTab.ColorType.Used))
                    color = color.HalfBlend(new Rgba32(0x80)); // Half red
                else if (!colorType.HasFlag(ShpkTab.ColorType.FilteredUsed))
                    color = color.HalfBlend(0x8080u); // Half yellow
                if (colorType.HasFlag(ShpkTab.ColorType.Continuation))
                    color = color.HalfTransparent(); // Half opacity
                using var _         = Im.Id.Push(i * 4 + j);
                var       deletable = !disabled && idx >= 0;
                using (Im.Font.Mono.Push(tooltip.Length > 0))
                {
                    using (ImGuiColor.Text.Push(color))
                    {
                        table.NextColumn();
                        Im.Selectable(name);
                        if (deletable && Im.Item.RightClicked() && Im.Io.KeyControl)
                        {
                            tab.Shpk.MaterialParams = tab.Shpk.MaterialParams.RemoveItems(idx);
                            ret                     = true;
                            tab.Update();
                        }
                    }

                    Im.Tooltip.OnHover(tooltip);
                }

                if (deletable)
                    Im.Tooltip.OnHover("\nControl + Right-Click to remove."u8);
            }
        }

        return ret;
    }

    private static void DrawShaderPackageMaterialDevkitExport(ShpkTab tab)
    {
        if (!Im.Button("Export globally unused parameters as material dev-kit file"u8))
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
        using var t = Im.Tree.Node("Misaligned / Overflowing Parameters"u8);
        if (!t)
            return;

        using var _ = Im.Font.PushMono();
        foreach (var name in tab.MalformedParameters)
            Im.Tree.Leaf(name);
    }

    private static void DrawShaderPackageStartCombo(ShpkTab tab)
    {
        using var s = ImStyleDouble.ItemSpacing.Push(Im.Style.ItemInnerSpacing);
        using (Im.Font.PushMono())
        {
            Im.Item.SetNextWidth(Im.Style.GlobalScale * 400);
            using var c = Im.Combo.Begin("##Start"u8, tab.Orphans[tab.NewMaterialParamStart].Name);
            if (c)
                foreach (var (idx, start) in tab.Orphans.Index())
                {
                    if (Im.Selectable(start.Name, idx == tab.NewMaterialParamStart))
                        tab.UpdateOrphanStart(idx);
                }
        }

        Im.Line.Same();
        Im.Text("Start"u8);
    }

    private static void DrawShaderPackageEndCombo(ShpkTab tab)
    {
        using var s = ImStyleDouble.ItemSpacing.Push(Im.Style.ItemInnerSpacing);
        using (Im.Font.PushMono())
        {
            Im.Item.SetNextWidth(Im.Style.GlobalScale * 400);
            using var c = Im.Combo.Begin("##End"u8, tab.Orphans[tab.NewMaterialParamEnd].Name);
            if (c)
            {
                var current = tab.Orphans[tab.NewMaterialParamStart].Index;
                for (var i = tab.NewMaterialParamStart; i < tab.Orphans.Count; ++i)
                {
                    var next = tab.Orphans[i];
                    if (current++ != next.Index)
                        break;

                    if (Im.Selectable(next.Name, i == tab.NewMaterialParamEnd))
                        tab.NewMaterialParamEnd = i;
                }
            }
        }

        Im.Line.Same();
        Im.Text("End"u8);
    }

    private static bool DrawShaderPackageNewParameter(ShpkTab tab)
    {
        if (tab.Orphans.Count is 0)
            return false;

        DrawShaderPackageStartCombo(tab);
        DrawShaderPackageEndCombo(tab);

        Im.Item.SetNextWidth(Im.Style.GlobalScale * 400);
        var newName = tab.NewMaterialParamName.Value!;
        if (Im.Input.Text("Name"u8, ref newName))
            tab.NewMaterialParamName = newName;

        var tooltip = tab.UsedIds.Contains(tab.NewMaterialParamName.Crc32)
            ? "The ID is already in use. Please choose a different name."u8
            : ""u8;
        if (!ImEx.Button($"Add {tab.NewMaterialParamName} (0x{tab.NewMaterialParamName.Crc32:X8})",
                new Vector2(400 * Im.Style.GlobalScale, Im.Style.FrameHeight),
                tooltip, tooltip.Length > 0))
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

        if (!Im.Tree.Header("Shader Resources"u8))
            return false;

        var hasFilters = tab.FilterPopCount != tab.FilterMaximumPopCount;
        ret |= DrawShaderPackageResourceArray("Constant Buffers"u8, "type", true,  tab.Shpk.Constants, hasFilters, disabled);
        ret |= DrawShaderPackageResourceArray("Samplers"u8,         "type", false, tab.Shpk.Samplers,  hasFilters, disabled);
        if (!tab.Shpk.IsLegacy)
            ret |= DrawShaderPackageResourceArray("Textures"u8, "type", false, tab.Shpk.Textures, hasFilters, disabled);
        ret |= DrawShaderPackageResourceArray("Unordered Access Views"u8, "type", false, tab.Shpk.Uavs, hasFilters, disabled);

        return ret;
    }

    private static void DrawKeyArray(ShpkTab tab, ReadOnlySpan<byte> arrayName, bool withId, IReadOnlyCollection<ShpkFile.Key> keys)
    {
        if (keys.Count is 0)
            return;

        using var t = Im.Tree.Node(arrayName);
        if (!t)
            return;

        using var font = Im.Font.PushMono();
        foreach (var (idx, key) in keys.Index())
        {
            using var t2 = Im.Tree.Node(withId ? $"#{idx}: {tab.TryResolveName(key.Id)} (0x{key.Id:X8})" : $"#{idx}");
            if (t2)
            {
                Im.Tree.Leaf($"Default Value: {tab.TryResolveName(key.DefaultValue)} (0x{key.DefaultValue:X8})");
                Im.Tree.Leaf($"Known Values: {tab.NameSetToString(key.Values, true)}");
            }
        }
    }

    private static void DrawShaderPackageNodes(ShpkTab tab)
    {
        if (tab.Shpk.Nodes.Length <= 0)
            return;

        using var t = Im.Tree.Node($"Nodes ({tab.Shpk.Nodes.Length})###Nodes");
        if (!t)
            return;

        using var font = Im.Font.PushMono();

        foreach (var (idx, node) in tab.Shpk.Nodes.Index())
        {
            if (!tab.IsFilterMatch(node))
                continue;

            using var t2 = Im.Tree.Node($"#{idx:D4}: Selector: 0x{node.Selector:X8}");
            if (!t2)
                continue;

            foreach (var (keyIdx, key) in node.SystemKeys.Index())
            {
                Im.Tree.Leaf(
                    $"System Key {tab.TryResolveName(tab.Shpk.SystemKeys[keyIdx].Id)} = {tab.TryResolveName(key)} / \u2208 {{ {tab.NameSetToString(node.SystemValues![keyIdx])} }}");
            }

            foreach (var (keyIdx, key) in node.SceneKeys.Index())
            {
                Im.Tree.Leaf(
                    $"Scene Key {tab.TryResolveName(tab.Shpk.SceneKeys[keyIdx].Id)} = {tab.TryResolveName(key)} / \u2208 {{ {tab.NameSetToString(node.SceneValues![keyIdx])} }}");
            }

            foreach (var (keyIdx, key) in node.MaterialKeys.Index())
            {
                Im.Tree.Leaf(
                    $"Material Key {tab.TryResolveName(tab.Shpk.MaterialKeys[keyIdx].Id)} = {tab.TryResolveName(key)} / \u2208 {{ {tab.NameSetToString(node.MaterialValues![keyIdx])} }}");
            }

            foreach (var (keyIdx, key) in node.SubViewKeys.Index())
            {
                Im.Tree.Leaf(
                    $"Sub-View Key #{keyIdx} = {tab.TryResolveName(key)} / \u2208 {{ {tab.NameSetToString(node.SubViewValues![keyIdx])} }}");
            }

            Im.Tree.Leaf($"Pass Indices: {string.Join(' ', node.PassIndices.Select(c => $"{c:X2}"))}");
            foreach (var (passIdx, pass) in node.Passes.Index())
            {
                Im.Tree.Leaf(
                    $"Pass #{passIdx}: ID: {tab.TryResolveName(pass.Id)}, Vertex Shader #{pass.VertexShader}, Pixel Shader #{pass.PixelShader}");
            }
        }
    }

    private static void DrawShaderPackageSelection(ShpkTab tab)
    {
        if (!Im.Tree.Header("Shader Selection"u8))
            return;

        DrawKeyArray(tab, "System Keys"u8,   true,  tab.Shpk.SystemKeys);
        DrawKeyArray(tab, "Scene Keys"u8,    true,  tab.Shpk.SceneKeys);
        DrawKeyArray(tab, "Material Keys"u8, true,  tab.Shpk.MaterialKeys);
        DrawKeyArray(tab, "Sub-View Keys"u8, false, tab.Shpk.SubViewKeys);

        DrawShaderPackageNodes(tab);
        using var t = Im.Tree.Node($"Node Selectors ({tab.Shpk.NodeSelectors.Count})###NodeSelectors");
        if (t)
        {
            using var font = Im.Font.PushMono();
            foreach (var selector in tab.Shpk.NodeSelectors)
                Im.Tree.Leaf($"#{selector.Value:D4}: Selector: 0x{selector.Key:X8}");
        }
    }

    private static void DrawOtherShaderPackageDetails(ShpkTab tab)
    {
        if (!Im.Tree.Header("Further Content"u8))
            return;

        Im.Tree.Leaf($"Version: 0x{tab.Shpk.Version:X8}");

        if (tab.Shpk.AdditionalData.Length > 0)
        {
            using var t = Im.Tree.Node($"Additional Data (Size: {tab.Shpk.AdditionalData.Length})###AdditionalData");
            if (t)
                ImEx.HexViewer(tab.Shpk.AdditionalData);
        }
    }

    private static string UsedComponentString(bool withSize, bool filtered, in Resource resource)
    {
        var used            = filtered ? resource.FilteredUsed : resource.Used;
        var usedDynamically = filtered ? resource.FilteredUsedDynamically : resource.UsedDynamically;
        var sb              = new StringBuilder(256);
        if (withSize)
        {
            foreach (var (i, components) in (used ?? []).Index())
            {
                switch (components)
                {
                    case 0:                                       break;
                    case DisassembledShader.VectorComponents.All: sb.Append($"[{i}], "); break;
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
                case 0:                                       break;
                case DisassembledShader.VectorComponents.All: sb.Append("[*], "); break;
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

        return sb.Length is 0 ? string.Empty : sb.ToString(0, sb.Length - 2);
    }
}

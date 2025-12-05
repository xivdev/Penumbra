using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using ImSharp;
using Lumina.Data.Parsing;
using Luna;
using OtterGui;
using OtterGui.Custom;
using OtterGui.Raii;
using OtterGui.Text;
using OtterGui.Widgets;
using Penumbra.GameData;
using Penumbra.GameData.Files;
using Penumbra.Import.Models;
using Penumbra.Import.Models.Import;
using Penumbra.String.Classes;
using Penumbra.UI.Classes;

namespace Penumbra.UI.AdvancedWindow;

public partial class ModEditWindow
{
    private const int MdlMaterialMaximum = ModelImporter.MaterialLimit;

    private const string MdlImportDocumentation =
        @"https://github.com/xivdev/Penumbra/wiki/Model-IO#user-content-9b49d296-23ab-410a-845b-a3be769b71ea";

    private const string MdlExportDocumentation =
        @"https://github.com/xivdev/Penumbra/wiki/Model-IO#user-content-25968400-ebe5-4861-b610-cb1556db7ec4";

    private readonly FileEditor<MdlTab> _modelTab;
    private readonly ModelManager       _models;

    private class LoadedData
    {
        public          MdlFile          LastFile             = null!;
        public readonly List<TagButtons> SubMeshAttributeTags = [];
        public          long[]           LodTriCount          = [];
    }

    private string _modelNewMaterial = string.Empty;

    private readonly LoadedData _main    = new();
    private readonly LoadedData _preview = new();

    private string       _customPath     = string.Empty;
    private Utf8GamePath _customGamePath = Utf8GamePath.Empty;


    private LoadedData UpdateFile(MdlFile file, bool force, bool disabled)
    {
        var data = disabled ? _preview : _main;
        if (file == data.LastFile && !force)
            return data;

        data.LastFile = file;
        var subMeshTotal = file.Meshes.Aggregate(0, (count, mesh) => count + mesh.SubMeshCount);
        if (data.SubMeshAttributeTags.Count != subMeshTotal)
        {
            data.SubMeshAttributeTags.Clear();
            data.SubMeshAttributeTags.AddRange(
                Enumerable.Range(0, subMeshTotal).Select(_ => new TagButtons())
            );
        }

        data.LodTriCount = Enumerable.Range(0, file.Lods.Length).Select(l => GetTriangleCountForLod(file, l)).ToArray();
        return data;
    }

    private bool DrawModelPanel(MdlTab tab, bool disabled)
    {
        var ret  = tab.Dirty;
        var data = UpdateFile(tab.Mdl, ret, disabled);
        DrawVersionUpdate(tab, disabled);
        DrawImportExport(tab, disabled);

        ret |= DrawModelMaterialDetails(tab, disabled);

        if (ImGui.CollapsingHeader($"Meshes ({data.LastFile.Meshes.Length})###meshes"))
            for (var i = 0; i < data.LastFile.LodCount; ++i)
                ret |= DrawModelLodDetails(tab, i, disabled);

        ret |= DrawOtherModelDetails(data);

        return !disabled && ret;
    }

    private void DrawVersionUpdate(MdlTab tab, bool disabled)
    {
        if (disabled || tab.Mdl.Version is not MdlFile.V5)
            return;

        if (!ImUtf8.ButtonEx("Update MDL Version from V5 to V6"u8,
                "Try using this if the bone weights of a pre-Dawntrail model seem wrong.\n\nThis is not revertible."u8,
                new Vector2(-0.1f, 0), false, 0, new Rgba32(Colors.PressEnterWarningBg).Color))
            return;

        tab.Mdl.ConvertV5ToV6();
        _modelTab.SaveFile();
    }

    private void DrawImportExport(MdlTab tab, bool disabled)
    {
        if (!ImGui.CollapsingHeader("Import / Export"))
            return;

        var childSize = new Vector2((Im.ContentRegion.Available.X - Im.Style.ItemSpacing.X) / 2, 0);

        DrawImport(tab, childSize, disabled);
        Im.Line.Same();
        DrawExport(tab, childSize, disabled);

        DrawIoExceptions(tab);
        DrawIoWarnings(tab);
    }

    private void DrawImport(MdlTab tab, Vector2 size, bool _1)
    {
        using var id = ImRaii.PushId("import");

        _dragDropManager.CreateImGuiSource("ModelDragDrop",
            m => m.Extensions.Any(e => ValidModelExtensions.Contains(e.ToLowerInvariant())), m =>
            {
                if (!GetFirstModel(m.Files, out var file))
                    return false;

                Im.Text($"Dragging model for editing: {Path.GetFileName(file)}");
                return true;
            });

        using (ImRaii.FramedGroup("Import", size, headerPreIcon: FontAwesomeIcon.FileImport))
        {
            ImGui.Checkbox("Keep current materials",  ref tab.ImportKeepMaterials);
            ImGui.Checkbox("Keep current attributes", ref tab.ImportKeepAttributes);

            if (ImGuiUtil.DrawDisabledButton("Import from glTF", Vector2.Zero, "Imports a glTF file, overriding the content of this mdl.",
                    tab.PendingIo))
                _fileDialog.OpenFilePicker("Load model from glTF.", "glTF{.gltf,.glb}", (success, paths) =>
                {
                    if (success && paths.Count > 0)
                        tab.Import(paths[0]);
                }, 1, Mod!.ModPath.FullName, false);

            Im.Line.Same();
            DrawDocumentationLink(MdlImportDocumentation);
        }

        if (_dragDropManager.CreateImGuiTarget("ModelDragDrop", out var files, out _) && GetFirstModel(files, out var importFile))
            tab.Import(importFile);
    }

    private void DrawExport(MdlTab tab, Vector2 size, bool _)
    {
        using var id    = ImRaii.PushId("export");
        using var frame = ImRaii.FramedGroup("Export", size, headerPreIcon: FontAwesomeIcon.FileExport);

        if (tab.GamePaths == null)
        {
            Im.Text(tab.IoExceptions.Count is 0 ? "Resolving model game paths."u8 : "Failed to resolve model game paths."u8);

            return;
        }

        DrawGamePathCombo(tab);

        ImGui.Checkbox("##exportGeneratedMissingBones", ref tab.ExportConfig.GenerateMissingBones);
        Im.Line.Same();
        ImGuiUtil.LabeledHelpMarker("Generate missing bones",
            "WARNING: Enabling this option can result in unusable exported meshes.\n"
          + "It is primarily intended to allow exporting models weighted to bones that do not exist.\n"
          + "Before enabling, ensure dependencies are enabled in the current collection, and EST metadata is correctly configured.");

        var gamePath = tab.GamePathIndex >= 0 && tab.GamePathIndex < tab.GamePaths.Count
            ? tab.GamePaths[tab.GamePathIndex]
            : _customGamePath;

        if (ImGuiUtil.DrawDisabledButton("Export to glTF", Vector2.Zero, "Exports this mdl file to glTF, for use in 3D authoring applications.",
                tab.PendingIo || gamePath.IsEmpty))
            _fileDialog.OpenSavePicker("Save model as glTF.", ".glb", Path.GetFileNameWithoutExtension(gamePath.Filename().ToString()),
                ".glb", (valid, path) =>
                {
                    if (!valid)
                        return;

                    tab.Export(path, gamePath);
                },
                Mod!.ModPath.FullName,
                false
            );

        Im.Line.Same();
        DrawDocumentationLink(MdlExportDocumentation);
    }

    private static void DrawIoExceptions(MdlTab tab)
    {
        if (tab.IoExceptions.Count == 0)
            return;

        var size = new Vector2(Im.ContentRegion.Available.X, 0);
        using var frame = ImRaii.FramedGroup("Exceptions", size, headerPreIcon: FontAwesomeIcon.TimesCircle,
            borderColor: new Rgba32(Colors.RegexWarningBorder).Color);

        var spaceAvail = Im.ContentRegion.Available.X - Im.Style.ItemSpacing.X - 100;
        foreach (var (index, exception) in tab.IoExceptions.Index())
        {
            using var id       = ImRaii.PushId(index);
            var       message  = $"{exception.GetType().Name}: {exception.Message}";
            var       textSize = ImGui.CalcTextSize(message).X;
            if (textSize > spaceAvail)
                message = message[..(int)Math.Floor(message.Length * (spaceAvail / textSize))] + "...";

            using var exceptionNode = ImRaii.TreeNode(message);
            if (exceptionNode)
            {
                using var indent = ImRaii.PushIndent();
                ImGuiUtil.TextWrapped(exception.ToString());
            }
        }
    }

    private static void DrawIoWarnings(MdlTab tab)
    {
        if (tab.IoWarnings.Count == 0)
            return;

        var       size  = new Vector2(Im.ContentRegion.Available.X, 0);
        using var frame = ImRaii.FramedGroup("Warnings", size, headerPreIcon: FontAwesomeIcon.ExclamationCircle, borderColor: 0xFF40FFFF);

        var spaceAvail = Im.ContentRegion.Available.X - Im.Style.ItemSpacing.X - 100;
        foreach (var (index, warning) in tab.IoWarnings.Index())
        {
            using var id       = ImRaii.PushId(index);
            var       textSize = ImGui.CalcTextSize(warning).X;

            if (textSize <= spaceAvail)
            {
                ImRaii.TreeNode(warning, ImGuiTreeNodeFlags.Leaf).Dispose();
                continue;
            }

            var firstLine = warning[..(int)Math.Floor(warning.Length * (spaceAvail / textSize))] + "...";

            using var warningNode = ImRaii.TreeNode(firstLine);
            if (warningNode)
            {
                using var indent = ImRaii.PushIndent();
                ImGuiUtil.TextWrapped(warning);
            }
        }
    }

    private void DrawGamePathCombo(MdlTab tab)
    {
        if (tab.GamePaths!.Count != 0)
        {
            DrawComboButton(tab);
            return;
        }

        Im.Text("No associated game path detected. Valid game paths are currently necessary for exporting."u8);
        if (!ImGui.InputTextWithHint("##customInput", "Enter custom game path...", ref _customPath, 256))
            return;

        if (!Utf8GamePath.FromString(_customPath, out _customGamePath))
            _customGamePath = Utf8GamePath.Empty;
    }

    /// <summary> I disliked the combo with only one selection so turn it into a button in that case. </summary>
    private static void DrawComboButton(MdlTab tab)
    {
        const string label       = "Game Path";
        var          preview     = tab.GamePaths![tab.GamePathIndex].ToString();
        var          labelWidth  = ImGui.CalcTextSize(label).X + Im.Style.ItemInnerSpacing.X;
        var          buttonWidth = Im.ContentRegion.Available.X - labelWidth - Im.Style.ItemSpacing.X;
        if (tab.GamePaths!.Count == 1)
        {
            using var style = ImStyleDouble.ButtonTextAlign.Push(new Vector2(0, 0.5f));
            using var color = ImGuiColor.Button.Push(Im.Style[ImGuiColor.FrameBackground])
                .Push(ImGuiColor.ButtonHovered, Im.Style[ImGuiColor.FrameBackgroundHovered])
                .Push(ImGuiColor.ButtonActive,  Im.Style[ImGuiColor.FrameBackgroundActive]);
            using var group = Im.Group();
            ImGui.Button(preview, new Vector2(buttonWidth, 0));
            Im.Line.Same(0, Im.Style.ItemInnerSpacing.X);
            Im.Text("Game Path"u8);
        }
        else
        {
            Im.Item.SetNextWidth(buttonWidth);
            using var combo = ImRaii.Combo("Game Path", preview);
            if (combo.Success)
                foreach (var (index, path) in tab.GamePaths.Index())
                {
                    if (!ImGui.Selectable(path.ToString(), index == tab.GamePathIndex))
                        continue;

                    tab.GamePathIndex = index;
                }
        }

        if (Im.Item.RightClicked())
            ImGui.SetClipboardText(preview);
        Im.Tooltip.OnHover("Right-Click to copy to clipboard."u8, HoveredFlags.AllowWhenDisabled);
    }

    private void DrawDocumentationLink(string address)
    {
        var text = "Documentation →"u8;

        var framePadding = Im.Style.FramePadding;
        var width        = ImGui.CalcTextSize(text).X + framePadding.X * 2;

        // Draw the link button. We set the background colour to transparent to mimic the look of a link.
        using var color = ImGuiColor.Button.Push(Vector4.Zero);
        SupportButton.Link(Penumbra.Messager, text, address, width, ""u8);

        // Draw an underline for the text.
        var lineStart = ImGui.GetItemRectMax();
        lineStart -= framePadding;
        var lineEnd = lineStart with { X = ImGui.GetItemRectMin().X + framePadding.X };
        ImGui.GetWindowDrawList().AddLine(lineStart, lineEnd, 0xFFFFFFFF);
    }

    private bool DrawModelMaterialDetails(MdlTab tab, bool disabled)
    {
        var invalidMaterialCount = tab.Mdl.Materials.Count(material => !tab.ValidateMaterial(material));

        var oldPos = ImGui.GetCursorPosY();
        var header = ImGui.CollapsingHeader("Materials");
        var newPos = ImGui.GetCursorPos();
        if (invalidMaterialCount > 0)
        {
            var text = $"{invalidMaterialCount} invalid material{(invalidMaterialCount > 1 ? "s" : "")}";
            var size = ImGui.CalcTextSize(text).X;
            ImGui.SetCursorPos(new Vector2(Im.ContentRegion.Available.X - size, oldPos + Im.Style.FramePadding.Y));
            ImGuiUtil.TextColored(0xFF0000FF, text);
            ImGui.SetCursorPos(newPos);
        }

        if (!header)
            return false;

        using var table = Im.Table.Begin(StringU8.Empty, disabled ? 2 : 4, TableFlags.SizingFixedFit);
        if (!table)
            return false;

        var ret       = false;
        var materials = tab.Mdl.Materials;

        table.SetupColumn("index"u8, TableColumnFlags.WidthFixed,   80 * Im.Style.GlobalScale);
        table.SetupColumn("path"u8,  TableColumnFlags.WidthStretch, 1);
        if (!disabled)
        {
            table.SetupColumn("actions"u8, TableColumnFlags.WidthFixed, UiHelpers.IconButtonSize.X);
            table.SetupColumn("help"u8,    TableColumnFlags.WidthFixed, UiHelpers.IconButtonSize.X);
        }

        var inputFlags = disabled ? ImGuiInputTextFlags.ReadOnly : ImGuiInputTextFlags.None;
        for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            ret |= DrawMaterialRow(tab, disabled, materials, materialIndex, inputFlags);

        if (materials.Length >= MdlMaterialMaximum || disabled)
            return ret;

        ImGui.TableNextColumn();

        ImGui.TableNextColumn();
        Im.Item.SetNextWidth(-1);
        ImGui.InputTextWithHint("##newMaterial", "Add new material...", ref _modelNewMaterial, Utf8GamePath.MaxGamePathLength, inputFlags);
        var validName = tab.ValidateMaterial(_modelNewMaterial);
        ImGui.TableNextColumn();
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Plus.ToIconString(), UiHelpers.IconButtonSize, string.Empty, !validName, true))
        {
            ret               |= true;
            tab.Mdl.Materials =  materials.AddItem(_modelNewMaterial);
            _modelNewMaterial =  string.Empty;
        }

        ImGui.TableNextColumn();
        if (!validName && _modelNewMaterial.Length > 0)
            DrawInvalidMaterialMarker();

        return ret;
    }

    private bool DrawMaterialRow(MdlTab tab, bool disabled, string[] materials, int materialIndex, ImGuiInputTextFlags inputFlags)
    {
        using var id  = ImRaii.PushId(materialIndex);
        var       ret = false;
        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        Im.Text($"Material #{materialIndex + 1}");

        var temp = materials[materialIndex];
        ImGui.TableNextColumn();
        Im.Item.SetNextWidth(-1);
        if (ImGui.InputText($"##material{materialIndex}", ref temp, Utf8GamePath.MaxGamePathLength, inputFlags)
         && temp.Length > 0
         && temp != materials[materialIndex]
           )
        {
            materials[materialIndex] = temp;
            ret                      = true;
        }

        if (disabled)
            return ret;

        ImGui.TableNextColumn();
        // Need to have at least one material.
        if (materials.Length > 1)
        {
            var tt             = "Delete this material.\nAny meshes targeting this material will be updated to use material #1.";
            var modifierActive = _config.DeleteModModifier.IsActive();
            if (!modifierActive)
                tt += $"\nHold {_config.DeleteModModifier} to delete.";

            if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Trash.ToIconString(), UiHelpers.IconButtonSize, tt, !modifierActive, true))
            {
                tab.RemoveMaterial(materialIndex);
                ret |= true;
            }
        }

        ImGui.TableNextColumn();
        // Add markers to invalid materials.
        if (!tab.ValidateMaterial(temp))
            DrawInvalidMaterialMarker();

        return ret;
    }

    private static void DrawInvalidMaterialMarker()
    {
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            ImGuiUtil.TextColored(0xFF0000FF, FontAwesomeIcon.TimesCircle.ToIconString());
        }

        ImGuiUtil.HoverTooltip(
            "Materials must be either relative (e.g. \"/filename.mtrl\")\n"
          + "or absolute (e.g. \"bg/full/path/to/filename.mtrl\"),\n"
          + "and must end in \".mtrl\".");
    }

    private bool DrawModelLodDetails(MdlTab tab, int lodIndex, bool disabled)
    {
        using var lodNode = ImRaii.TreeNode($"Level of Detail #{lodIndex + 1}", ImGuiTreeNodeFlags.DefaultOpen);
        if (!lodNode)
            return false;

        var lod = tab.Mdl.Lods[lodIndex];
        var ret = false;

        for (var meshOffset = 0; meshOffset < lod.MeshCount; meshOffset++)
            ret |= DrawModelMeshDetails(tab, lod.MeshIndex + meshOffset, disabled);

        return ret;
    }

    private bool DrawModelMeshDetails(MdlTab tab, int meshIndex, bool disabled)
    {
        using var meshNode = ImRaii.TreeNode($"Mesh #{meshIndex + 1}", ImGuiTreeNodeFlags.DefaultOpen);
        if (!meshNode)
            return false;

        using var id    = ImRaii.PushId(meshIndex);
        using var table = Im.Table.Begin(StringU8.Empty, 2, TableFlags.SizingFixedFit);
        if (!table)
            return false;

        table.SetupColumn("name"u8,  TableColumnFlags.WidthFixed,   100 * Im.Style.GlobalScale);
        table.SetupColumn("field"u8, TableColumnFlags.WidthStretch, 1);

        var file = tab.Mdl;
        var mesh = file.Meshes[meshIndex];

        // Vertex elements
        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        Im.Text("Vertex Elements"u8);

        ImGui.TableNextColumn();
        DrawVertexElementDetails(file.VertexDeclarations[meshIndex].VertexElements);

        // Mesh material
        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        Im.Text("Material"u8);

        ImGui.TableNextColumn();
        var ret = DrawMaterialCombo(tab, meshIndex, disabled);

        // Sub meshes
        for (var subMeshOffset = 0; subMeshOffset < mesh.SubMeshCount; subMeshOffset++)
            ret |= DrawSubMeshAttributes(tab, meshIndex, subMeshOffset, disabled);

        return ret;
    }

    private static void DrawVertexElementDetails(MdlStructs.VertexElement[] vertexElements)
    {
        using var node = ImRaii.TreeNode($"Click to expand");
        if (!node)
            return;

        var flags = TableFlags.SizingFixedFit
          | TableFlags.RowBackground
          | TableFlags.Borders
          | TableFlags.NoHostExtendX;
        using var table = Im.Table.Begin(StringU8.Empty, 4, flags);
        if (!table)
            return;

        table.SetupColumn("Usage"u8);
        table.SetupColumn("Type"u8);
        table.SetupColumn("Stream"u8);
        table.SetupColumn("Offset"u8);

        ImGui.TableHeadersRow();

        foreach (var element in vertexElements)
        {
            ImGui.TableNextColumn();
            Im.Text($"{(MdlFile.VertexUsage)element.Usage}");
            ImGui.TableNextColumn();
            Im.Text($"{(MdlFile.VertexType)element.Type}");
            ImGui.TableNextColumn();
            Im.Text($"{element.Stream}");
            ImGui.TableNextColumn();
            Im.Text($"{element.Offset}");
        }
    }

    private static bool DrawMaterialCombo(MdlTab tab, int meshIndex, bool disabled)
    {
        var       mesh = tab.Mdl.Meshes[meshIndex];
        using var _    = ImRaii.Disabled(disabled);
        Im.Item.SetNextWidth(-1);
        using var materialCombo = ImRaii.Combo("##material", tab.Mdl.Materials[mesh.MaterialIndex]);

        if (!materialCombo)
            return false;

        var ret = false;
        foreach (var (materialIndex, material) in tab.Mdl.Materials.Index())
        {
            if (!ImGui.Selectable(material, mesh.MaterialIndex == materialIndex))
                continue;

            tab.Mdl.Meshes[meshIndex].MaterialIndex = (ushort)materialIndex;
            ret                                     = true;
        }

        return ret;
    }

    private bool DrawSubMeshAttributes(MdlTab tab, int meshIndex, int subMeshOffset, bool disabled)
    {
        using var _ = ImRaii.PushId(subMeshOffset);

        var mesh         = tab.Mdl.Meshes[meshIndex];
        var subMeshIndex = mesh.SubMeshIndex + subMeshOffset;

        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        Im.Text($"Attributes #{subMeshOffset + 1}");

        ImGui.TableNextColumn();
        var data       = disabled ? _preview : _main;
        var widget     = data.SubMeshAttributeTags[subMeshIndex];
        var attributes = tab.GetSubMeshAttributes(subMeshIndex);

        if (attributes == null)
        {
            attributes = ["invalid attribute data"];
            disabled   = true;
        }

        var tagIndex = widget.Draw(string.Empty, string.Empty, attributes,
            out var editedAttribute, !disabled);
        if (tagIndex < 0)
            return false;

        var oldName = tagIndex < attributes.Count ? attributes[tagIndex] : null;
        var newName = editedAttribute.Length > 0 ? editedAttribute : null;
        tab.UpdateSubMeshAttribute(subMeshIndex, oldName, newName);

        return true;
    }

    private bool DrawOtherModelDetails(LoadedData data)
    {
        using var header = ImRaii.CollapsingHeader("Further Content");
        if (!header)
            return false;

        var ret = false;
        using (var table = Im.Table.Begin("##data"u8, 2, TableFlags.SizingFixedFit))
        {
            if (table)
            {
                ImGuiUtil.DrawTableColumn("Version");
                ImGuiUtil.DrawTableColumn($"0x{data.LastFile.Version:X}");
                ImGuiUtil.DrawTableColumn("Radius");
                ImGuiUtil.DrawTableColumn(data.LastFile.Radius.ToString(CultureInfo.InvariantCulture));
                ImGuiUtil.DrawTableColumn("Model Clip Out Distance");
                ImGuiUtil.DrawTableColumn(data.LastFile.ModelClipOutDistance.ToString(CultureInfo.InvariantCulture));
                ImGuiUtil.DrawTableColumn("Shadow Clip Out Distance");
                ImGuiUtil.DrawTableColumn(data.LastFile.ShadowClipOutDistance.ToString(CultureInfo.InvariantCulture));
                ImGuiUtil.DrawTableColumn("LOD Count");
                ImGuiUtil.DrawTableColumn(data.LastFile.LodCount.ToString());
                ImGuiUtil.DrawTableColumn("Enable Index Buffer Streaming");
                ImGuiUtil.DrawTableColumn(data.LastFile.EnableIndexBufferStreaming.ToString());
                ImGuiUtil.DrawTableColumn("Enable Edge Geometry");
                ImGuiUtil.DrawTableColumn(data.LastFile.EnableEdgeGeometry.ToString());
                ImGuiUtil.DrawTableColumn("Flags 1");
                ImGuiUtil.DrawTableColumn(data.LastFile.Flags1.ToString());
                ImGuiUtil.DrawTableColumn("Flags 2");
                ImGuiUtil.DrawTableColumn(data.LastFile.Flags2.ToString());
                ImGuiUtil.DrawTableColumn("Vertex Declarations");
                ImGuiUtil.DrawTableColumn(data.LastFile.VertexDeclarations.Length.ToString());
                ImGuiUtil.DrawTableColumn("Bone Bounding Boxes");
                ImGuiUtil.DrawTableColumn(data.LastFile.BoneBoundingBoxes.Length.ToString());
                ImGuiUtil.DrawTableColumn("Bone Tables");
                ImGuiUtil.DrawTableColumn(data.LastFile.BoneTables.Length.ToString());
                ImGuiUtil.DrawTableColumn("Element IDs");
                ImGuiUtil.DrawTableColumn(data.LastFile.ElementIds.Length.ToString());
                ImGuiUtil.DrawTableColumn("Extra LoDs");
                ImGuiUtil.DrawTableColumn(data.LastFile.ExtraLods.Length.ToString());
                ImGuiUtil.DrawTableColumn("Meshes");
                ImGuiUtil.DrawTableColumn(data.LastFile.Meshes.Length.ToString());
                ImGuiUtil.DrawTableColumn("Shape Meshes");
                ImGuiUtil.DrawTableColumn(data.LastFile.ShapeMeshes.Length.ToString());
                ImGuiUtil.DrawTableColumn("LoDs");
                ImGuiUtil.DrawTableColumn(data.LastFile.Lods.Length.ToString());
                ImGuiUtil.DrawTableColumn("Vertex Declarations");
                ImGuiUtil.DrawTableColumn(data.LastFile.VertexDeclarations.Length.ToString());
                ImGuiUtil.DrawTableColumn("Stack Size");
                ImGuiUtil.DrawTableColumn(data.LastFile.StackSize.ToString());
                foreach (var (lod, triCount) in data.LodTriCount.Index())
                {
                    ImGuiUtil.DrawTableColumn($"LOD #{lod + 1} Triangle Count");
                    ImGuiUtil.DrawTableColumn(triCount.ToString());
                }
            }
        }

        using (var materials = ImRaii.TreeNode("Materials", ImGuiTreeNodeFlags.DefaultOpen))
        {
            if (materials)
                foreach (var material in data.LastFile.Materials)
                    ImRaii.TreeNode(material, ImGuiTreeNodeFlags.Leaf).Dispose();
        }

        using (var attributes = ImRaii.TreeNode("Attributes", ImGuiTreeNodeFlags.DefaultOpen))
        {
            if (attributes)
                for (var i = 0; i < data.LastFile.Attributes.Length; ++i)
                {
                    using var id        = ImUtf8.PushId(i);
                    ref var   attribute = ref data.LastFile.Attributes[i];
                    var       name      = attribute;
                    if (ImUtf8.InputText("##attribute"u8, ref name, "Attribute Name..."u8) && name.Length > 0 && name != attribute)
                    {
                        attribute = name;
                        ret       = true;
                    }
                }
        }

        using (var bones = ImRaii.TreeNode("Bones", ImGuiTreeNodeFlags.DefaultOpen))
        {
            if (bones)
                for (var i = 0; i < data.LastFile.Bones.Length; ++i)
                {
                    using var id   = ImUtf8.PushId(i);
                    ref var   bone = ref data.LastFile.Bones[i];
                    var       name = bone;
                    if (ImUtf8.InputText("##bone"u8, ref name, "Bone Name..."u8) && name.Length > 0 && name != bone)
                    {
                        bone = name;
                        ret  = true;
                    }
                }
        }

        using (var shapes = ImRaii.TreeNode("Shapes", ImGuiTreeNodeFlags.DefaultOpen))
        {
            if (shapes)
                for (var i = 0; i < data.LastFile.Shapes.Length; ++i)
                {
                    using var id    = ImUtf8.PushId(i);
                    ref var   shape = ref data.LastFile.Shapes[i];
                    var       name  = shape.ShapeName;
                    if (ImUtf8.InputText("##shape"u8, ref name, "Shape Name..."u8) && name.Length > 0 && name != shape.ShapeName)
                    {
                        shape.ShapeName = name;
                        ret             = true;
                    }
                }
        }

        if (data.LastFile.RemainingData.Length > 0)
        {
            using var t = ImRaii.TreeNode($"Additional Data (Size: {data.LastFile.RemainingData.Length})###AdditionalData");
            if (t)
                Widget.DrawHexViewer(data.LastFile.RemainingData);
        }

        return ret;
    }

    private static bool GetFirstModel(IEnumerable<string> files, [NotNullWhen(true)] out string? file)
    {
        file = files.FirstOrDefault(f => ValidModelExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()));
        return file != null;
    }

    private static long GetTriangleCountForLod(MdlFile model, int lod)
    {
        var vertSum   = 0u;
        var meshIndex = model.Lods[lod].MeshIndex;
        var meshCount = model.Lods[lod].MeshCount;

        for (var i = meshIndex; i < meshIndex + meshCount; i++)
            vertSum += model.Meshes[i].IndexCount;

        return vertSum / 3;
    }

    private static readonly string[] ValidModelExtensions =
    [
        ".gltf",
        ".glb",
    ];
}

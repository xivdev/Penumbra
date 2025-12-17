using Dalamud.Interface;
using ImSharp;
using Lumina.Data.Parsing;
using Luna;
using OtterGui.Widgets;
using Penumbra.GameData;
using Penumbra.GameData.Files;
using Penumbra.Import.Models;
using Penumbra.Import.Models.Import;
using Penumbra.String.Classes;
using Penumbra.UI.Classes;
using TagButtons = Luna.TagButtons;

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

        if (Im.Tree.Header($"Meshes ({data.LastFile.Meshes.Length})###meshes"))
            for (var i = 0; i < data.LastFile.LodCount; ++i)
                ret |= DrawModelLodDetails(tab, i, disabled);

        ret |= DrawOtherModelDetails(data);

        return !disabled && ret;
    }

    private void DrawVersionUpdate(MdlTab tab, bool disabled)
    {
        if (disabled || tab.Mdl.Version is not MdlFile.V5)
            return;

        if (!ImEx.Button("Update MDL Version from V5 to V6"u8, Colors.PressEnterWarningBg, default, Im.ContentRegion.Available with { Y = 0 },
                "Try using this if the bone weights of a pre-Dawntrail model seem wrong.\n\nThis is not revertible."u8))
            return;

        tab.Mdl.ConvertV5ToV6();
        _modelTab.SaveFile();
    }

    private void DrawImportExport(MdlTab tab, bool disabled)
    {
        if (!Im.Tree.Header("Import / Export"u8))
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
        using var id = Im.Id.Push("import"u8);

        _dragDropManager.CreateImGuiSource("ModelDragDrop",
            m => m.Extensions.Any(e => ValidModelExtensions.Contains(e.ToLowerInvariant())), m =>
            {
                if (!GetFirstModel(m.Files, out var file))
                    return false;

                Im.Text($"Dragging model for editing: {Path.GetFileName(file)}");
                return true;
            });

        using (ImEx.FramedGroup("Import"u8, LunaStyle.ImportIcon, default, StringU8.Empty, ColorParameter.Default, ColorParameter.Default,
                   size))
        {
            Im.Checkbox("Keep current materials"u8,  ref tab.ImportKeepMaterials);
            Im.Checkbox("Keep current attributes"u8, ref tab.ImportKeepAttributes);

            if (ImEx.Button("Import from glTF"u8, Vector2.Zero, "Imports a glTF file, overriding the content of this mdl."u8, tab.PendingIo))
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
        using var id = Im.Id.Push("export"u8);
        using var frame = ImEx.FramedGroup("Export"u8, LunaStyle.FileExportIcon, default, StringU8.Empty, ColorParameter.Default,
            ColorParameter.Default, size);

        if (tab.GamePaths is null)
        {
            Im.Text(tab.IoExceptions.Count is 0 ? "Resolving model game paths."u8 : "Failed to resolve model game paths."u8);

            return;
        }

        DrawGamePathCombo(tab);

        Im.Checkbox("##exportGeneratedMissingBones"u8, ref tab.ExportConfig.GenerateMissingBones);
        LunaStyle.DrawAlignedHelpMarkerLabel("Generate Missing Bones"u8,
            "WARNING: Enabling this option can result in unusable exported meshes.\n"u8
          + "It is primarily intended to allow exporting models weighted to bones that do not exist.\n"u8
          + "Before enabling, ensure dependencies are enabled in the current collection, and EST metadata is correctly configured."u8);

        var gamePath = tab.GamePathIndex >= 0 && tab.GamePathIndex < tab.GamePaths.Count
            ? tab.GamePaths[tab.GamePathIndex]
            : _customGamePath;

        if (ImEx.Button("Export to glTF"u8, Vector2.Zero, "Exports this mdl file to glTF, for use in 3D authoring applications."u8,
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
        if (tab.IoExceptions.Count is 0)
            return;

        var size = Im.ContentRegion.Available with { Y = 0 };
        using var frame = ImEx.FramedGroup("Exceptions"u8, LunaStyle.ErrorIcon, default, StringU8.Empty, ColorParameter.Default,
            LunaStyle.ErrorBorderColor, size);

        var spaceAvail = Im.ContentRegion.Available.X - Im.Style.ItemSpacing.X - 100;
        foreach (var (index, exception) in tab.IoExceptions.Index())
        {
            using var id       = Im.Id.Push(index);
            var       message  = new StringU8($"{exception.GetType().Name}: {exception.Message}");
            var       textSize = Im.Font.CalculateSize(message).X;
            if (textSize > spaceAvail)
                message = new StringU8($"{message.Span[..(int)Math.Floor(message.Length * (spaceAvail / textSize))]}...");

            using var exceptionNode = Im.Tree.Node(message);
            if (exceptionNode)
            {
                using var indent = Im.Indent();
                Im.TextWrapped($"{exception}");
            }
        }
    }

    private static void DrawIoWarnings(MdlTab tab)
    {
        if (tab.IoWarnings.Count is 0)
            return;

        var size = Im.ContentRegion.Available with { Y = 0 };
        using var frame = ImEx.FramedGroup("Warnings"u8, LunaStyle.WarningIcon, default, StringU8.Empty, ColorParameter.Default,
            LunaStyle.WarningBorderColor, size);

        var spaceAvail = Im.ContentRegion.Available.X - Im.Style.ItemSpacing.X - 100;
        foreach (var (index, warning) in tab.IoWarnings.Index())
        {
            using var id       = Im.Id.Push(index);
            var       textSize = Im.Font.CalculateSize(warning).X;

            if (textSize <= spaceAvail)
            {
                Im.Tree.Leaf(warning);
                continue;
            }

            var firstLine = new StringU8($"{warning.AsSpan(0, (int)Math.Floor(warning.Length * (spaceAvail / textSize)))}...");

            using var warningNode = Im.Tree.Node(firstLine);
            if (warningNode)
            {
                using var indent = Im.Indent();
                Im.TextWrapped(warning);
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
        if (!Im.Input.Text("##customInput"u8, ref _customPath, "Enter custom game path..."u8))
            return;

        if (!Utf8GamePath.FromString(_customPath, out _customGamePath))
            _customGamePath = Utf8GamePath.Empty;
    }

    /// <summary> I disliked the combo with only one selection so turn it into a button in that case. </summary>
    private static void DrawComboButton(MdlTab tab)
    {
        var preview     = tab.GamePaths![tab.GamePathIndex].Path.Span;
        var labelWidth  = Im.Font.CalculateSize("Game Path"u8).X + Im.Style.ItemInnerSpacing.X;
        var buttonWidth = Im.ContentRegion.Available.X - labelWidth - Im.Style.ItemSpacing.X;
        if (tab.GamePaths!.Count == 1)
        {
            using var style = ImStyleDouble.ButtonTextAlign.Push(new Vector2(0, 0.5f));
            using var color = ImGuiColor.Button.Push(Im.Style[ImGuiColor.FrameBackground])
                .Push(ImGuiColor.ButtonHovered, Im.Style[ImGuiColor.FrameBackgroundHovered])
                .Push(ImGuiColor.ButtonActive,  Im.Style[ImGuiColor.FrameBackgroundActive]);
            using var group = Im.Group();
            Im.Button(preview, new Vector2(buttonWidth, 0));
            Im.Line.Same(0, Im.Style.ItemInnerSpacing.X);
            Im.Text("Game Path"u8);
        }
        else
        {
            Im.Item.SetNextWidth(buttonWidth);
            using var combo = Im.Combo.Begin("Game Path"u8, preview);
            if (combo.Success)
                foreach (var (index, path) in tab.GamePaths.Index())
                {
                    if (!Im.Selectable(path.Path.Span, index == tab.GamePathIndex))
                        continue;

                    tab.GamePathIndex = index;
                }
        }

        if (Im.Item.RightClicked())
            Im.Clipboard.Set(preview);
        Im.Tooltip.OnHover("Right-Click to copy to clipboard."u8, HoveredFlags.AllowWhenDisabled);
    }

    private static void DrawDocumentationLink(string address)
    {
        var text  = "Documentation →"u8;
        var width = Im.Font.CalculateButtonSize(text).X;
        // Draw the link button. We set the background colour to transparent to mimic the look of a link.
        using var color = ImGuiColor.Button.Push(Vector4.Zero);
        SupportButton.Link(Penumbra.Messager, text, address, width, ""u8);

        // Draw an underline for the text.
        var lineStart = Im.Item.LowerRightCorner;
        lineStart -= Im.Style.FramePadding;
        var lineEnd = lineStart with { X = Im.Item.UpperLeftCorner.X + Im.Style.FramePadding.X };
        Im.Window.DrawList.Shape.Line(lineStart, lineEnd, 0xFFFFFFFF);
    }

    private bool DrawModelMaterialDetails(MdlTab tab, bool disabled)
    {
        var invalidMaterialCount = tab.Mdl.Materials.Count(material => !tab.ValidateMaterial(material));

        var oldPos = Im.Cursor.Y;
        var header = Im.Tree.Header("Materials"u8);
        var newPos = Im.Cursor.Position;
        if (invalidMaterialCount > 0)
        {
            var text = new StringU8($"{invalidMaterialCount} invalid material{(invalidMaterialCount > 1 ? "s" : "")}");
            var size = Im.Font.CalculateSize(text).X;
            Im.Cursor.Position = new Vector2(Im.ContentRegion.Available.X - size, oldPos + Im.Style.FramePadding.Y);
            Im.Text(text, Rgba32.Red);
            Im.Cursor.Position = newPos;
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

        var inputFlags = disabled ? InputTextFlags.ReadOnly : InputTextFlags.None;
        for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            ret |= DrawMaterialRow(table, tab, disabled, materials, materialIndex, inputFlags);

        if (materials.Length >= MdlMaterialMaximum || disabled)
            return ret;

        table.NextColumn();

        table.NextColumn();
        Im.Item.SetNextWidth(Im.ContentRegion.Available.X);
        Im.Input.Text("##newMaterial"u8, ref _modelNewMaterial, "Add new material..."u8, maxLength: Utf8GamePath.MaxGamePathLength,
            flags: inputFlags);
        var validName = tab.ValidateMaterial(_modelNewMaterial);
        table.NextColumn();
        if (ImEx.Icon.Button(LunaStyle.AddObjectIcon, StringU8.Empty, !validName))
        {
            ret               = true;
            tab.Mdl.Materials = materials.AddItem(_modelNewMaterial);
            _modelNewMaterial = string.Empty;
        }

        table.NextColumn();
        if (!validName && _modelNewMaterial.Length > 0)
            DrawInvalidMaterialMarker();

        return ret;
    }

    private bool DrawMaterialRow(in Im.TableDisposable table, MdlTab tab, bool disabled, string[] materials, int materialIndex,
        InputTextFlags inputFlags)
    {
        using var id  = Im.Id.Push(materialIndex);
        var       ret = false;
        table.DrawFrameColumn($"Material #{materialIndex + 1}");

        var temp = materials[materialIndex];
        table.NextColumn();
        Im.Item.SetNextWidth(Im.ContentRegion.Available.X);
        if (Im.Input.Text($"##material{materialIndex}", ref temp, maxLength: Utf8GamePath.MaxGamePathLength, flags: inputFlags)
         && temp.Length > 0
         && temp != materials[materialIndex]
           )
        {
            materials[materialIndex] = temp;
            ret                      = true;
        }

        if (disabled)
            return ret;

        table.NextColumn();
        // Need to have at least one material.
        if (materials.Length > 1)
        {
            var modifierActive = _config.DeleteModModifier.IsActive();
            if (ImEx.Icon.Button(LunaStyle.DeleteIcon,
                    "Delete this material.\nAny meshes targeting this material will be updated to use material #1."u8, !modifierActive))
            {
                tab.RemoveMaterial(materialIndex);
                ret = true;
            }

            if (!modifierActive)
                Im.Tooltip.OnHover($"\nHold {_config.DeleteModModifier} to delete.");
        }

        table.NextColumn();
        // Add markers to invalid materials.
        if (!tab.ValidateMaterial(temp))
            DrawInvalidMaterialMarker();

        return ret;
    }

    private static void DrawInvalidMaterialMarker()
    {
        ImEx.Icon.Draw(FontAwesomeIcon.TimesCircle.Icon(), Rgba32.Red);
        Im.Tooltip.OnHover(
            "Materials must be either relative (e.g. \"/filename.mtrl\")\n"u8
          + "or absolute (e.g. \"bg/full/path/to/filename.mtrl\"),\n"u8
          + "and must end in \".mtrl\"."u8);
    }

    private static bool DrawModelLodDetails(MdlTab tab, int lodIndex, bool disabled)
    {
        using var lodNode = Im.Tree.Node($"Level of Detail #{lodIndex + 1}", TreeNodeFlags.DefaultOpen);
        if (!lodNode)
            return false;

        var lod = tab.Mdl.Lods[lodIndex];
        var ret = false;

        for (var meshOffset = 0; meshOffset < lod.MeshCount; meshOffset++)
            ret |= DrawModelMeshDetails(tab, lod.MeshIndex + meshOffset, disabled);

        return ret;
    }

    private static bool DrawModelMeshDetails(MdlTab tab, int meshIndex, bool disabled)
    {
        using var meshNode = Im.Tree.Node($"Mesh #{meshIndex + 1}", TreeNodeFlags.DefaultOpen);
        if (!meshNode)
            return false;

        using var id    = Im.Id.Push(meshIndex);
        using var table = Im.Table.Begin(StringU8.Empty, 2, TableFlags.SizingFixedFit);
        if (!table)
            return false;

        table.SetupColumn("name"u8,  TableColumnFlags.WidthFixed,   100 * Im.Style.GlobalScale);
        table.SetupColumn("field"u8, TableColumnFlags.WidthStretch, 1);

        var file = tab.Mdl;
        var mesh = file.Meshes[meshIndex];

        // Vertex elements
        table.DrawFrameColumn("Vertex Elements"u8);

        table.NextColumn();
        DrawVertexElementDetails(file.VertexDeclarations[meshIndex].VertexElements);

        // Mesh material
        table.DrawFrameColumn("Material"u8);

        table.NextColumn();
        var ret = DrawMaterialCombo(tab, meshIndex, disabled);

        // Sub meshes
        for (var subMeshOffset = 0; subMeshOffset < mesh.SubMeshCount; subMeshOffset++)
            ret |= DrawSubMeshAttributes(table, tab, meshIndex, subMeshOffset, disabled);

        return ret;
    }

    private static void DrawVertexElementDetails(MdlStructs.VertexElement[] vertexElements)
    {
        using var node = Im.Tree.Node("Click to expand"u8);
        if (!node)
            return;

        const TableFlags flags = TableFlags.SizingFixedFit
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
        table.HeaderRow();

        foreach (var element in vertexElements)
        {
            table.DrawColumn($"{(MdlFile.VertexUsage)element.Usage}");
            table.DrawColumn($"{(MdlFile.VertexType)element.Type}");
            table.DrawColumn($"{element.Stream}");
            table.DrawColumn($"{element.Offset}");
        }
    }

    private static bool DrawMaterialCombo(MdlTab tab, int meshIndex, bool disabled)
    {
        var       mesh = tab.Mdl.Meshes[meshIndex];
        using var _    = Im.Disabled(disabled);
        Im.Item.SetNextWidth(Im.ContentRegion.Available.X);
        using var materialCombo = Im.Combo.Begin("##material"u8, tab.Mdl.Materials[mesh.MaterialIndex]);

        if (!materialCombo)
            return false;

        var ret = false;
        foreach (var (materialIndex, material) in tab.Mdl.Materials.Index())
        {
            if (!Im.Selectable(material, mesh.MaterialIndex == materialIndex))
                continue;

            tab.Mdl.Meshes[meshIndex].MaterialIndex = (ushort)materialIndex;
            ret                                     = true;
        }

        return ret;
    }

    private static bool DrawSubMeshAttributes(in Im.TableDisposable table, MdlTab tab, int meshIndex, int subMeshOffset, bool disabled)
    {
        using var _ = Im.Id.Push(subMeshOffset);

        var mesh         = tab.Mdl.Meshes[meshIndex];
        var subMeshIndex = mesh.SubMeshIndex + subMeshOffset;

        table.DrawFrameColumn($"Attributes #{subMeshOffset + 1}");

        table.NextColumn();
        var attributes = tab.GetSubMeshAttributes(subMeshIndex);

        if (attributes is null)
        {
            attributes = ["invalid attribute data"];
            disabled   = true;
        }

        var tagIndex = TagButtons.Draw(StringU8.Empty, StringU8.Empty, attributes, out var editedAttribute, !disabled);
        if (tagIndex < 0)
            return false;

        var oldName = tagIndex < attributes.Count ? attributes[tagIndex] : null;
        var newName = editedAttribute.Length > 0 ? editedAttribute : null;
        tab.UpdateSubMeshAttribute(subMeshIndex, oldName, newName);

        return true;
    }

    private bool DrawOtherModelDetails(LoadedData data)
    {
        using var header = Im.Tree.HeaderId("Further Content"u8);
        if (!header)
            return false;

        var ret = false;
        using (var table = Im.Table.Begin("##data"u8, 2, TableFlags.SizingFixedFit))
        {
            if (table)
            {
                table.DrawDataPair("Version"u8, $"0x{data.LastFile.Version:X}");
                table.DrawDataPair("Radius"u8, data.LastFile.Radius.ToString(CultureInfo.InvariantCulture));
                table.DrawDataPair("Model Clip Out Distance"u8, data.LastFile.ModelClipOutDistance.ToString(CultureInfo.InvariantCulture));
                table.DrawDataPair("Shadow Clip Out Distance"u8, data.LastFile.ShadowClipOutDistance.ToString(CultureInfo.InvariantCulture));
                table.DrawDataPair("LOD Count"u8, data.LastFile.LodCount);
                table.DrawDataPair("Enable Index Buffer Streaming"u8, data.LastFile.EnableIndexBufferStreaming);
                table.DrawDataPair("Enable Edge Geometry"u8, data.LastFile.EnableEdgeGeometry);
                table.DrawDataPair("Flags 1"u8, data.LastFile.Flags1);
                table.DrawDataPair("Flags 2"u8, data.LastFile.Flags2);
                table.DrawDataPair("Vertex Declarations"u8, data.LastFile.VertexDeclarations.Length);
                table.DrawDataPair("Bone Bounding Boxes"u8, data.LastFile.BoneBoundingBoxes.Length);
                table.DrawDataPair("Bone Tables"u8, data.LastFile.BoneTables.Length);
                table.DrawDataPair("Element IDs"u8, data.LastFile.ElementIds.Length);
                table.DrawDataPair("Extra LoDs"u8, data.LastFile.ExtraLods.Length);
                table.DrawDataPair("Meshes"u8, data.LastFile.Meshes.Length);
                table.DrawDataPair("Shape Meshes"u8, data.LastFile.ShapeMeshes.Length);
                table.DrawDataPair("LoDs"u8, data.LastFile.Lods.Length);
                table.DrawDataPair("Vertex Declarations"u8, data.LastFile.VertexDeclarations.Length);
                table.DrawDataPair("Stack Size"u8, data.LastFile.StackSize);
                foreach (var (lod, triCount) in data.LodTriCount.Index())
                    table.DrawDataPair($"LOD #{lod + 1} Triangle Count", triCount);
            }
        }

        using (var materials = Im.Tree.Node("Materials"u8, TreeNodeFlags.DefaultOpen))
        {
            if (materials)
                foreach (var material in data.LastFile.Materials)
                    Im.Tree.Leaf(material);
        }

        using (var attributes = Im.Tree.Node("Attributes"u8, TreeNodeFlags.DefaultOpen))
        {
            if (attributes)
                for (var i = 0; i < data.LastFile.Attributes.Length; ++i)
                {
                    using var id        = Im.Id.Push(i);
                    ref var   attribute = ref data.LastFile.Attributes[i];
                    var       name      = attribute;
                    if (Im.Input.Text("##attribute"u8, ref name, "Attribute Name..."u8) && name.Length > 0 && name != attribute)
                    {
                        attribute = name;
                        ret       = true;
                    }
                }
        }

        using (var bones = Im.Tree.Node("Bones"u8, TreeNodeFlags.DefaultOpen))
        {
            if (bones)
                for (var i = 0; i < data.LastFile.Bones.Length; ++i)
                {
                    using var id   = Im.Id.Push(i);
                    ref var   bone = ref data.LastFile.Bones[i];
                    var       name = bone;
                    if (Im.Input.Text("##bone"u8, ref name, "Bone Name..."u8) && name.Length > 0 && name != bone)
                    {
                        bone = name;
                        ret  = true;
                    }
                }
        }

        using (var shapes = Im.Tree.Node("Shapes"u8, TreeNodeFlags.DefaultOpen))
        {
            if (shapes)
                for (var i = 0; i < data.LastFile.Shapes.Length; ++i)
                {
                    using var id    = Im.Id.Push(i);
                    ref var   shape = ref data.LastFile.Shapes[i];
                    var       name  = shape.ShapeName;
                    if (Im.Input.Text("##shape"u8, ref name, "Shape Name..."u8) && name.Length > 0 && name != shape.ShapeName)
                    {
                        shape.ShapeName = name;
                        ret             = true;
                    }
                }
        }

        if (data.LastFile.RemainingData.Length > 0)
        {
            using var t = Im.Tree.Node($"Additional Data (Size: {data.LastFile.RemainingData.Length})###AdditionalData");
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

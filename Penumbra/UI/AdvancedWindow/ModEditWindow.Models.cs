using Dalamud.Interface;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Widgets;
using Penumbra.GameData;
using Penumbra.GameData.Files;
using Penumbra.String.Classes;

namespace Penumbra.UI.AdvancedWindow;

public partial class ModEditWindow
{
    private const int MdlMaterialMaximum = 4;

    private readonly FileEditor<MdlTab> _modelTab;

    private static string _modelNewMaterial = string.Empty;
    private static List<TagButtons> _submeshAttributeTagWidgets = new();

    private static bool DrawModelPanel(MdlTab tab, bool disabled)
    {
        var file = tab.Mdl;

        var submeshTotal = file.Meshes.Aggregate(0, (count, mesh) => count + mesh.SubMeshCount);
        if (_submeshAttributeTagWidgets.Count != submeshTotal)
        {
            _submeshAttributeTagWidgets.Clear();
            _submeshAttributeTagWidgets.AddRange(
                Enumerable.Range(0, submeshTotal).Select(_ => new TagButtons())
            );
        }

        var ret = false;

        ret |= DrawModelMaterialDetails(tab, disabled);

        if (ImGui.CollapsingHeader($"Meshes ({file.Meshes.Length})###meshes"))
            for (var i = 0; i < file.LodCount; ++i)
                ret |= DrawModelLodDetails(tab, i, disabled);

        ret |= DrawOtherModelDetails(file, disabled);

        return !disabled && ret;
    }

    private static bool DrawModelMaterialDetails(MdlTab tab, bool disabled)
    {
        if (!ImGui.CollapsingHeader("Materials"))
            return false;

        using var table = ImRaii.Table(string.Empty, 3, ImGuiTableFlags.SizingFixedFit);
        if (!table)
            return false;

        var ret = false;
        var materials = tab.Mdl.Materials;

        ImGui.TableSetupColumn("index", ImGuiTableColumnFlags.WidthFixed, 80 * UiHelpers.Scale);
        ImGui.TableSetupColumn("path", ImGuiTableColumnFlags.WidthStretch, 1);
        ImGui.TableSetupColumn("actions", ImGuiTableColumnFlags.WidthFixed, UiHelpers.IconButtonSize.X);

        var inputFlags = ImGuiInputTextFlags.None;
        if (disabled)
            inputFlags |= ImGuiInputTextFlags.ReadOnly;

        for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
        {
            using var id = ImRaii.PushId(materialIndex);

            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.Text($"Material #{materialIndex + 1}");

            var temp = materials[materialIndex];
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1);
            if (
                ImGui.InputText($"##material{materialIndex}", ref temp, Utf8GamePath.MaxGamePathLength, inputFlags)
                && temp.Length > 0
                && temp != materials[materialIndex]
            ) {
                materials[materialIndex] = temp;
                ret = true;
            }

            ImGui.TableNextColumn();

            // Need to have at least one material.
            if (materials.Length <= 1)
                continue;

            if (ImGuiUtil.DrawDisabledButton(
                FontAwesomeIcon.Trash.ToIconString(),
                UiHelpers.IconButtonSize,
                "Delete this material.\nAny meshes targeting this material will be updated to use material #1.\nHold Control while clicking to delete.",
                disabled || !ImGui.GetIO().KeyCtrl,
                true
            )) {
                tab.RemoveMaterial(materialIndex);
                ret = true;
            }
        }

        if (materials.Length < MdlMaterialMaximum)
        {
            ImGui.TableNextColumn();

            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint($"##newMaterial", "Add new material...", ref _modelNewMaterial, Utf8GamePath.MaxGamePathLength, inputFlags);
            
            var validName = _modelNewMaterial != "";
            ImGui.TableNextColumn();
            if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Plus.ToIconString(), UiHelpers.IconButtonSize, "description", disabled || !validName, true))
            {
                tab.Mdl.Materials = materials.AddItem(_modelNewMaterial);
                _modelNewMaterial = string.Empty;
                ret = true;
            }
        }
    
        return ret;
    }

    private static bool DrawModelLodDetails(MdlTab tab, int lodIndex, bool disabled)
    {
        using var lodNode = ImRaii.TreeNode($"Level of Detail #{lodIndex}", ImGuiTreeNodeFlags.DefaultOpen);
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
        using var meshNode = ImRaii.TreeNode($"Mesh #{meshIndex}", ImGuiTreeNodeFlags.DefaultOpen);
        if (!meshNode)
            return false;

        using var id = ImRaii.PushId(meshIndex); 
        using var table = ImRaii.Table(string.Empty, 2, ImGuiTableFlags.SizingFixedFit);
        if (!table)
            return false;

        ImGui.TableSetupColumn("name", ImGuiTableColumnFlags.WidthFixed, 100 * UiHelpers.Scale);
        ImGui.TableSetupColumn("field", ImGuiTableColumnFlags.WidthStretch, 1);

        var file = tab.Mdl;
        var mesh = file.Meshes[meshIndex];

        var ret = false;

        // Mesh material.
        // var temp = tab.GetMeshMaterial(meshIndex);
        // if (
        //     ImGui.InputText("Material", ref temp, Utf8GamePath.MaxGamePathLength, disabled ? ImGuiInputTextFlags.ReadOnly : ImGuiInputTextFlags.None)
        //     && temp.Length > 0
        //     && temp != tab.GetMeshMaterial(meshIndex)
        // ) {
        //     tab.SetMeshMaterial(meshIndex, temp);
        //     ret = true;
        // }
        ImGui.TableNextColumn();
        ImGui.Text("Material");

        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(-1);
        using (var materialCombo = ImRaii.Combo("##material", "TODO material"))
        {
            // todo
        }

        // Submeshes.
        for (var submeshOffset = 0; submeshOffset < mesh.SubMeshCount; submeshOffset++)
        {
            using var submeshId = ImRaii.PushId(submeshOffset);

            var submeshIndex = mesh.SubMeshIndex + submeshOffset;
   
            ImGui.TableNextColumn();
            ImGui.Text($"Attributes #{submeshOffset}");

            ImGui.TableNextColumn();
            var widget = _submeshAttributeTagWidgets[submeshIndex];
            var attributes = tab.GetSubmeshAttributes(submeshIndex);

            var tagIndex = widget.Draw("", "", attributes, out var editedAttribute, !disabled);
            if (tagIndex >= 0)
            {
                tab.UpdateSubmeshAttribute(
                    submeshIndex,
                    tagIndex < attributes.Count() ? attributes.ElementAt(tagIndex) : null,
                    editedAttribute != "" ? editedAttribute : null
                );

                ret = true;
            }
        }

        return ret;
    }

    private static bool DrawOtherModelDetails(MdlFile file, bool _)
    {
        if (!ImGui.CollapsingHeader("Further Content"))
            return false;

        using (var table = ImRaii.Table("##data", 2, ImGuiTableFlags.SizingFixedFit))
        {
            if (table)
            {
                ImGuiUtil.DrawTableColumn("Version");
                ImGuiUtil.DrawTableColumn(file.Version.ToString());
                ImGuiUtil.DrawTableColumn("Radius");
                ImGuiUtil.DrawTableColumn(file.Radius.ToString(CultureInfo.InvariantCulture));
                ImGuiUtil.DrawTableColumn("Model Clip Out Distance");
                ImGuiUtil.DrawTableColumn(file.ModelClipOutDistance.ToString(CultureInfo.InvariantCulture));
                ImGuiUtil.DrawTableColumn("Shadow Clip Out Distance");
                ImGuiUtil.DrawTableColumn(file.ShadowClipOutDistance.ToString(CultureInfo.InvariantCulture));
                ImGuiUtil.DrawTableColumn("LOD Count");
                ImGuiUtil.DrawTableColumn(file.LodCount.ToString());
                ImGuiUtil.DrawTableColumn("Enable Index Buffer Streaming");
                ImGuiUtil.DrawTableColumn(file.EnableIndexBufferStreaming.ToString());
                ImGuiUtil.DrawTableColumn("Enable Edge Geometry");
                ImGuiUtil.DrawTableColumn(file.EnableEdgeGeometry.ToString());
                ImGuiUtil.DrawTableColumn("Flags 1");
                ImGuiUtil.DrawTableColumn(file.Flags1.ToString());
                ImGuiUtil.DrawTableColumn("Flags 2");
                ImGuiUtil.DrawTableColumn(file.Flags2.ToString());
                ImGuiUtil.DrawTableColumn("Vertex Declarations");
                ImGuiUtil.DrawTableColumn(file.VertexDeclarations.Length.ToString());
                ImGuiUtil.DrawTableColumn("Bone Bounding Boxes");
                ImGuiUtil.DrawTableColumn(file.BoneBoundingBoxes.Length.ToString());
                ImGuiUtil.DrawTableColumn("Bone Tables");
                ImGuiUtil.DrawTableColumn(file.BoneTables.Length.ToString());
                ImGuiUtil.DrawTableColumn("Element IDs");
                ImGuiUtil.DrawTableColumn(file.ElementIds.Length.ToString());
                ImGuiUtil.DrawTableColumn("Extra LoDs");
                ImGuiUtil.DrawTableColumn(file.ExtraLods.Length.ToString());
                ImGuiUtil.DrawTableColumn("Meshes");
                ImGuiUtil.DrawTableColumn(file.Meshes.Length.ToString());
                ImGuiUtil.DrawTableColumn("Shape Meshes");
                ImGuiUtil.DrawTableColumn(file.ShapeMeshes.Length.ToString());
                ImGuiUtil.DrawTableColumn("LoDs");
                ImGuiUtil.DrawTableColumn(file.Lods.Length.ToString());
                ImGuiUtil.DrawTableColumn("Vertex Declarations");
                ImGuiUtil.DrawTableColumn(file.VertexDeclarations.Length.ToString());
                ImGuiUtil.DrawTableColumn("Stack Size");
                ImGuiUtil.DrawTableColumn(file.StackSize.ToString());
            }
        }

        using (var materials = ImRaii.TreeNode("Materials", ImGuiTreeNodeFlags.DefaultOpen))
        {
            if (materials)
                foreach (var material in file.Materials)
                    ImRaii.TreeNode(material, ImGuiTreeNodeFlags.Leaf).Dispose();
        }

        using (var attributes = ImRaii.TreeNode("Attributes", ImGuiTreeNodeFlags.DefaultOpen))
        {
            if (attributes)
                foreach (var attribute in file.Attributes)
                    ImRaii.TreeNode(attribute, ImGuiTreeNodeFlags.Leaf).Dispose();
        }

        using (var bones = ImRaii.TreeNode("Bones", ImGuiTreeNodeFlags.DefaultOpen))
        {
            if (bones)
                foreach (var bone in file.Bones)
                    ImRaii.TreeNode(bone, ImGuiTreeNodeFlags.Leaf).Dispose();
        }

        using (var shapes = ImRaii.TreeNode("Shapes", ImGuiTreeNodeFlags.DefaultOpen))
        {
            if (shapes)
                foreach (var shape in file.Shapes)
                    ImRaii.TreeNode(shape.ShapeName, ImGuiTreeNodeFlags.Leaf).Dispose();
        }

        if (file.RemainingData.Length > 0)
        {
            using var t = ImRaii.TreeNode($"Additional Data (Size: {file.RemainingData.Length})###AdditionalData");
            if (t)
                ImGuiUtil.TextWrapped(string.Join(' ', file.RemainingData.Select(c => $"{c:X2}")));
        }

        return false;
    }
}

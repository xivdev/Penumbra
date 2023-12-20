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
    private readonly FileEditor<MdlFile> _modelTab;

    private static List<TagButtons> _submeshAttributeTagWidgets = new();

    private static bool DrawModelPanel(MdlFile file, bool disabled)
    {
        var submeshTotal = file.Meshes.Aggregate(0, (count, mesh) => count + mesh.SubMeshCount);
        if (_submeshAttributeTagWidgets.Count != submeshTotal)
        {
            _submeshAttributeTagWidgets.Clear();
            _submeshAttributeTagWidgets.AddRange(
                Enumerable.Range(0, submeshTotal).Select(_ => new TagButtons())
            );
        }

        var ret = false;

        for (var i = 0; i < file.Meshes.Length; ++i)
            ret |= DrawMeshDetails(file, i, disabled);

        ret |= DrawOtherModelDetails(file, disabled);

        return !disabled && ret;
    }

    private static bool DrawMeshDetails(MdlFile file, int meshIndex, bool disabled)
    {
        if (!ImGui.CollapsingHeader($"Mesh {meshIndex}"))
            return false;

        using var id = ImRaii.PushId(meshIndex); 

        var mesh = file.Meshes[meshIndex];

        var ret = false;

        // Mesh material.
        var temp = file.Materials[mesh.MaterialIndex];
        if (
            ImGui.InputText("Material", ref temp, Utf8GamePath.MaxGamePathLength, disabled ? ImGuiInputTextFlags.ReadOnly : ImGuiInputTextFlags.None)
            && temp.Length > 0
            && temp != file.Materials[mesh.MaterialIndex]
        ) {
            file.Materials[mesh.MaterialIndex] = temp;
            ret = true;
        }

        // Submeshes.
        for (var submeshOffset = 0; submeshOffset < mesh.SubMeshCount; submeshOffset++)
            ret |= DrawSubMeshDetails(file, mesh.SubMeshIndex + submeshOffset, disabled);

        return ret;
    }

    private static bool DrawSubMeshDetails(MdlFile file, int submeshIndex, bool disabled)
    {
        using var id = ImRaii.PushId(submeshIndex);

        var submesh = file.SubMeshes[submeshIndex];
        var widget = _submeshAttributeTagWidgets[submeshIndex];

        var attributes = Enumerable
            .Range(0, 32)
            .Where(index => ((submesh.AttributeIndexMask >> index) & 1) == 1)
            .Select(index => file.Attributes[index])
            .ToArray();

        UiHelpers.DefaultLineSpace();
        var tagIndex = widget.Draw($"Submesh {submeshIndex} Attributes", "", attributes, out var editedAttribute, !disabled);
        if (tagIndex >= 0)
        {
            // Eagerly remove the edited attribute from the attribute mask.
            if (tagIndex < attributes.Length)
            {
                var previousAttributeIndex = file.Attributes.IndexOf(attributes[tagIndex]);
                submesh.AttributeIndexMask &= ~(1u << previousAttributeIndex);
                
                // If no other submeshes use this attribute, remove it.
                var usages = file.SubMeshes
                    .Where(submesh => ((submesh.AttributeIndexMask >> previousAttributeIndex) & 1) == 1)
                    .Count();
                if (usages <= 1)
                {
                    // TODO THIS BLOWS UP ALL OTHER INDICES BEYOND WHAT WE JUST REMOVED - I NEED TO VIRTUALISE THIS SHIT
                    // file.Attributes = file.Attributes.RemoveItems(previousAttributeIndex);
                }
            }

            // If there's a new or edited name, add it to the mask, and the attribute list if it's not already known.
            if (editedAttribute != "")
            {
                var attributeIndex = file.Attributes.IndexOf(editedAttribute);
                if (attributeIndex == -1)
                {
                    file.Attributes.AddItem(editedAttribute);
                    attributeIndex = file.Attributes.Length - 1;
                }
                submesh.AttributeIndexMask |= 1u << attributeIndex;
            }

            file.SubMeshes[submeshIndex] = submesh;

            return true;
        }

        ImGui.SameLine();
        ImGui.Text($"{Convert.ToString(submesh.AttributeIndexMask, 2)}");

        return false;
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

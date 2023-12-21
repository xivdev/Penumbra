using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Widgets;
using Penumbra.GameData.Files;
using Penumbra.String.Classes;

namespace Penumbra.UI.AdvancedWindow;

public partial class ModEditWindow
{
    private readonly FileEditor<MdlTab> _modelTab;

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
        {
            using var submeshId = ImRaii.PushId(submeshOffset);

            var submeshIndex = mesh.SubMeshIndex + submeshOffset;

            var submesh = file.SubMeshes[submeshIndex];
            var widget = _submeshAttributeTagWidgets[submeshIndex];

            var attributes = HydrateAttributes(file, submesh.AttributeIndexMask).ToArray();

            UiHelpers.DefaultLineSpace();
            var tagIndex = widget.Draw($"Submesh {submeshOffset} Attributes", "", attributes, out var editedAttribute, !disabled);
            if (tagIndex >= 0)
            {
                EditSubmeshAttribute(
                    file,
                    submeshIndex,
                    tagIndex < attributes.Length ? attributes[tagIndex] : null,
                    editedAttribute != "" ? editedAttribute : null
                );

                ret = true;
            }
        }

        return ret;
    }

    private static void EditSubmeshAttribute(MdlFile file, int changedSubmeshIndex, string? old, string? new_)
    {
        // Build a hydrated view of all attributes in the model
        var submeshAttributes = file.SubMeshes
            .Select(submesh => HydrateAttributes(file, submesh.AttributeIndexMask).ToList())
            .ToArray();

        // Make changes to the submesh we're actually editing here.
        var changedSubmesh = submeshAttributes[changedSubmeshIndex];

        if (old != null)
            changedSubmesh.Remove(old);

        if (new_ != null)
            changedSubmesh.Add(new_);

        // Re-serialize all the attributes.
        var allAttributes = new List<string>();
        foreach (var (attributes, submeshIndex) in submeshAttributes.WithIndex())
        {
            var mask = 0u;

            foreach (var attribute in attributes)
            {
                var attributeIndex = allAttributes.IndexOf(attribute);
                if (attributeIndex == -1)
                {
                    allAttributes.Add(attribute);
                    attributeIndex = allAttributes.Count() - 1;
                }

                mask |= 1u << attributeIndex;
            }

            file.SubMeshes[submeshIndex].AttributeIndexMask = mask;
        }

        file.Attributes = allAttributes.ToArray();
    }

    private static IEnumerable<string> HydrateAttributes(MdlFile file, uint mask)
    {
        return Enumerable
            .Range(0, 32)
            .Where(index => ((mask >> index) & 1) == 1)
            .Select(index => file.Attributes[index]);
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

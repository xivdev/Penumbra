using System.Collections.ObjectModel;
using OtterGui;
using Penumbra.GameData.Files;

namespace Penumbra.UI.AdvancedWindow;

public partial class ModEditWindow
{
    private class MdlTab : IWritable
    {
        public readonly MdlFile Mdl;

        private List<string> _materials;
        private List<string>[] _attributes;

        public MdlTab(byte[] bytes)
        {
            Mdl = new MdlFile(bytes);

            _materials = Mdl.Meshes.Select(mesh => Mdl.Materials[mesh.MaterialIndex]).ToList();
            _attributes = HydrateAttributes(Mdl);
        }

        private List<string>[] HydrateAttributes(MdlFile mdl)
        {
            return mdl.SubMeshes.Select(submesh => 
                Enumerable.Range(0,32)
                    .Where(index => ((submesh.AttributeIndexMask >> index) & 1) == 1)
                    .Select(index => mdl.Attributes[index])
                    .ToList()
            ).ToArray();
        }

        public string GetMeshMaterial(int meshIndex) => _materials[meshIndex];

        public void SetMeshMaterial(int meshIndex, string materialPath)
        {
            _materials[meshIndex] = materialPath;

            PersistMaterials();
        }

        private void PersistMaterials()
        {
            var allMaterials = new List<string>();

            foreach (var (material, meshIndex) in _materials.WithIndex())
            {
                var materialIndex = allMaterials.IndexOf(material);
                if (materialIndex == -1)
                {
                    allMaterials.Add(material);
                    materialIndex = allMaterials.Count() - 1;
                }

                Mdl.Meshes[meshIndex].MaterialIndex = (ushort)materialIndex;
            }

            Mdl.Materials = allMaterials.ToArray();
        }

        public IReadOnlyCollection<string> GetSubmeshAttributes(int submeshIndex) => _attributes[submeshIndex]; 

        public void UpdateSubmeshAttribute(int submeshIndex, string? old, string? new_)
        {
            var attributes = _attributes[submeshIndex];

            if (old != null)
                attributes.Remove(old);

            if (new_ != null)
                attributes.Add(new_);

            PersistAttributes();
        }

        private void PersistAttributes()
        {
            var allAttributes = new List<string>();

            foreach (var (attributes, submeshIndex) in _attributes.WithIndex())
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

                Mdl.SubMeshes[submeshIndex].AttributeIndexMask = mask;
            }

            Mdl.Attributes = allAttributes.ToArray();
        }

        public bool Valid => Mdl.Valid;

        public byte[] Write() => Mdl.Write();
    }
}
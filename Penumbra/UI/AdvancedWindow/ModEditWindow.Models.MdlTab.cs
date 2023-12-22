using System.Collections.ObjectModel;
using OtterGui;
using Penumbra.GameData;
using Penumbra.GameData.Files;

namespace Penumbra.UI.AdvancedWindow;

public partial class ModEditWindow
{
    private class MdlTab : IWritable
    {
        public readonly MdlFile Mdl;

        private List<string>[] _attributes;

        public MdlTab(byte[] bytes)
        {
            Mdl = new MdlFile(bytes);
            _attributes = PopulateAttributes();
        }

        public void RemoveMaterial(int materialIndex)
        {
            // Meshes using the removed material are redirected to material 0, and those after the index are corrected.
            for (var meshIndex = 0; meshIndex < Mdl.Meshes.Length; meshIndex++)
            {
                var mesh = Mdl.Meshes[meshIndex];
                if (mesh.MaterialIndex == materialIndex)
                    mesh.MaterialIndex = 0;
                else if (mesh.MaterialIndex > materialIndex)
                    mesh.MaterialIndex -= 1;
            }

            Mdl.Materials = Mdl.Materials.RemoveItems(materialIndex);
        }

        private List<string>[] PopulateAttributes()
        {
            return Mdl.SubMeshes.Select(submesh => 
                Enumerable.Range(0,32)
                    .Where(index => ((submesh.AttributeIndexMask >> index) & 1) == 1)
                    .Select(index => Mdl.Attributes[index])
                    .ToList()
            ).ToArray();
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
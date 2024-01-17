using Lumina.Data.Parsing;
using OtterGui;
using Penumbra.GameData;
using Penumbra.GameData.Files;
using Penumbra.Meta.Manipulations;
using Penumbra.String.Classes;

namespace Penumbra.UI.AdvancedWindow;

public partial class ModEditWindow
{
    private class MdlTab : IWritable
    {
        private readonly ModEditWindow _edit;

        public  MdlFile        Mdl         { get; private set; }
        private List<string>[] _attributes;

        public bool ImportKeepMaterials;
        public bool ImportKeepAttributes;

        public List<Utf8GamePath>? GamePaths { get; private set; }
        public int                 GamePathIndex;

        private bool            _dirty;
        public  bool            PendingIo    { get; private set; }
        public  List<Exception> IoExceptions { get; private set; } = [];

        public MdlTab(ModEditWindow edit, byte[] bytes, string path)
        {
            _edit = edit;

            Initialize(new MdlFile(bytes));

            FindGamePaths(path);
        }

        [MemberNotNull(nameof(Mdl), nameof(_attributes))]
        private void Initialize(MdlFile mdl)
        {
            Mdl = mdl;
            _attributes = CreateAttributes(Mdl);
        }

        /// <inheritdoc/>
        public bool Valid
            => Mdl.Valid;

        /// <inheritdoc/>
        public byte[] Write()
            => Mdl.Write();

        public bool Dirty
        {
            get
            {
                var dirty = _dirty;
                _dirty = false;
                return dirty;
            }
        }

        /// <summary> Find the list of game paths that may correspond to this model. </summary>
        /// <param name="path"> Resolved path to a .mdl. </param>
        private void FindGamePaths(string path)
        {
            // If there's no current mod (somehow), there's nothing to resolve the model within.
            var mod = _edit._editor.Mod;
            if (mod == null)
                return;

            if (!Path.IsPathRooted(path) && Utf8GamePath.FromString(path, out var p))
            {
                GamePaths = [p];
                return;
            }

            PendingIo = true;
            var task = Task.Run(() =>
            {
                // TODO: Is it worth trying to order results based on option priorities for cases where more than one match is found?
                // NOTE: We're using case-insensitive comparisons, as option group paths in mods are stored in lower case, but the mod editor uses paths directly from the file system, which may be mixed case.
                return mod.AllSubMods
                    .SelectMany(m => m.Files.Concat(m.FileSwaps))
                    .Where(kv => kv.Value.FullName.Equals(path, StringComparison.OrdinalIgnoreCase))
                    .Select(kv => kv.Key)
                    .ToList();
            });

            task.ContinueWith(t =>
            {
                RecordIoExceptions(t.Exception);
                GamePaths   = t.Result;
                PendingIo   = false;
            });
        }

        private EstManipulation[] GetCurrentEstManipulations()
        {
            var mod = _edit._editor.Mod;
            var option = _edit._editor.Option;
            if (mod == null || option == null)
                return [];

            // Filter then prepend the current option to ensure it's chosen first.
            return mod.AllSubMods
                .Where(subMod => subMod != option)
                .Prepend(option)
                .SelectMany(subMod => subMod.Manipulations)
                .Where(manipulation => manipulation.ManipulationType is MetaManipulation.Type.Est)
                .Select(manipulation => manipulation.Est)
                .ToArray();
        }

        /// <summary> Export model to an interchange format. </summary>
        /// <param name="outputPath"> Disk path to save the resulting file to. </param>
        /// <param name="mdlPath"> .mdl game path to resolve satellite files such as skeletons relative to. </param>
        public void Export(string outputPath, Utf8GamePath mdlPath)
        {
            IEnumerable<string> sklbPaths;
            try
            {
                sklbPaths = _edit._models.ResolveSklbsForMdl(mdlPath.ToString(), GetCurrentEstManipulations());
            }
            catch (Exception exception)
            {
                RecordIoExceptions(exception);
                return;
            }

            PendingIo = true;
            _edit._models.ExportToGltf(Mdl, sklbPaths, ReadFile, outputPath)
                .ContinueWith(task =>
                {
                    RecordIoExceptions(task.Exception);
                    PendingIo   = false;
                });
        }
		
		/// <summary> Import a model from an interchange format. </summary>
        /// <param name="inputPath"> Disk path to load model data from. </param>
        public void Import(string inputPath)
        {
            PendingIo = true;
            _edit._models.ImportGltf(inputPath)
                .ContinueWith(task =>
                {
                    RecordIoExceptions(task.Exception);
                    if (task is { IsCompletedSuccessfully: true, Result: not null })
                        FinalizeImport(task.Result);
                    PendingIo = false;
                });
        }

        /// <summary> Finalise the import of a .mdl, applying any post-import transformations and state updates. </summary>
        /// <param name="newMdl"> Model data to finalize. </param>
        private void FinalizeImport(MdlFile newMdl)
        {
            if (ImportKeepMaterials)
                MergeMaterials(newMdl, Mdl);

            if (ImportKeepAttributes)
                MergeAttributes(newMdl, Mdl);

            // Until someone works out how to actually author these, unconditionally merge element ids.
            MergeElementIds(newMdl, Mdl);

            // TODO: Add flag editing.
            newMdl.Flags1 = Mdl.Flags1;
            newMdl.Flags2 = Mdl.Flags2;
            
            Initialize(newMdl);
            _dirty = true;
        }
        
        /// <summary> Merge material configuration from the source onto the target. </summary>
        /// <param name="target"> Model that will be updated. </param>
        /// <param name="source"> Model to copy material configuration from. </param>
        public void MergeMaterials(MdlFile target, MdlFile source)
        {
            target.Materials = source.Materials;

            for (var meshIndex = 0; meshIndex < target.Meshes.Length; meshIndex++)
            {
                target.Meshes[meshIndex].MaterialIndex = meshIndex < source.Meshes.Length
                    ? source.Meshes[meshIndex].MaterialIndex
                    : (ushort)0;
            }
        }

        /// <summary> Merge attribute configuration from the source onto the target. </summary>
        /// <param name="target"> Model that will be updated. ></param>
        /// <param name="source"> Model to copy attribute configuration from. </param>
        public static void MergeAttributes(MdlFile target, MdlFile source)
        {
            target.Attributes = source.Attributes;

            var indexEnumerator = Enumerable.Range(0, target.Meshes.Length)
                .SelectMany(mi => Enumerable.Range(0, target.Meshes[mi].SubMeshCount).Select(so => (mi, so)));
            foreach (var (meshIndex, subMeshOffset) in indexEnumerator)
            {
                var subMeshIndex = target.Meshes[meshIndex].SubMeshIndex + subMeshOffset;

                // Preemptively reset the mask in case we need to shortcut out.
                target.SubMeshes[subMeshIndex].AttributeIndexMask = 0u;

                // Rather than comparing sub-meshes directly, we're grouping by parent mesh in an attempt
                // to maintain semantic connection between mesh index and sub mesh attributes.
                if (meshIndex >= source.Meshes.Length)
                    continue;
                var sourceMesh = source.Meshes[meshIndex];

                if (subMeshOffset >= sourceMesh.SubMeshCount)
                    continue;
                var sourceSubMesh = source.SubMeshes[sourceMesh.SubMeshIndex + subMeshOffset];

                target.SubMeshes[subMeshIndex].AttributeIndexMask = sourceSubMesh.AttributeIndexMask;
            }
        }

        /// <summary> Merge element ids from the source onto the target. </summary>
        /// <param name="target"> Model that will be updated. ></param>
        /// <param name="source"> Model to copy element ids from. </param>
        private static void MergeElementIds(MdlFile target, MdlFile source)
        {
            var elementIds = new List<MdlStructs.ElementIdStruct>();

            foreach (var sourceElement in source.ElementIds)
            {
                var sourceBone = source.Bones[sourceElement.ParentBoneName];
                var targetIndex = target.Bones.IndexOf(sourceBone);
                // Given that there's no means of authoring these at the moment, this should probably remain a hard error.
                if (targetIndex == -1)
                    throw new Exception($"Failed to merge element IDs. Original model contains element IDs targeting bone {sourceBone}, which is not present on the imported model.");
                elementIds.Add(sourceElement with
                {
                    ParentBoneName = (uint)targetIndex,
                });
            }

            target.ElementIds = [.. elementIds];
        }

        private void RecordIoExceptions(Exception? exception)
        {
            IoExceptions = exception switch {
                null                  => [],
                AggregateException ae => [.. ae.Flatten().InnerExceptions],
                _                     => [exception],
            };
        }
        
        /// <summary> Read a file from the active collection or game. </summary>
        /// <param name="path"> Game path to the file to load. </param>
        // TODO: Also look up files within the current mod regardless of mod state?
        private byte[] ReadFile(string path)
        {
            // TODO: if cross-collection lookups are turned off, this conversion can be skipped
            if (!Utf8GamePath.FromString(path, out var utf8Path, true))
                throw new Exception($"Resolved path {path} could not be converted to a game path.");

            var resolvedPath = _edit._activeCollections.Current.ResolvePath(utf8Path) ?? new FullPath(utf8Path);

            // TODO: is it worth trying to use streams for these instead? I'll need to do this for mtrl/tex too, so might be a good idea. that said, the mtrl reader doesn't accept streams, so...
            var bytes = resolvedPath.IsRooted
                ? File.ReadAllBytes(resolvedPath.FullName)
                : _edit._gameData.GetFile(resolvedPath.InternalName.ToString())?.Data;

            // TODO: some callers may not care about failures - handle exceptions separately?
            return bytes ?? throw new Exception(
                $"Resolved path {path} could not be found. If modded, is it enabled in the current collection?");
        }

        /// <summary> Remove the material given by the index. </summary>
        /// <remarks> Meshes using the removed material are redirected to material 0, and those after the index are corrected. </remarks>
        public void RemoveMaterial(int materialIndex)
        {
            for (var meshIndex = 0; meshIndex < Mdl.Meshes.Length; meshIndex++)
            {
                var newIndex = Mdl.Meshes[meshIndex].MaterialIndex;
                if (newIndex == materialIndex)
                    newIndex = 0;
                else if (newIndex > materialIndex)
                    --newIndex;

                Mdl.Meshes[meshIndex].MaterialIndex = newIndex;
            }

            Mdl.Materials = Mdl.Materials.RemoveItems(materialIndex);
        }

        /// <summary> Create a list of attributes per sub mesh. </summary>
        private static List<string>[] CreateAttributes(MdlFile mdl)
            => mdl.SubMeshes.Select(s => Enumerable.Range(0, 32)
                .Where(idx => ((s.AttributeIndexMask >> idx) & 1) == 1)
                .Select(idx => mdl.Attributes[idx])
                .ToList()
            ).ToArray();

        /// <summary> Obtain the attributes associated with a sub mesh by its index. </summary>
        public IReadOnlyList<string> GetSubMeshAttributes(int subMeshIndex)
            => _attributes[subMeshIndex];

        /// <summary> Remove or add attributes from a sub mesh by its index. </summary>
        /// <param name="subMeshIndex"> The index of the sub mesh to update. </param>
        /// <param name="old"> If non-null, remove this attribute. </param>
        /// <param name="new"> If non-null, add this attribute. </param>
        public void UpdateSubMeshAttribute(int subMeshIndex, string? old, string? @new)
        {
            var attributes = _attributes[subMeshIndex];

            if (old != null)
                attributes.Remove(old);

            if (@new != null)
                attributes.Add(@new);

            PersistAttributes();
        }

        /// <summary> Apply changes to attributes to the file in memory. </summary>
        private void PersistAttributes()
        {
            var allAttributes = new List<string>();

            foreach (var (attributes, subMeshIndex) in _attributes.WithIndex())
            {
                var mask = 0u;

                foreach (var attribute in attributes)
                {
                    var attributeIndex = allAttributes.IndexOf(attribute);
                    if (attributeIndex == -1)
                    {
                        allAttributes.Add(attribute);
                        attributeIndex = allAttributes.Count - 1;
                    }

                    mask |= 1u << attributeIndex;
                }

                Mdl.SubMeshes[subMeshIndex].AttributeIndexMask = mask;
            }

            Mdl.Attributes = [.. allAttributes];
        }
    }
}

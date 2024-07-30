using OtterGui;
using Penumbra.GameData;
using Penumbra.GameData.Files;
using Penumbra.Import.Models;
using Penumbra.Import.Models.Export;
using Penumbra.Meta.Manipulations;
using Penumbra.String.Classes;

namespace Penumbra.UI.AdvancedWindow;

public partial class ModEditWindow
{
    private class MdlTab : IWritable
    {
        private readonly ModEditWindow _edit;

        public  MdlFile         Mdl { get; private set; }
        private List<string>?[] _attributes;

        public bool ImportKeepMaterials;
        public bool ImportKeepAttributes;

        public ExportConfig ExportConfig;

        public List<Utf8GamePath>? GamePaths { get; private set; }
        public int                 GamePathIndex;

        private bool            _dirty;
        public  bool            PendingIo    { get; private set; }
        public  List<Exception> IoExceptions { get; } = [];
        public  List<string>    IoWarnings   { get; } = [];

        public MdlTab(ModEditWindow edit, byte[] bytes, string path)
        {
            _edit = edit;

            Initialize(new MdlFile(bytes));

            FindGamePaths(path);
        }

        [MemberNotNull(nameof(Mdl), nameof(_attributes))]
        private void Initialize(MdlFile mdl)
        {
            Mdl         = mdl;
            _attributes = CreateAttributes(Mdl);
        }

        /// <inheritdoc/>
        public bool Valid
            => Mdl.Valid && Mdl.Materials.All(ValidateMaterial);

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

            BeginIo();
            var task = Task.Run(() =>
            {
                // TODO: Is it worth trying to order results based on option priorities for cases where more than one match is found?
                // NOTE: We're using case-insensitive comparisons, as option group paths in mods are stored in lower case, but the mod editor uses paths directly from the file system, which may be mixed case.
                return mod.AllDataContainers
                    .SelectMany(m => m.Files.Concat(m.FileSwaps))
                    .Where(kv => kv.Value.FullName.Equals(path, StringComparison.OrdinalIgnoreCase))
                    .Select(kv => kv.Key)
                    .ToList();
            });

            task.ContinueWith(t => { GamePaths = FinalizeIo(t); }, TaskScheduler.Default);
        }

        private KeyValuePair<EstIdentifier, EstEntry>[] GetCurrentEstManipulations()
        {
            var mod    = _edit._editor.Mod;
            var option = _edit._editor.Option;
            if (mod == null || option == null)
                return [];

            // Filter then prepend the current option to ensure it's chosen first.
            return mod.AllDataContainers
                .Where(subMod => subMod != option)
                .Prepend(option)
                .SelectMany(subMod => subMod.Manipulations.Est)
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

            BeginIo();
            _edit._models.ExportToGltf(ExportConfig, Mdl, sklbPaths, ReadFile, outputPath)
                .ContinueWith(FinalizeIo, TaskScheduler.Default);
        }

        /// <summary> Import a model from an interchange format. </summary>
        /// <param name="inputPath"> Disk path to load model data from. </param>
        public void Import(string inputPath)
        {
            BeginIo();
            _edit._models.ImportGltf(inputPath)
                .ContinueWith(task =>
                {
                    var mdlFile = FinalizeIo(task, result => result.Item1, result => result.Item2);
                    if (mdlFile != null)
                        FinalizeImport(mdlFile);
                }, TaskScheduler.Default);
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
        private static void MergeMaterials(MdlFile target, MdlFile source)
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
        private static void MergeAttributes(MdlFile target, MdlFile source)
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
            // This is overly simplistic, but effectively reproduces what TT did, sort of.
            // TODO: Get a better idea of what these values represent. `ParentBoneName`, if it is a pointer into the bone array, does not seem to be _bounded_ by the bone array length, at least in the model. I'm guessing it _may_ be pointing into a .sklb instead? (i.e. the weapon's skeleton). EID stuff in general needs more work.
            target.ElementIds = [.. source.ElementIds];
        }

        private void BeginIo()
        {
            PendingIo = true;
            IoWarnings.Clear();
            IoExceptions.Clear();
        }

        private void FinalizeIo(Task<IoNotifier> task)
            => FinalizeIo<IoNotifier, object?>(task, _ => null, notifier => notifier);

        private TResult? FinalizeIo<TResult>(Task<TResult> task)
            => FinalizeIo(task, result => result, null);

        private TResult? FinalizeIo<TTask, TResult>(Task<TTask> task, Func<TTask, TResult> getResult, Func<TTask, IoNotifier>? getNotifier)
        {
            TResult? result = default;
            RecordIoExceptions(task.Exception);
            if (task is { IsCompletedSuccessfully: true, Result: not null })
            {
                result = getResult(task.Result);
                if (getNotifier != null)
                    IoWarnings.AddRange(getNotifier(task.Result).GetWarnings());
            }

            PendingIo = false;

            return result;
        }

        private void RecordIoExceptions(Exception? exception)
        {
            switch (exception)
            {
                case null: break;
                case AggregateException ae:
                    IoExceptions.AddRange(ae.Flatten().InnerExceptions);
                    break;
                default:
                    IoExceptions.Add(exception);
                    break;
            }
        }

        /// <summary> Read a file from the active collection or game. </summary>
        /// <param name="path"> Game path to the file to load. </param>
        // TODO: Also look up files within the current mod regardless of mod state?
        private byte[]? ReadFile(string path)
        {
            // TODO: if cross-collection lookups are turned off, this conversion can be skipped
            if (!Utf8GamePath.FromString(path, out var utf8Path))
                throw new Exception($"Resolved path {path} could not be converted to a game path.");

            var resolvedPath = _edit._activeCollections.Current.ResolvePath(utf8Path) ?? new FullPath(utf8Path);

            // TODO: is it worth trying to use streams for these instead? I'll need to do this for mtrl/tex too, so might be a good idea. that said, the mtrl reader doesn't accept streams, so...
            return resolvedPath.IsRooted
                ? File.ReadAllBytes(resolvedPath.FullName)
                : _edit._gameData.GetFile(resolvedPath.InternalName.ToString())?.Data;
        }

        /// <summary> Validate the specified material. </summary>
        /// <remarks>
        /// While materials can be relative (`/mt_...`) or absolute (`bg/...`),
        /// they invariably must contain at least one directory seperator.
        /// Missing this can lead to a crash.
        /// 
        /// They must also be at least one character (though this is enforced
        /// by containing a `/`), and end with `.mtrl`.
        /// </remarks>
        public bool ValidateMaterial(string material)
        {
            return material.Contains('/') && material.EndsWith(".mtrl");
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
        private static List<string>?[] CreateAttributes(MdlFile mdl)
            => mdl.SubMeshes.Select(s =>
            {
                var maxAttribute = 31 - BitOperations.LeadingZeroCount(s.AttributeIndexMask);
                // TODO: Research what results in this - it seems to primarily be reproducible on bgparts, is it garbage data, or an alternative usage of the value?
                return maxAttribute < mdl.Attributes.Length
                    ? Enumerable.Range(0, 32)
                        .Where(idx => ((s.AttributeIndexMask >> idx) & 1) == 1)
                        .Select(idx => mdl.Attributes[idx])
                        .ToList()
                    : null;
            }).ToArray();

        /// <summary> Obtain the attributes associated with a sub mesh by its index. </summary>
        public IReadOnlyList<string>? GetSubMeshAttributes(int subMeshIndex)
            => _attributes[subMeshIndex];

        /// <summary> Remove or add attributes from a sub mesh by its index. </summary>
        /// <param name="subMeshIndex"> The index of the sub mesh to update. </param>
        /// <param name="old"> If non-null, remove this attribute. </param>
        /// <param name="new"> If non-null, add this attribute. </param>
        public void UpdateSubMeshAttribute(int subMeshIndex, string? old, string? @new)
        {
            var attributes = _attributes[subMeshIndex];
            if (attributes == null)
                return;

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
                if (attributes == null)
                    continue;

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

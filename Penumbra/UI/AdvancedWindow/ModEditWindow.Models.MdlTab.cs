using OtterGui;
using Penumbra.GameData;
using Penumbra.GameData.Files;
using Penumbra.Mods;
using Penumbra.String.Classes;

namespace Penumbra.UI.AdvancedWindow;

public partial class ModEditWindow
{
    private partial class MdlTab : IWritable
    {
        private ModEditWindow _edit;

        public readonly  MdlFile        Mdl;
        private readonly List<string>[] _attributes;

        public List<Utf8GamePath>? GamePaths { get; private set; }
        public int                 GamePathIndex;

        public bool    PendingIo   { get; private set; } = false;
        public string? IoException { get; private set; } = null;

        [GeneratedRegex(@"chara/(?:equipment|accessory)/(?'Set'[a-z]\d{4})/model/(?'Race'c\d{4})\k'Set'_[^/]+\.mdl", RegexOptions.Compiled)]
        private static partial Regex CharaEquipmentRegex();

        [GeneratedRegex(@"chara/human/(?'Race'c\d{4})/obj/(?'Type'[^/]+)/(?'Set'[^/]\d{4})/model/(?'Race'c\d{4})\k'Set'_[^/]+\.mdl",
            RegexOptions.Compiled)]
        private static partial Regex CharaHumanRegex();

        [GeneratedRegex(@"chara/(?'SubCategory'demihuman|monster|weapon)/(?'Set'w\d{4})/obj/body/(?'Body'b\d{4})/model/\k'Set'\k'Body'.mdl",
            RegexOptions.Compiled)]
        private static partial Regex CharaBodyRegex();

        public MdlTab(ModEditWindow edit, byte[] bytes, string path, Mod? mod)
        {
            _edit = edit;

            Mdl         = new MdlFile(bytes);
            _attributes = CreateAttributes(Mdl);

            if (mod != null)
                FindGamePaths(path, mod);
        }

        /// <inheritdoc/>
        public bool Valid
            => Mdl.Valid;

        /// <inheritdoc/>
        public byte[] Write()
            => Mdl.Write();

        /// <summary> Find the list of game paths that may correspond to this model. </summary>
        /// <param name="path"> Resolved path to a .mdl. </param>
        /// <param name="mod"> Mod within which the .mdl is resolved. </param>
        private void FindGamePaths(string path, Mod mod)
        {
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
                IoException = t.Exception?.ToString();
                PendingIo   = false;
                GamePaths   = t.Result;
            });
        }

        /// <summary> Export model to an interchange format. </summary>
        /// <param name="outputPath"> Disk path to save the resulting file to. </param>
        public void Export(string outputPath, Utf8GamePath mdlPath)
        {
            SklbFile? sklb = null;
            try
            {
                var sklbPath = GetSklbPath(mdlPath.ToString());
                sklb = sklbPath != null ? ReadSklb(sklbPath) : null;
            }
            catch (Exception exception)
            {
                IoException = exception?.ToString();
                return;
            }

            PendingIo = true;
            _edit._models.ExportToGltf(Mdl, sklb, outputPath)
                .ContinueWith(task =>
                {
                    IoException = task.Exception?.ToString();
                    PendingIo   = false;
                });
        }

        /// <summary> Try to find the .sklb path for a .mdl file. </summary>
        /// <param name="mdlPath"> .mdl file to look up the skeleton for. </param>
        private string? GetSklbPath(string mdlPath)
        {
            // Equipment is skinned to the base body skeleton of the race they target.
            var match = CharaEquipmentRegex().Match(mdlPath);
            if (match.Success)
            {
                var race = match.Groups["Race"].Value;
                return $"chara/human/{race}/skeleton/base/b0001/skl_{race}b0001.sklb";
            }

            // Some parts of human have their own skeletons.
            match = CharaHumanRegex().Match(mdlPath);
            if (match.Success)
            {
                var type = match.Groups["Type"].Value;
                var race = match.Groups["Race"].Value;
                return type switch
                {
                    "body" or "tail" => $"chara/human/{race}/skeleton/base/b0001/skl_{race}b0001.sklb",
                    _                => throw new Exception($"Currently unsupported human model type \"{type}\"."),
                };
            }

            // A few subcategories - such as weapons, demihumans, and monsters - have dedicated per-"body" skeletons.
            match = CharaBodyRegex().Match(mdlPath);
            if (match.Success)
            {
                var subCategory = match.Groups["SubCategory"].Value;
                var set         = match.Groups["Set"].Value;
                return $"chara/{subCategory}/{set}/skeleton/base/b0001/skl_{set}b0001.sklb";
            }

            return null;
        }

        /// <summary> Read a .sklb from the active collection or game. </summary>
        /// <param name="sklbPath"> Game path to the .sklb to load. </param>
        private SklbFile ReadSklb(string sklbPath)
        {
            // TODO: if cross-collection lookups are turned off, this conversion can be skipped
            if (!Utf8GamePath.FromString(sklbPath, out var utf8SklbPath, true))
                throw new Exception($"Resolved skeleton path {sklbPath} could not be converted to a game path.");

            var resolvedPath = _edit._activeCollections.Current.ResolvePath(utf8SklbPath);
            // TODO: is it worth trying to use streams for these instead? i'll need to do this for mtrl/tex too, so might be a good idea. that said, the mtrl reader doesn't accept streams, so...
            var bytes = resolvedPath switch
            {
                null          => _edit._gameData.GetFile(sklbPath)?.Data,
                FullPath path => File.ReadAllBytes(path.ToPath()),
            };
            if (bytes == null)
                throw new Exception(
                    $"Resolved skeleton path {sklbPath} could not be found. If modded, is it enabled in the current collection?");

            return new SklbFile(bytes);
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

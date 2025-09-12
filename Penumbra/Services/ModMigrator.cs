using Dalamud.Plugin.Services;
using Luna;
using Penumbra.Api.Enums;
using Penumbra.GameData.Data;
using Penumbra.GameData.Files;
using Penumbra.GameData.Files.MaterialStructs;
using Penumbra.GameData.Structs;
using Penumbra.Import.Textures;
using Penumbra.Mods;
using Penumbra.Mods.SubMods;
using Penumbra.String.Classes;

namespace Penumbra.Services;

public class ModMigrator(IDataManager gameData, TextureManager textures) : IService
{
    private sealed class FileDataDict : ListDictionary<string, (string GamePath, IModDataContainer Container)>;

    private readonly Lazy<MtrlFile> _glassReferenceMaterial = new(() =>
    {
        var bytes = gameData.GetFile("chara/equipment/e5001/material/v0001/mt_c0101e5001_met_b.mtrl");
        return new MtrlFile(bytes!.Data);
    });

    private readonly HashSet<Mod> _changedMods = [];
    private readonly HashSet<Mod> _failedMods  = [];

    private readonly FileDataDict Textures  = [];
    private readonly FileDataDict Models    = [];
    private readonly FileDataDict Materials = [];
    private readonly FileDataDict FileSwaps = [];

    private readonly ConcurrentBag<string> _messages = [];

    public void Update(IEnumerable<Mod> mods)
    {
        CollectFiles(mods);
        foreach (var (from, (to, container)) in FileSwaps)
            MigrateFileSwaps(from, to, container);
        foreach (var (model, list) in Models.Grouped)
            MigrateModel(model, (Mod)list[0].Container.Mod);
    }

    private void CollectFiles(IEnumerable<Mod> mods)
    {
        foreach (var mod in mods)
        {
            foreach (var container in mod.AllDataContainers)
            {
                foreach (var (gamePath, file) in container.Files)
                {
                    switch (ResourceTypeExtensions.FromExtension(gamePath.Extension().Span))
                    {
                        case ResourceType.Tex:  Textures.TryAdd(file.FullName, (gamePath.ToString(), container)); break;
                        case ResourceType.Mdl:  Models.TryAdd(file.FullName, (gamePath.ToString(), container)); break;
                        case ResourceType.Mtrl: Materials.TryAdd(file.FullName, (gamePath.ToString(), container)); break;
                    }
                }

                foreach (var (swapFrom, swapTo) in container.FileSwaps)
                    FileSwaps.TryAdd(swapTo.FullName, (swapFrom.ToString(), container));
            }
        }
    }

    public Task CreateIndexFile(string normalPath, string targetPath)
    {
        const int rowBlend = 17;

        return Task.Run(async () =>
        {
            var tex      = textures.LoadTex(normalPath);
            var data     = tex.GetPixelData();
            var rgbaData = new RgbaPixelData(data.Width, data.Height, data.Rgba);
            if (!BitOperations.IsPow2(rgbaData.Height) || !BitOperations.IsPow2(rgbaData.Width))
            {
                var requiredHeight = (int)BitOperations.RoundUpToPowerOf2((uint)rgbaData.Height);
                var requiredWidth  = (int)BitOperations.RoundUpToPowerOf2((uint)rgbaData.Width);
                rgbaData = rgbaData.Resize((requiredWidth, requiredHeight));
            }

            Parallel.ForEach(Enumerable.Range(0, rgbaData.PixelData.Length / 4), idx =>
            {
                var pixelIdx = 4 * idx;
                var normal   = rgbaData.PixelData[pixelIdx + 3];

                // Copied from TT
                var blendRem    = normal % (2 * rowBlend);
                var originalRow = normal / rowBlend;
                switch (blendRem)
                {
                    // Goes to next row, clamped to the closer row.
                    case > 25:
                        blendRem = 0;
                        ++originalRow;
                        break;
                    // Stays in this row, clamped to the closer row.
                    case > 17: blendRem = 17; break;
                }

                var newBlend = (byte)(255 - MathF.Round(blendRem / 17f * 255f));

                // Slight add here to push the color deeper into the row to ensure BC5 compression doesn't
                // cause any artifacting.
                var newRow = (byte)(originalRow / 2 * 17 + 4);

                rgbaData.PixelData[pixelIdx] = newRow;
                rgbaData.PixelData[pixelIdx] = newBlend;
                rgbaData.PixelData[pixelIdx] = 0;
                rgbaData.PixelData[pixelIdx] = 255;
            });
            await textures.SaveAs(CombinedTexture.TextureSaveType.BC5, true, true, new BaseImage(), targetPath, rgbaData.PixelData,
                rgbaData.Width, rgbaData.Height);
        });
    }

    private void MigrateModel(string filePath, Mod mod)
    {
        if (MigrationManager.TryMigrateSingleModel(filePath, true))
        {
            _messages.Add($"Migrated model {filePath} in {mod.Name}.");
        }
        else
        {
            _messages.Add($"Failed to migrate model {filePath} in {mod.Name}");
            _failedMods.Add(mod);
        }
    }

    private void SetGlassReferenceValues(MtrlFile mtrl)
    {
        var reference = _glassReferenceMaterial.Value;
        mtrl.ShaderPackage.ShaderKeys =  reference.ShaderPackage.ShaderKeys.ToArray();
        mtrl.ShaderPackage.Constants  =  reference.ShaderPackage.Constants.ToArray();
        mtrl.AdditionalData           =  reference.AdditionalData.ToArray();
        mtrl.ShaderPackage.Flags      &= ~(0x04u | 0x08u);
        // From TT.
        if (mtrl.Table is ColorTable t)
            foreach (ref var row in t.AsRows())
                row.SpecularColor = new HalfColor((Half)0.8100586, (Half)0.8100586, (Half)0.8100586);
    }

    private ref struct MaterialPack
    {
        public readonly MtrlFile File;
        public readonly bool     UsesMaskAsSpecular;

        private readonly Dictionary<TextureUsage, SamplerIndex> Samplers = [];

        public MaterialPack(MtrlFile file)
        {
            File               = file;
            UsesMaskAsSpecular = File.ShaderPackage.ShaderKeys.Any(x => x.Key is 0xC8BD1DEF && x.Value is 0xA02F4828 or 0x198D11CD);
            Add(Samplers, TextureUsage.Normal,   ShpkFile.NormalSamplerId);
            Add(Samplers, TextureUsage.Index,    ShpkFile.IndexSamplerId);
            Add(Samplers, TextureUsage.Mask,     ShpkFile.MaskSamplerId);
            Add(Samplers, TextureUsage.Diffuse,  ShpkFile.DiffuseSamplerId);
            Add(Samplers, TextureUsage.Specular, ShpkFile.SpecularSamplerId);
            return;

            void Add(Dictionary<TextureUsage, SamplerIndex> dict, TextureUsage usage, uint samplerId)
            {
                var idx = new SamplerIndex(file, samplerId);
                if (idx.Texture >= 0)
                    dict.Add(usage, idx);
            }
        }

        public readonly record struct SamplerIndex(int Sampler, int Texture)
        {
            public SamplerIndex(MtrlFile file, uint samplerId)
                : this(file.FindSampler(samplerId), -1)
                => Texture = Sampler < 0 ? -1 : file.ShaderPackage.Samplers[Sampler].TextureIndex;
        }

        public enum TextureUsage
        {
            Unknown,
            Normal,
            Index,
            Mask,
            Diffuse,
            Specular,
        }

        public static bool AdaptPath(IDataManager data, string path, TextureUsage usage, out string newPath)
        {
            newPath = path;
            if (Path.GetExtension(newPath) is not ".tex")
                return false;

            if (data.FileExists(newPath))
                return true;

            ReadOnlySpan<(string, string)> pairs = usage switch
            {
                TextureUsage.Unknown =>
                [
                    ("_n.tex", "_norm.tex"),
                    ("_m.tex", "_mult.tex"),
                    ("_m.tex", "_mask.tex"),
                    ("_d.tex", "_base.tex"),
                ],
                TextureUsage.Normal =>
                [
                    ("_n_", "_norm_"),
                    ("_n.tex", "_norm.tex"),
                ],
                TextureUsage.Mask =>
                [
                    ("_m_", "_mult_"),
                    ("_m_", "_mask_"),
                    ("_m.tex", "_mult.tex"),
                    ("_m.tex", "_mask.tex"),
                ],
                TextureUsage.Diffuse =>
                [
                    ("_d_", "_base_"),
                    ("_d.tex", "_base.tex"),
                ],
                TextureUsage.Index    => [],
                TextureUsage.Specular => [],
                _                     => [],
            };
            foreach (var (from, to) in pairs)
            {
                newPath = path.Replace(from, to);
                if (data.FileExists(newPath))
                    return true;
            }

            return false;
        }
    }

    private void MigrateMaterial(string filePath, IReadOnlyList<(string GamePath, IModDataContainer Container)> redirections)
    {
        try
        {
            var bytes = File.ReadAllBytes(filePath);
            var mtrl  = new MtrlFile(bytes);
            if (!CheckUpdateNeeded(mtrl))
                return;

            // Update colorsets, flags and character shader package.
            var changes = mtrl.MigrateToDawntrail();

            if (!changes)
                switch (mtrl.ShaderPackage.Name)
                {
                    case "hair.shpk": break;
                    case "characterglass.shpk":
                        SetGlassReferenceValues(mtrl);
                        changes = true;
                        break;
                }

            // Remove DX11 flags and update paths if necessary.
            foreach (ref var tex in mtrl.Textures.AsSpan())
            {
                if (tex.DX11)
                {
                    changes = true;
                    if (GamePaths.Tex.HandleDx11Path(tex, out var newPath))
                        tex.Path = newPath;
                    tex.DX11 = false;
                }

                if (gameData.FileExists(tex.Path))
                    continue;
            }

            // Dyeing, from TT.
            if (mtrl.DyeTable is ColorDyeTable dye)
                foreach (ref var row in dye.AsRows())
                    row.Template += 1000;
        }
        catch
        {
            // ignored
        }

        static bool CheckUpdateNeeded(MtrlFile mtrl)
        {
            if (!mtrl.IsDawntrail)
                return true;

            if (mtrl.ShaderPackage.Name is not "hair.shpk")
                return false;

            var foundOld = 0;
            foreach (var c in mtrl.ShaderPackage.Constants)
            {
                switch (c.Id)
                {
                    case 0x36080AD0: foundOld |= 1; break; // == 1, from TT
                    case 0x992869AB: foundOld |= 2; break; // == 3 (skin) or 4 (hair) from TT
                }

                if (foundOld is 3)
                    return true;
            }

            return false;
        }
    }

    private void MigrateFileSwaps(string swapFrom, string swapTo, IModDataContainer container)
    {
        var fromExists = gameData.FileExists(swapFrom);
        var toExists   = gameData.FileExists(swapTo);
        if (fromExists && toExists)
            return;

        if (ResourceTypeExtensions.FromExtension(Path.GetExtension(swapFrom.AsSpan())) is not ResourceType.Tex
         || ResourceTypeExtensions.FromExtension(Path.GetExtension(swapTo.AsSpan())) is not ResourceType.Tex)
        {
            _messages.Add(
                $"Could not migrate file swap {swapFrom} -> {swapTo} in {container.Mod.Name}: {container.GetFullName()}. Only textures may be migrated.{(fromExists ? "\n\tSource File does not exist." : "")}{(toExists ? "\n\tTarget File does not exist." : "")}");
            return;
        }

        var newSwapFrom = swapFrom;
        if (!fromExists && !MaterialPack.AdaptPath(gameData, swapFrom, MaterialPack.TextureUsage.Unknown, out newSwapFrom))
        {
            _messages.Add($"Could not migrate file swap {swapFrom} -> {swapTo} in {container.Mod.Name}: {container.GetFullName()}.");
            return;
        }

        var newSwapTo = swapTo;
        if (!toExists && !MaterialPack.AdaptPath(gameData, swapTo, MaterialPack.TextureUsage.Unknown, out newSwapTo))
        {
            _messages.Add($"Could not migrate file swap {swapFrom} -> {swapTo} in {container.Mod.Name}: {container.GetFullName()}.");
            return;
        }

        if (!Utf8GamePath.FromString(swapFrom, out var path) || !Utf8GamePath.FromString(newSwapFrom, out var newPath))
        {
            _messages.Add(
                $"Could not migrate file swap {swapFrom} -> {swapTo} in {container.Mod.Name}: {container.GetFullName()}. Unknown Error.");
            return;
        }

        container.FileSwaps.Remove(path);
        container.FileSwaps.Add(newPath, new FullPath(newSwapTo));
        _changedMods.Add((Mod)container.Mod);
    }
}

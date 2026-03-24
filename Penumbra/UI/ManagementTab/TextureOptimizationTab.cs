using ImSharp;
using Lumina.Data.Files;
using Luna;
using OtterTex;
using Penumbra.Communication;
using Penumbra.Import.Textures;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.Mods.SubMods;
using Penumbra.String.Classes;

namespace Penumbra.UI.ManagementTab;

public sealed class TextureOptimizationTab(ModManager mods, TextureManager textures, UiNavigator navigator) : ITab<ManagementTabType>
{
    private static long _lowerSizeLimit      = 1 << 20;
    private static int  _smallDimensionLimit = 32;
    private static int  _largeDimensionLimit = 4096;

    public ReadOnlySpan<byte> Label
        => "Texture Optimization (WIP)"u8;

    public void DrawContent()
    {
        Im.Item.SetNextWidthScaled(100);
        ImEx.LogarithmicInput("Ignore Textures Below This Size"u8, FormattingFunctions.HumanReadableSize(_lowerSizeLimit), ref _lowerSizeLimit);
        Im.Item.SetNextWidthScaled(100);
        ImEx.LogarithmicInput("Ignore Textures With Smaller Dimensions"u8, ref _smallDimensionLimit);
        Im.Item.SetNextWidthScaled(100);
        ImEx.LogarithmicInput("Show Textures With Larger Dimensions, Even If Compressed"u8, ref _largeDimensionLimit);

        var cache = CacheManager.Instance.GetOrCreateCache(Im.Id.Current, () => new Cache(mods, textures));
        ManagementTab.DrawScanButtons(cache.Scanner);

        using var table = Im.Table.Begin("t"u8, 7,
            TableFlags.RowBackground | TableFlags.SizingFixedFit | TableFlags.ScrollX | TableFlags.ScrollY | TableFlags.BordersOuter,
            Im.ContentRegion.Available);
        if (!table)
            return;

        var       data = cache.Scanner.GetCurrentList();
        var       id   = new Im.IdDisposable();
        using var clip = new Im.ListClipper(data.Count, Im.Style.TextHeightWithSpacing);
        foreach (var (idx, file) in clip.Iterate(data).Index())
        {
            id.Push(idx);
            if (!file.Container.TryGetTarget(out var container))
                continue;

            table.DrawColumn($"{file.GamePath}");
            table.DrawColumn(file.Redirection.FullName);
            table.NextColumn();
            if (Im.Selectable(container.GetFullName()))
                navigator.OpenTo(container.Mod as Mod);
            table.DrawColumn(file.Size < 0 ? "MISSING"u8 : FormattingFunctions.HumanReadableSize(file.Size));
            table.DrawColumn($"{file.Width}x{file.Height}");
            table.DrawColumn($"{file.Format}");
            if (!file.SolidColor.IsDefault)
                table.DrawColumn($"Solid {file.SolidColor.Color!.Value}");
            else
                table.NextColumn();
            id.Pop();
        }
    }

    public ManagementTabType Identifier
        => ManagementTabType.TextureOptimization;

    private sealed class Cache(ModManager mods, TextureManager textures) : BasicCache(TimeSpan.FromMinutes(10))
    {
        public sealed class OptimizableTextureRedirection : BaseScannedRedirection
        {
            public readonly long           Size;
            public readonly bool           Invalid;
            public readonly bool           OptimizedTexture;
            public readonly int            Width;
            public readonly int            Height;
            public readonly DXGIFormat     Format;
            public readonly ColorParameter SolidColor;

            /// <summary> Invalid.  </summary>
            public OptimizableTextureRedirection(Utf8GamePath gamePath, FullPath redirection, IModDataContainer container)
                : base(gamePath, redirection, container, false)
            {
                OptimizedTexture = false;
                Invalid          = true;
                Size             = -1;
            }

            /// <summary> Small. </summary>
            public OptimizableTextureRedirection(Utf8GamePath gamePath, FullPath redirection, IModDataContainer container, bool small)
                : base(gamePath, redirection, container, false)
            {
                Invalid          = false;
                OptimizedTexture = small;
            }

            public OptimizableTextureRedirection(Utf8GamePath gamePath, FullPath redirection, IModDataContainer container, long size,
                DXGIFormat format, int width, int height)
                : base(gamePath, redirection, container, false)
            {
                Invalid          = false;
                OptimizedTexture = false;
                Size             = size;
                Format           = format;
                Width            = width;
                Height           = height;
            }

            public OptimizableTextureRedirection(Utf8GamePath gamePath, FullPath redirection, IModDataContainer container, long size,
                DXGIFormat format, Rgba32 solidColor, int width, int height)
                : base(gamePath, redirection, container, false)
            {
                Invalid          = false;
                OptimizedTexture = false;
                Size             = size;
                Format           = format;
                SolidColor       = solidColor;
                Width            = width;
                Height           = height;
            }

            public override bool DataPredicate()
                => !Invalid && !OptimizedTexture;
        }

        public sealed class TextureOptimizationScanner(ModManager mods, TextureManager textures)
            : RedirectionScanner<OptimizableTextureRedirection>(mods)
        {
            protected override unsafe OptimizableTextureRedirection Create(Utf8GamePath gamePath, FullPath redirection,
                IModDataContainer container, bool swap)
            {
                try
                {
                    // We have checked existence before.
                    var fileInfo = new FileInfo(redirection.FullName);
                    var size     = fileInfo.Length;
                    if (size <= sizeof(TexFile.TexHeader))
                        return new OptimizableTextureRedirection(gamePath, redirection, container);
                    if (size <= _lowerSizeLimit)
                        return new OptimizableTextureRedirection(gamePath, redirection, container, true);

                    var data = textures.LoadTex(fileInfo.FullName);
                    if (data.Width <= _smallDimensionLimit && data.Height <= _smallDimensionLimit)
                        return new OptimizableTextureRedirection(gamePath, redirection, container, true);

                    var large        = data.Width >= _largeDimensionLimit || data.Height >= _largeDimensionLimit;
                    var uncompressed = !data.Format.IsCompressed();
                    var solid        = data.IsSolidColor();
                    if (!solid.IsDefault)
                        return new OptimizableTextureRedirection(gamePath, redirection, container, size, data.Format, solid.Color!.Value,
                            data.Width, data.Height);

                    if (large || uncompressed)
                        return new OptimizableTextureRedirection(gamePath, redirection, container, size, data.Format, data.Width,
                            data.Height);
                }
                catch
                {
                    return new OptimizableTextureRedirection(gamePath, redirection, container);
                }

                return new OptimizableTextureRedirection(gamePath, redirection, container, true);
            }


            protected override bool DoCreateRedirection(Utf8GamePath gamePath, FullPath redirection, IModDataContainer container, bool swap)
            {
                if (swap)
                    return false;

                if (!File.Exists(redirection.FullName))
                    return false;

                if (redirection.Extension is not ".tex" and not ".atex")
                    return false;

                return true;
            }
        }

        public readonly TextureOptimizationScanner Scanner = new(mods, textures);

        public override void Update()
        { }
    }
}

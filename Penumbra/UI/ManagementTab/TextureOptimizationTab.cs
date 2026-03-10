using Dalamud.Plugin.Services;
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
using TerraFX.Interop.Windows;

namespace Penumbra.UI.ManagementTab;

public sealed class TextureOptimizationTab(ModManager mods, TextureManager textures, UiNavigator navigator) : ITab<ManagementTabType>
{
    public ReadOnlySpan<byte> Label
        => "Texture Optimization"u8;

    public void DrawContent()
    {
        var cache = CacheManager.Instance.GetOrCreateCache(Im.Id.Current, () => new Cache(mods, textures));
        if (Im.Button("Scan"u8))
            cache.Scanner.ScanRedirections();
        Im.Line.Same();
        var running = cache.Scanner.Running;
        if (ImEx.Button("Cancel"u8, default, StringU8.Empty, !running))
            cache.Scanner.Cancel();
        if (running)
        {
            Im.Line.Same();
            Im.ProgressBar(cache.Scanner.Progress, ImEx.ScaledVectorX(200));
        }

        using var table = Im.Table.Begin("t"u8, 7, TableFlags.RowBackground | TableFlags.SizingFixedFit, Im.ContentRegion.Available);
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
            public readonly bool           LargeTexture;
            public readonly int            Width;
            public readonly int            Height;
            public readonly DXGIFormat     Format;
            public readonly ColorParameter SolidColor;

            /// <summary> Invalid.  </summary>
            public unsafe OptimizableTextureRedirection(Utf8GamePath gamePath, FullPath redirection, IModDataContainer container)
                : base(gamePath, redirection, container, false)
            {
                OptimizedTexture = false;
                Invalid          = true;
                Size             = -1;
            }

            /// <summary> Small. </summary>
            public unsafe OptimizableTextureRedirection(Utf8GamePath gamePath, FullPath redirection, IModDataContainer container, bool small)
                : base(gamePath, redirection, container, false)
            {
                Invalid          = false;
                OptimizedTexture = small;
            }

            public unsafe OptimizableTextureRedirection(Utf8GamePath gamePath, FullPath redirection, IModDataContainer container, long size,
                DXGIFormat format, bool large, int width, int height)
                : base(gamePath, redirection, container, false)
            {
                Invalid          = false;
                OptimizedTexture = false;
                LargeTexture     = large;
                Size             = size;
                Format           = format;
                Width            = width;
                Height           = height;
            }

            public unsafe OptimizableTextureRedirection(Utf8GamePath gamePath, FullPath redirection, IModDataContainer container, long size,
                DXGIFormat format, Rgba32 solidColor, int width, int height)
                : base(gamePath, redirection, container, false)
            {
                Invalid          = false;
                OptimizedTexture = false;
                LargeTexture     = true;
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

                    var data = textures.LoadTex(fileInfo.FullName);
                    if (data is { Width: <= 32, Height: <= 32 })
                        return new OptimizableTextureRedirection(gamePath, redirection, container, true);

                    var large        = data.Width >= 2048 || data.Height >= 2048;
                    var uncompressed = !data.Format.IsCompressed();
                    var solid        = IsSolidColor(data);
                    if (!solid.IsDefault)
                        return new OptimizableTextureRedirection(gamePath, redirection, container, size, data.Format, solid.Color!.Value, data.Width, data.Height);

                    if (large || uncompressed)
                        return new OptimizableTextureRedirection(gamePath, redirection, container, size, data.Format, large, data.Width, data.Height);
                }
                catch (Exception ex)
                {
                    return new OptimizableTextureRedirection(gamePath, redirection, container);
                }

                return new OptimizableTextureRedirection(gamePath, redirection, container, true);
            }

            private static ColorParameter IsSolidColor(in BaseImage image)
            {
                var (rgba, _, _) = image.GetPixelData();
                if (rgba.Length < 4 || (rgba.Length & 3) is not 0)
                    return ColorParameter.Default;

                if (rgba.Length < 8)
                    return Unsafe.As<byte, Rgba32>(ref rgba[0]);

                var startValue = Unsafe.As<byte, uint>(ref rgba[0]);
                if ((rgba.Length & 7) is 0)
                {
                    if (startValue != Unsafe.As<byte, uint>(ref rgba[4]))
                        return ColorParameter.Default;

                    rgba = rgba[8..];
                }
                else
                {
                    rgba = rgba[4..];
                }

                var doubleValue = startValue | ((ulong)startValue << 32);
                var span        = MemoryMarshal.Cast<byte, ulong>(rgba);
                foreach (var value in span)
                {
                    if (doubleValue != value)
                        return ColorParameter.Default;
                }

                return new Rgba32(startValue);
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

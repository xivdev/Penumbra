using ImSharp;
using ImSharp.Table;
using Luna;
using Penumbra.Communication;
using Penumbra.Import.Textures;
using Penumbra.Mods;
using Penumbra.Mods.Manager;

namespace Penumbra.UI.ManagementTab;

public sealed class TextureOptimizationTable(
    ModManager mods,
    TextureManager textures,
    TextureOptimization optimization,
    UiNavigator navigator,
    Configuration config,
    ManagementLog<TextureOptimization> log)
    : TableBase<TextureOptimizationCacheObject, TextureOptimizationTable.Cache>(new StringU8("##tot"u8),
        new ActionColumn(config, textures, optimization),
        new FileColumn<TextureOptimizationCacheObject, OptimizableTexture> { Label = new StringU8("File"u8) },
        new ModColumn(navigator) { Label                                           = new StringU8("Mod"u8) },
        new FormatColumn { Label                                                   = new StringU8("Format"u8) },
        new WidthColumn { Label                                                    = new StringU8("Width"u8) },
        new HeightColumn { Label                                                   = new StringU8("Height"u8) },
        new SizeColumn { Label                                                     = new StringU8("Size"u8) },
        new SolidColorColumn { Label                                               = new StringU8("Color"u8) }
    )
{
    public long LowerSizeLimit      = 1 << 20;
    public int  SmallDimensionLimit = 32;
    public int  LargeDimensionLimit = 4096;

    protected override void PreDraw(in Cache cache)
    {
        Im.Item.SetNextWidthScaled(100);
        ImEx.LogarithmicInput("Ignore Textures Below This Size"u8, FormattingFunctions.HumanReadableSize(LowerSizeLimit), ref LowerSizeLimit);
        Im.Item.SetNextWidthScaled(100);
        ImEx.LogarithmicInput("Ignore Textures With Smaller Dimensions"u8, ref SmallDimensionLimit);
        Im.Item.SetNextWidthScaled(100);
        ImEx.LogarithmicInput("Show Textures With Larger Dimensions, Even If Compressed"u8, ref LargeDimensionLimit);
        cache.DrawScanButtons();
    }

    /// <remarks> Implemented in the cache due to use of scanner. </remarks>>
    public override IEnumerable<TextureOptimizationCacheObject> GetItems()
        => [];

    protected override Cache CreateCache()
        => new(this, mods, log);

    public sealed class Cache(TextureOptimizationTable parent, ModManager mods, ManagementLog<TextureOptimization> log)
        : ScannerTabCache<TextureOptimizationCacheObject, OptimizableTexture>(parent,
            new TextureOptimizationScanner(mods, parent, log))
    {
        protected override TextureOptimizationCacheObject Convert(OptimizableTexture obj)
            => new(obj);
    }

    private sealed class ActionColumn : BasicColumn<TextureOptimizationCacheObject>
    {
        private const    int                 NumButtons = 2;
        private readonly TextureManager      _textures;
        private readonly Configuration       _config;
        private readonly TextureOptimization _optimization;
        private          int                 _deleteIndex = -1;

        public ActionColumn(Configuration config, TextureManager textures, TextureOptimization optimization)
        {
            _config       =  config;
            _textures     =  textures;
            _optimization =  optimization;
            Flags         |= TableColumnFlags.NoSort | TableColumnFlags.NoResize;
        }

        public override void PostDraw(in TableCache<TextureOptimizationCacheObject> cache)
        {
            if (_deleteIndex is -1)
                return;

            cache.DeleteSingleItem(_deleteIndex);
            _deleteIndex = -1;
        }

        public override unsafe void DrawColumn(in TextureOptimizationCacheObject item, int globalIndex)
        {
            const float maxSize = 512f;
            ImEx.Icon.Button(LunaStyle.OnHoverIcon);
            if (Im.Item.Hovered())
            {
                using var tt   = Im.Tooltip.Begin();
                var       file = _textures.Provider.GetFromFile(item.ScannedObject.FilePath);
                if (file.TryGetWrap(out var wrap, out var exception))
                    Im.Image.Draw(wrap.Id, wrap.ScaledDownSize(maxSize) * Im.Style.GlobalScale);
                else if (exception is null)
                    ImEx.Spinner("##spin"u8, 256 * Im.Style.GlobalScale, 10 * (int)Im.Style.GlobalScale, ImGuiColor.Text.Get());
                else
                    Im.TextWrapped($"Failed to load image:\n{exception}");
            }

            if (!item.ScannedObject.SolidColor.IsDefault)
            {
                var active = _config.IncognitoModifier.IsActive();
                var color  = item.ScannedObject.SolidColor.Color!.Value;
                Im.Line.SameInner();
                if (ImEx.Icon.Button(LunaStyle.DyeIcon, StringU8.Empty, !active) && item.ScannedObject.Mod.TryGetTarget(out var mod))
                {
                    _optimization.ReplaceWithSolidColor(item.ScannedObject.FilePath, mod, color, true);
                    _deleteIndex = globalIndex;
                }

                if (Im.Item.Hovered(HoveredFlags.AllowWhenDisabled))
                {
                    using var tt = Im.Tooltip.Begin();
                    if (Texture.SolidTextures.TryGetValue(color, out var path))
                        Im.Text($"Replace all usages of this file by a file swap to {path} and delete the file.");
                    else
                        Im.Text($"Replace this file with a 1x1 texture of color {color}.");
                    if (!active)
                        Im.Text($"\nHold {_config.IncognitoModifier} to replace.");
                }
            }
        }

        public override float ComputeWidth(IEnumerable<TextureOptimizationCacheObject> _)
            => (Im.Style.FrameHeight + Im.Style.ItemInnerSpacing.X) * NumButtons;
    }

    private sealed class ModColumn(UiNavigator navigator) : ModColumn<TextureOptimizationCacheObject>(navigator)
    {
        protected override Mod? GetMod(in TextureOptimizationCacheObject item, int globalIndex)
            => item.ScannedObject.Mod.TryGetTarget(out var c) ? c : null;

        protected override StringPair GetModName(in TextureOptimizationCacheObject item, int globalIndex)
            => item.Mod;
    }

    private sealed class FormatColumn : TextColumn<TextureOptimizationCacheObject>
    {
        protected override string ComparisonText(in TextureOptimizationCacheObject item, int globalIndex)
            => item.Format;

        protected override StringU8 DisplayText(in TextureOptimizationCacheObject item, int globalIndex)
            => item.Format;
    }

    private sealed class HeightColumn() : NumberColumn<int, TextureOptimizationCacheObject>(NumberFilterMethod.GreaterEqual)
    {
        public override float ComputeWidth(IEnumerable<TextureOptimizationCacheObject> _)
            => Im.Font.CalculateSize("0000000"u8).X + ImEx.Table.ArrowWidth;

        public override int ToValue(in TextureOptimizationCacheObject item, int globalIndex)
            => item.ScannedObject.Height;

        protected override StringU8 DisplayNumber(in TextureOptimizationCacheObject item, int globalIndex)
            => item.Height;

        protected override string ComparisonText(in TextureOptimizationCacheObject item, int globalIndex)
            => item.Height;
    }

    private sealed class WidthColumn() : NumberColumn<int, TextureOptimizationCacheObject>(NumberFilterMethod.GreaterEqual)
    {
        public override float ComputeWidth(IEnumerable<TextureOptimizationCacheObject> _)
            => Im.Font.CalculateSize("0000000"u8).X + ImEx.Table.ArrowWidth;

        public override int ToValue(in TextureOptimizationCacheObject item, int globalIndex)
            => item.ScannedObject.Width;

        protected override StringU8 DisplayNumber(in TextureOptimizationCacheObject item, int globalIndex)
            => item.Width;

        protected override string ComparisonText(in TextureOptimizationCacheObject item, int globalIndex)
            => item.Width;
    }

    private sealed class SolidColorColumn : TextColumn<TextureOptimizationCacheObject>
    {
        public override float ComputeWidth(IEnumerable<TextureOptimizationCacheObject> _)
            => Im.Font.CalculateSize("#000000000"u8).X + ImEx.Table.ArrowWidth;

        protected override string ComparisonText(in TextureOptimizationCacheObject item, int globalIndex)
            => item.SolidColor;

        protected override StringU8 DisplayText(in TextureOptimizationCacheObject item, int globalIndex)
            => item.SolidColor;
    }

    private sealed class SizeColumn() : NumberColumn<long, TextureOptimizationCacheObject>(NumberFilterMethod.GreaterEqual)
    {
        public override float ComputeWidth(IEnumerable<TextureOptimizationCacheObject> _)
            => Im.Font.CalculateSize("0000000000"u8).X + ImEx.Table.ArrowWidth;

        public override long ToValue(in TextureOptimizationCacheObject item, int globalIndex)
            => item.ScannedObject.Size;

        protected override StringU8 DisplayNumber(in TextureOptimizationCacheObject item, int globalIndex)
            => item.Size;

        protected override string ComparisonText(in TextureOptimizationCacheObject item, int globalIndex)
            => item.Size;
    }
}

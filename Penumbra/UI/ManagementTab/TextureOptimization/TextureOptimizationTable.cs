using ImSharp;
using ImSharp.Table;
using Lumina.Data.Files;
using Luna;
using OtterTex;
using Penumbra.Communication;
using Penumbra.Import.Textures;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.UI.Classes;

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
        new MipMapColumn { Label                                                   = new StringU8("Mips"u8) },
        new SolidColorColumn { Label                                               = new StringU8("Color"u8) }
    )
{
    

    protected override void PreDraw(in Cache cache)
    {
        Im.Item.SetNextWidthScaled(100);
        ImEx.LogarithmicInput("Ignore Textures Below This Size"u8, FormattingFunctions.HumanReadableSize(config.TextureOptimization.LowerSizeLimit), ref config.TextureOptimization.LowerSizeLimit);
        Im.Item.SetNextWidthScaled(100);
        ImEx.LogarithmicInput("Ignore Textures With Smaller Dimensions"u8, ref config.TextureOptimization.SmallDimensionLimit);
        Im.Item.SetNextWidthScaled(100);
        ImEx.LogarithmicInput("Show Textures With Larger Dimensions, Even If Compressed"u8, ref config.TextureOptimization.LargeDimensionLimit);
        cache.DrawScanButtons();
        LunaStyle.DrawSeparator();
        Im.Checkbox("Create Backups Before Destructive Operations"u8, ref config.TextureOptimization.CreateBackups);
        Im.Tooltip.OnHover("When this is enabled, before any texture is overwritten, it will be moved to its path with a '.bak' appended, overwriting previously existing backup files of the same name."u8);
        Im.Item.SetNextWidthScaled(100);
        ImEx.LogarithmicInput("Texture Dimension Restriction"u8, ref config.TextureOptimization.TextureDimensionLimit, 4);
        Im.Tooltip.OnHover("This is the upper limit for texture dimensions in both directions when using automatic resizing. The texture size will be halved in both dimensions until both directions are less than this or equal to it."u8);
    }

    /// <remarks> Implemented in the cache due to use of scanner. </remarks>>
    public override IEnumerable<TextureOptimizationCacheObject> GetItems()
        => [];

    protected override Cache CreateCache()
        => new(this, config, mods, log);

    public sealed class Cache(TextureOptimizationTable parent, Configuration config, ModManager mods, ManagementLog<TextureOptimization> log)
        : ScannerTabCache<TextureOptimizationCacheObject, OptimizableTexture>(parent,
            new TextureOptimizationScanner(mods, config.TextureOptimization, log))
    {
        protected override TextureOptimizationCacheObject Convert(OptimizableTexture obj)
            => new(obj);
    }

    private sealed class ActionColumn : BasicColumn<TextureOptimizationCacheObject>
    {
        private const    int                      NumButtons = 3;
        private readonly TextureManager           _textures;
        private readonly Configuration            _config;
        private readonly TextureOptimization      _optimization;
        private          int                      _deleteIndex = -1;

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

        private void DrawHoverButton(in TextureOptimizationCacheObject item)
        {
            const float maxSize = 512f;
            ImEx.Icon.Button(LunaStyle.OnHoverIcon);
            if (!Im.Item.Hovered())
                return;

            using var tt   = Im.Tooltip.Begin();
            var       file = _textures.Provider.GetFromFile(item.ScannedObject.FilePath);
            if (file.TryGetWrap(out var wrap, out var exception))
                Im.Image.Draw(wrap.Id, wrap.ScaledDownSize(maxSize) * Im.Style.GlobalScale);
            else if (exception is null)
                ImEx.Spinner("##spin"u8, 256 * Im.Style.GlobalScale, 10 * (int)Im.Style.GlobalScale, ImGuiColor.Text.Get());
            else
                Im.TextWrapped($"Failed to load image:\n{exception}");
        }

        private void DrawSolidColorButton(in TextureOptimizationCacheObject item, int globalIndex)
        {
            Mod? mod    = null;
            var  active = _config.IncognitoModifier.IsActive();
            var  color  = item.ScannedObject.SolidColor.Color!.Value;
            Im.Line.SameInner();
            if (ImEx.Icon.Button(LunaStyle.DyeIcon, StringU8.Empty, !active) && item.ScannedObject.Mod.TryGetTarget(out mod))
            {
                _optimization.ReplaceWithSolidColor(item.ScannedObject.FilePath, mod, color, _config.TextureOptimization.CreateBackups);
                _deleteIndex = globalIndex;
            }

            if (Im.Item.Hovered(HoveredFlags.AllowWhenDisabled))
            {
                using var tt = Im.Tooltip.Begin();
                if (_optimization.CanReplaceWithSwap(item.ScannedObject.FilePath, mod, color, out var path))
                    Im.Text($"Replace all usages of this file by a file swap to {path} and delete the file.");
                else
                    Im.Text($"Replace this file with a 1x1 texture of color {color}.");
                if (!active)
                    Im.Text($"\nHold {_config.IncognitoModifier} to replace.");
            }
        }

        private void DrawCompressionButton(in TextureOptimizationCacheObject item, int globalIndex)
        {
            Mod? mod       = null;
            var  active    = _config.DeleteModModifier.IsActive();
            var  otherTask = item.ResizeTask is not null && !item.ResizeTask.IsCompleted;
            Im.Line.SameInner();
            if (item.CompressionTask is null)
            {
                if (ImEx.Icon.Button(LunaStyle.CompressIcon, StringU8.Empty, !active || otherTask)
                 && item.ScannedObject.Mod.TryGetTarget(out mod))
                    item.CompressionTask = _optimization.Compress(item.ScannedObject.FilePath, mod, _config.TextureOptimization.CreateBackups);

                if (Im.Item.Hovered(HoveredFlags.AllowWhenDisabled))
                {
                    using var tt    = Im.Tooltip.Begin();
                    var       usage = _optimization.GetTargetFormat(item.ScannedObject.FilePath, mod);
                    Im.Text($"Compress this texture using block compression {usage}.");
                    if (!active)
                        Im.Text($"\nHold {_config.DeleteModModifier} to compress.");
                    if (otherTask)
                        Im.Text("\nWait until the resizing task is finished."u8);
                }
            }
            else if (item.CompressionTask.IsFaulted)
            {
                if (ImEx.Icon.Button(LunaStyle.ErrorIcon, StringU8.Empty, textColor: Colors.RegexWarningBorder, disabled: !active || otherTask)
                 && item.ScannedObject.Mod.TryGetTarget(out mod))
                    item.CompressionTask = _optimization.Compress(item.ScannedObject.FilePath, mod, _config.TextureOptimization.CreateBackups);

                if (Im.Item.Hovered(HoveredFlags.AllowWhenDisabled))
                {
                    using var tt    = Im.Tooltip.Begin();
                    var       usage = _optimization.GetTargetFormat(item.ScannedObject.FilePath, mod);
                    Im.Text($"Failed to compress the texture. Retry compressing to {usage}.");
                    if (item.CompressionTask.Exception is { } exception)
                    {
                        using var color = ImGuiColor.Text.Push(Colors.RegexWarningBorder);
                        Im.TextWrapped($"{exception}");
                    }

                    if (!active)
                        Im.Text($"\nHold {_config.DeleteModModifier} to retry.");
                    if (otherTask)
                        Im.Text("\nWait until the resizing task is finished."u8);
                }
            }
            else if (item.CompressionTask.IsCompletedSuccessfully)
            {
                item.CompressionTask = null;
                _deleteIndex         = globalIndex;
            }
            else
            {
                Im.Cursor.Position += Im.Style.FramePadding;
                ImEx.Spinner("##compression"u8, Im.Style.TextHeight / 2, 3, ImGuiColor.Text.Get());
                Im.Tooltip.OnHover("Compressing..."u8);
            }
        }

        private void DrawRestrictDimensionsButton(in TextureOptimizationCacheObject item, int globalIndex)
        {
            Mod? mod       = null;
            var  active    = _config.DeleteModModifier.IsActive();
            var  otherTask = item.CompressionTask is not null && !item.CompressionTask.IsCompleted;
            Im.Line.SameInner();
            if (item.ResizeTask is null)
            {
                if (ImEx.Icon.Button(LunaStyle.AutoResizeIcon, StringU8.Empty, !active || otherTask)
                 && item.ScannedObject.Mod.TryGetTarget(out mod))
                {
                    var targetFormat = item.ScannedObject.Format.ToTexFormat() is TexFile.TextureFormat.B8G8R8A8
                        ? _optimization.GetTargetFormat(item.ScannedObject.FilePath, mod)
                        : CombinedTexture
                            .TextureSaveType.AsIs;
                    item.ResizeTask = _optimization.RestrictDimensions(item.ScannedObject.FilePath, _config.TextureOptimization.TextureDimensionLimit,
                        _config.TextureOptimization.TextureDimensionLimit, _config.TextureOptimization.CreateBackups, targetFormat);
                }

                if (Im.Item.Hovered(HoveredFlags.AllowWhenDisabled))
                {
                    using var tt = Im.Tooltip.Begin();
                    Im.Text(
                        $"Restrict this texture to the maximum specified size by halving its size until it is small enough in both dimensions.");
                    var targetFormat = item.ScannedObject.Format.ToTexFormat() is TexFile.TextureFormat.B8G8R8A8
                        ? _optimization.GetTargetFormat(item.ScannedObject.FilePath, mod)
                        : CombinedTexture
                            .TextureSaveType.AsIs;
                    if (targetFormat is not CombinedTexture.TextureSaveType.AsIs)
                        Im.Text($"\nThis will also compress the texture to {targetFormat}.");

                    if (!active)
                        Im.Text($"\nHold {_config.DeleteModModifier} to resize.");
                }
            }
            else if (item.ResizeTask.IsFaulted)
            {
                if (ImEx.Icon.Button(LunaStyle.ErrorIcon, StringU8.Empty, textColor: Colors.RegexWarningBorder, disabled: !active)
                 && item.ScannedObject.Mod.TryGetTarget(out mod))
                {
                    var targetFormat = item.ScannedObject.Format.ToTexFormat() is TexFile.TextureFormat.B8G8R8A8
                        ? _optimization.GetTargetFormat(item.ScannedObject.FilePath, mod)
                        : CombinedTexture
                            .TextureSaveType.AsIs;
                    item.ResizeTask = _optimization.RestrictDimensions(item.ScannedObject.FilePath, _config.TextureOptimization.TextureDimensionLimit,
                        _config.TextureOptimization.TextureDimensionLimit, _config.TextureOptimization.CreateBackups, targetFormat);
                }

                if (Im.Item.Hovered(HoveredFlags.AllowWhenDisabled))
                {
                    using var tt = Im.Tooltip.Begin();
                    var targetFormat = item.ScannedObject.Format.ToTexFormat() is TexFile.TextureFormat.B8G8R8A8
                        ? _optimization.GetTargetFormat(item.ScannedObject.FilePath, mod)
                        : CombinedTexture
                            .TextureSaveType.AsIs;

                    Im.Text(targetFormat is CombinedTexture.TextureSaveType.AsIs
                        ? "Failed to resize the texture. Retry resizing."u8
                        : $"Failed to resize the texture. Retry resizing and compressing the texture to {targetFormat}.");
                    if (item.ResizeTask.Exception is { } exception)
                    {
                        using var color = ImGuiColor.Text.Push(Colors.RegexWarningBorder);
                        Im.TextWrapped($"{exception}");
                    }

                    if (!active)
                        Im.Text($"\nHold {_config.DeleteModModifier} to retry.");
                }
            }
            else if (item.ResizeTask.IsCompletedSuccessfully)
            {
                item.ResizeTask = null;
                _deleteIndex    = globalIndex;
            }
            else
            {
                Im.Cursor.Position += Im.Style.FramePadding;
                ImEx.Spinner("##resizing"u8, Im.Style.TextHeight / 2, 3, ImGuiColor.Text.Get());
                Im.Tooltip.OnHover("Resizing..."u8);
            }
        }

        public override void DrawColumn(in TextureOptimizationCacheObject item, int globalIndex)
        {
            DrawHoverButton(item);

            if (!item.ScannedObject.SolidColor.IsDefault)
            {
                DrawSolidColorButton(item, globalIndex);
            }
            else
            {
                if (!item.ScannedObject.Format.IsCompressed())
                    DrawCompressionButton(item, globalIndex);
                if (item.ScannedObject.Width > _config.TextureOptimization.TextureDimensionLimit || item.ScannedObject.Height > _config.TextureOptimization.TextureDimensionLimit)
                    DrawRestrictDimensionsButton(item, globalIndex);
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

    private sealed class MipMapColumn() : NumberColumn<int, TextureOptimizationCacheObject>(NumberFilterMethod.GreaterEqual)
    {
        public override float ComputeWidth(IEnumerable<TextureOptimizationCacheObject> _)
            => Im.Font.CalculateSize("Mips   "u8).X + ImEx.Table.ArrowWidth;

        public override int ToValue(in TextureOptimizationCacheObject item, int globalIndex)
            => item.ScannedObject.MipMaps;

        protected override StringU8 DisplayNumber(in TextureOptimizationCacheObject item, int globalIndex)
            => item.MipMaps;

        protected override string ComparisonText(in TextureOptimizationCacheObject item, int globalIndex)
            => item.MipMaps;
    }
}

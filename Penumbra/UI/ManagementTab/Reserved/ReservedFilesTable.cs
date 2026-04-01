using ImSharp;
using ImSharp.Table;
using Luna;
using Penumbra.Communication;
using Penumbra.Import.Textures;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.UI.Classes;

namespace Penumbra.UI.ManagementTab;

public sealed class ReservedFilesTable(
    ModManager mods,
    TextureManager textures,
    UiNavigator navigator,
    Configuration config,
    ReservedFiles reservedFiles,
    ManagementLog<ReservedFiles> log)
    : TableBase<ReservedFileCacheObject, ScannerTabCache<ReservedFileCacheObject, ReservedFileRedirection>>(new StringU8("##fft"u8),
        new ActionColumn(reservedFiles, config),
        new GamePathColumn<ReservedFileCacheObject, ReservedFileRedirection> { Label = new StringU8("Game Path"u8) },
        new StateColumn { Label                                                      = new StringU8("State"u8) },
        new TargetColumn<ReservedFileCacheObject, ReservedFileRedirection> { Label   = new StringU8("Target File"u8) },
        new ModColumn(navigator) { Label                                             = new StringU8("Mod"u8) },
        new ContainerColumn(navigator) { Label                                       = new StringU8("Option"u8) })
{
    /// <remarks> Implemented in the cache due to use of scanner. </remarks>>
    public override IEnumerable<ReservedFileCacheObject> GetItems()
        => [];

    protected override void PreDraw(in ScannerTabCache<ReservedFileCacheObject, ReservedFileRedirection> cache)
    {
        cache.DrawScanButtons();

        var active = config.IncognitoModifier.IsActive();
        if (ImEx.Button("Remove All Simple Redirections"u8, default, !active))
            reservedFiles.RemoveRedundant(cache, false);

        if (Im.Item.Hovered(HoveredFlags.AllowWhenDisabled))
        {
            using var tt = Im.Tooltip.Begin();
            Im.Text("Executing this will"u8);
            Im.BulletText("Remove all listed file swaps, as they can not be reasonable."u8);
            Im.BulletText("Remove all redirections listed as 'Broken', as they could not be read and can not be useful."u8);
            Im.BulletText("Remove all redirections listed as 'Missing', as their target files do not exist and thus can not be useful."u8);
            Im.BulletText(
                "Remove all redirections listed as 'Equal', as the target files are equivalent to the game files and thus the redirections are not meaningful."u8);
            Im.BulletText("Delete all target files who have no remaining redirections in their mods left afterwards."u8);
            Im.Text("\nTHIS IS NOT REVERTIBLE."u8, Colors.RegexWarningBorder);

            if (!active)
                Im.Text($"\nHold {config.IncognitoModifier} while clicking.");
        }

        active = config.DeleteModModifier.IsActive();
        Im.Line.Same();
        using (ImGuiColor.Text.Push(Colors.RegexWarningBorder))
        {
            if (ImEx.Button("Remove All"u8, default, !active))
                reservedFiles.RemoveRedundant(cache, true);
        }

        if (Im.Item.Hovered(HoveredFlags.AllowWhenDisabled))
        {
            using var tt = Im.Tooltip.Begin();
            Im.Text("Executing this will"u8);
            Im.BulletText("Do everything 'Remove Simple Redirections' does."u8);
            Im.BulletText(
                "Also remove all redirections listed as 'Different'. Any mod with any of those redirections may not be working correctly anymore, but removing the redirection will not change that."u8);
            Im.Text("\nTHIS IS NOT REVERTIBLE."u8, Colors.RegexWarningBorder);
            Im.Text(
                "\nAfter executing this, the next scan of this tab should yield no redirections and all warnings should be gone, but you will have no idea what mods where previously affected and might be broken now unless you took note beforehand."u8,
                Colors.RegexWarningBorder);

            if (!active)
                Im.Text($"\nHold {config.DeleteModModifier} while clicking.");
        }
    }

    protected override ScannerTabCache<ReservedFileCacheObject, ReservedFileRedirection> CreateCache()
        => new Cache(mods, textures, log, this);

    private sealed class Cache(ModManager mods, TextureManager textures, ManagementLog<ReservedFiles> log, ReservedFilesTable parent)
        : ScannerTabCache<ReservedFileCacheObject, ReservedFileRedirection>(parent, new ReservedFileScanner(mods, textures, log))
    {
        protected override ReservedFileCacheObject Convert(ReservedFileRedirection obj)
            => new(obj);
    }

    private sealed class ActionColumn : BasicColumn<ReservedFileCacheObject>
    {
        private readonly ReservedFiles _service;
        private readonly Configuration       _config;
        private          int                 _deleteIndex = -1;

        public ActionColumn(ReservedFiles service, Configuration config)
        {
            _service =  service;
            _config  =  config;
            Flags    |= TableColumnFlags.NoSort | TableColumnFlags.NoResize;
        }

        public override void PostDraw(in TableCache<ReservedFileCacheObject> cache)
        {
            if (_deleteIndex is -1)
                return;

            cache.DeleteSingleItem(_deleteIndex);
            _deleteIndex = -1;
        }

        public override void DrawColumn(in ReservedFileCacheObject item, int globalIndex)
        {
            var disabled = !_config.DeleteModModifier.IsActive();
            if (ImEx.Icon.Button(LunaStyle.DeleteIcon,
                    item.ScannedObject.FileSwap
                        ? "Remove this file swap."u8
                        : "Remove this redirection and delete the target file if it was the last redirection in the mod referencing it."u8,
                    disabled))
            {
                _service.DeleteItem(item.ScannedObject);
                _deleteIndex = globalIndex;
            }

            if (disabled)
                Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, $"\nHold {_config.DeleteModModifier} while clicking to remove.");
        }

        public override float ComputeWidth(IEnumerable<ReservedFileCacheObject> _)
            => Im.Style.FrameHeight;
    }

    private sealed class ModColumn(UiNavigator navigator) : ModColumn<ReservedFileCacheObject>(navigator)
    {
        protected override Mod? GetMod(in ReservedFileCacheObject item, int globalIndex)
            => item.ScannedObject.Container.TryGetTarget(out var c) ? c.Mod as Mod : null;

        protected override StringPair GetModName(in ReservedFileCacheObject item, int globalIndex)
            => item.Mod;
    }

    private sealed class ContainerColumn(UiNavigator navigator) : ModColumn<ReservedFileCacheObject>(navigator)
    {
        protected override Mod? GetMod(in ReservedFileCacheObject item, int globalIndex)
            => item.ScannedObject.Container.TryGetTarget(out var c) ? c.Mod as Mod : null;

        protected override StringPair GetModName(in ReservedFileCacheObject item, int globalIndex)
            => item.Container;

        private string _lastContainer = string.Empty;

        protected override bool MatchesLastItem(in ReservedFileCacheObject item)
        {
            var ret = base.MatchesLastItem(item) && _lastContainer == item.Container.Utf16;
            _lastContainer = item.Container.Utf16;
            return ret;
        }

        public override void PostDraw(in TableCache<ReservedFileCacheObject> cache)
        {
            base.PostDraw(cache);
            _lastContainer = string.Empty;
        }
    }

    private sealed class StateColumn : TextColumn<ReservedFileCacheObject>
    {
        protected override string ComparisonText(in ReservedFileCacheObject item, int globalIndex)
            => item.State;

        protected override StringU8 DisplayText(in ReservedFileCacheObject item, int globalIndex)
            => item.State;

        protected override void DrawTooltip(in ReservedFileCacheObject item, int globalIndex)
        {
            using var tt = Im.Tooltip.Begin();
            Im.Text(item.ScannedObject.FileSwap
                ? "There can no be any file swaps that are valid and useful for this kind of file."u8
                : item.ScannedObject.Broken
                    ? "The scanner was unable to read or parse this file, so it is invalid and should be removed."u8
                    : item.ScannedObject.Missing
                        ? "The file this is redirected to does not exist, so the redirection should just be removed."u8
                        : item.ScannedObject.ConceptuallyEqual
                            ? "The file this is redirected to is equivalent to the original file, so the redirection can just be removed without consequences."u8
                            : "This file is conceptually different from the original game file. The mod may have to be fixed by its creator.\n\nYou can freely remove this redirection to silence the warning, as it is not applied either way, but the mod may not work as intended."u8);
        }

        public override void DrawColumn(in ReservedFileCacheObject item, int globalIndex)
        {
            base.DrawColumn(in item, globalIndex);
            if (item.State.Utf16 is not "Different")
                return;

            Im.Line.SameInner();
            ImEx.Icon.Draw(LunaStyle.WarningIcon, Rgba32.Yellow);
            if (Im.Item.Hovered())
                DrawTooltip(item, globalIndex);
        }

        public override float ComputeWidth(IEnumerable<ReservedFileCacheObject> _)
            => ReservedFileCacheObject.Different.Utf8.CalculateSize().X + Im.Style.FrameHeightWithSpacing;
    }
}

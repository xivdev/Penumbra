using System.Text.Json;
using Dalamud.Interface.ImGuiNotification;
using ImSharp;
using Luna;
using Penumbra.Mods;
using Penumbra.Mods.Manager;

namespace Penumbra.UI.ManagementTab;

public sealed class BrokenModsTab(ModManager mods, FailedModNotification notification) : ITab<ManagementTabType>
{
    public ReadOnlySpan<byte> Label
        => "Broken Mods"u8;

    public ManagementTabType Identifier
        => ManagementTabType.BrokenMods;

    public void DrawContent()
    {
        var cache = CacheManager.Instance.GetOrCreateCache(Im.Id.Current, () => new Cache(mods));

        if (Im.Button("Refresh"u8))
            cache.Dirty |= IManagedCache.DirtyFlags.Custom;

        Im.Line.Same();
        DrawDeleteEmptyButton(notification, cache);

        Im.Line.Same();
        DrawMoveToTempButton(notification, mods, cache);

        LunaStyle.DrawSeparator();
        DrawTable(notification, cache);
    }


    private static void DeleteNotification(FailedModNotification notification, string name)
        => notification.Notifications.FirstOrDefault(n => n.Object.Mod.Equals(name, StringComparison.OrdinalIgnoreCase))?.Remove();

    private static void DrawDeleteEmptyButton(FailedModNotification notification, Cache cache)
    {
        if (ImEx.Button("Delete All Empty Folders"u8, default,
                "Deletes all folders that contain no objects or have no size at all (meaning they only have empty subfolders).\n\nTHIS IS NOT REVERTIBLE!"u8,
                !LunaStyle.Modifier.Destructive))
            for (var i = 0; i < cache.BrokenMods.Count; ++i)
            {
                var directory = cache.BrokenMods[i];
                if (directory.Size > 0)
                    continue;

                try
                {
                    Directory.Delete(directory.FullPath, true);
                    DeleteNotification(notification, directory.Name.Utf16);
                    cache.BrokenMods.RemoveAt(i--);
                }
                catch (Exception ex)
                {
                    Penumbra.Log.Error($"Failed to delete empty mod directory {directory.Name.Utf16}:\n{ex}");
                }
            }

        LunaStyle.Modifier.Destructive.TooltipLineBreak("delete"u8);
    }

    private static void DrawMoveToTempButton(FailedModNotification notification, ModManager mods, Cache cache)
    {
        if (ImEx.Button("Move All Folders to Temp"u8, default,
                $"Moves all folders in the list to the '{mods.BasePath.FullName}/broken_mods' directory to make it easier to move them out of the Root directory.",
                !LunaStyle.Modifier.Destructive))
        {
            cache.Dirty |= IManagedCache.DirtyFlags.Custom;
            var path = Path.Combine(mods.BasePath.FullName, "broken_mods");
            try
            {
                var tmpDirectory = Directory.CreateDirectory(path);
                foreach (var directory in cache.BrokenMods)
                {
                    if (directory.Name.Utf16 is "broken_mods")
                        continue;

                    try
                    {
                        var tmp = Path.Combine(tmpDirectory.FullName, directory.Name.Utf16);
                        Directory.Move(directory.FullPath, tmp);
                        DeleteNotification(notification, directory.Name);
                    }
                    catch (Exception ex)
                    {
                        Penumbra.Log.Error($"Failed to move broken mod directory {directory.Name.Utf16} to {path}:\n{ex}");
                    }
                }
            }
            catch (Exception ex)
            {
                Penumbra.Log.Error($"Could not create temporary broken mod directory {path}:\n{ex}");
            }
        }

        LunaStyle.Modifier.Destructive.TooltipLineBreak("move"u8);
    }

    private static void DrawTable(FailedModNotification notification, Cache cache)
    {
        using var table = Im.Table.Begin("##table"u8, 5, TableFlags.RowBackground);
        if (!table)
            return;

        table.SetupColumn("##"u8,            TableColumnFlags.WidthFixed,   2 * Im.Style.FrameHeight + Im.Style.ItemInnerSpacing.X);
        table.SetupColumn("Mod Directory"u8, TableColumnFlags.WidthStretch, 0.3f);
        table.SetupColumn("Error"u8,         TableColumnFlags.WidthStretch, 0.7f);
        table.SetupColumn("Size"u8,          TableColumnFlags.WidthFixed,   70 * Im.Style.GlobalScale);
        table.SetupColumn("Objects"u8,       TableColumnFlags.WidthFixed,   60 * Im.Style.GlobalScale);

        table.HeaderRow();

        using var clipper = new Im.ListClipper(cache.BrokenMods.Count, Im.Style.FrameHeight);
        var       removed = 0;
        foreach (var idx in clipper)
        {
            var mod = cache.BrokenMods[idx - removed];
            table.NextColumn();
            if (ImEx.Icon.Button(LunaStyle.FolderIcon, "Open this directory in the file explorer of your choice."u8))
                try
                {
                    Process.Start(new ProcessStartInfo(mod.FullPath) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    Penumbra.Messager.NotificationMessage(ex, $"Could not open directory {mod.FullPath}.",
                        $"Could not open directory {mod.FullPath}",
                        NotificationType.Warning);
                }

            Im.Line.SameInner();
            if (ImEx.Icon.Button(LunaStyle.RemoveFolderIcon, "Delete this directory. This is NOT revertible!"u8,
                    !LunaStyle.Modifier.Destructive))
                try
                {
                    Directory.Delete(mod.FullPath, true);
                    cache.BrokenMods.RemoveAt(idx);
                    DeleteNotification(notification, mod.Name.Utf16);
                    ++removed;
                }
                catch (Exception ex)
                {
                    Penumbra.Log.Error($"Failed to delete broken mod directory {mod.Name.Utf16}:\n{ex}");
                }

            LunaStyle.Modifier.Destructive.TooltipLineBreak("delete"u8);
            table.DrawFrameColumn(mod.Name.Utf8);
            table.DrawFrameColumn(mod.Error.Utf8);
            Im.Tooltip.OnHover($"{mod.Exception}");
            table.NextColumn();
            ImEx.TextRightAligned(mod.SizeString.Utf8);
            table.NextColumn();
            ImEx.TextRightAligned(mod.CountString);
        }
    }


    private sealed class Cache(ModManager mods) : BasicCache
    {
        public readonly struct BrokenModData
        {
            public readonly string     FullPath;
            public readonly StringPair Name;
            public readonly StringPair Error;
            public readonly Exception  Exception;
            public readonly long       Size;
            public readonly int        FileCount;
            public readonly int        DirectoryCount;
            public readonly StringPair SizeString;
            public readonly StringU8   CountString;

            public BrokenModData(DirectoryInfo mod, Exception error)
            {
                FullPath    = mod.FullName;
                Name        = new StringPair(mod.Name);
                Error       = new StringPair(error.Message);
                Exception   = error;
                Size        = WindowsFunctions.GetDirectorySize(mod.FullName, out FileCount, out DirectoryCount);
                SizeString  = new StringPair(FormattingFunctions.HumanReadableSize(Size));
                CountString = new StringU8($"{FileCount + DirectoryCount}");
            }
        }

        public readonly List<BrokenModData> BrokenMods = [];

        public override void Update()
        {
            if (!CustomDirty)
                return;

            BrokenMods.Clear();
            Dirty &= ~IManagedCache.DirtyFlags.Custom;

            var fileNames = mods.DataEditor.SaveService.FileNames;
            foreach (var directory in mods.BasePath.EnumerateDirectories())
            {
                // Mod is loaded, ignore.
                if (mods.TryGetMod(directory.Name, string.Empty, out _))
                    continue;

                var metaFile = fileNames.ModMetaPath(directory.FullName);
                try
                {
                    var text   = JsonFunctions.ReadUtf8Bytes(metaFile, out _);
                    var reader = new Utf8JsonReader(text.Span, JsonFunctions.ReaderOptions);
                    var dto    = ModMeta.Dto.Read(ref reader);
                    if (!dto.Validate(out var error))
                        BrokenMods.Add(new BrokenModData(directory, new Exception(error)));
                }
                catch (Exception ex)
                {
                    BrokenMods.Add(new BrokenModData(directory, ex));
                }
            }
        }
    }
}

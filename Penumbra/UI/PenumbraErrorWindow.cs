using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ImSharp;
using Lumina.Data;
using Luna;
using Microsoft.Extensions.DependencyInjection;

namespace Penumbra.UI;

public sealed class PenumbraErrorWindow(IDalamudPluginInterface pi)
    : ErrorWindow(pi, Penumbra.Log, GetLabel(), "Penumbra")
{
    public static void DrawFileTest(IDataManager dataManager)
    {
        var cache = CacheManager.Instance.GetOrCreateCache(Im.Id.Current, () => new Cache());

        if (Im.Input.Text("##FileCheck"u8, ref cache.FileName, "Check Game File Existence..."u8))
        {
            // TODO 20260824 remove the / check when Lumina doesn't throw on this.
            if (cache.FileName.Contains('/') && dataManager.FileExists(cache.FileName))
                cache.File = dataManager.GetFile(cache.FileName);
            else
                cache.File = null;
        }

        if (cache.File is not null)
        {
            Im.Line.Same();
            Im.Text($"Exists, Size {FormattingFunctions.HumanReadableSize(cache.File.DataSpan.Length)}");
        }

        Im.Input.Text("##Path"u8, ref cache.Path, "Write Path..."u8);
        Im.Line.Same();
        if (ImEx.Button("Save File"u8, default, cache.Path.Length is 0 || cache.File is null))
            try
            {
                File.WriteAllBytes(cache.Path, cache.File!.DataSpan);
                cache.LastWriteFailure = null;
            }
            catch (Exception ex)
            {
                cache.LastWriteFailure = ex;
            }

        if (cache.LastWriteFailure is not null)
            using (ImGuiColor.Text.Push(LunaStyle.ErrorForeground))
            {
                Im.TextWrapped($"{cache.LastWriteFailure}");
            }
    }

    protected override void DrawDebugUtilities()
        => DrawFileTest(PluginInterface.GetRequiredService<IDataManager>());

    private sealed class Cache : BasicCache
    {
        public string        FileName = string.Empty;
        public FileResource? File     = null;
        public string        Path     = string.Empty;
        public Exception?    LastWriteFailure;

        public override void Update()
        { }
    }

    private static string GetLabel()
    {
        var assembly = typeof(PenumbraErrorWindow).Assembly;
        var version  = assembly.GetName().Version?.ToString() ?? string.Empty;
        return version.Length is 0
            ? "Penumbra###PenumbraConfigWindow"
            : $"Penumbra v{version}###PenumbraConfigWindow";
    }
}

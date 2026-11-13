using Dalamud.Plugin.Services;
using ImSharp;
using Lumina.Data;
using Luna;
using Microsoft.Extensions.DependencyInjection;
using Penumbra.Services;

namespace Penumbra.UI;

public sealed class PenumbraErrorWindow(PenumbraIntermediary intermediary, ImSharpDalamudContext context, PenumbraSupportInfo supportInfo)
    : ErrorWindow(intermediary.Parent.PluginInterface, intermediary.Parent.Log, intermediary.Parent.GetWindowLabel("PenumbraConfigWindow"),
        "Penumbra", context)
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

    protected override void DrawFailure()
    {
        var validationErrors = BitOperations.PopCount((uint)intermediary.Validator.Error);
        if (validationErrors is 0)
        {
            base.DrawFailure();
            if (intermediary.Failure is { } failure)
            {
                Im.TextWrapped($"Initialization failed during {failure.Stage.ToNameU8()}.");
                Im.TextWrapped($"{failure.Exception}");
            }
        }
        else
        {
            using var color = ImGuiColor.Text.Push(LunaStyle.ErrorForeground);

            if (intermediary.Validator.Error.HasFlag(ValidationError.InvalidSourceRepository))
                Im.TextWrapped(
                    $"Penumbra was loaded from an unsupported repository. Please install it from {ValidationService.Repository} or {ValidationService.SeaOfStars}.");
            if (intermediary.Validator.Error.HasFlag(ValidationError.InvalidInstall))
                Im.TextWrapped(
                    $"Penumbra was loaded from \"{PluginInterface.AssemblyLocation.Directory?.FullName}\". Please install it through Dalamud's plugin installer.");
            if (intermediary.Validator.Error.HasFlag(ValidationError.DevPluginExists))
                Im.TextWrapped(
                    @"Old plugin files exist in '%appdata%\XIVLauncher\devPlugins\Penumbra'. Remove that obsolete folder, including when using a developer installation."u8);

            Im.TextWrapped(validationErrors > 1
                ? "After correcting these problems, please reload Penumbra."u8
                : "After correcting this problem, please reload Penumbra."u8);
        }

        Im.Line.New();
        SupportButton.Discord(Penumbra.Messager, 0);
        Im.Line.Same();
        supportInfo.DrawSupportButton();
    }

    private sealed class Cache : BasicCache
    {
        public string        FileName = string.Empty;
        public FileResource? File     = null;
        public string        Path     = string.Empty;
        public Exception?    LastWriteFailure;

        public override void Update()
        { }
    }
}

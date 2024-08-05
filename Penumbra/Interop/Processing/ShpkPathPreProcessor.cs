using FFXIVClientStructs.FFXIV.Client.System.Resource;
using Penumbra.Api.Enums;
using Penumbra.Collections;
using Penumbra.GameData.Files;
using Penumbra.GameData.Files.Utility;
using Penumbra.Interop.Hooks.ResourceLoading;
using Penumbra.String;
using Penumbra.String.Classes;
using Penumbra.UI;

namespace Penumbra.Interop.Processing;

/// <summary>
/// Path pre-processor for shader packages that reverts redirects to known invalid files, as bad ShPks can crash the game.
/// </summary>
public sealed class ShpkPathPreProcessor(ResourceManagerService resourceManager, ChatWarningService chatWarningService) : IPathPreProcessor
{
    public ResourceType Type
        => ResourceType.Shpk;

    public unsafe FullPath? PreProcess(ResolveData resolveData, CiByteString path, Utf8GamePath originalGamePath, bool nonDefault, FullPath? resolved)
    {
        chatWarningService.CleanLastFileWarnings(false);

        if (!resolved.HasValue)
            return null;

        // Skip the sanity check for game files. We are not considering the case where the user has modified game file: it's at their own risk.
        var resolvedPath = resolved.Value;
        if (!resolvedPath.IsRooted)
            return resolvedPath;

        // If the ShPk is already loaded, it means that it already passed the sanity check.
        var existingResource = resourceManager.FindResource(ResourceCategory.Shader, ResourceType.Shpk, unchecked((uint)resolvedPath.InternalName.Crc32));
        if (existingResource != null)
            return resolvedPath;

        var checkResult = SanityCheck(resolvedPath.FullName);
        if (checkResult == SanityCheckResult.Success)
            return resolvedPath;

        Penumbra.Log.Warning($"Refusing to honor file redirection because of failed sanity check (result: {checkResult}). Original path: {originalGamePath} Redirected path: {resolvedPath}");
        chatWarningService.PrintFileWarning(resolvedPath.FullName, originalGamePath, WarningMessageComplement(checkResult));

        return null;
    }

    private static SanityCheckResult SanityCheck(string path)
    {
        try
        {
            using var file = MmioMemoryManager.CreateFromFile(path);
            var bytes = file.GetSpan();

            return ShpkFile.FastIsLegacy(bytes)
                ? SanityCheckResult.Legacy
                : SanityCheckResult.Success;
        }
        catch (FileNotFoundException)
        {
            return SanityCheckResult.NotFound;
        }
        catch (IOException)
        {
            return SanityCheckResult.IoError;
        }
    }

    private static string WarningMessageComplement(SanityCheckResult result)
        => result switch
        {
            SanityCheckResult.IoError  => "cannot read the modded file.",
            SanityCheckResult.NotFound => "the modded file does not exist.",
            SanityCheckResult.Legacy   => "this mod is not compatible with Dawntrail. Get an updated version, if possible, or disable it.",
            _                          => string.Empty,
        };

    private enum SanityCheckResult
    {
        Success,
        IoError,
        NotFound,
        Legacy,
    }
}

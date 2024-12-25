using OtterGui.Classes;
using Penumbra.Collections;
using Penumbra.Mods.Editor;
using Penumbra.String.Classes;

namespace Penumbra.Communication;

/// <summary>
/// Triggered whenever a redirection in a mod collection cache is manipulated.
/// <list type="number">
///     <item>Parameter is collection with a changed cache. </item>
///     <item>Parameter is the type of change. </item>
///     <item>Parameter is the game path to be redirected or empty for FullRecompute. </item>
///     <item>Parameter is the new redirection path or empty for Removed or FullRecompute </item>
///     <item>Parameter is the old redirection path for Replaced, or empty. </item>
///     <item>Parameter is the mod responsible for the new redirection if any. </item>
/// </list> </summary>
public sealed class ResolvedFileChanged()
    : EventWrapper<ModCollection, ResolvedFileChanged.Type, Utf8GamePath, FullPath, FullPath, IMod?, ResolvedFileChanged.Priority>(
        nameof(ResolvedFileChanged))
{
    public enum Type
    {
        Added,
        Removed,
        Replaced,
        FullRecomputeStart,
        FullRecomputeFinished,
    }

    public enum Priority
    {
        /// <seealso cref="Api.DalamudSubstitutionProvider.OnResolvedFileChange"/>
        DalamudSubstitutionProvider = 0,

        /// <seealso cref="Interop.Services.SchedulerResourceManagementService.OnResolvedFileChange"/>
        SchedulerResourceManagementService = 0,
    }
}

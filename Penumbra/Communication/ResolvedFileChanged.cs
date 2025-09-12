using Luna;
using Penumbra.Collections;
using Penumbra.Mods.Editor;
using Penumbra.String.Classes;

namespace Penumbra.Communication;

/// <summary> Triggered whenever a redirection in a mod collection cache is manipulated. </summary>
public sealed class ResolvedFileChanged(Logger log) : EventBase<ResolvedFileChanged.Arguments, ResolvedFileChanged.Priority>(
    nameof(ResolvedFileChanged), log)
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

    /// <summary> The arguments for a ResolvedFileChanged event. </summary>
    /// <param name="Type"> The type of the redirection change. </param>
    /// <param name="Collection"> The collection with a changed cache. </param>
    /// <param name="GamePath"> The game path to be redirected or empty for FullRecompute </param>
    /// <param name="NewRedirection"> The new redirection path or empty for Removed or FullRecompute. </param>
    /// <param name="OldRedirection"> The old redirection path for Replaced, or empty. </param>
    /// <param name="Mod"> The mod responsible for the new redirection if any. </param>
    public readonly record struct Arguments(
        Type Type,
        ModCollection Collection,
        Utf8GamePath GamePath,
        FullPath OldRedirection,
        FullPath NewRedirection,
        IMod? Mod);
}

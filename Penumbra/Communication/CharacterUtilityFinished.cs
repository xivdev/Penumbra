using OtterGui.Classes;
using Penumbra.Api;
using Penumbra.Interop.Services;

namespace Penumbra.Communication;

/// <summary>
/// Triggered when the Character Utility becomes ready.
/// </summary>
public sealed class CharacterUtilityFinished() : EventWrapper<CharacterUtilityFinished.Priority>(nameof(CharacterUtilityFinished))
{
    public enum Priority
    {
        /// <seealso cref="CharacterUtility"/>
        OnFinishedLoading = int.MaxValue,

        /// <seealso cref="IpcProviders.OnCharacterUtilityReady"/>
        IpcProvider = int.MinValue,

        /// <seealso cref="Collections.Cache.CollectionCacheManager"/>
        CollectionCacheManager = 0,
    }
}

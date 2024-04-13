using OtterGui.Log;
using OtterGui.Services;
using Penumbra.Api.Enums;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.GameData.Actors;
using Penumbra.GameData.Interop;
using Penumbra.Interop.PathResolving;

namespace Penumbra.Api.Api;

public class ApiHelpers(
    CollectionManager collectionManager,
    ObjectManager objects,
    CollectionResolver collectionResolver,
    ActorManager actors) : IApiService
{
    /// <summary> Return the associated identifier for an object given by its index. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal ActorIdentifier AssociatedIdentifier(int gameObjectIdx)
    {
        if (gameObjectIdx < 0 || gameObjectIdx >= objects.TotalCount)
            return ActorIdentifier.Invalid;

        var ptr = objects[gameObjectIdx];
        return actors.FromObject(ptr, out _, false, true, true);
    }

    /// <summary>
    /// Return the collection associated to a current game object. If it does not exist, return the default collection.
    /// If the index is invalid, returns false and the default collection.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal unsafe bool AssociatedCollection(int gameObjectIdx, out ModCollection collection)
    {
        collection = collectionManager.Active.Default;
        if (gameObjectIdx < 0 || gameObjectIdx >= objects.TotalCount)
            return false;

        var ptr  = objects[gameObjectIdx];
        var data = collectionResolver.IdentifyCollection(ptr.AsObject, false);
        if (data.Valid)
            collection = data.ModCollection;

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal static PenumbraApiEc Return(PenumbraApiEc ec, LazyString args, [CallerMemberName] string name = "Unknown")
    {
        if (ec is PenumbraApiEc.Success or PenumbraApiEc.NothingChanged)
            Penumbra.Log.Verbose($"[{name}] Called with {args}, returned {ec}.");
        else
            Penumbra.Log.Debug($"[{name}] Called with {args}, returned {ec}.");
        return ec;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal static LazyString Args(params object[] arguments)
    {
        if (arguments.Length == 0)
            return new LazyString(() => "no arguments");

        return new LazyString(() =>
        {
            var sb = new StringBuilder();
            for (var i = 0; i < arguments.Length / 2; ++i)
            {
                sb.Append(arguments[2 * i]);
                sb.Append(" = ");
                sb.Append(arguments[2 * i + 1]);
                sb.Append(", ");
            }

            return sb.ToString(0, sb.Length - 2);
        });
    }
}

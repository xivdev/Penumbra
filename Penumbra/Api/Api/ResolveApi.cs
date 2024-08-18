using Dalamud.Plugin.Services;
using OtterGui.Services;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.Interop.PathResolving;
using Penumbra.Mods.Manager;
using Penumbra.String.Classes;

namespace Penumbra.Api.Api;

public class ResolveApi(
    ModManager modManager,
    CollectionManager collectionManager,
    Configuration config,
    CollectionResolver collectionResolver,
    ApiHelpers helpers,
    IFramework framework) : IPenumbraApiResolve, IApiService
{
    public string ResolveDefaultPath(string gamePath)
        => ResolvePath(gamePath, modManager, collectionManager.Active.Default);

    public string ResolveInterfacePath(string gamePath)
        => ResolvePath(gamePath, modManager, collectionManager.Active.Interface);

    public string ResolveGameObjectPath(string gamePath, int gameObjectIdx)
    {
        helpers.AssociatedCollection(gameObjectIdx, out var collection);
        return ResolvePath(gamePath, modManager, collection);
    }

    public string ResolvePlayerPath(string gamePath)
        => ResolvePath(gamePath, modManager, collectionResolver.PlayerCollection());

    public string[] ReverseResolveGameObjectPath(string moddedPath, int gameObjectIdx)
    {
        if (!config.EnableMods)
            return [moddedPath];

        helpers.AssociatedCollection(gameObjectIdx, out var collection);
        var ret = collection.ReverseResolvePath(new FullPath(moddedPath));
        return ret.Select(r => r.ToString()).ToArray();
    }

    public string[] ReverseResolvePlayerPath(string moddedPath)
    {
        if (!config.EnableMods)
            return [moddedPath];

        var ret = collectionResolver.PlayerCollection().ReverseResolvePath(new FullPath(moddedPath));
        return ret.Select(r => r.ToString()).ToArray();
    }

    public (string[], string[][]) ResolvePlayerPaths(string[] forward, string[] reverse)
    {
        if (!config.EnableMods)
            return (forward, reverse.Select(p => new[]
            {
                p,
            }).ToArray());

        var playerCollection = collectionResolver.PlayerCollection();
        var resolved         = forward.Select(p => ResolvePath(p, modManager, playerCollection)).ToArray();
        var reverseResolved  = playerCollection.ReverseResolvePaths(reverse);
        return (resolved, reverseResolved.Select(a => a.Select(p => p.ToString()).ToArray()).ToArray());
    }

    public async Task<(string[], string[][])> ResolvePlayerPathsAsync(string[] forward, string[] reverse)
    {
        if (!config.EnableMods)
            return (forward, reverse.Select(p => new[]
            {
                p,
            }).ToArray());

        return await Task.Run(async () =>
        {
            var playerCollection = await framework.RunOnFrameworkThread(collectionResolver.PlayerCollection).ConfigureAwait(false);
            var forwardTask = Task.Run(() =>
            {
                var forwardRet = new string[forward.Length];
                Parallel.For(0, forward.Length, idx => forwardRet[idx] = ResolvePath(forward[idx], modManager, playerCollection));
                return forwardRet;
            }).ConfigureAwait(false);
            var reverseTask     = Task.Run(() => playerCollection.ReverseResolvePaths(reverse)).ConfigureAwait(false);
            var reverseResolved = (await reverseTask).Select(a => a.Select(p => p.ToString()).ToArray()).ToArray();
            return (await forwardTask, reverseResolved);
        }).ConfigureAwait(false);
    }

    /// <summary> Resolve a path given by string for a specific collection. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private string ResolvePath(string path, ModManager _, ModCollection collection)
    {
        if (!config.EnableMods)
            return path;

        var gamePath = Utf8GamePath.FromString(path, out var p) ? p : Utf8GamePath.Empty;
        var ret      = collection.ResolvePath(gamePath);
        return ret?.ToString() ?? path;
    }
}

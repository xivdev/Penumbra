using Penumbra.GameData;

namespace Penumbra.Interop.Hooks.ResourceLoading;

public sealed class PapHandler(PapRewriter.PapResourceHandlerPrototype papResourceHandler) : IDisposable
{
    private readonly PapRewriter _papRewriter = new(papResourceHandler);

    public void Enable()
    {
        ReadOnlySpan<string> signatures =
        [
            Sigs.LoadAlwaysResidentMotionPacks,
            Sigs.LoadWeaponDependentResidentMotionPacks,
            Sigs.LoadInitialResidentMotionPacks,
            Sigs.LoadMotionPacks,
            Sigs.LoadMotionPacks2,
            Sigs.LoadMigratoryMotionPack,
        ];

        var stopwatch = Stopwatch.StartNew();
        foreach (var sig in signatures)
            _papRewriter.Rewrite(sig);
        Penumbra.Log.Debug(
            $"[PapHandler] Rewrote {signatures.Length} .pap functions for inlined GetResourceAsync in {stopwatch.ElapsedMilliseconds} ms.");
    }

    public void Dispose()
        => _papRewriter.Dispose();
}

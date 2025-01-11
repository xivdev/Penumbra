using Penumbra.GameData;

namespace Penumbra.Interop.Hooks.ResourceLoading;

public sealed class PapHandler(PeSigScanner sigScanner, PapRewriter.PapResourceHandlerPrototype papResourceHandler) : IDisposable
{
    private readonly PapRewriter _papRewriter = new(sigScanner, papResourceHandler);

    public void Enable()
    {
        if (HookOverrides.Instance.ResourceLoading.PapHooks)
            return;

        ReadOnlySpan<(string Sig, string Name)> signatures =
        [
            (Sigs.LoadAlwaysResidentMotionPacks, nameof(Sigs.LoadAlwaysResidentMotionPacks)),
            (Sigs.LoadWeaponDependentResidentMotionPacks, nameof(Sigs.LoadWeaponDependentResidentMotionPacks)),
            (Sigs.LoadInitialResidentMotionPacks, nameof(Sigs.LoadInitialResidentMotionPacks)),
            (Sigs.LoadMotionPacks, nameof(Sigs.LoadMotionPacks)),
            (Sigs.LoadMotionPacks2, nameof(Sigs.LoadMotionPacks2)),
            (Sigs.LoadMigratoryMotionPack, nameof(Sigs.LoadMigratoryMotionPack)),
        ];

        var stopwatch = Stopwatch.StartNew();
        foreach (var (sig, name) in signatures)
            _papRewriter.Rewrite(sig, name);
        Penumbra.Log.Debug(
            $"[PapHandler] Rewrote {signatures.Length} .pap functions for inlined GetResourceAsync in {stopwatch.ElapsedMilliseconds} ms.");
    }

    public void Dispose()
        => _papRewriter.Dispose();
}

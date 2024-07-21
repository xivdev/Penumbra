using Penumbra.GameData;

namespace Penumbra.Interop.Hooks.ResourceLoading;

public sealed class PapHandler(PapRewriter.PapResourceHandlerPrototype papResourceHandler) : IDisposable
{
    private readonly PapRewriter _papRewriter = new(papResourceHandler);
    
    public void Enable()
    {
        _papRewriter.Rewrite(Sigs.LoadAlwaysResidentMotionPacks);
        _papRewriter.Rewrite(Sigs.LoadWeaponDependentResidentMotionPacks);
        _papRewriter.Rewrite(Sigs.LoadInitialResidentMotionPacks);
        _papRewriter.Rewrite(Sigs.LoadMotionPacks);
        _papRewriter.Rewrite(Sigs.LoadMotionPacks2);
        _papRewriter.Rewrite(Sigs.LoadMigratoryMotionPack);
    }
    
    public void Dispose()
    {
        _papRewriter.Dispose();
    }
}

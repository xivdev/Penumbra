using Penumbra.Meta.Manipulations;

namespace Penumbra.UI.AdvancedWindow.Meta;

public class MetaDrawers(
    EqdpMetaDrawer eqdp,
    EqpMetaDrawer eqp,
    EstMetaDrawer est,
    GlobalEqpMetaDrawer globalEqp,
    GmpMetaDrawer gmp,
    ImcMetaDrawer imc,
    RspMetaDrawer rsp,
    AtchMetaDrawer atch,
    ShpMetaDrawer shp,
    AtrMetaDrawer atr) : Luna.IService
{
    public readonly EqdpMetaDrawer      Eqdp      = eqdp;
    public readonly EqpMetaDrawer       Eqp       = eqp;
    public readonly EstMetaDrawer       Est       = est;
    public readonly GmpMetaDrawer       Gmp       = gmp;
    public readonly RspMetaDrawer       Rsp       = rsp;
    public readonly ImcMetaDrawer       Imc       = imc;
    public readonly GlobalEqpMetaDrawer GlobalEqp = globalEqp;
    public readonly AtchMetaDrawer      Atch      = atch;
    public readonly ShpMetaDrawer       Shp       = shp;
    public readonly AtrMetaDrawer       Atr       = atr;

    public IMetaDrawer? Get(MetaManipulationType type)
        => type switch
        {
            MetaManipulationType.Imc       => Imc,
            MetaManipulationType.Eqdp      => Eqdp,
            MetaManipulationType.Eqp       => Eqp,
            MetaManipulationType.Est       => Est,
            MetaManipulationType.Gmp       => Gmp,
            MetaManipulationType.Rsp       => Rsp,
            MetaManipulationType.Atch      => Atch,
            MetaManipulationType.Shp       => Shp,
            MetaManipulationType.Atr       => Atr,
            MetaManipulationType.GlobalEqp => GlobalEqp,
            _                              => null,
        };
}

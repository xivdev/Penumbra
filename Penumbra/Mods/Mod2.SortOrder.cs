namespace Penumbra.Mods;

public sealed partial class Mod2
{
    public Mod.SortOrder Order;
    public override string ToString()
        => Order.FullPath;
}
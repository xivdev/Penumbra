using System.Collections.Generic;

namespace Penumbra.Mods;

public class ModCache
{
    public int  TotalFileCount;
    public int  TotalSwapCount;
    public int  TotalManipulations;
    public bool HasOptions;

    public readonly SortedList<string, object?> ChangedItems            = new();
    public          string                      LowerChangedItemsString = string.Empty;
    public          string                      AllTagsLower            = string.Empty;

    public ModCache()
    {}

    public void Reset()
    {
        TotalFileCount     = 0;
        TotalSwapCount     = 0;
        TotalManipulations = 0;
        HasOptions         = false;
        ChangedItems.Clear();
        LowerChangedItemsString = string.Empty;
        AllTagsLower            = string.Empty;
    }
}

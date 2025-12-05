using System.Collections.Frozen;
using Luna;
using Penumbra.Mods;

namespace Penumbra.UI.ModsTab.Selector;

public readonly struct ImportDate : ISortMode
{
    public static readonly ImportDate Instance = new();

    public ReadOnlySpan<byte> Name
        => "Import Date (Older First)"u8;

    public ReadOnlySpan<byte> Description
        => "In each folder, sort all subfolders lexicographically, then sort all leaves using their import date."u8;

    public IEnumerable<IFileSystemNode> GetChildren(IFileSystemFolder f)
        => f.GetSubFolders().Cast<IFileSystemNode>().Concat(f.GetLeaves().OfType<IFileSystemData<Mod>>().OrderBy(l => l.Value.ImportDate));
}

public readonly struct InverseImportDate : ISortMode
{
    public static readonly InverseImportDate Instance = new();

    public ReadOnlySpan<byte> Name
        => "Import Date (Newer First)"u8;

    public ReadOnlySpan<byte> Description
        => "In each folder, sort all subfolders lexicographically, then sort all leaves using their inverse import date."u8;

    public IEnumerable<IFileSystemNode> GetChildren(IFileSystemFolder f)
        => f.GetSubFolders().Cast<IFileSystemNode>()
            .Concat(f.GetLeaves().OfType<IFileSystemData<Mod>>().OrderByDescending(l => l.Value.ImportDate));
}

public static class SortModeExtensions
{
    private static readonly FrozenDictionary<string, ISortMode> ValidSortModes = new Dictionary<string, ISortMode>
    {
        [nameof(ISortMode.FoldersFirst)]           = ISortMode.FoldersFirst,
        [nameof(ISortMode.Lexicographical)]        = ISortMode.Lexicographical,
        [nameof(ImportDate)]                       = ISortMode.ImportDate,
        [nameof(InverseImportDate)]                = ISortMode.InverseImportDate,
        [nameof(ISortMode.InverseFoldersFirst)]    = ISortMode.InverseFoldersFirst,
        [nameof(ISortMode.InverseLexicographical)] = ISortMode.InverseLexicographical,
        [nameof(ISortMode.FoldersLast)]            = ISortMode.FoldersLast,
        [nameof(ISortMode.InverseFoldersLast)]     = ISortMode.InverseFoldersLast,
        [nameof(ISortMode.InternalOrder)]          = ISortMode.InternalOrder,
        [nameof(ISortMode.InverseInternalOrder)]   = ISortMode.InverseInternalOrder,
    }.ToFrozenDictionary();

    extension(ISortMode)
    {
        public static ISortMode ImportDate
            => ImportDate.Instance;

        public static ISortMode InverseImportDate
            => InverseImportDate.Instance;

        public static IReadOnlyDictionary<string, ISortMode> Valid
            => ValidSortModes;
    }
}

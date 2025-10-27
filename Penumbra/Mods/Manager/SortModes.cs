using Luna;

namespace Penumbra.Mods.Manager;

public readonly struct ImportDate : ISortMode
{
    public ReadOnlySpan<byte> Name
        => "Import Date (Older First)"u8;

    public ReadOnlySpan<byte> Description
        => "In each folder, sort all subfolders lexicographically, then sort all leaves using their import date."u8;

    public IEnumerable<IFileSystemNode> GetChildren(IFileSystemFolder f)
        => f.GetSubFolders().Cast<IFileSystemNode>().Concat(f.GetLeaves().OfType<IFileSystemData<Mod>>().OrderBy(l => l.Value.ImportDate));
}

public readonly struct InverseImportDate : ISortMode
{
    public ReadOnlySpan<byte> Name
        => "Import Date (Newer First)"u8;

    public ReadOnlySpan<byte> Description
        => "In each folder, sort all subfolders lexicographically, then sort all leaves using their inverse import date."u8;

    public IEnumerable<IFileSystemNode> GetChildren(IFileSystemFolder f)
        => f.GetSubFolders().Cast<IFileSystemNode>()
            .Concat(f.GetLeaves().OfType<IFileSystemData<Mod>>().OrderByDescending(l => l.Value.ImportDate));
}

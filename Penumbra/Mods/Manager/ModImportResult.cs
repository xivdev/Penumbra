namespace Penumbra.Mods.Manager;

public readonly record struct ModImportResult(FileInfo File, DirectoryInfo? Mod, Exception? Error);

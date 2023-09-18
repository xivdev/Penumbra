namespace Penumbra.Import.Structs;

public enum ImporterState
{
    None,
    WritingPackToDisk,
    ExtractingModFiles,
    DeduplicatingFiles,
    Done,
}

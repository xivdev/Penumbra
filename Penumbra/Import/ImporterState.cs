namespace Penumbra.Import;

public enum ImporterState
{
    None,
    WritingPackToDisk,
    ExtractingModFiles,
    DeduplicatingFiles,
    Done,
}
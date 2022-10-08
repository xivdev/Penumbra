namespace Penumbra.Api.Enums;

public enum PenumbraApiEc
{
    Success            = 0,
    NothingChanged     = 1,
    CollectionMissing  = 2,
    ModMissing         = 3,
    OptionGroupMissing = 4,
    OptionMissing      = 5,

    CharacterCollectionExists = 6,
    LowerPriority             = 7,
    InvalidGamePath           = 8,
    FileMissing               = 9,
    InvalidManipulation       = 10,
    InvalidArgument           = 11,
    PathRenameFailed          = 12,
    UnknownError              = 255,
}
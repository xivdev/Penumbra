using Luna.Generators;

namespace Penumbra.Import.Textures;

/// <remarks> Presented to the user as "Hardware Acceleration Mode for Texture Compression". </remarks>
[NamedEnum(Utf16: false)]
[TooltipEnum]
public enum AuxiliaryDeviceMode
{
    [Name("Ephemeral")]
    [Tooltip("Create an ephemeral Direct3D device object per texture compression operation.")]
    Transient,

    [Name("Persistent")]
    [Tooltip(
        "Create a persistent Direct3D device object on the first texture compression operation and keep it until Penumbra is unloaded or when this setting is changed.")]
    Singleton,

    [Name("Use Main Game Device")]
    [Tooltip(
        "Do not create an auxiliary Direct3D device object, and use the game's main one instead.\nWill cause freezes while doing texture compression operations.\nPrefer the above options if possible.")]
    Borrowed,

    [Name("Disable Hardware Acceleration")]
    [Tooltip(
        "Do not create an auxiliary Direct3D device object, and use a software compression method instead.\nWill significantly slow down texture compression operations, and significantly degrade the output quality.\nONLY USE AS A LAST RESORT.")]
    None,
}

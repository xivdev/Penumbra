using System;
using System.IO;
using System.Linq;
using FFXIVClientStructs.FFXIV.Client.System.Resource;
using Penumbra.String;
using Penumbra.String.Functions;

namespace Penumbra.GameData.Enums;

public enum ResourceType : uint
{
    Unknown = 0,
    Aet     = 0x00616574,
    Amb     = 0x00616D62,
    Atch    = 0x61746368,
    Atex    = 0x61746578,
    Avfx    = 0x61766678,
    Awt     = 0x00617774,
    Cmp     = 0x00636D70,
    Dic     = 0x00646963,
    Eid     = 0x00656964,
    Envb    = 0x656E7662,
    Eqdp    = 0x65716470,
    Eqp     = 0x00657170,
    Essb    = 0x65737362,
    Est     = 0x00657374,
    Evp     = 0x00657670,
    Exd     = 0x00657864,
    Exh     = 0x00657868,
    Exl     = 0x0065786C,
    Fdt     = 0x00666474,
    Gfd     = 0x00676664,
    Ggd     = 0x00676764,
    Gmp     = 0x00676D70,
    Gzd     = 0x00677A64,
    Imc     = 0x00696D63,
    Lcb     = 0x006C6362,
    Lgb     = 0x006C6762,
    Luab    = 0x6C756162,
    Lvb     = 0x006C7662,
    Mdl     = 0x006D646C,
    Mlt     = 0x006D6C74,
    Mtrl    = 0x6D74726C,
    Obsb    = 0x6F627362,
    Pap     = 0x00706170,
    Pbd     = 0x00706264,
    Pcb     = 0x00706362,
    Phyb    = 0x70687962,
    Plt     = 0x00706C74,
    Scd     = 0x00736364,
    Sgb     = 0x00736762,
    Shcd    = 0x73686364,
    Shpk    = 0x7368706B,
    Sklb    = 0x736B6C62,
    Skp     = 0x00736B70,
    Stm     = 0x0073746D,
    Svb     = 0x00737662,
    Tera    = 0x74657261,
    Tex     = 0x00746578,
    Tmb     = 0x00746D62,
    Ugd     = 0x00756764,
    Uld     = 0x00756C64,
    Waoe    = 0x77616F65,
    Wtd     = 0x00777464,
}

[Flags]
public enum ResourceTypeFlag : ulong
{
    Aet  = 0x0000_0000_0000_0001,
    Amb  = 0x0000_0000_0000_0002,
    Atch = 0x0000_0000_0000_0004,
    Atex = 0x0000_0000_0000_0008,
    Avfx = 0x0000_0000_0000_0010,
    Awt  = 0x0000_0000_0000_0020,
    Cmp  = 0x0000_0000_0000_0040,
    Dic  = 0x0000_0000_0000_0080,
    Eid  = 0x0000_0000_0000_0100,
    Envb = 0x0000_0000_0000_0200,
    Eqdp = 0x0000_0000_0000_0400,
    Eqp  = 0x0000_0000_0000_0800,
    Essb = 0x0000_0000_0000_1000,
    Est  = 0x0000_0000_0000_2000,
    Evp  = 0x0000_0000_0000_4000,
    Exd  = 0x0000_0000_0000_8000,
    Exh  = 0x0000_0000_0001_0000,
    Exl  = 0x0000_0000_0002_0000,
    Fdt  = 0x0000_0000_0004_0000,
    Gfd  = 0x0000_0000_0008_0000,
    Ggd  = 0x0000_0000_0010_0000,
    Gmp  = 0x0000_0000_0020_0000,
    Gzd  = 0x0000_0000_0040_0000,
    Imc  = 0x0000_0000_0080_0000,
    Lcb  = 0x0000_0000_0100_0000,
    Lgb  = 0x0000_0000_0200_0000,
    Luab = 0x0000_0000_0400_0000,
    Lvb  = 0x0000_0000_0800_0000,
    Mdl  = 0x0000_0000_1000_0000,
    Mlt  = 0x0000_0000_2000_0000,
    Mtrl = 0x0000_0000_4000_0000,
    Obsb = 0x0000_0000_8000_0000,
    Pap  = 0x0000_0001_0000_0000,
    Pbd  = 0x0000_0002_0000_0000,
    Pcb  = 0x0000_0004_0000_0000,
    Phyb = 0x0000_0008_0000_0000,
    Plt  = 0x0000_0010_0000_0000,
    Scd  = 0x0000_0020_0000_0000,
    Sgb  = 0x0000_0040_0000_0000,
    Shcd = 0x0000_0080_0000_0000,
    Shpk = 0x0000_0100_0000_0000,
    Sklb = 0x0000_0200_0000_0000,
    Skp  = 0x0000_0400_0000_0000,
    Stm  = 0x0000_0800_0000_0000,
    Svb  = 0x0000_1000_0000_0000,
    Tera = 0x0000_2000_0000_0000,
    Tex  = 0x0000_4000_0000_0000,
    Tmb  = 0x0000_8000_0000_0000,
    Ugd  = 0x0001_0000_0000_0000,
    Uld  = 0x0002_0000_0000_0000,
    Waoe = 0x0004_0000_0000_0000,
    Wtd  = 0x0008_0000_0000_0000,
}

[Flags]
public enum ResourceCategoryFlag : ushort
{
    Common     = 0x0001,
    BgCommon   = 0x0002,
    Bg         = 0x0004,
    Cut        = 0x0008,
    Chara      = 0x0010,
    Shader     = 0x0020,
    Ui         = 0x0040,
    Sound      = 0x0080,
    Vfx        = 0x0100,
    UiScript   = 0x0200,
    Exd        = 0x0400,
    GameScript = 0x0800,
    Music      = 0x1000,
    SqpackTest = 0x2000,
}

public static class ResourceExtensions
{
    public static readonly ResourceTypeFlag     AllResourceTypes      = Enum.GetValues<ResourceTypeFlag>().Aggregate((v, f) => v | f);
    public static readonly ResourceCategoryFlag AllResourceCategories = Enum.GetValues<ResourceCategoryFlag>().Aggregate((v, f) => v | f);

    public static ResourceTypeFlag ToFlag(this ResourceType type)
        => type switch
        {
            ResourceType.Aet  => ResourceTypeFlag.Aet,
            ResourceType.Amb  => ResourceTypeFlag.Amb,
            ResourceType.Atch => ResourceTypeFlag.Atch,
            ResourceType.Atex => ResourceTypeFlag.Atex,
            ResourceType.Avfx => ResourceTypeFlag.Avfx,
            ResourceType.Awt  => ResourceTypeFlag.Awt,
            ResourceType.Cmp  => ResourceTypeFlag.Cmp,
            ResourceType.Dic  => ResourceTypeFlag.Dic,
            ResourceType.Eid  => ResourceTypeFlag.Eid,
            ResourceType.Envb => ResourceTypeFlag.Envb,
            ResourceType.Eqdp => ResourceTypeFlag.Eqdp,
            ResourceType.Eqp  => ResourceTypeFlag.Eqp,
            ResourceType.Essb => ResourceTypeFlag.Essb,
            ResourceType.Est  => ResourceTypeFlag.Est,
            ResourceType.Evp  => ResourceTypeFlag.Evp,
            ResourceType.Exd  => ResourceTypeFlag.Exd,
            ResourceType.Exh  => ResourceTypeFlag.Exh,
            ResourceType.Exl  => ResourceTypeFlag.Exl,
            ResourceType.Fdt  => ResourceTypeFlag.Fdt,
            ResourceType.Gfd  => ResourceTypeFlag.Gfd,
            ResourceType.Ggd  => ResourceTypeFlag.Ggd,
            ResourceType.Gmp  => ResourceTypeFlag.Gmp,
            ResourceType.Gzd  => ResourceTypeFlag.Gzd,
            ResourceType.Imc  => ResourceTypeFlag.Imc,
            ResourceType.Lcb  => ResourceTypeFlag.Lcb,
            ResourceType.Lgb  => ResourceTypeFlag.Lgb,
            ResourceType.Luab => ResourceTypeFlag.Luab,
            ResourceType.Lvb  => ResourceTypeFlag.Lvb,
            ResourceType.Mdl  => ResourceTypeFlag.Mdl,
            ResourceType.Mlt  => ResourceTypeFlag.Mlt,
            ResourceType.Mtrl => ResourceTypeFlag.Mtrl,
            ResourceType.Obsb => ResourceTypeFlag.Obsb,
            ResourceType.Pap  => ResourceTypeFlag.Pap,
            ResourceType.Pbd  => ResourceTypeFlag.Pbd,
            ResourceType.Pcb  => ResourceTypeFlag.Pcb,
            ResourceType.Phyb => ResourceTypeFlag.Phyb,
            ResourceType.Plt  => ResourceTypeFlag.Plt,
            ResourceType.Scd  => ResourceTypeFlag.Scd,
            ResourceType.Sgb  => ResourceTypeFlag.Sgb,
            ResourceType.Shcd => ResourceTypeFlag.Shcd,
            ResourceType.Shpk => ResourceTypeFlag.Shpk,
            ResourceType.Sklb => ResourceTypeFlag.Sklb,
            ResourceType.Skp  => ResourceTypeFlag.Skp,
            ResourceType.Stm  => ResourceTypeFlag.Stm,
            ResourceType.Svb  => ResourceTypeFlag.Svb,
            ResourceType.Tera => ResourceTypeFlag.Tera,
            ResourceType.Tex  => ResourceTypeFlag.Tex,
            ResourceType.Tmb  => ResourceTypeFlag.Tmb,
            ResourceType.Ugd  => ResourceTypeFlag.Ugd,
            ResourceType.Uld  => ResourceTypeFlag.Uld,
            ResourceType.Waoe => ResourceTypeFlag.Waoe,
            ResourceType.Wtd  => ResourceTypeFlag.Wtd,
            _                 => 0,
        };

    public static bool FitsFlag(this ResourceType type, ResourceTypeFlag flags)
        => (type.ToFlag() & flags) != 0;

    public static ResourceCategoryFlag ToFlag(this ResourceCategory type)
        => type switch
        {
            ResourceCategory.Common     => ResourceCategoryFlag.Common,
            ResourceCategory.BgCommon   => ResourceCategoryFlag.BgCommon,
            ResourceCategory.Bg         => ResourceCategoryFlag.Bg,
            ResourceCategory.Cut        => ResourceCategoryFlag.Cut,
            ResourceCategory.Chara      => ResourceCategoryFlag.Chara,
            ResourceCategory.Shader     => ResourceCategoryFlag.Shader,
            ResourceCategory.Ui         => ResourceCategoryFlag.Ui,
            ResourceCategory.Sound      => ResourceCategoryFlag.Sound,
            ResourceCategory.Vfx        => ResourceCategoryFlag.Vfx,
            ResourceCategory.UiScript   => ResourceCategoryFlag.UiScript,
            ResourceCategory.Exd        => ResourceCategoryFlag.Exd,
            ResourceCategory.GameScript => ResourceCategoryFlag.GameScript,
            ResourceCategory.Music      => ResourceCategoryFlag.Music,
            ResourceCategory.SqpackTest => ResourceCategoryFlag.SqpackTest,
            _                           => 0,
        };

    public static bool FitsFlag(this ResourceCategory type, ResourceCategoryFlag flags)
        => (type.ToFlag() & flags) != 0;

    public static ResourceType FromBytes(byte a1, byte a2, byte a3)
        => (ResourceType)(((uint)ByteStringFunctions.AsciiToLower(a1) << 16)
          | ((uint)ByteStringFunctions.AsciiToLower(a2) << 8)
          | ByteStringFunctions.AsciiToLower(a3));

    public static ResourceType FromBytes(byte a1, byte a2, byte a3, byte a4)
        => (ResourceType)(((uint)ByteStringFunctions.AsciiToLower(a1) << 24)
          | ((uint)ByteStringFunctions.AsciiToLower(a2) << 16)
          | ((uint)ByteStringFunctions.AsciiToLower(a3) << 8)
          | ByteStringFunctions.AsciiToLower(a4));

    public static ResourceType FromBytes(char a1, char a2, char a3)
        => FromBytes((byte)a1, (byte)a2, (byte)a3);

    public static ResourceType FromBytes(char a1, char a2, char a3, char a4)
        => FromBytes((byte)a1, (byte)a2, (byte)a3, (byte)a4);

    public static ResourceType Type(string path)
    {
        var ext = Path.GetExtension(path.AsSpan());
        ext = ext.Length == 0 ? path.AsSpan() : ext[1..];

        return ext.Length switch
        {
            0 => 0,
            1 => (ResourceType)ext[^1],
            2 => FromBytes('\0',    ext[^2], ext[^1]),
            3 => FromBytes(ext[^3], ext[^2], ext[^1]),
            _ => FromBytes(ext[^4], ext[^3], ext[^2], ext[^1]),
        };
    }

    public static ResourceType Type(ByteString path)
    {
        var extIdx = path.LastIndexOf((byte)'.');
        var ext    = extIdx == -1 ? path : extIdx == path.Length - 1 ? ByteString.Empty : path.Substring(extIdx + 1);

        return ext.Length switch
        {
            0 => 0,
            1 => (ResourceType)ext[^1],
            2 => FromBytes(0,       ext[^2], ext[^1]),
            3 => FromBytes(ext[^3], ext[^2], ext[^1]),
            _ => FromBytes(ext[^4], ext[^3], ext[^2], ext[^1]),
        };
    }

    public static ResourceCategory Category(ByteString path)
    {
        if (path.Length < 3)
            return ResourceCategory.Debug;

        return ByteStringFunctions.AsciiToUpper(path[0]) switch
        {
            (byte)'C' => ByteStringFunctions.AsciiToUpper(path[1]) switch
            {
                (byte)'O' => ResourceCategory.Common,
                (byte)'U' => ResourceCategory.Cut,
                (byte)'H' => ResourceCategory.Chara,
                _         => ResourceCategory.Debug,
            },
            (byte)'B' => ByteStringFunctions.AsciiToUpper(path[2]) switch
            {
                (byte)'C' => ResourceCategory.BgCommon,
                (byte)'/' => ResourceCategory.Bg,
                _         => ResourceCategory.Debug,
            },
            (byte)'S' => ByteStringFunctions.AsciiToUpper(path[1]) switch
            {
                (byte)'H' => ResourceCategory.Shader,
                (byte)'O' => ResourceCategory.Sound,
                (byte)'Q' => ResourceCategory.SqpackTest,
                _         => ResourceCategory.Debug,
            },
            (byte)'U' => ByteStringFunctions.AsciiToUpper(path[2]) switch
            {
                (byte)'/' => ResourceCategory.Ui,
                (byte)'S' => ResourceCategory.UiScript,
                _         => ResourceCategory.Debug,
            },
            (byte)'V' => ResourceCategory.Vfx,
            (byte)'E' => ResourceCategory.Exd,
            (byte)'G' => ResourceCategory.GameScript,
            (byte)'M' => ResourceCategory.Music,
            _         => ResourceCategory.Debug,
        };
    }
}

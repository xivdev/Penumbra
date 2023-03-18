using System;
using Penumbra.GameData.Files;
using Penumbra.Meta.Manipulations;
using Penumbra.String.Classes;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Penumbra.GameData.Enums;
using static Penumbra.Mods.ItemSwap.ItemSwap;
using Penumbra.Services;

namespace Penumbra.Mods.ItemSwap;

public class Swap
{
    /// <summary> Any further swaps belonging specifically to this tree of changes. </summary>
    public readonly List< Swap > ChildSwaps = new();

    public IEnumerable< Swap > WithChildren()
        => ChildSwaps.SelectMany( c => c.WithChildren() ).Prepend( this );
}

public sealed class MetaSwap : Swap
{
    /// <summary> The default value of a specific meta manipulation that needs to be redirected. </summary>
    public MetaManipulation SwapFrom;

    /// <summary> The default value of the same Meta entry of the redirected item. </summary>
    public MetaManipulation SwapToDefault;

    /// <summary> The modded value of the same Meta entry of the redirected item, or the same as SwapToDefault if unmodded. </summary>
    public MetaManipulation SwapToModded;

    /// <summary> The modded value applied to the specific meta manipulation target before redirection. </summary>
    public MetaManipulation SwapApplied;

    /// <summary> Whether SwapToModded equals SwapToDefault. </summary>
    public bool SwapToIsDefault;

    /// <summary> Whether the applied meta manipulation does not change anything against the default. </summary>
    public bool SwapAppliedIsDefault;

    /// <summary>
    /// Create a new MetaSwap from the original meta identifier and the target meta identifier.
    /// </summary>
    /// <param name="manipulations">A function that converts the given manipulation to the modded one.</param>
    /// <param name="manipFrom">The original meta identifier with its default value.</param>
    /// <param name="manipTo">The target meta identifier with its default value.</param>
    public MetaSwap( Func< MetaManipulation, MetaManipulation > manipulations, MetaManipulation manipFrom, MetaManipulation manipTo )
    {
        SwapFrom      = manipFrom;
        SwapToDefault = manipTo;

        SwapToModded         = manipulations( manipTo );
        SwapToIsDefault      = manipTo.EntryEquals( SwapToModded );
        SwapApplied          = SwapFrom.WithEntryOf( SwapToModded );
        SwapAppliedIsDefault = SwapApplied.EntryEquals( SwapFrom );
    }
}

public sealed class FileSwap : Swap
{
    /// <summary> The file type, used for bookkeeping. </summary>
    public ResourceType Type;

    /// <summary> The binary or parsed data of the file at SwapToModded. </summary>
    public IWritable FileData = GenericFile.Invalid;

    /// <summary> The path that would be requested without manipulated parent files. </summary>
    public string SwapFromPreChangePath = string.Empty;

    /// <summary> The Path that needs to be redirected. </summary>
    public Utf8GamePath SwapFromRequestPath;

    /// <summary> The path that the game should request instead, if no mods are involved. </summary>
    public Utf8GamePath SwapToRequestPath;

    /// <summary> The path to the actual file that should be loaded. This can be the same as SwapToRequestPath or a file on the drive. </summary>
    public FullPath SwapToModded;

    /// <summary> Whether the target file is an actual game file. </summary>
    public bool SwapToModdedExistsInGame;

    /// <summary> Whether the target file could be read either from the game or the drive. </summary>
    public bool SwapToModdedExists
        => FileData.Valid;

    /// <summary> Whether SwapToModded is a path to a game file that equals SwapFromGamePath. </summary>
    public bool SwapToModdedEqualsOriginal;

    /// <summary> Whether the data in FileData was manipulated from the original file. </summary>
    public bool DataWasChanged;

    /// <summary> Whether SwapFromPreChangePath equals SwapFromRequest. </summary>
    public bool SwapFromChanged;

    public string GetNewPath( string newMod )
        => Path.Combine( newMod, new Utf8RelPath( SwapFromRequestPath ).ToString() );

    public MdlFile? AsMdl()
        => FileData as MdlFile;

    public MtrlFile? AsMtrl()
        => FileData as MtrlFile;

    public AvfxFile? AsAvfx()
        => FileData as AvfxFile;

    /// <summary>
    /// Create a full swap container for a specific file type using a modded redirection set, the actually requested path and the game file it should load instead after the swap.
    /// </summary>
    /// <param name="type">The file type. Mdl and Mtrl have special file loading treatment.</param>
    /// <param name="redirections">A function either returning the path after mod application.</param>
    /// <param name="swapFromRequest">The path the game is going to request when loading the file.</param>
    /// <param name="swapToRequest">The unmodded path to the file the game is supposed to load instead.</param>
    /// <param name="swap">A full swap container with the actual file in memory.</param>
    /// <returns>True if everything could be read correctly, false otherwise.</returns>
    public static FileSwap CreateSwap( ResourceType type, Func< Utf8GamePath, FullPath > redirections, string swapFromRequest, string swapToRequest,
        string? swapFromPreChange = null )
    {
        var swap = new FileSwap
        {
            Type                  = type,
            FileData              = GenericFile.Invalid,
            DataWasChanged        = false,
            SwapFromPreChangePath = swapFromPreChange ?? swapFromRequest,
            SwapFromChanged       = swapFromPreChange != swapFromRequest,
            SwapFromRequestPath   = Utf8GamePath.Empty,
            SwapToRequestPath     = Utf8GamePath.Empty,
            SwapToModded          = FullPath.Empty,
        };

        if( swapFromRequest.Length == 0
        || swapToRequest.Length    == 0
        || !Utf8GamePath.FromString( swapToRequest, out swap.SwapToRequestPath )
        || !Utf8GamePath.FromString( swapFromRequest, out swap.SwapFromRequestPath ) )
        {
            throw new Exception( $"Could not create UTF8 String for \"{swapFromRequest}\" or \"{swapToRequest}\"." );
        }

        swap.SwapToModded               = redirections( swap.SwapToRequestPath );
        swap.SwapToModdedExistsInGame   = !swap.SwapToModded.IsRooted && DalamudServices.SGameData.FileExists( swap.SwapToModded.InternalName.ToString() );
        swap.SwapToModdedEqualsOriginal = !swap.SwapToModded.IsRooted && swap.SwapToModded.InternalName.Equals( swap.SwapFromRequestPath.Path );

        swap.FileData = type switch
        {
            ResourceType.Mdl  => LoadMdl( swap.SwapToModded, out var f ) ? f : throw new MissingFileException( type, swap.SwapToModded ),
            ResourceType.Mtrl => LoadMtrl( swap.SwapToModded, out var f ) ? f : throw new MissingFileException( type, swap.SwapToModded ),
            ResourceType.Avfx => LoadAvfx( swap.SwapToModded, out var f ) ? f : throw new MissingFileException( type, swap.SwapToModded ),
            _                 => LoadFile( swap.SwapToModded, out var f ) ? f : throw new MissingFileException( type, swap.SwapToModded ),
        };

        return swap;
    }


    /// <summary>
    /// Convert a single file redirection to use the file name and extension given by type and the files SHA256 hash, if possible.
    /// </summary>
    /// <param name="redirections">The set of redirections that need to be considered.</param>
    /// <param name="path">The in- and output path for a file</param>
    /// <param name="dataWasChanged">Will be set to true if <paramref name="path"/> was changed.</param>
    /// <param name="swap">Will be updated.</param>
    public static bool CreateShaRedirection( Func< Utf8GamePath, FullPath > redirections, ref string path, ref bool dataWasChanged, ref FileSwap swap )
    {
        var oldFilename = Path.GetFileName( path );
        var hash        = SHA256.HashData( swap.FileData.Write() );
        var name =
            $"{( oldFilename.StartsWith( "--" ) ? "--" : string.Empty )}{string.Join( null, hash.Select( c => c.ToString( "x2" ) ) )}.{swap.Type.ToString().ToLowerInvariant()}";
        var newPath = path.Replace( oldFilename, name );
        var newSwap = CreateSwap( swap.Type, redirections, newPath, swap.SwapToRequestPath.ToString() );

        path           = newPath;
        dataWasChanged = true;
        swap           = newSwap;
        return true;
    }
}
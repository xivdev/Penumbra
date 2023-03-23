using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Game.ClientState.Objects.SubKinds;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Client.System.Resource;
using OtterGui;
using Penumbra.Collections;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Files;
using Penumbra.Interop.Resolver;
using Penumbra.Interop.Structs;
using Penumbra.String;
using Penumbra.String.Classes;
using Objects = Dalamud.Game.ClientState.Objects.Types;

namespace Penumbra.Interop;

public class ResourceTree
{
    public readonly string     Name;
    public readonly nint       SourceAddress;
    public readonly string     CollectionName;
    public readonly List<Node> Nodes;

    public ResourceTree( string name, nint sourceAddress, string collectionName )
    {
        Name           = name;
        SourceAddress  = sourceAddress;
        CollectionName = collectionName;
        Nodes          = new();
    }

    public static ResourceTree[] FromObjectTable( bool withNames = true )
    {
        var cache = new FileCache();

        return Dalamud.Objects
            .OfType<Objects.Character>()
            .Select( chara => FromCharacter( chara, cache, withNames ) )
            .OfType<ResourceTree>()
            .ToArray();
    }

    public static IEnumerable<(Objects.Character Character, ResourceTree ResourceTree)> FromCharacters( IEnumerable<Objects.Character> characters, bool withNames = true )
    {
        var cache = new FileCache();
        foreach( var chara in characters )
        {
            var tree = FromCharacter( chara, cache, withNames );
            if( tree != null )
            {
                yield return (chara, tree);
            }
        }
    }

    public static unsafe ResourceTree? FromCharacter( Objects.Character chara, bool withNames = true )
    {
        return FromCharacter( chara, new FileCache(), withNames );
    }

    private static unsafe ResourceTree? FromCharacter( Objects.Character chara, FileCache cache, bool withNames = true )
    {
        var charaStruct = ( Character* )chara.Address;
        var gameObjStruct = &charaStruct->GameObject;
        var model = ( CharacterBase* )gameObjStruct->GetDrawObject();
        if( model == null )
        {
            return null;
        }

        var equipment = new ReadOnlySpan<EquipmentRecord>( charaStruct->EquipSlotData, 10 );
        // var customize = new ReadOnlySpan<byte>( charaStruct->CustomizeData, 26 );

        var collectionResolveData = PathResolver.IdentifyCollection( gameObjStruct, true );
        if( !collectionResolveData.Valid )
        {
            return null;
        }
        var collection = collectionResolveData.ModCollection;

        var tree = new ResourceTree( chara.Name.ToString(), new nint( charaStruct ), collection.Name );

        var globalContext = new GlobalResolveContext(
            FileCache: cache,
            Collection: collection,
            Skeleton: charaStruct->ModelSkeletonId,
            WithNames: withNames
        );

        for( var i = 0; i < model->SlotCount; ++i )
        {
            var context = globalContext.CreateContext(
                Slot: ( i < equipment.Length ) ? ( ( uint )i ).ToEquipSlot() : EquipSlot.Unknown,
                Equipment: ( i < equipment.Length ) ? equipment[i] : default
            );

            var imc = ( ResourceHandle* )model->IMCArray[i];
            var imcNode = context.CreateNodeFromImc( imc );
            if( imcNode != null )
            {
                tree.Nodes.Add( withNames ? imcNode.WithName( imcNode.Name ?? $"IMC #{i}" ) : imcNode );
            }

            var mdl = ( RenderModel* )model->ModelArray[i];
            var mdlNode = context.CreateNodeFromRenderModel( mdl );
            if( mdlNode != null )
            {
                tree.Nodes.Add( withNames ? mdlNode.WithName(mdlNode.Name ?? $"Model #{i}") : mdlNode );
            }
        }

        if( chara is PlayerCharacter )
        {
            AddHumanResources( tree, globalContext, ( HumanExt* )model );
        }

        return tree;
    }

    private static unsafe void AddHumanResources( ResourceTree tree, GlobalResolveContext globalContext, HumanExt* human )
    {
        var prependIndex = 0;

        var firstWeapon = ( WeaponExt* )human->Human.CharacterBase.DrawObject.Object.ChildObject;
        if( firstWeapon != null )
        {
            var weapon = firstWeapon;
            var weaponIndex = 0;
            do
            {
                var weaponContext = globalContext.CreateContext(
                    Slot: EquipSlot.MainHand,
                    Equipment: new EquipmentRecord( weapon->Weapon.ModelSetId, ( byte )weapon->Weapon.Variant, ( byte )weapon->Weapon.ModelUnknown )
                );

                var weaponMdlNode = weaponContext.CreateNodeFromRenderModel( *weapon->WeaponRenderModel );
                if( weaponMdlNode != null )
                {
                    tree.Nodes.Insert( prependIndex++, globalContext.WithNames ? weaponMdlNode.WithName( weaponMdlNode.Name ?? $"Weapon Model #{weaponIndex}" ) : weaponMdlNode );
                }

                weapon = ( WeaponExt* )weapon->Weapon.CharacterBase.DrawObject.Object.NextSiblingObject;
                ++weaponIndex;
            } while( weapon != null && weapon != firstWeapon );
        }

        var context = globalContext.CreateContext(
            Slot: EquipSlot.Unknown,
            Equipment: default
        );

        var skeletonNode = context.CreateHumanSkeletonNode( human->Human.RaceSexId );
        if( skeletonNode != null )
        {
            tree.Nodes.Add( globalContext.WithNames ? skeletonNode.WithName( skeletonNode.Name ?? "Skeleton" ) : skeletonNode );
        }

        var decalNode = context.CreateNodeFromTex( human->Decal );
        if( decalNode != null )
        {
            tree.Nodes.Add( globalContext.WithNames ? decalNode.WithName( decalNode.Name ?? "Face Decal" ) : decalNode );
        }

        var legacyDecalNode = context.CreateNodeFromTex( human->LegacyBodyDecal );
        if( legacyDecalNode != null )
        {
            tree.Nodes.Add( globalContext.WithNames ? legacyDecalNode.WithName( legacyDecalNode.Name ?? "Legacy Body Decal" ) : legacyDecalNode );
        }
    }

    private static unsafe bool CreateOwnedGamePath( byte* rawGamePath, out Utf8GamePath gamePath, bool addDx11Prefix = false, bool isShader = false )
    {
        if( rawGamePath == null )
        {
            gamePath = default;
            return false;
        }

        if( isShader )
        {
            var path = $"shader/sm5/shpk/{new ByteString( rawGamePath )}";
            return Utf8GamePath.FromString( path, out gamePath );
        }

        if( addDx11Prefix )
        {
            var unprefixed = MemoryMarshal.CreateReadOnlySpanFromNullTerminated( rawGamePath );
            var lastDirectorySeparator = unprefixed.LastIndexOf( ( byte )'/' );
            if( unprefixed[lastDirectorySeparator + 1] != ( byte )'-' || unprefixed[lastDirectorySeparator + 2] != ( byte )'-' )
            {
                Span<byte> prefixed = stackalloc byte[unprefixed.Length + 2];
                unprefixed[..( lastDirectorySeparator + 1 )].CopyTo( prefixed );
                prefixed[lastDirectorySeparator + 1] = ( byte )'-';
                prefixed[lastDirectorySeparator + 2] = ( byte )'-';
                unprefixed[( lastDirectorySeparator + 1 )..].CopyTo( prefixed[( lastDirectorySeparator + 3 )..] );

                if( !Utf8GamePath.FromSpan( prefixed, out gamePath ) )
                {
                    return false;
                }

                gamePath = gamePath.Clone();
                return true;
            }
        }

        if( !Utf8GamePath.FromPointer( rawGamePath, out gamePath ) )
        {
            return false;
        }

        gamePath = gamePath.Clone();
        return true;
    }

    [StructLayout( LayoutKind.Sequential, Pack = 1, Size = 4 )]
    private readonly record struct EquipmentRecord( ushort SetId, byte Variant, byte Dye );

    private class FileCache
    {
        private readonly Dictionary<FullPath, MtrlFile?> Materials      = new();
        private readonly Dictionary<FullPath, ShpkFile?> ShaderPackages = new();

        public MtrlFile? ReadMaterial( FullPath path )
        {
            return ReadFile( path, Materials, bytes => new MtrlFile( bytes ) );
        }

        public ShpkFile? ReadShaderPackage( FullPath path )
        {
            return ReadFile( path, ShaderPackages, bytes => new ShpkFile( bytes ) );
        }

        private static T? ReadFile<T>( FullPath path, Dictionary<FullPath, T?> cache, Func<byte[], T> parseFile ) where T : class
        {
            if( path.FullName.Length == 0 )
            {
                return null;
            }

            if( cache.TryGetValue( path, out var cached ) )
            {
                return cached;
            }

            var pathStr = path.ToPath();
            T? parsed;
            try
            {
                if( path.IsRooted )
                {
                    parsed = parseFile( File.ReadAllBytes( pathStr ) );
                }
                else
                {
                    var bytes = Dalamud.GameData.GetFile( pathStr )?.Data;
                    parsed = ( bytes != null ) ? parseFile( bytes ) : null;
                }
            }
            catch( Exception e )
            {
                Penumbra.Log.Error( $"Could not read file {pathStr}:\n{e}" );
                parsed = null;
            }
            cache.Add( path, parsed );

            return parsed;
        }
    }

    private record class GlobalResolveContext( FileCache FileCache, ModCollection Collection, int Skeleton, bool WithNames )
    {
        public ResolveContext CreateContext( EquipSlot Slot, EquipmentRecord Equipment )
            => new( FileCache, Collection, Skeleton, WithNames, Slot, Equipment );
    }

    private record class ResolveContext( FileCache FileCache, ModCollection Collection, int Skeleton, bool WithNames, EquipSlot Slot, EquipmentRecord Equipment )
    {
        private unsafe Node? CreateNodeFromGamePath( ResourceType type, nint sourceAddress, byte* rawGamePath, bool @internal, bool addDx11Prefix = false, bool isShader = false )
        {
            if( !CreateOwnedGamePath( rawGamePath, out var gamePath, addDx11Prefix, isShader ) )
            {
                return null;
            }

            return CreateNodeFromGamePath( type, sourceAddress, gamePath, @internal );
        }

        private unsafe Node CreateNodeFromGamePath( ResourceType type, nint sourceAddress, Utf8GamePath gamePath, bool @internal )
            => new( null, type, sourceAddress, gamePath, FilterFullPath( Collection.ResolvePath( gamePath ) ?? new FullPath( gamePath ) ), @internal );

        private unsafe Node? CreateNodeFromResourceHandle( ResourceType type, nint sourceAddress, ResourceHandle* handle, bool @internal, bool withName )
        {
            if( handle == null )
            {
                return null;
            }

            var name = handle->FileNameAsSpan();
            if( name.Length == 0 )
            {
                return null;
            }

            if( name[0] == ( byte )'|' )
            {
                name = name[1..];
                var pos = name.IndexOf( ( byte )'|' );
                if( pos < 0 )
                {
                    return null;
                }
                name = name[( pos + 1 )..];
            }

            var fullPath = new FullPath( Encoding.UTF8.GetString( name ) );
            var gamePaths = Collection.ReverseResolvePath( fullPath ).ToList();
            fullPath = FilterFullPath( fullPath );

            if( gamePaths.Count > 1 )
            {
                gamePaths = Filter( gamePaths );
            }

            if( gamePaths.Count == 1 )
            {
                return new Node( withName ? GuessNameFromPath( gamePaths[0] ) : null, type, sourceAddress, gamePaths[0], fullPath, @internal );
            }
            else
            {
                Penumbra.Log.Information( $"Found {gamePaths.Count} game paths while reverse-resolving {fullPath} in {Collection.Name}:" );
                foreach( var gamePath in gamePaths )
                {
                    Penumbra.Log.Information( $"Game path: {gamePath}" );
                }

                return new Node( null, type, sourceAddress, gamePaths.ToArray(), fullPath, @internal );
            }
        }

        public unsafe Node? CreateHumanSkeletonNode( ushort raceSexId )
        {
            var raceSexIdStr = raceSexId.ToString( "D4" );
            var path = $"chara/human/c{raceSexIdStr}/skeleton/base/b0001/skl_c{raceSexIdStr}b0001.sklb";

            if( !Utf8GamePath.FromString( path, out var gamePath ) )
            {
                return null;
            }

            return CreateNodeFromGamePath( ResourceType.Sklb, 0, gamePath, false );
        }

        public unsafe Node? CreateNodeFromImc( ResourceHandle* imc )
        {
            var node = CreateNodeFromResourceHandle( ResourceType.Imc, new nint( imc ), imc, true, false );
            if( node == null )
            {
                return null;
            }
            if( WithNames )
            {
                var name = GuessModelName( node.GamePath );
                node = node.WithName( ( name != null ) ? $"IMC: {name}" : null );
            }

            return node;
        }

        public unsafe Node? CreateNodeFromTex( ResourceHandle* tex )
        {
            return CreateNodeFromResourceHandle( ResourceType.Tex, new nint( tex ), tex, false, WithNames );
        }

        public unsafe Node? CreateNodeFromRenderModel( RenderModel* mdl )
        {
            if( mdl == null || mdl->ResourceHandle == null || mdl->ResourceHandle->Category != ResourceCategory.Chara )
            {
                return null;
            }

            var node = CreateNodeFromResourceHandle( ResourceType.Mdl, new nint( mdl ), mdl->ResourceHandle, false, false );
            if( node == null )
            {
                return null;
            }
            if( WithNames )
            {
                node = node.WithName( GuessModelName( node.GamePath ) );
            }

            for( var i = 0; i < mdl->MaterialCount; i++ )
            {
                var mtrl = ( Material* )mdl->Materials[i];
                var mtrlNode = CreateNodeFromMaterial( mtrl );
                if( mtrlNode != null )
                {
                    // Don't keep the material's name if it's redundant with the model's name.
                    node.Children.Add( WithNames ? mtrlNode.WithName( ( string.Equals( mtrlNode.Name, node.Name, StringComparison.Ordinal ) ? null : mtrlNode.Name ) ?? $"Material #{i}" ) : mtrlNode );
                }
            }

            return node;
        }

        private unsafe Node? CreateNodeFromMaterial( Material* mtrl )
        {
            if( mtrl == null )
            {
                return null;
            }

            var resource = ( MtrlResource* )mtrl->ResourceHandle;
            var node = CreateNodeFromResourceHandle( ResourceType.Mtrl, new nint( mtrl ), &resource->Handle, false, WithNames );
            if( node == null )
            {
                return null;
            }
            var mtrlFile = WithNames ? FileCache.ReadMaterial( node.FullPath ) : null;

            var shpkNode = CreateNodeFromGamePath( ResourceType.Shpk, 0, resource->ShpkString, false, isShader: true );
            if( shpkNode != null )
            {
                node.Children.Add( WithNames ? shpkNode.WithName( "Shader Package" ) : shpkNode );
            }
            var shpkFile = ( WithNames && shpkNode != null ) ? FileCache.ReadShaderPackage( shpkNode.FullPath ) : null;
            var samplers = WithNames ? mtrlFile?.GetSamplersByTexture(shpkFile) : null;

            for( var i = 0; i < resource->NumTex; i++ )
            {
                var texNode = CreateNodeFromGamePath( ResourceType.Tex, 0, resource->TexString( i ), false, addDx11Prefix: resource->TexIsDX11( i ) );
                if( texNode != null )
                {
                    if( WithNames )
                    {
                        var name = ( samplers != null && i < samplers.Count ) ? samplers[i].Item2?.Name : null;
                        node.Children.Add( texNode.WithName( name ?? $"Texture #{i}" ) );
                    }
                    else
                    {
                        node.Children.Add( texNode );
                    }
                }
            }

            return node;
        }

        private static FullPath FilterFullPath( FullPath fullPath )
        {
            if( !fullPath.IsRooted )
            {
                return fullPath;
            }

            var relPath = Path.GetRelativePath( Penumbra.Config.ModDirectory, fullPath.FullName );
            if( relPath == "." || !relPath.StartsWith( '.' ) && !Path.IsPathRooted( relPath ) )
            {
                return fullPath.Exists ? fullPath : FullPath.Empty;
            }
            return FullPath.Empty;
        }

        private List<Utf8GamePath> Filter( List<Utf8GamePath> gamePaths )
        {
            var filtered = new List<Utf8GamePath>( gamePaths.Count );
            foreach( var path in gamePaths )
            {
                // In doubt, keep the paths.
                if( IsMatch( path.ToString().Split( new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries ) ) ?? true )
                {
                    filtered.Add( path );
                }
            }

            return filtered;
        }

        private bool? IsMatch( ReadOnlySpan<string> path )
            => SafeGet( path, 0 ) switch
            {
                "chara" => SafeGet( path, 1 ) switch
                {
                    "accessory" => IsMatchEquipment( path[2..], $"a{Equipment.SetId:D4}" ),
                    "equipment" => IsMatchEquipment( path[2..], $"e{Equipment.SetId:D4}" ),
                    "monster"   => SafeGet( path, 2 ) == $"m{Skeleton:D4}",
                    "weapon"    => IsMatchEquipment( path[2..], $"w{Equipment.SetId:D4}" ),
                    _           => null,
                },
                _ => null,
            };

        private bool? IsMatchEquipment( ReadOnlySpan<string> path, string equipmentDir )
            => SafeGet( path, 0 ) == equipmentDir
                ? SafeGet( path, 1 ) switch
                {
                    "material" => SafeGet( path, 2 ) == $"v{Equipment.Variant:D4}",
                    _          => null,
                }
                : false;

        private string? GuessModelName( Utf8GamePath gamePath )
        {
            var path = gamePath.ToString().Split( new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries );
            // Weapons intentionally left out.
            var isEquipment = SafeGet( path, 0 ) == "chara" && SafeGet( path, 1 ) is "accessory" or "equipment";
            if( isEquipment )
            {
                foreach( var item in Penumbra.Identifier.Identify( Equipment.SetId, Equipment.Variant, Slot.ToSlot() ) )
                {
                    return Slot switch
                    {
                        EquipSlot.RFinger => "R: ",
                        EquipSlot.LFinger => "L: ",
                        _                 => string.Empty,
                    } + item.Name.ToString();
                }
            }
            var nameFromPath = GuessNameFromPath( gamePath );
            if( nameFromPath != null )
            {
                return nameFromPath;
            }
            if( isEquipment )
            {
                return Slot.ToName();
            }

            return null;
        }

        private static string? GuessNameFromPath( Utf8GamePath gamePath )
        {
            foreach( var obj in Penumbra.Identifier.Identify( gamePath.ToString() ) )
            {
                var name = obj.Key;
                if( name.StartsWith( "Customization:" ) )
                {
                    name = name[14..].Trim();
                }
                if( name != "Unknown" )
                {
                    return name;
                }
            }

            return null;
        }

        private static string? SafeGet( ReadOnlySpan<string> array, Index index )
        {
            var i = index.GetOffset( array.Length );
            return ( i >= 0 && i < array.Length ) ? array[i] : null;
        }
    }

    public class Node
    {
        public readonly string?        Name;
        public readonly ResourceType   Type;
        public readonly nint           SourceAddress;
        public readonly Utf8GamePath   GamePath;
        public readonly Utf8GamePath[] PossibleGamePaths;
        public readonly FullPath       FullPath;
        public readonly bool           Internal;
        public readonly List<Node>     Children;

        public Node( string? name, ResourceType type, nint sourceAddress, Utf8GamePath gamePath, FullPath fullPath, bool @internal )
        {
            Name              = name;
            Type              = type;
            SourceAddress     = sourceAddress;
            GamePath          = gamePath;
            PossibleGamePaths = new[] { gamePath };
            FullPath          = fullPath;
            Internal          = @internal;
            Children          = new();
        }

        public Node( string? name, ResourceType type, nint sourceAddress, Utf8GamePath[] possibleGamePaths, FullPath fullPath, bool @internal )
        {
            Name              = name;
            Type              = type;
            SourceAddress     = sourceAddress;
            GamePath          = ( possibleGamePaths.Length == 1 ) ? possibleGamePaths[0] : Utf8GamePath.Empty;
            PossibleGamePaths = possibleGamePaths;
            FullPath          = fullPath;
            Internal          = @internal;
            Children          = new();
        }

        private Node( string? name, Node originalNode )
        {
            Name              = name;
            Type              = originalNode.Type;
            SourceAddress     = originalNode.SourceAddress;
            GamePath          = originalNode.GamePath;
            PossibleGamePaths = originalNode.PossibleGamePaths;
            FullPath          = originalNode.FullPath;
            Internal          = originalNode.Internal;
            Children          = originalNode.Children;
        }

        public Node WithName( string? name )
            => string.Equals( Name, name, StringComparison.Ordinal ) ? this : new Node( name, this );
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using ImGuiNET;
using Lumina.Data.Files;
using Penumbra.Game;
using Penumbra.GameData.Enums;
using Penumbra.Meta;
using Penumbra.Meta.Files;
using Penumbra.Util;
using ObjectType = Penumbra.GameData.Enums.ObjectType;

namespace Penumbra.UI
{
    public partial class SettingsInterface
    {
        private partial class PluginDetails
        {
            private int    _newManipTypeIdx     = 0;
            private ushort _newManipSetId       = 0;
            private ushort _newManipSecondaryId = 0;
            private int    _newManipSubrace     = 0;
            private int    _newManipRace        = 0;
            private int    _newManipAttribute   = 0;
            private int    _newManipEquipSlot   = 0;
            private int    _newManipObjectType  = 0;
            private int    _newManipGender      = 0;
            private int    _newManipBodySlot    = 0;
            private ushort _newManipVariant     = 0;

            private static readonly (string, EqpEntry)[] EqpAttributesBody =
            {
                ( "Enabled", EqpEntry.BodyEnabled ),
                ( "Hide Waist", EqpEntry.BodyHideWaist ),
                ( "Hide Small Gloves", EqpEntry.BodyHideGlovesS ),
                ( "Hide Medium Gloves", EqpEntry.BodyHideGlovesM ),
                ( "Hide Large Gloves", EqpEntry.BodyHideGlovesL ),
                ( "Hide Gorget", EqpEntry.BodyHideGorget ),
                ( "Show Legs", EqpEntry.BodyShowLeg ),
                ( "Show Hands", EqpEntry.BodyShowHand ),
                ( "Show Head", EqpEntry.BodyShowHead ),
                ( "Show Necklace", EqpEntry.BodyShowNecklace ),
                ( "Show Bracelet", EqpEntry.BodyShowBracelet ),
                ( "Show Tail", EqpEntry.BodyShowTail ),
                ( "Unknown  2", EqpEntry._2 ),
                ( "Unknown  4", EqpEntry._4 ),
                ( "Unknown 14", EqpEntry._14 ),
                ( "Unknown 15", EqpEntry._15 ),
            };

            private static readonly (string, EqpEntry)[] EqpAttributesLegs =
            {
                ( "Enabled", EqpEntry.LegsEnabled ),
                ( "Hide Kneepads", EqpEntry.LegsHideKneePads ),
                ( "Hide Small Boots", EqpEntry.LegsHideBootsS ),
                ( "Hide Medium Boots", EqpEntry.LegsHideBootsM ),
                ( "Hide Show Foot", EqpEntry.LegsShowFoot ),
                ( "Hide Show Tail", EqpEntry.LegsShowTail ),
                ( "Unknown 20", EqpEntry._20 ),
                ( "Unknown 23", EqpEntry._23 ),
            };

            private static readonly (string, EqpEntry)[] EqpAttributesHands =
            {
                ( "Enabled", EqpEntry.HandsEnabled ),
                ( "Hide Elbow", EqpEntry.HandsHideElbow ),
                ( "Hide Forearm", EqpEntry.HandsHideForearm ),
                ( "Show Bracelet", EqpEntry.HandShowBracelet ),
                ( "Show Left Ring", EqpEntry.HandShowRingL ),
                ( "Show Right Ring", EqpEntry.HandShowRingR ),
                ( "Unknown 27", EqpEntry._27 ),
                ( "Unknown 31", EqpEntry._31 ),
            };

            private static readonly (string, EqpEntry)[] EqpAttributesFeet =
            {
                ( "Enabled", EqpEntry.FeetEnabled ),
                ( "Hide Knees", EqpEntry.FeetHideKnee ),
                ( "Hide Calfs", EqpEntry.FeetHideCalf ),
                ( "Hide Ankles", EqpEntry.FeetHideAnkle ),
                ( "Unknown 36", EqpEntry._36 ),
                ( "Unknown 37", EqpEntry._37 ),
                ( "Unknown 38", EqpEntry._38 ),
                ( "Unknown 39", EqpEntry._39 ),
            };

            private static readonly (string, EqpEntry)[] EqpAttributesHead =
            {
                ( "Enabled", EqpEntry.HeadEnabled ),
                ( "Hide Scalp", EqpEntry.HeadHideScalp ),
                ( "Hide Hair", EqpEntry.HeadHideHair ),
                ( "Show Hair Override", EqpEntry.HeadShowHairOverride ),
                ( "Hide Neck", EqpEntry.HeadHideNeck ),
                ( "Show Necklace", EqpEntry.HeadShowNecklace ),
                ( "Show Earrings", EqpEntry.HeadShowEarrings ),
                ( "Show Earrings (Human)", EqpEntry.HeadShowEarringsHuman ),
                ( "Show Earrings (Au Ra)", EqpEntry.HeadShowEarringsAura ),
                ( "Show Ears (Human)", EqpEntry.HeadShowEarHuman ),
                ( "Show Ears (Miqo'te)", EqpEntry.HeadShowEarMiqote ),
                ( "Show Ears (Au Ra)", EqpEntry.HeadShowEarAuRa ),
                ( "Show Ears (Viera)", EqpEntry.HeadShowEarViera ),
                ( "Show on Hrothgar", EqpEntry.HeadShowHrothgarHat ),
                ( "Show on Viera", EqpEntry.HeadShowVieraHat ),
                ( "Unknown 46", EqpEntry._46 ),
                ( "Unknown 54", EqpEntry._54 ),
                ( "Unknown 55", EqpEntry._55 ),
                ( "Unknown 58", EqpEntry._58 ),
                ( "Unknown 59", EqpEntry._59 ),
                ( "Unknown 60", EqpEntry._60 ),
                ( "Unknown 61", EqpEntry._61 ),
                ( "Unknown 62", EqpEntry._62 ),
                ( "Unknown 63", EqpEntry._63 ),
            };

            private static readonly (string, EquipSlot)[] EqpEquipSlots = new[]
            {
                ( "Head", EquipSlot.Head ),
                ( "Body", EquipSlot.Body ),
                ( "Hands", EquipSlot.Hands ),
                ( "Legs", EquipSlot.Legs ),
                ( "Feet", EquipSlot.Feet ),
            };

            private static readonly (string, EquipSlot)[] EqdpEquipSlots = new[]
            {
                EqpEquipSlots[ 0 ],
                EqpEquipSlots[ 1 ],
                EqpEquipSlots[ 2 ],
                EqpEquipSlots[ 3 ],
                EqpEquipSlots[ 4 ],
                ( "Ears", EquipSlot.Ears ),
                ( "Neck", EquipSlot.Neck ),
                ( "Wrist", EquipSlot.Wrists ),
                ( "Left Finger", EquipSlot.RingL ),
                ( "Right Finger", EquipSlot.RingR ),
            };

            private static readonly (string, Race)[] Races = new[]
            {
                ( Race.Midlander.ToName(), Race.Midlander ),
                ( Race.Highlander.ToName(), Race.Highlander ),
                ( Race.Elezen.ToName(), Race.Elezen ),
                ( Race.Miqote.ToName(), Race.Miqote ),
                ( Race.Roegadyn.ToName(), Race.Roegadyn ),
                ( Race.Lalafell.ToName(), Race.Lalafell ),
                ( Race.AuRa.ToName(), Race.AuRa ),
                ( Race.Viera.ToName(), Race.Viera ),
                ( Race.Hrothgar.ToName(), Race.Hrothgar ),
            };

            private static readonly (string, Gender)[] Genders = new[]
            {
                ( Gender.Male.ToName(), Gender.Male ),
                ( Gender.Female.ToName(), Gender.Female ),
                ( Gender.MaleNpc.ToName(), Gender.MaleNpc ),
                ( Gender.FemaleNpc.ToName(), Gender.FemaleNpc ),
            };

            private static readonly (string, ObjectType)[] ObjectTypes = new[]
            {
                ( "Equipment", ObjectType.Equipment ),
                ( "Customization", ObjectType.Character ),
            };

            private static readonly (string, EquipSlot)[] EstEquipSlots = new[]
            {
                EqpEquipSlots[ 0 ],
                EqpEquipSlots[ 1 ],
            };

            private static readonly (string, BodySlot)[] EstBodySlots = new[]
            {
                ( "Hair", BodySlot.Hair ),
                ( "Face", BodySlot.Face ),
            };

            private static readonly (string, SubRace)[] Subraces = new[]
            {
                ( SubRace.Midlander.ToName(), SubRace.Midlander ),
                ( SubRace.Highlander.ToName(), SubRace.Highlander ),
                ( SubRace.Wildwood.ToName(), SubRace.Wildwood ),
                ( SubRace.Duskwright.ToName(), SubRace.Duskwright ),
                ( SubRace.SeekerOfTheSun.ToName(), SubRace.SeekerOfTheSun ),
                ( SubRace.KeeperOfTheMoon.ToName(), SubRace.KeeperOfTheMoon ),
                ( SubRace.Seawolf.ToName(), SubRace.Seawolf ),
                ( SubRace.Hellsguard.ToName(), SubRace.Hellsguard ),
                ( SubRace.Plainsfolk.ToName(), SubRace.Plainsfolk ),
                ( SubRace.Dunesfolk.ToName(), SubRace.Dunesfolk ),
                ( SubRace.Raen.ToName(), SubRace.Raen ),
                ( SubRace.Xaela.ToName(), SubRace.Xaela ),
                ( SubRace.Rava.ToName(), SubRace.Rava ),
                ( SubRace.Veena.ToName(), SubRace.Veena ),
                ( SubRace.Hellion.ToName(), SubRace.Hellion ),
                ( SubRace.Lost.ToName(), SubRace.Lost ),
            };

            private static readonly (string, RspAttribute)[] RspAttributes = new[]
            {
                ( "Male Minimum Size", RspAttribute.MaleMinSize ),
                ( "Male Maximum Size", RspAttribute.MaleMaxSize ),
                ( "Female Minimum Size", RspAttribute.FemaleMinSize ),
                ( "Female Maximum Size", RspAttribute.FemaleMaxSize ),
                ( "Bust Minimum X-Axis", RspAttribute.BustMinX ),
                ( "Bust Maximum X-Axis", RspAttribute.BustMaxX ),
                ( "Bust Minimum Y-Axis", RspAttribute.BustMinY ),
                ( "Bust Maximum Y-Axis", RspAttribute.BustMaxY ),
                ( "Bust Minimum Z-Axis", RspAttribute.BustMinZ ),
                ( "Bust Maximum Z-Axis", RspAttribute.BustMaxZ ),
                ( "Male Minimum Tail Length", RspAttribute.MaleMinTail ),
                ( "Male Maximum Tail Length", RspAttribute.MaleMaxTail ),
                ( "Female Minimum Tail Length", RspAttribute.FemaleMinTail ),
                ( "Female Maximum Tail Length", RspAttribute.FemaleMaxTail ),
            };

            private static readonly (string, ObjectType)[] ImcObjectType = new[]
            {
                ObjectTypes[ 0 ],
                ( "Weapon", ObjectType.Weapon ),
                ( "Demihuman", ObjectType.DemiHuman ),
                ( "Monster", ObjectType.Monster ),
            };

            private static readonly (string, BodySlot)[] ImcBodySlots = new[]
            {
                EstBodySlots[ 0 ],
                EstBodySlots[ 1 ],
                ( "Body", BodySlot.Body ),
                ( "Tail", BodySlot.Tail ),
                ( "Ears", BodySlot.Zear ),
            };

            private static bool PrintCheckBox( string name, ref bool value, bool def )
            {
                var color = value == def ? 0 : value ? ColorDarkGreen : ColorDarkRed;
                if( color == 0 )
                {
                    return ImGui.Checkbox( name, ref value );
                }

                ImGui.PushStyleColor( ImGuiCol.Text, color );
                var ret = ImGui.Checkbox( name, ref value );
                ImGui.PopStyleColor();
                return ret;
            }

            private bool RestrictedInputInt( string name, ref ushort value, ushort min, ushort max )
            {
                int tmp = value;
                if( ImGui.InputInt( name, ref tmp, 0, 0, _editMode ? ImGuiInputTextFlags.EnterReturnsTrue : ImGuiInputTextFlags.ReadOnly )
                 && tmp != value
                 && tmp >= min
                 && tmp <= max )
                {
                    value = ( ushort )tmp;
                    return true;
                }

                return false;
            }

            private static bool DefaultButton< T >( string name, ref T value, T defaultValue ) where T : IComparable< T >
            {
                var compare = defaultValue.CompareTo( value );
                var color = compare < 0 ? ColorDarkGreen :
                    compare         > 0 ? ColorDarkRed : ImGui.ColorConvertFloat4ToU32( ImGui.GetStyle().Colors[ ( int )ImGuiCol.Button ] );

                ImGui.PushStyleColor( ImGuiCol.Button, color );
                var ret = ImGui.Button( name, Vector2.UnitX * 120 ) && compare != 0;
                ImGui.SameLine();
                ImGui.PopStyleColor();
                return ret;
            }

            private bool DrawInputWithDefault( string name, ref ushort value, ushort defaultValue, ushort max )
                => DefaultButton( $"{( _editMode ? "Set to " : "" )}Default: {defaultValue}##imc{name}", ref value, defaultValue )
                 || RestrictedInputInt( name, ref value, 0, max );

            private static bool CustomCombo< T >( string label, IList< (string, T) > namesAndValues, out T value, ref int idx )
            {
                value = idx < namesAndValues.Count ? namesAndValues[ idx ].Item2 : default!;

                if( !ImGui.BeginCombo( label, idx < namesAndValues.Count ? namesAndValues[ idx ].Item1 : string.Empty ) )
                {
                    return false;
                }

                for( var i = 0; i < namesAndValues.Count; ++i )
                {
                    if( ImGui.Selectable( $"{namesAndValues[ i ].Item1}##{label}{i}", idx == i ) && idx != i )
                    {
                        idx   = i;
                        value = namesAndValues[ i ].Item2;
                        ImGui.EndCombo();
                        return true;
                    }
                }

                ImGui.EndCombo();
                return false;
            }

            private bool DrawEqpRow( int manipIdx, IList< MetaManipulation > list )
            {
                var ret = false;
                var id  = list[ manipIdx ].EqpIdentifier;
                var val = list[ manipIdx ].EqpValue;

                if( ImGui.BeginPopup( $"##MetaPopup{manipIdx}" ) )
                {
                    var defaults = ( EqpEntry )Service< MetaDefaults >.Get().GetDefaultValue( list[ manipIdx ] )!;
                    var attributes = id.Slot switch
                    {
                        EquipSlot.Head  => EqpAttributesHead,
                        EquipSlot.Body  => EqpAttributesBody,
                        EquipSlot.Hands => EqpAttributesHands,
                        EquipSlot.Legs  => EqpAttributesLegs,
                        EquipSlot.Feet  => EqpAttributesFeet,
                        _               => Array.Empty< (string, EqpEntry) >(),
                    };

                    foreach( var (name, flag) in attributes )
                    {
                        var tmp = val.HasFlag( flag );
                        if( PrintCheckBox( $"{name}##manip", ref tmp, defaults.HasFlag( flag ) ) && _editMode && tmp != val.HasFlag( flag ) )
                        {
                            list[ manipIdx ] = MetaManipulation.Eqp( id.Slot, id.SetId, tmp ? val | flag : val & ~flag );
                            ret              = true;
                        }
                    }

                    ImGui.EndPopup();
                }

                ImGui.Text( ObjectType.Equipment.ToString() );
                ImGui.TableNextColumn();
                ImGui.Text( id.SetId.ToString() );
                ImGui.TableNextColumn();
                ImGui.Text( id.Slot.ToString() );
                return ret;
            }

            private bool DrawGmpRow( int manipIdx, IList< MetaManipulation > list )
            {
                var defaults = ( GmpEntry )Service< MetaDefaults >.Get().GetDefaultValue( list[ manipIdx ] )!;
                var ret      = false;
                var id       = list[ manipIdx ].GmpIdentifier;
                var val      = list[ manipIdx ].GmpValue;

                if( ImGui.BeginPopup( $"##MetaPopup{manipIdx}" ) )
                {
                    var    enabled   = val.Enabled;
                    var    animated  = val.Animated;
                    var    rotationA = val.RotationA;
                    var    rotationB = val.RotationB;
                    var    rotationC = val.RotationC;
                    ushort unk       = val.UnknownTotal;

                    ret |= PrintCheckBox( "Visor Enabled##manip", ref enabled, defaults.Enabled ) && enabled != val.Enabled;
                    ret |= PrintCheckBox( "Visor Animated##manip", ref animated, defaults.Animated );
                    ret |= DrawInputWithDefault( "Rotation A##manip", ref rotationA, defaults.RotationA, 0x3FF );
                    ret |= DrawInputWithDefault( "Rotation B##manip", ref rotationB, defaults.RotationB, 0x3FF );
                    ret |= DrawInputWithDefault( "Rotation C##manip", ref rotationC, defaults.RotationC, 0x3FF );
                    ret |= DrawInputWithDefault( "Unknown Byte##manip", ref unk, defaults.UnknownTotal, 0xFF );

                    if( ret && _editMode )
                    {
                        list[ manipIdx ] = MetaManipulation.Gmp( id.SetId,
                            new GmpEntry
                            {
                                Animated  = animated, Enabled    = enabled, UnknownTotal = ( byte )unk,
                                RotationA = rotationA, RotationB = rotationB, RotationC  = rotationC,
                            } );
                    }

                    ImGui.EndPopup();
                }

                ImGui.Text( ObjectType.Equipment.ToString() );
                ImGui.TableNextColumn();
                ImGui.Text( id.SetId.ToString() );
                ImGui.TableNextColumn();
                ImGui.Text( EquipSlot.Head.ToString() );
                return ret;
            }

            private static (bool, bool) GetEqdpBits( EquipSlot slot, EqdpEntry entry )
            {
                return slot switch
                {
                    EquipSlot.Head   => ( entry.HasFlag( EqdpEntry.Head1 ), entry.HasFlag( EqdpEntry.Head2 ) ),
                    EquipSlot.Body   => ( entry.HasFlag( EqdpEntry.Body1 ), entry.HasFlag( EqdpEntry.Body2 ) ),
                    EquipSlot.Hands  => ( entry.HasFlag( EqdpEntry.Hands1 ), entry.HasFlag( EqdpEntry.Hands2 ) ),
                    EquipSlot.Legs   => ( entry.HasFlag( EqdpEntry.Legs1 ), entry.HasFlag( EqdpEntry.Legs2 ) ),
                    EquipSlot.Feet   => ( entry.HasFlag( EqdpEntry.Feet1 ), entry.HasFlag( EqdpEntry.Feet2 ) ),
                    EquipSlot.Neck   => ( entry.HasFlag( EqdpEntry.Neck1 ), entry.HasFlag( EqdpEntry.Neck2 ) ),
                    EquipSlot.Ears   => ( entry.HasFlag( EqdpEntry.Ears1 ), entry.HasFlag( EqdpEntry.Ears2 ) ),
                    EquipSlot.Wrists => ( entry.HasFlag( EqdpEntry.Wrists1 ), entry.HasFlag( EqdpEntry.Wrists2 ) ),
                    EquipSlot.RingR  => ( entry.HasFlag( EqdpEntry.RingR1 ), entry.HasFlag( EqdpEntry.RingR2 ) ),
                    EquipSlot.RingL  => ( entry.HasFlag( EqdpEntry.RingL1 ), entry.HasFlag( EqdpEntry.RingL2 ) ),
                    _                => ( false, false ),
                };
            }

            private static EqdpEntry SetEqdpBits( EquipSlot slot, EqdpEntry value, bool bit1, bool bit2 )
            {
                switch( slot )
                {
                    case EquipSlot.Head:
                        value = bit1 ? value | EqdpEntry.Head1 : value & ~EqdpEntry.Head1;
                        value = bit2 ? value | EqdpEntry.Head2 : value & ~EqdpEntry.Head2;
                        return value;
                    case EquipSlot.Body:
                        value = bit1 ? value | EqdpEntry.Body1 : value & ~EqdpEntry.Body1;
                        value = bit2 ? value | EqdpEntry.Body2 : value & ~EqdpEntry.Body2;
                        return value;
                    case EquipSlot.Hands:
                        value = bit1 ? value | EqdpEntry.Hands1 : value & ~EqdpEntry.Hands1;
                        value = bit2 ? value | EqdpEntry.Hands2 : value & ~EqdpEntry.Hands2;
                        return value;
                    case EquipSlot.Legs:
                        value = bit1 ? value | EqdpEntry.Legs1 : value & ~EqdpEntry.Legs1;
                        value = bit2 ? value | EqdpEntry.Legs2 : value & ~EqdpEntry.Legs2;
                        return value;
                    case EquipSlot.Feet:
                        value = bit1 ? value | EqdpEntry.Feet1 : value & ~EqdpEntry.Feet1;
                        value = bit2 ? value | EqdpEntry.Feet2 : value & ~EqdpEntry.Feet2;
                        return value;
                    case EquipSlot.Neck:
                        value = bit1 ? value | EqdpEntry.Neck1 : value & ~EqdpEntry.Neck1;
                        value = bit2 ? value | EqdpEntry.Neck2 : value & ~EqdpEntry.Neck2;
                        return value;
                    case EquipSlot.Ears:
                        value = bit1 ? value | EqdpEntry.Ears1 : value & ~EqdpEntry.Ears1;
                        value = bit2 ? value | EqdpEntry.Ears2 : value & ~EqdpEntry.Ears2;
                        return value;
                    case EquipSlot.Wrists:
                        value = bit1 ? value | EqdpEntry.Wrists1 : value & ~EqdpEntry.Wrists1;
                        value = bit2 ? value | EqdpEntry.Wrists2 : value & ~EqdpEntry.Wrists2;
                        return value;
                    case EquipSlot.RingR:
                        value = bit1 ? value | EqdpEntry.RingR1 : value & ~EqdpEntry.RingR1;
                        value = bit2 ? value | EqdpEntry.RingR2 : value & ~EqdpEntry.RingR2;
                        return value;
                    case EquipSlot.RingL:
                        value = bit1 ? value | EqdpEntry.RingL1 : value & ~EqdpEntry.RingL1;
                        value = bit2 ? value | EqdpEntry.RingL2 : value & ~EqdpEntry.RingL2;
                        return value;
                }

                return value;
            }

            private bool DrawEqdpRow( int manipIdx, IList< MetaManipulation > list )
            {
                var defaults = ( EqdpEntry )Service< MetaDefaults >.Get().GetDefaultValue( list[ manipIdx ] )!;
                var ret      = false;
                var id       = list[ manipIdx ].EqdpIdentifier;
                var val      = list[ manipIdx ].EqdpValue;

                if( ImGui.BeginPopup( $"##MetaPopup{manipIdx}" ) )
                {
                    var (bit1, bit2)       = GetEqdpBits( id.Slot, val );
                    var (defBit1, defBit2) = GetEqdpBits( id.Slot, defaults );

                    ret |= PrintCheckBox( "Bit 1##manip", ref bit1, defBit1 );
                    ret |= PrintCheckBox( "Bit 2##manip", ref bit2, defBit2 );

                    if( ret && _editMode )
                    {
                        list[ manipIdx ] = MetaManipulation.Eqdp( id.Slot, id.GenderRace, id.SetId, SetEqdpBits( id.Slot, val, bit1, bit2 ) );
                    }

                    ImGui.EndPopup();
                }

                ImGui.Text( id.Slot.IsAccessory()
                    ? ObjectType.Accessory.ToString()
                    : ObjectType.Equipment.ToString() );
                ImGui.TableNextColumn();
                ImGui.Text( id.SetId.ToString() );
                ImGui.TableNextColumn();
                ImGui.Text( id.Slot.ToString() );
                ImGui.TableNextColumn();
                var (gender, race) = id.GenderRace.Split();
                ImGui.Text( race.ToString() );
                ImGui.TableNextColumn();
                ImGui.Text( gender.ToString() );
                return ret;
            }

            private bool DrawEstRow( int manipIdx, IList< MetaManipulation > list )
            {
                var defaults = ( ushort )Service< MetaDefaults >.Get().GetDefaultValue( list[ manipIdx ] )!;
                var ret      = false;
                var id       = list[ manipIdx ].EstIdentifier;
                var val      = list[ manipIdx ].EstValue;
                if( ImGui.BeginPopup( $"##MetaPopup{manipIdx}" ) )
                {
                    if( DrawInputWithDefault( "No Idea what this does!##manip", ref val, defaults, ushort.MaxValue ) && _editMode )
                    {
                        list[ manipIdx ] = new MetaManipulation( id.Value, val );
                        ret              = true;
                    }

                    ImGui.EndPopup();
                }

                ImGui.Text( id.ObjectType.ToString() );
                ImGui.TableNextColumn();
                ImGui.Text( id.PrimaryId.ToString() );
                ImGui.TableNextColumn();
                ImGui.Text( id.ObjectType == ObjectType.Equipment
                    ? id.EquipSlot.ToString()
                    : id.BodySlot.ToString() );
                ImGui.TableNextColumn();
                var (gender, race) = id.GenderRace.Split();
                ImGui.Text( race.ToString() );
                ImGui.TableNextColumn();
                ImGui.Text( gender.ToString() );

                return ret;
            }

            private bool DrawImcRow( int manipIdx, IList< MetaManipulation > list )
            {
                var defaults = ( ImcFile.ImageChangeData )Service< MetaDefaults >.Get().GetDefaultValue( list[ manipIdx ] )!;
                var ret      = false;
                var id       = list[ manipIdx ].ImcIdentifier;
                var val      = list[ manipIdx ].ImcValue;

                if( ImGui.BeginPopup( $"##MetaPopup{manipIdx}" ) )
                {
                    ushort materialId          = val.MaterialId;
                    ushort vfxId               = val.VfxId;
                    ushort decalId             = val.DecalId;
                    var    soundId             = ( ushort )( val.SoundId >> 10 );
                    var    attributeMask       = val.AttributeMask;
                    var    materialAnimationId = ( ushort )( val.MaterialAnimationId >> 12 );
                    ret |= DrawInputWithDefault( "Material Id", ref materialId, defaults.MaterialId, byte.MaxValue );
                    ret |= DrawInputWithDefault( "Vfx Id", ref vfxId, defaults.VfxId, byte.MaxValue );
                    ret |= DrawInputWithDefault( "Decal Id", ref decalId, defaults.DecalId, byte.MaxValue );
                    ret |= DrawInputWithDefault( "Sound Id", ref soundId, defaults.SoundId, 0x3F );
                    ret |= DrawInputWithDefault( "Attribute Mask", ref attributeMask, defaults.AttributeMask, 0x3FF );
                    ret |= DrawInputWithDefault( "Material Animation Id", ref materialAnimationId, defaults.MaterialAnimationId,
                        byte.MaxValue );

                    if( ret && _editMode )
                    {
                        var value = ImcExtensions.FromValues( ( byte )materialId, ( byte )decalId, attributeMask, ( byte )soundId,
                            ( byte )vfxId, ( byte )materialAnimationId );
                        list[ manipIdx ] = new MetaManipulation( id.Value, value.ToInteger() );
                    }

                    ImGui.EndPopup();
                }

                ImGui.Text( id.ObjectType.ToString() );
                ImGui.TableNextColumn();
                ImGui.Text( id.PrimaryId.ToString() );
                ImGui.TableNextColumn();
                if( id.ObjectType == ObjectType.Accessory
                 || id.ObjectType == ObjectType.Equipment )
                {
                    ImGui.Text( id.ObjectType == ObjectType.Equipment
                     || id.ObjectType         == ObjectType.Accessory
                            ? id.EquipSlot.ToString()
                            : id.BodySlot.ToString() );
                }

                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                if( id.ObjectType != ObjectType.Equipment
                 && id.ObjectType != ObjectType.Accessory )
                {
                    ImGui.Text( id.SecondaryId.ToString() );
                }

                ImGui.TableNextColumn();
                ImGui.Text( id.Variant.ToString() );
                return ret;
            }

            private bool DrawRspRow( int manipIdx, IList< MetaManipulation > list )
            {
                var defaults = ( float )Service< MetaDefaults >.Get().GetDefaultValue( list[ manipIdx ] )!;
                var ret      = false;
                var id       = list[ manipIdx ].RspIdentifier;
                var val      = list[ manipIdx ].RspValue;

                if( ImGui.BeginPopup( $"##MetaPopup{manipIdx}" ) )
                {
                    var color = defaults < val ? ColorDarkGreen :
                        defaults > val ? ColorDarkRed : ImGui.ColorConvertFloat4ToU32( ImGui.GetStyle().Colors[ ( int )ImGuiCol.Button ] );

                    if( DefaultButton(
                            $"{( _editMode ? "Set to " : "" )}Default: {defaults:F3}##scaleManip", ref val, defaults )
                     && _editMode )
                    {
                        list[ manipIdx ] = MetaManipulation.Rsp( id.SubRace, id.Attribute, defaults );
                        ret              = true;
                    }

                    ImGui.SetNextItemWidth( 50 );
                    if( ImGui.InputFloat( "Scale###manip", ref val, 0, 0, "%.3f",
                            _editMode ? ImGuiInputTextFlags.EnterReturnsTrue : ImGuiInputTextFlags.ReadOnly )
                     && val >= 0
                     && val <= 5
                     && _editMode )
                    {
                        list[ manipIdx ] = MetaManipulation.Rsp( id.SubRace, id.Attribute, val );
                        ret              = true;
                    }

                    ImGui.EndPopup();
                }

                ImGui.Text( id.Attribute.ToUngenderedString() );
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                ImGui.Text( id.SubRace.ToString() );
                ImGui.TableNextColumn();
                ImGui.Text( id.Attribute.ToGender().ToString() );
                return ret;
            }

            private bool DrawManipulationRow( ref int manipIdx, IList< MetaManipulation > list )
            {
                var type = list[ manipIdx ].Type;

                if( _editMode )
                {
                    ImGui.TableNextColumn();
                    ImGui.PushFont( UiBuilder.IconFont );
                    if( ImGui.Button( $"{FontAwesomeIcon.Trash.ToIconString()}##manipDelete{manipIdx}" ) )
                    {
                        list.RemoveAt( manipIdx );
                        ImGui.PopFont();
                        ImGui.TableNextRow();
                        --manipIdx;
                        return true;
                    }

                    ImGui.PopFont();
                }

                ImGui.TableNextColumn();
                ImGui.Text( type.ToString() );
                ImGui.TableNextColumn();

                var changes = false;
                switch( type )
                {
                    case MetaType.Eqp:
                        changes = DrawEqpRow( manipIdx, list );
                        break;
                    case MetaType.Gmp:
                        changes = DrawGmpRow( manipIdx, list );
                        break;
                    case MetaType.Eqdp:
                        changes = DrawEqdpRow( manipIdx, list );
                        break;
                    case MetaType.Est:
                        changes = DrawEstRow( manipIdx, list );
                        break;
                    case MetaType.Imc:
                        changes = DrawImcRow( manipIdx, list );
                        break;
                    case MetaType.Rsp:
                        changes = DrawRspRow( manipIdx, list );
                        break;
                }

                ImGui.TableSetColumnIndex( 9 );
                if( ImGui.Selectable( $"{list[ manipIdx ].Value}##{manipIdx}" ) )
                {
                    ImGui.OpenPopup( $"##MetaPopup{manipIdx}" );
                }

                ImGui.TableNextRow();
                return changes;
            }


            private MetaType DrawNewTypeSelection()
            {
                ImGui.RadioButton( "IMC##newManipType", ref _newManipTypeIdx, 1 );
                ImGui.SameLine();
                ImGui.RadioButton( "EQDP##newManipType", ref _newManipTypeIdx, 2 );
                ImGui.SameLine();
                ImGui.RadioButton( "EQP##newManipType", ref _newManipTypeIdx, 3 );
                ImGui.SameLine();
                ImGui.RadioButton( "EST##newManipType", ref _newManipTypeIdx, 4 );
                ImGui.SameLine();
                ImGui.RadioButton( "GMP##newManipType", ref _newManipTypeIdx, 5 );
                ImGui.SameLine();
                ImGui.RadioButton( "RSP##newManipType", ref _newManipTypeIdx, 6 );
                return ( MetaType )_newManipTypeIdx;
            }

            private bool DrawNewManipulationPopup( string popupName, IList< MetaManipulation > list )
            {
                var change = false;
                if( ImGui.BeginPopup( popupName ) )
                {
                    var               manipType = DrawNewTypeSelection();
                    MetaManipulation? newManip  = null;
                    switch( manipType )
                    {
                        case MetaType.Imc:
                        {
                            RestrictedInputInt( "Set Id##newManipImc", ref _newManipSetId, 0, ushort.MaxValue );
                            RestrictedInputInt( "Variant##newManipImc", ref _newManipVariant, 0, byte.MaxValue );
                            CustomCombo( "Object Type", ImcObjectType, out var objectType, ref _newManipObjectType );
                            EquipSlot equipSlot = default;
                            switch( objectType )
                            {
                                case ObjectType.Equipment:
                                    CustomCombo( "Equipment Slot", EqdpEquipSlots, out equipSlot, ref _newManipEquipSlot );
                                    newManip = MetaManipulation.Imc( equipSlot, _newManipSetId, _newManipVariant,
                                        new ImcFile.ImageChangeData() );
                                    break;
                                case ObjectType.DemiHuman:
                                case ObjectType.Weapon:
                                case ObjectType.Monster:
                                    RestrictedInputInt( "Secondary Id##newManipImc", ref _newManipSecondaryId, 0, ushort.MaxValue );
                                    CustomCombo( "Body Slot", ImcBodySlots, out var bodySlot, ref _newManipBodySlot );
                                    newManip = MetaManipulation.Imc( objectType, bodySlot, _newManipSetId, _newManipSecondaryId,
                                        _newManipVariant, new ImcFile.ImageChangeData() );
                                    break;
                            }

                            break;
                        }
                        case MetaType.Eqdp:
                        {
                            RestrictedInputInt( "Set Id##newManipEqdp", ref _newManipSetId, 0, ushort.MaxValue );
                            CustomCombo( "Equipment Slot", EqdpEquipSlots, out var equipSlot, ref _newManipEquipSlot );
                            CustomCombo( "Race", Races, out var race, ref _newManipRace );
                            CustomCombo( "Gender", Genders, out var gender, ref _newManipGender );
                            newManip = MetaManipulation.Eqdp( equipSlot, GameData.Enums.GameData.CombinedRace( gender, race ), ( ushort )_newManipSetId,
                                new EqdpEntry() );
                            break;
                        }
                        case MetaType.Eqp:
                        {
                            RestrictedInputInt( "Set Id##newManipEqp", ref _newManipSetId, 0, ushort.MaxValue );
                            CustomCombo( "Equipment Slot", EqpEquipSlots, out var equipSlot, ref _newManipEquipSlot );
                            newManip = MetaManipulation.Eqp( equipSlot, ( ushort )_newManipSetId, 0 );
                            break;
                        }
                        case MetaType.Est:
                        {
                            RestrictedInputInt( "Set Id##newManipEst", ref _newManipSetId, 0, ushort.MaxValue );
                            CustomCombo( "Object Type", ObjectTypes, out var objectType, ref _newManipObjectType );
                            EquipSlot equipSlot = default;
                            BodySlot  bodySlot  = default;
                            switch( ( ObjectType )_newManipObjectType )
                            {
                                case ObjectType.Equipment:
                                    CustomCombo( "Equipment Slot", EstEquipSlots, out equipSlot, ref _newManipEquipSlot );
                                    break;
                                case ObjectType.Character:
                                    CustomCombo( "Body Slot", EstBodySlots, out bodySlot, ref _newManipBodySlot );
                                    break;
                            }

                            CustomCombo( "Race", Races, out var race, ref _newManipRace );
                            CustomCombo( "Gender", Genders, out var gender, ref _newManipGender );
                            newManip = MetaManipulation.Est( objectType, equipSlot, GameData.Enums.GameData.CombinedRace( gender, race ), bodySlot,
                                ( ushort )_newManipSetId, 0 );
                            break;
                        }
                        case MetaType.Gmp:
                            RestrictedInputInt( "Set Id##newManipGmp", ref _newManipSetId, 0, ushort.MaxValue );
                            newManip = MetaManipulation.Gmp( ( ushort )_newManipSetId, new GmpEntry() );
                            break;
                        case MetaType.Rsp:
                            CustomCombo( "Subrace", Subraces, out var subRace, ref _newManipSubrace );
                            CustomCombo( "Attribute", RspAttributes, out var rspAttribute, ref _newManipAttribute );
                            newManip = MetaManipulation.Rsp( subRace, rspAttribute, 1f );
                            break;
                    }

                    if( ImGui.Button( "Create Manipulation##newManip", Vector2.UnitX * -1 )
                     && newManip != null
                     && list.All( m => m.Identifier != newManip.Value.Identifier ) )
                    {
                        var def = Service< MetaDefaults >.Get().GetDefaultValue( newManip.Value );
                        if( def != null )
                        {
                            var manip = newManip.Value.Type switch
                            {
                                MetaType.Est  => new MetaManipulation( newManip.Value.Identifier, ( ulong )def ),
                                MetaType.Eqp  => new MetaManipulation( newManip.Value.Identifier, ( ulong )def ),
                                MetaType.Eqdp => new MetaManipulation( newManip.Value.Identifier, ( ulong )def ),
                                MetaType.Gmp  => new MetaManipulation( newManip.Value.Identifier, ( ulong )def ),
                                MetaType.Imc => new MetaManipulation( newManip.Value.Identifier,
                                    ( ( ImcFile.ImageChangeData )def ).ToInteger() ),
                                MetaType.Rsp => MetaManipulation.Rsp( newManip.Value.RspIdentifier.SubRace,
                                    newManip.Value.RspIdentifier.Attribute, ( float )def ),
                                _ => throw new InvalidEnumArgumentException(),
                            };
                            list.Add( manip );
                            change = true;
                        }
                    }

                    ImGui.EndPopup();
                }

                return change;
            }

            private bool DrawMetaManipulationsTable( string label, IList< MetaManipulation > list )
            {
                var numRows = _editMode ? 11 : 10;
                var changes = false;
                if( list.Count > 0
                 && ImGui.BeginTable( label, numRows,
                        ImGuiTableFlags.BordersInner | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit ) )
                {
                    if( _editMode )
                    {
                        ImGui.TableNextColumn();
                    }

                    ImGui.TableNextColumn();
                    ImGui.TableHeader( $"Type##{label}" );
                    ImGui.TableNextColumn();
                    ImGui.TableHeader( $"Object Type##{label}" );
                    ImGui.TableNextColumn();
                    ImGui.TableHeader( $"Set##{label}" );
                    ImGui.TableNextColumn();
                    ImGui.TableHeader( $"Slot##{label}" );
                    ImGui.TableNextColumn();
                    ImGui.TableHeader( $"Race##{label}" );
                    ImGui.TableNextColumn();
                    ImGui.TableHeader( $"Gender##{label}" );
                    ImGui.TableNextColumn();
                    ImGui.TableHeader( $"Secondary ID##{label}" );
                    ImGui.TableNextColumn();
                    ImGui.TableHeader( $"Variant##{label}" );
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.TableHeader( $"Value##{label}" );
                    ImGui.TableNextRow();

                    for( var i = 0; i < list.Count; ++i )
                    {
                        changes |= DrawManipulationRow( ref i, list );
                    }

                    ImGui.EndTable();
                }

                var popupName = $"##newManip{label}";
                if( _editMode )
                {
                    changes |= DrawNewManipulationPopup( $"##newManip{label}", list );
                    if( ImGui.Button( $"Add New Manipulation##{label}", Vector2.UnitX * -1 ) )
                    {
                        ImGui.OpenPopup( popupName );
                    }

                    return changes;
                }

                return false;
            }
        }
    }
}
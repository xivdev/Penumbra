using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.System.Framework;

namespace Penumbra;

[StructLayout( LayoutKind.Explicit )]
public unsafe struct AgentBannerInterface
{
    [FieldOffset( 0x0 )]  public AgentInterface          AgentInterface;
    [FieldOffset( 0x28 )] public BannerInterfaceStorage* Data;

    public BannerInterfaceStorage.CharacterData* Character( int idx )
        => idx switch
        {
            _ when Data == null => null,
            0                   => &Data->Character1,
            1                   => &Data->Character2,
            2                   => &Data->Character3,
            3                   => &Data->Character4,
            4                   => &Data->Character5,
            5                   => &Data->Character6,
            6                   => &Data->Character7,
            7                   => &Data->Character8,
            _                   => null,
        };
}

[StructLayout(LayoutKind.Explicit)]
public unsafe struct AgentBannerParty
{
    public static AgentBannerParty* Instance() => ( AgentBannerParty* )Framework.Instance()->GetUiModule()->GetAgentModule()->GetAgentByInternalId( AgentId.BannerParty );

    [FieldOffset( 0x0 )] public AgentBannerInterface AgentBannerInterface;
}

[StructLayout( LayoutKind.Explicit )]
public unsafe struct AgentBannerMIP
{
    public static AgentBannerMIP* Instance() => ( AgentBannerMIP* )Framework.Instance()->GetUiModule()->GetAgentModule()->GetAgentByInternalId( AgentId.BannerMIP );
    [FieldOffset( 0x0 )] public AgentBannerInterface AgentBannerInterface;
}

// Client::UI::Agent::AgentBannerInterface::Storage
// destructed in Client::UI::Agent::AgentBannerInterface::dtor
[StructLayout( LayoutKind.Explicit, Size = 0x3B30 )]
public unsafe struct BannerInterfaceStorage
{
    // vtable: 48 8D 05 ?? ?? ?? ?? 48 89 01 48 8B F9 7E 
    // dtor: E8 ?? ?? ?? ?? 48 83 EF ?? 75 ?? BA ?? ?? ?? ?? 48 8B CE E8 ?? ?? ?? ?? 48 89 7D
    [StructLayout( LayoutKind.Explicit, Size = 0x760 )]
    public struct CharacterData
    {
        [FieldOffset( 0x000 )] public void** VTable;

        [FieldOffset( 0x018 )] public Utf8String Name1;
        [FieldOffset( 0x080 )] public Utf8String Name2;
        [FieldOffset( 0x0E8 )] public Utf8String UnkString1;
        [FieldOffset( 0x150 )] public Utf8String UnkString2;
        [FieldOffset( 0x1C0 )] public Utf8String Job;
        [FieldOffset( 0x238 )] public uint WorldId;
        [FieldOffset( 0x240 )] public Utf8String UnkString3;

        [FieldOffset( 0x2B0 )] public void*      CharaView;
        [FieldOffset( 0x5D0 )] public AtkTexture AtkTexture;

        [FieldOffset( 0x6E0 )] public Utf8String Title;
        [FieldOffset( 0x750 )] public void* SomePointer;

    }

    [FieldOffset( 0x0000 )] public void* Agent; // AgentBannerParty, maybe other Banner agents
    [FieldOffset( 0x0008 )] public UIModule* UiModule;
    [FieldOffset( 0x0010 )] public uint Unk1; // Maybe count or bitfield, but probably not
    [FieldOffset( 0x0014 )] public uint Unk2;

    [FieldOffset( 0x0020 )] public CharacterData Character1;
    [FieldOffset( 0x0780 )] public CharacterData Character2;
    [FieldOffset( 0x0EE0 )] public CharacterData Character3;
    [FieldOffset( 0x1640 )] public CharacterData Character4;
    [FieldOffset( 0x1DA0 )] public CharacterData Character5;
    [FieldOffset( 0x2500 )] public CharacterData Character6;
    [FieldOffset( 0x2C60 )] public CharacterData Character7;
    [FieldOffset( 0x33C0 )] public CharacterData Character8;

    [FieldOffset( 0x3B20 )] public long Unk3;
    [FieldOffset( 0x3B28 )] public long Unk4;
}
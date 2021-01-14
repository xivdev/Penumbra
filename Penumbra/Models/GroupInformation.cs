using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Linq;

namespace Penumbra.Models
{
    [Serializable]
    public class GroupInformation : ISerializable
    {

        // This class is just used as a temp class while (de)-serializing.
        // It converts the flags into lists and back.
        [Serializable]
        private class GroupDescription : ISerializable
        {
            public GroupDescription(GroupInformation info, (string, uint, uint, ulong) vars)
            {
                GamePath = vars.Item1;

                static List<string> AddGroupTypes(ulong flags, ulong bound, List<string> groupType)
                {
                    List<string> ret = null;
                    if (flags != uint.MaxValue)
                    {
                        ret = new();
                        for (var i = 0; i < groupType.Count; ++i)
                        {
                            var flag = 1u << i;
                            if ((flags & flag) == flag)
                                ret.Add(groupType[i]);
                        }
                    }
                    return ret;
                }

                // Tops and Bottoms are uint.
                TopTypes    = AddGroupTypes(vars.Item2, uint.MaxValue, info.TopTypes);
                BottomTypes = AddGroupTypes(vars.Item3, uint.MaxValue, info.BottomTypes);
                // Exclusions are the other way around and ulong.
                GroupExclusions = AddGroupTypes(~vars.Item4, 0, info.OtherGroups);
            }

            public (string, uint, uint, ulong) ToTuple(GroupInformation info)
            {
                static ulong TypesToFlags(List<string> ownTypes, List<string> globalTypes)
                {
                    if (ownTypes == null)
                        return ulong.MaxValue;

                    ulong flags = 0;
                    foreach (var x in ownTypes)
                    {
                        var index = globalTypes.IndexOf(x);
                        if (index >= 0)
                            flags |= (1u << index);
                    }
                    return flags;
                }
                var tops    = (uint) TypesToFlags(TopTypes,    info.TopTypes);
                var bottoms = (uint) TypesToFlags(BottomTypes, info.BottomTypes);
                // Exclusions are the other way around.
                var groupEx = (GroupExclusions == null) ? ulong.MaxValue : ~TypesToFlags(GroupExclusions, info.OtherGroups);
                return (GamePath, tops, bottoms, groupEx);
            }

            public string GamePath { get; set; }
            public List<string> TopTypes  { get; set; } = null;
            public List<string> BottomTypes { get; set; } = null;
            public List<string> GroupExclusions { get; set; } = null;

            // Customize (De)-Serialization to ignore nulls.
            public GroupDescription(SerializationInfo info, StreamingContext context)
            {
                List<string> readListOrNull(string name)
                {
                    try
                    {
                        var ret = (List<string>) info.GetValue(name, typeof(List<string>));
                        if (ret == null || ret.Count == 0)
                            return null;
                        return ret;
                    }
                    catch (Exception) { return null; }
                }
                GamePath        = info.GetString("GamePath");
                TopTypes        = readListOrNull("TopTypes");
                BottomTypes     = readListOrNull("BottomTypes");
                GroupExclusions = readListOrNull("GroupExclusions");
            }

            public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
            {
                info.AddValue( "GamePath", GamePath );
                if (TopTypes        != null) info.AddValue("TopTypes", TopTypes);
                if (BottomTypes     != null) info.AddValue("BottomTypes", BottomTypes);
                if (GroupExclusions != null) info.AddValue("GroupExclusions", GroupExclusions);
            }
        }

        public List<string>    TopTypes { get; set; } = new();
        public List<string> BottomTypes { get; set; } = new();
        public List<string> OtherGroups { get; set; } = new();

        public void AddFileToOtherGroups(string optionName, string fileName, string gamePath)
        {
            var idx = OtherGroups.IndexOf(optionName);
            if (idx < 0)
            {
                idx = OtherGroups.Count;
                OtherGroups.Add(optionName);
            }

            (string, uint, uint, ulong) tuple = (gamePath, uint.MaxValue, uint.MaxValue, (1ul << idx));

            if (!FileToGameAndGroup.TryGetValue(fileName, out var tuple2))
            {
                FileToGameAndGroup.Add(fileName, tuple);
            }
            else
            {
                tuple2.Item1  = tuple.Item1;
                tuple2.Item4 |= tuple.Item4;
            }
        }

        public Dictionary<string, (string, uint, uint, ulong)> FileToGameAndGroup { get; set; } = new();

        public GroupInformation(){ }

        public GroupInformation(SerializationInfo info, StreamingContext context)
        {
            try { TopTypes    = (List<string>) info.GetValue( "TopTypes",       TopTypes.GetType() ); } catch(Exception){ }
            try { BottomTypes = (List<string>) info.GetValue( "BottomTypes", BottomTypes.GetType() ); } catch(Exception){ }
            try { OtherGroups = (List<string>) info.GetValue( "Groups",      OtherGroups.GetType() ); } catch(Exception){ }
            try
            {
                Dictionary<string, GroupDescription> dict = new();
                dict = (Dictionary<string, GroupDescription>) info.GetValue( "FileToGameAndGroups", dict.GetType());
                foreach (var pair in dict)
                    FileToGameAndGroup.Add(pair.Key, pair.Value.ToTuple(this));
            } catch (Exception){  }
        }

        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if ((TopTypes?.Count    ?? 0) > 0) info.AddValue("TopTypes",       TopTypes);
            if ((BottomTypes?.Count ?? 0) > 0) info.AddValue("BottomTypes", BottomTypes);
            if ((OtherGroups?.Count ?? 0) > 0) info.AddValue("Groups",      OtherGroups);
            if ((FileToGameAndGroup?.Count ?? 0) > 0)
            {
                var dict = FileToGameAndGroup.ToDictionary( pair => pair.Key, pair => new GroupDescription( this, pair.Value ) );
                info.AddValue("FileToGameAndGroups", dict);
            }
        }
    }
}
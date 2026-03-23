using Dalamud.Plugin.Ipc;
using Penumbra.Api.IpcSubscribers;
using Penumbra.Mods;

namespace Penumbra.Api;

public sealed class ModAdapter(Mod mod) : BasicAdapter<Mod>(mod)
{
    public bool TryInvoke<TRet>(int method, out TRet? ret) where TRet : allows ref struct
    {
        ret = method switch
        {
            (int)ModWrapper.Method.GetModPath     => ret = CheckRet<TRet, DirectoryInfo>(method, 0, Manager.ModPath),
            (int)ModWrapper.Method.GetIndex       => ret = CheckRet<TRet, int>(method, 0, Manager.Index),
            (int)ModWrapper.Method.GetName        => ret = CheckRet<TRet, string>(method, 0, Manager.Name),
            (int)ModWrapper.Method.GetIdentifier  => ret = CheckRet<TRet, string>(method, 0, Manager.Identifier),
            (int)ModWrapper.Method.GetAuthor      => ret = CheckRet<TRet, string>(method, 0, Manager.Author),
            (int)ModWrapper.Method.GetDescription => ret = CheckRet<TRet, string>(method, 0, Manager.Description),
            (int)ModWrapper.Method.GetVersion     => ret = CheckRet<TRet, string>(method, 0, Manager.Version),
            (int)ModWrapper.Method.GetWebsite     => ret = CheckRet<TRet, string>(method, 0, Manager.Website),
            (int)ModWrapper.Method.GetImage       => ret = CheckRet<TRet, string>(method, 0, Manager.Website),
            (int)ModWrapper.Method.GetSortName    => ret = CheckRet<TRet, string>(method, 0, Manager.Path.SortName),
            (int)ModWrapper.Method.GetFolder      => ret = CheckRet<TRet, string>(method, 0, Manager.Path.Folder),
            (int)ModWrapper.Method.GetFullPath    => ret = CheckRet<TRet, string>(method, 0, Manager.Path.CurrentPath),
            (int)ModWrapper.Method.GetImportDate => ret =
                CheckRet<TRet, DateTimeOffset>(method, 0, DateTimeOffset.FromUnixTimeMilliseconds(Manager.ImportDate)),
            (int)ModWrapper.Method.GetLastConfigEdit => ret =
                CheckRet<TRet, DateTimeOffset>(method, 0, DateTimeOffset.FromUnixTimeMilliseconds(Manager.LastConfigEdit)),
            (int)ModWrapper.Method.GetFavorite         => ret = CheckRet<TRet, bool>(method, 0, Manager.Favorite),
            (int)ModWrapper.Method.GetModTags          => ret = CheckRet<TRet, IReadOnlyList<string>>(method, 0, Manager.ModTags),
            (int)ModWrapper.Method.GetLocalTags        => ret = CheckRet<TRet, IReadOnlyList<string>>(method, 0, Manager.LocalTags),
            (int)ModWrapper.Method.GetRequiredFeatures => ret = CheckRet<TRet, long>(method, 0, (long)Manager.RequiredFeatures),
            (int)ModWrapper.Method.GetGroupCount       => ret = CheckRet<TRet, int>(method, 0, Manager.Groups.Count),
            _                                          => throw new AdapterMethodMissingException(method, 0, true),
        };
        return true;
    }

    public bool TryInvoke<T1, TRet>(int method, T1 arg1, out TRet? output)
        where T1 : allows ref struct
        where TRet : allows ref struct
    {
        switch (method)
        {
            case (int)ModWrapper.Method.GetGroup:
            {
                var index = CheckValue<T1, int>(method, 1, true, 0, ref arg1);
                CheckRet<TRet, IIdDataShareAdapter>(method, 1);

                if (index < 0 || index >= Manager.Groups.Count || new GroupAdapter(mod.Groups[index]) is not TRet r)
                {
                    output = default;
                    return true;
                }

                output = r;
                return true;
            }
        }

        throw new AdapterMethodMissingException(method, 1, true);
    }
}

﻿using MyProject.Domains;

namespace MyProject.Services.Interfaces
{
    public interface IPlayerSkinService
    {
        void Update(ulong steamId, string skinName);

        void Reset(ulong steamId);

        string GetActiveSkinName(ulong steamId);
        void SaveToDBFromCache(IEnumerable<PlayerSkin> skinsFromCache);
    }
}

﻿using UnoGame.GameComponents;

namespace UnoGame
{
    public interface IGameService
    {
        Task<ResponseInfo> JoinPlayerAsync(string groupId, Player player);
        Task<ResponseInfo> NewGameAsync(string groupId, Player host);
        Task<bool> SaveGameGroupsAsync(Dictionary<string, GameGroup> gameGroups);
    }
}
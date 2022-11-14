using UnoGame.GameComponents;
using UnoGame.Telegram.Models;

namespace UnoGame
{
    public interface IGameService
    {
        string GroupIdMapper { get; }
        string GameGroupsKey { get; }
        Task<ResponseInfo> JoinPlayerAsync(string groupId, Player player);
        Task<ResponseInfo> JoinBotPlayerAsync(string groupId, int number);
        Task<ResponseInfo> NewGameAsync(string groupId, Player host, List<Card> cards);
        //Task<ResponseInfo> GetPlayersAsync(string groupdId);
        Task<string> GetGroupIdAsync(string userId);
        Task<GameGroup?> GetGameGroupAsync(string groupId);
        Task<GameGroup?> GetGameGrouopByUserAsync(string userId);
        Task<ResponseInfo> StartGameAsync(string groupId, Player player);
        Task<ResponseInfo> EndGameAsync(string groupId, Player player);
        Task<ResponseInfo> ShowGameStateAsync(string groupId);
        Task<Card?> GetPlayerCardAsync(string fileId, string playerId);
        Task<Player?> GetPlayerAsync(string playerId);
        Task<ResponseInfo> HandlePlayerAction(string playerId,ImageFileInfo imgFile, CardColor? color = null, bool isPass = false);
        Task HandleBotActionAsync(GameGroup gameGroup, ResponseInfo res);
        //Task<bool> SaveGameGroupsAsync(Dictionary<string, GameGroup> gameGroups);
    }
}
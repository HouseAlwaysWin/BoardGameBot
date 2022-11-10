using UnoGame.GameComponents;

namespace UnoGame
{
    public interface IGameService
    {
        Task<ResponseInfo> JoinPlayerAsync(string groupId, Player player);
        Task<ResponseInfo> JoinBotPlayerAsync(string groupId, int number);
        Task<ResponseInfo> NewGameAsync(string groupId, Player host, List<Card> cards);
        Task<ResponseInfo> GetPlayersAsync(string groupdId);
        Task<string> GetGroupIdAsync(string userId);
        Task<GameGroup?> GetGameGroupAsync(string groupId);
        Task<GameGroup?> GetGameGrouopByUserAsync(string userId);
        Task<ResponseInfo> StartGameAsync(string groupId, Player player);
        Task<ResponseInfo> EndGameAsync(string groupId, Player player);

        Task<ResponseInfo> ShowGameStateAsync(string groupId);
        //Task<bool> SaveGameGroupsAsync(Dictionary<string, GameGroup> gameGroups);
    }
}
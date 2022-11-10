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
        Task<GameGroup?> GetGameGrouopAsync(string groupId);
        Task<GameGroup?> GetGameGrouopByUserAsync(string userId);
        Task<ResponseInfo> StartGame(string groupId, Player player);
        //Task<bool> SaveGameGroupsAsync(Dictionary<string, GameGroup> gameGroups);
    }
}
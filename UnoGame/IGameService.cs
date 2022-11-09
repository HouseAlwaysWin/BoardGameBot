using UnoGame.GameComponents;

namespace UnoGame
{
    public interface IGameService
    {
        Task<ResponseInfo> JoinPlayerAsync(string groupId, Player player);
        Task<ResponseInfo> NewGameAsync(string groupId, Player host, List<Card> cards);
        Task<ResponseInfo> GetPlayersAsync(string groupdId);
        Task<string> GetGroupId(string userId);
        //Task<bool> SaveGameGroupsAsync(Dictionary<string, GameGroup> gameGroups);
    }
}
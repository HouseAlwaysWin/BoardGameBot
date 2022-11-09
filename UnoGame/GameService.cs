using CommonGameLib.Extensions;
using CommonGameLib.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using UnoGame.Extensions;
using UnoGame.GameComponents;
using UnoGame.Telegram;

namespace UnoGame
{
    public class GameService : IGameService
    {
        public Dictionary<string, GameGroup> GameGroups;
        private readonly ICachedService _cachedService;
        private readonly ILogger<GameService> _logger;
        private Dictionary<string, string> _gameGroupIdMapper;
        public string GroupIdMapper = "GroupIdMapper";
        public string GameGroupsKey = "GameGroups";

        private string ImgSourceRootPath = @"Source/Images";
        public GameService(ICachedService cachedService, ILogger<GameService> logger)
        {
            _cachedService = cachedService;
            _logger = logger;
            GameGroups = _cachedService.GetAndSetAsync(GameGroupsKey, new Dictionary<string, GameGroup>()).Result;
        }

        public async Task<string> GetGroupId(string userId)
        {
            var groupIdMapper = await _cachedService.GetAsync<Dictionary<string, string>>(GroupIdMapper);
            if (groupIdMapper != null)
            {
                return groupIdMapper[userId];
            }
            return string.Empty;
        }

        public async Task<ResponseInfo> NewGameAsync(string groupId, Player host, List<Card> baseCards)
        {
            return await Task.Run(async () =>
            {
                var response = new ResponseInfo();
                if (string.IsNullOrEmpty(groupId))
                {
                    response.Message = "請先把機器人加入到群組";
                    return response;
                }

                List<Card> totalCards = new List<Card>();

                foreach (var card in baseCards)
                {
                    if (card.CardType == CardType.Number && card.Number == 0)
                    {
                        totalCards.Add(card);
                    }
                    else if (card.CardType == CardType.Number ||
                            card.CardType == CardType.Reverse ||
                            card.CardType == CardType.Skip ||
                            card.CardType == CardType.DrawTwo)
                    {
                        totalCards.Add(card);
                        totalCards.Add(card);
                    }
                    else
                    {
                        totalCards.Add(card);
                        totalCards.Add(card);
                        totalCards.Add(card);
                        totalCards.Add(card);
                    }
                }

                GameGroup newGameGroup = new GameGroup()
                {
                    GroupId = groupId,
                    Cards = totalCards,
                    Discards = new List<Card>(),
                    Players = new List<Player>(),
                    Host = host
                };

                if (!GameGroups.ContainsKey(groupId))
                {
                    GameGroups.Add(groupId, newGameGroup);
                    var currentGroup = GameGroups[groupId];
                    currentGroup.Players.Add(host);

                    _gameGroupIdMapper = new Dictionary<string, string>();
                    _gameGroupIdMapper.Add(host.Id, groupId);
                    var groupIdMapper = await _cachedService.GetAndSetAsync(GroupIdMapper, _gameGroupIdMapper);
                    if (groupIdMapper.ContainsKey(host.Id))
                    {
                        groupIdMapper[host.Id] = groupId;
                    }
                    await _cachedService.SetAsync(GroupIdMapper, _gameGroupIdMapper);

                    var saveToCached = await SaveGameGroupsAsync(GameGroups);
                    if (!saveToCached)
                    {
                        response.Message = @$"儲存GameGroups發生錯誤";
                        return response;
                    }
                }
                else
                {
                    response.Message = @$"已開局，開局者：@{host.Username}";
                    return response;
                }
                response.Message = @$"開始新遊戲，開局者：@{host.Username}";
                return response;
            });
        }

        public async Task<ResponseInfo> JoinPlayerAsync(string groupId, Player player)
        {
            return await Task.Run(async () =>
            {
                var response = new ResponseInfo();
                if (!GameGroups.ContainsKey(groupId))
                {
                    response.Message = "遊戲尚未開局，請先執行開局指令 /new";
                    return response;
                }

                var currentGroup = GameGroups[groupId];
                if (currentGroup.Players.Any(p => p.Id == player.Id))
                {
                    response.Message = @$"玩家@{player.Username}已經加入遊戲了";
                    return response;
                }

                currentGroup.Players.Add(player);

                var saveToCached = await SaveGameGroupsAsync(GameGroups);
                if (!saveToCached)
                {
                    response.Message = @$"儲存GameGroups發生錯誤";
                    return response;
                }

                StringBuilder msgBuilder = new StringBuilder();
                response.Message = @$"玩家@{player.Username}加入遊戲";
                return response;
            });
        }

        public async Task<ResponseInfo> GetPlayersAsync(string groupdId)
        {
            return await Task.Run(() =>
            {
                var response = new ResponseInfo();
                if (GameGroups.ContainsKey(groupdId))
                {
                    var players = GameGroups[groupdId].Players;
                    StringBuilder msgBuilder = new StringBuilder();
                    msgBuilder.AppendLine("目前遊戲玩家人數:");
                    for (int i = 0; i < players.Count; i++)
                    {
                        var player = players[i];
                        msgBuilder.AppendLine($@"玩家{i + 1} @{player.Username}");
                    }
                    response.Message = msgBuilder.ToString();
                    return response;
                }
                response.Message = "遊戲還未開局";
                return response;
            });
        }

        private async Task<bool> SaveGameGroupsAsync(Dictionary<string, GameGroup> gameGroups)
        {
            try
            {
                return await _cachedService.SetAsync(GameGroupsKey, gameGroups);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
                return false;
            }
        }

    }
}

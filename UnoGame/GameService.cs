using CommonGameLib.Extensions;
using CommonGameLib.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using UnoGame.Extensions;
using UnoGame.GameComponents;
using UnoGame.Telegram;

namespace UnoGame
{
    public class GameService : IGameService
    {
        //public Dictionary<string, GameGroup> GameGroups;
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
            //GameGroups = _cachedService.GetAndSetAsync(GameGroupsKey, new Dictionary<string, GameGroup>()).Result;
        }

        public async Task<string> GetGroupIdAsync(string userId)
        {
            var groupIdMapper = await _cachedService.GetAsync<Dictionary<string, string>>(GroupIdMapper);
            if (groupIdMapper != null)
            {
                return groupIdMapper[userId];
            }
            return string.Empty;
        }

        public async Task<GameGroup?> GetGameGrouopByUserAsync(string userId)
        {
            var groupId = await GetGroupIdAsync(userId);
            var gameGroups = await _cachedService.GetAsync<Dictionary<string, GameGroup>>(GameGroupsKey);
            if (gameGroups.ContainsKey(groupId))
            {
                return gameGroups[groupId];
            }
            return null;
        }

        public async Task<GameGroup?> GetGameGrouopAsync(string groupId)
        {
            var gameGroups = await _cachedService.GetAsync<Dictionary<string, GameGroup>>(GameGroupsKey);
            if (gameGroups != null)
            {
                if (gameGroups.ContainsKey(groupId))
                {
                    return gameGroups[groupId];
                }
            }
            return null;
        }

        public async Task<Dictionary<string, GameGroup>> GetOrInitGameGrouopsAsync()
        {
            return await _cachedService.GetAndSetAsync(GameGroupsKey, new Dictionary<string, GameGroup>());
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

                Stack<Card> totalCards = new Stack<Card>();

                foreach (var card in baseCards)
                {
                    if (card.CardType == CardType.Number && card.Number == 0)
                    {
                        totalCards.Push(card);
                    }
                    else if (card.CardType == CardType.Number ||
                            card.CardType == CardType.Reverse ||
                            card.CardType == CardType.Skip ||
                            card.CardType == CardType.DrawTwo)
                    {
                        totalCards.Push(card);
                        totalCards.Push(card);
                    }
                    else
                    {
                        totalCards.Push(card);
                        totalCards.Push(card);
                        totalCards.Push(card);
                        totalCards.Push(card);
                    }
                }

                GameGroup newGameGroup = new GameGroup()
                {
                    GroupId = groupId,
                    Cards = totalCards,
                    Discards = new Stack<Card>(),
                    Players = new List<Player>(),
                    Host = host
                };
                var currentGroup = await GetGameGrouopAsync(groupId);
                if (currentGroup == null)
                {
                    _gameGroupIdMapper = new Dictionary<string, string>();
                    _gameGroupIdMapper.Add(host.Id, groupId);
                    var groupIdMapper = await _cachedService.GetAndSetAsync(GroupIdMapper, _gameGroupIdMapper);
                    if (groupIdMapper.ContainsKey(host.Id))
                    {
                        groupIdMapper[host.Id] = groupId;
                    }

                    var gameGrouops = await GetOrInitGameGrouopsAsync();
                    currentGroup = new GameGroup
                    {
                        GroupId = groupId,
                        Players = new List<Player>(),
                        Cards = totalCards,
                        Host = host
                    };
                    currentGroup.Players.Add(host);
                    gameGrouops.Add(groupId, currentGroup);

                    var saveToCached = await SaveGameGroupsAsync(gameGrouops);
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

        public async Task<ResponseInfo> JoinBotPlayerAsync(string groupId, int number)
        {
            var res = new ResponseInfo();
            Player newBotPlayer = new Player
            {
                Id = Guid.NewGuid().ToString(),
                IsBot = true,
                Alias = $@"電腦玩家{number}",
                Username = $@"電腦玩家{number}",
            };
            var currentGroup = await GetGameGrouopAsync(groupId);
            if (currentGroup != null)
            {
                currentGroup.Players.Add(newBotPlayer);
                var saveToCached = await SaveGameGroupAsync(currentGroup);
                if (!saveToCached)
                {
                    res.Message = @$"儲存GameGroup發生錯誤";
                    res.Success = false;
                    return res;
                }

                res.Message = $@"已加入電腦玩家:{newBotPlayer.Username}";
                res.Success = true;
                return res;
            }

            res.Success = false;
            res.Message = "遊戲尚未開局，請先執行開局指令 /new";
            return res;
        }

        public async Task<ResponseInfo> JoinPlayerAsync(string groupId, Player player)
        {
            var res = new ResponseInfo();
            //var gameGroups = await _cachedService.GetAndSetAsync(GameGroupsKey, new Dictionary<string, GameGroup>());

            var currentGroup = await GetGameGrouopAsync(groupId);
            if (currentGroup != null)
            {
                if (currentGroup.Players.Any(p => p.Id == player.Id))
                {
                    res.Success = false;
                    res.Message = @$"玩家 @{player.Username} 已經加入遊戲了";
                    return res;
                }


                currentGroup.Players.Add(player);

                var saveToCached = await SaveGameGroupAsync(currentGroup);
                if (!saveToCached)
                {
                    res.Message = @$"儲存GameGroup發生錯誤";
                    res.Success = false;
                    return res;
                }

                StringBuilder msgBuilder = new StringBuilder();
                res.Message = @$"玩家 @{player.Username}加入遊戲";
                return res;
            }

            res.Success = false;
            res.Message = "遊戲尚未開局，請先執行開局指令 /new";
            return res;
        }

        public async Task<ResponseInfo> StartGame(string groupId, Player player)
        {
            var res = new ResponseInfo();
            res.Success = true;
            var gameGroup = await GetGameGrouopAsync(groupId);
            if (gameGroup == null)
            {
                res.Success = false;
                res.Message = "遊戲尚未開局，請先執行開局指令 /new";
                return res;
            }

            if (player.Id != gameGroup.Host.Id)
            {
                res.Success = false;
                res.Message = $@"必須由開局者 @{gameGroup.Host.Username}開始遊戲";
                return res;
            }

            if (gameGroup.Players.Count < 2)
            {
                res.Message = $@"遊戲人數不夠，至少要兩人";
                return res;
            }

            gameGroup.Cards = gameGroup.Cards.Shuffle();

            foreach (var p in gameGroup.Players)
            {
                for (int i = 0; i < 7; i++)
                {
                    p.HandCards.Add(gameGroup.Cards.Pop());
                }
            }

            gameGroup.Players.Shuffle();

            res.FirstCard = gameGroup.Cards.Pop();
            await SaveGameGroupAsync(gameGroup);
            StringBuilder msgBuilder = new StringBuilder();
            msgBuilder.AppendLine($@"
                起始順序為:");
            res.Message = $@"遊戲開始: 開局牌為:";

            return res;
        }

        public async Task<ResponseInfo> GetPlayersAsync(string groupdId)
        {
            var response = new ResponseInfo();
            var gameGroup = await GetGameGrouopAsync(groupdId);
            if (gameGroup != null)
            {
                var players = gameGroup.Players;
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
        }

        public async Task<bool> SaveGameGroupAsync(GameGroup gameGroup)
        {
            try
            {
                var gameGroups = await _cachedService.GetAndSetAsync(GameGroupsKey, new Dictionary<string, GameGroup>());
                gameGroups[gameGroup.GroupId] = gameGroup;
                return await _cachedService.SetAsync(GameGroupsKey, gameGroups);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
                return false;
            }
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

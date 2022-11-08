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
using UnoGame.Extensions;
using UnoGame.GameComponents;
using UnoGame.Telegram;

namespace UnoGame
{
    public class GameService : IGameService
    {
        public Dictionary<string, GameGroup> GameGroups;
        public string GameGroupsKey = "GameGroups";
        private readonly ICachedService _cachedService;
        private readonly ILogger<GameService> _logger;
        public GameService(ICachedService cachedService, ILogger<GameService> logger)
        {
            _cachedService = cachedService;
            _logger = logger;
            GameGroups = _cachedService.GetAndSetAsync(GameGroupsKey, new Dictionary<string, GameGroup>()).Result;
        }

        public async Task<ResponseInfo> NewGameAsync(string groupId, Player host)
        {
            return await Task.Run(async () =>
            {
                var response = new ResponseInfo();
                if (string.IsNullOrEmpty(groupId))
                {
                    response.Message = "請先把機器人加入到群組";
                    return response;
                }

                var TotalCards = new List<Card>();
                Action<int> AddNumbers = (i) =>
               {
                   TotalCards.Add(AddNumberCard(i, CardColor.Red));
                   TotalCards.Add(AddNumberCard(i, CardColor.Blue));
                   TotalCards.Add(AddNumberCard(i, CardColor.Green));
                   TotalCards.Add(AddNumberCard(i, CardColor.Yellow));
               };

                Action<int, CardType> AddFuncs = (i, cardType) =>
                {
                    TotalCards.Add(AddFunctionCard(cardType, CardColor.Red));
                    TotalCards.Add(AddFunctionCard(cardType, CardColor.Blue));
                    TotalCards.Add(AddFunctionCard(cardType, CardColor.Green));
                    TotalCards.Add(AddFunctionCard(cardType, CardColor.Yellow));
                };


                for (int i = 0; i <= 14; i++)
                {
                    switch (i)
                    {
                        case 0:
                            AddNumbers(i);
                            break;
                        case 10:
                            AddFuncs(i, CardType.Skip);
                            AddFuncs(i, CardType.Skip);
                            break;
                        case 11:
                            AddFuncs(i, CardType.Reverse);
                            AddFuncs(i, CardType.Reverse);
                            break;
                        case 12:
                            AddFuncs(i, CardType.DrawTwo);
                            AddFuncs(i, CardType.DrawTwo);
                            break;
                        case 13:
                            TotalCards.Add(AddWildCard());
                            TotalCards.Add(AddWildCard());
                            TotalCards.Add(AddWildCard());
                            TotalCards.Add(AddWildCard());
                            break;
                        case 14:
                            TotalCards.Add(AddWildDrawFourCard());
                            TotalCards.Add(AddWildDrawFourCard());
                            TotalCards.Add(AddWildDrawFourCard());
                            TotalCards.Add(AddWildDrawFourCard());
                            break;
                        default:
                            AddNumbers(i);
                            AddNumbers(i);
                            break;
                    }
                }

                TotalCards.Shuffle();

                GameGroup newGameGroup = new GameGroup()
                {
                    GroupId = groupId,
                    Cards = TotalCards,
                    Discards = new List<Card>(),
                    Players = new List<Player>(),
                    Host = host
                };
                if (!GameGroups.ContainsKey(groupId))
                {
                    GameGroups.Add(groupId, newGameGroup);
                    var currentGroup = GameGroups[groupId];
                    currentGroup.Players.Add(host);

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

        private Card AddNumberCard(int number, CardColor color)
        {
            return new Card
            {
                Id = Guid.NewGuid().ToString(),
                Name = $"{color}{number}",
                CardType = CardType.Number,
                Color = color,
                Image = $"Source/Images/unocards/{color.ToString().ToLower()}{number}.png",
                Number = number,
            };
        }

        private Card AddFunctionCard(CardType cardType, CardColor color)
        {
            return new Card
            {
                Id = Guid.NewGuid().ToString(),
                Name = $"{color}{cardType}",
                CardType = cardType,
                Color = color,
                Image = $"Source/Images/unocards/{color.ToString().ToLower()}{cardType}.png",
                Number = 0,
            };
        }

        private Card AddWildCard()
        {
            return new Card
            {
                Id = Guid.NewGuid().ToString(),
                Name = CardType.Wild.ToString(),
                CardType = CardType.Wild,
                Color = null,
                Image = $"Source/Images/unocards/wild.png",
                Number = 0,
            };
        }

        private Card AddWildDrawFourCard()
        {
            return new Card
            {
                Id = Guid.NewGuid().ToString(),
                Name = CardType.WildDrawFour.ToString(),
                CardType = CardType.WildDrawFour,
                Color = null,
                Image = $"Source/Images/unocards/wildDrawFour.png",
                Number = 0,
            };
        }

    }
}

using CommonGameLib.Extensions;
using CommonGameLib.Services;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Security.Cryptography.X509Certificates;
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
        private readonly ICachedService _cachedService;
        private readonly ILogger<GameService> _logger;
        private Dictionary<string, string> _gameGroupIdMapper;
        public string GroupIdMapper = "GroupIdMapper";
        public string GameGroupsKey = "GameGroups";

        public GameService(ICachedService cachedService, ILogger<GameService> logger)
        {
            _cachedService = cachedService;
            _logger = logger;
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

        public async Task<GameGroup?> GetGameGroupAsync(string groupId)
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
                var currentGroup = await GetGameGroupAsync(groupId);
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
                        PlayersQueue = new Queue<Player>(),
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
            var currentGroup = await GetGameGroupAsync(groupId);
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

            var currentGroup = await GetGameGroupAsync(groupId);
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


        public void CardNumberAction(GameGroup gameGroup, Card throwCard, Player currentPlayer, ResponseInfo res)
        {
            gameGroup.Discards.Push(throwCard);
            currentPlayer.HandCards.Remove(throwCard);

            gameGroup.PlayersQueue.Dequeue();
            gameGroup.PlayersQueue.Enqueue(currentPlayer);

            var nextPlayer = gameGroup.PlayersQueue.Peek();
            nextPlayer.PrevCard = throwCard;

            res.BotPlayActions.Add(new BotPlayerAction
            {
                Message = $@"玩家 @{currentPlayer.Username} 出牌:",
                Message2 = $@"下一個玩家為：@{nextPlayer.Username}",
                FileId = $@"{throwCard.FileId}",
                UserName = $@"{currentPlayer.Username}",
                CurrentCard = throwCard
            });
        }

        public void CardSkipAction(GameGroup gameGroup, Card throwCard, Player currentPlayer, ResponseInfo res)
        {
            gameGroup.Discards.Push(throwCard);
            currentPlayer.HandCards.Remove(throwCard);

            gameGroup.PlayersQueue.Dequeue();
            gameGroup.PlayersQueue.Enqueue(currentPlayer);

            var skipNextPlayer = gameGroup.PlayersQueue.Dequeue();
            gameGroup.PlayersQueue.Enqueue(skipNextPlayer);

            var nextPlayer = gameGroup.PlayersQueue.Peek();
            nextPlayer.PrevCard = throwCard;

            res.BotPlayActions.Add(new BotPlayerAction
            {
                Message = $@"玩家 @{currentPlayer.Username} 出牌:",
                Message2 = $@"跳過玩家 @{skipNextPlayer.Username}，下一個玩家為：@{nextPlayer.Username}",
                FileId = $@"{throwCard.FileId}",
                UserName = $@"{currentPlayer.Username}",
                CurrentCard = throwCard
            }); ;
        }

        public void CardDrawTwoAction(GameGroup gameGroup, Card throwCard, Player currentPlayer, ResponseInfo res)
        {
            gameGroup.Discards.Push(throwCard);
            currentPlayer.HandCards.Remove(throwCard);

            gameGroup.PlayersQueue.Dequeue();
            gameGroup.PlayersQueue.Enqueue(currentPlayer);

            // darw two cards
            var skipNextPlayer = gameGroup.PlayersQueue.Dequeue();
            skipNextPlayer.PrevCard = throwCard;
            for (int i = 0; i < 2; i++)
            {
                var card = gameGroup.Cards.Pop();
                skipNextPlayer.HandCards.Add(card);
            }
            gameGroup.PlayersQueue.Enqueue(skipNextPlayer);

            var nextPlayer = gameGroup.PlayersQueue.Peek();
            nextPlayer.PrevCard = throwCard;

            res.BotPlayActions.Add(new BotPlayerAction
            {
                Message = $@"玩家 @{currentPlayer.Username} 出牌:",
                Message2 = $@"玩家 @{skipNextPlayer.Username} 抽兩張牌並跳過，換下一個玩家 @{nextPlayer.Username}",
                FileId = $@"{throwCard.FileId}",
                UserName = $@"{currentPlayer.Username}",
                CurrentCard = throwCard
            }); ;

        }

        public void CardReverseAction(GameGroup gameGroup, Card throwCard, Player currentPlayer, ResponseInfo res)
        {
            gameGroup.Discards.Push(throwCard);
            currentPlayer.HandCards.Remove(throwCard);

            gameGroup.PlayersQueue = new Queue<Player>(gameGroup.PlayersQueue.Reverse());

            var nextPlayer = gameGroup.PlayersQueue.Peek();
            nextPlayer.PrevCard = throwCard;

            res.BotPlayActions.Add(new BotPlayerAction
            {
                Message = $@"玩家 @{currentPlayer.Username} 出牌:",
                Message2 = $@"反轉 下一個玩家 @{nextPlayer.Username}",
                FileId = $@"{throwCard.FileId}",
                UserName = $@"{currentPlayer.Username}",
                CurrentCard = throwCard
            }); ;
        }

        public void CardWildAction(GameGroup gameGroup, Card throwCard, Player currentPlayer, ResponseInfo res)
        {
            var copyCard = throwCard.CloneObj<Card>();

            gameGroup.Discards.Push(throwCard);
            currentPlayer.HandCards.Remove(throwCard);

            gameGroup.PlayersQueue.Dequeue();
            gameGroup.PlayersQueue.Enqueue(currentPlayer);

            var nextPlayer = gameGroup.PlayersQueue.Peek();
            copyCard.Color = GameExtensions.RandomEnumValue<CardColor>();
            nextPlayer.PrevCard = copyCard;

            res.BotPlayActions.Add(new BotPlayerAction
            {
                Message = $@"玩家 @{currentPlayer.Username} 出牌:",
                Message2 = $@"顏色選擇{copyCard.Color} ， 下一個玩家 @{nextPlayer.Username}",
                FileId = $@"{copyCard.FileId}",
                UserName = $@"{currentPlayer.Username}",
                CurrentCard = copyCard
            }); ;
        }

        public void CardWildDrawFourAction(GameGroup gameGroup, Card throwCard, Player currentPlayer, ResponseInfo res)
        {
            var copyCard = throwCard.CloneObj<Card>();

            gameGroup.Discards.Push(throwCard);
            currentPlayer.HandCards.Remove(throwCard);

            gameGroup.PlayersQueue.Dequeue();
            gameGroup.PlayersQueue.Enqueue(currentPlayer);

            var skipNextPlayer = gameGroup.PlayersQueue.Dequeue();
            skipNextPlayer.PrevCard = throwCard;
            for (int i = 0; i < 4; i++)
            {
                var card = gameGroup.Cards.Pop();
                skipNextPlayer.HandCards.Add(card);
            }
            gameGroup.PlayersQueue.Enqueue(skipNextPlayer);

            var nextPlayer = gameGroup.PlayersQueue.Peek();
            copyCard.Color = GameExtensions.RandomEnumValue<CardColor>();
            nextPlayer.PrevCard = copyCard;

            res.BotPlayActions.Add(new BotPlayerAction
            {
                Message = $@"玩家 @{currentPlayer.Username} 出牌:",
                Message2 = $@"顏色選擇{copyCard.Color} ， 下一個玩家 @{nextPlayer.Username}",
                FileId = $@"{copyCard.FileId}",
                UserName = $@"{currentPlayer.Username}",
                CurrentCard = copyCard
            });
        }

        public void CardPassAction(GameGroup gameGroup, Player currentPlayer, ResponseInfo res)
        {
            var newCard = gameGroup.Cards.Pop();
            currentPlayer.HandCards.Add(newCard);
            var prevPlayer = gameGroup.PlayersQueue.LastOrDefault();

            gameGroup.PlayersQueue.Dequeue();
            gameGroup.PlayersQueue.Enqueue(currentPlayer);

            var nextPlayer = gameGroup.PlayersQueue.Peek();
            if (prevPlayer != null)
            {
                nextPlayer.PrevCard = prevPlayer.PrevCard;
            }
            else
            {
                nextPlayer.PrevCard = currentPlayer.PrevCard;
            }

            res.BotPlayActions.Add(new BotPlayerAction
            {
                Message = $@"玩家 @{currentPlayer.Username} Pass",
                FileId = $@"",
                UserName = $@"{currentPlayer.Username}",
                CurrentCard = prevPlayer.PrevCard
            }); ;
        }


        public async Task<ResponseInfo> StartGameAsync(string groupId, Player player)
        {
            var res = new ResponseInfo();
            res.Success = true;
            var gameGroup = await GetGameGroupAsync(groupId);
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
                res.Success = false;
                res.Message = $@"遊戲人數不夠，至少要兩人";
                return res;
            }

            if (gameGroup.IsStart)
            {
                res.Success = false;
                res.Message = $@"遊戲已經開始";
                return res;
            }


            gameGroup.Cards = gameGroup.Cards.Shuffle();
            gameGroup.IsStart = true;

            foreach (var p in gameGroup.Players)
            {
                for (int i = 0; i < 7; i++)
                {
                    p.HandCards.Add(gameGroup.Cards.Pop());
                }
                gameGroup.PlayersQueue.Enqueue(p);
            }
            gameGroup.PlayersQueue = gameGroup.PlayersQueue.Shuffle();

            while (gameGroup.Cards.Peek().CardType != CardType.Number)
            {
                gameGroup.Cards = gameGroup.Cards.Shuffle();
            }
            var firstCard = gameGroup.Cards.Pop();

            gameGroup.Discards.Push(firstCard);
            res.FirstCard = firstCard;


            StringBuilder msgBuilder = new StringBuilder();
            msgBuilder.AppendLine($@"遊戲開始:");

            msgBuilder.AppendLine();
            msgBuilder.AppendLine($@"目前輪到玩家:");
            msgBuilder.AppendLine($@"@{gameGroup.PlayersQueue.Peek().Username}");
            msgBuilder.AppendLine();

            msgBuilder.AppendLine("起始順序為:");
            msgBuilder.AppendLine();

            for (int i = 0; i < gameGroup.PlayersQueue.Count; i++)
            {
                var playerQueue = gameGroup.PlayersQueue.ToList()[i];
                msgBuilder.AppendLine($@"{i + 1}. @{playerQueue.Username}");
            }

            msgBuilder.AppendLine();
            msgBuilder.AppendLine("開局牌為:");
            res.Message = msgBuilder.ToString();

            StringBuilder botMsgBuilder = new StringBuilder();
            var firstPlayer = gameGroup.PlayersQueue.Peek();
            firstPlayer.PrevCard = res.FirstCard;

            while (gameGroup.PlayersQueue.Peek().IsBot)
            {
                botMsgBuilder.AppendLine();
                botMsgBuilder.AppendLine($@"輪到玩家: @{gameGroup.PlayersQueue.Peek().Username} ...");

                var newQueue = new Queue<Player>();
                var currentPlayer = gameGroup.PlayersQueue.Peek();
                var hasCardWithType = currentPlayer.HandCards.FirstOrDefault(c => c.CardType == currentPlayer.PrevCard.CardType);
                var hasCardWithColor = currentPlayer.HandCards.FirstOrDefault(c => c.Color == currentPlayer.PrevCard.Color);
                var hasWildCard = currentPlayer.HandCards.FirstOrDefault(c => c.CardType == CardType.Wild || c.CardType == CardType.WildDrawFour);
                // 判斷出啥牌
                if (hasCardWithColor != null && hasCardWithColor.CardType == CardType.Number && hasCardWithColor.Number == currentPlayer.PrevCard.Number)
                {
                    CardNumberAction(gameGroup, hasCardWithColor, currentPlayer, res);
                }
                else if (hasCardWithType != null && hasCardWithType.CardType == CardType.Number && hasCardWithType.Number == currentPlayer.PrevCard.Number)
                {
                    CardNumberAction(gameGroup, hasCardWithType, currentPlayer, res);
                }
                else if (hasCardWithColor != null && hasCardWithColor.CardType == CardType.Skip)
                {
                    CardSkipAction(gameGroup, hasCardWithColor, currentPlayer, res);
                }
                else if (hasCardWithType != null && hasCardWithType.CardType == CardType.Skip)
                {
                    CardSkipAction(gameGroup, hasCardWithType, currentPlayer, res);
                }
                else if (hasCardWithColor != null && hasCardWithColor.CardType == CardType.DrawTwo)
                {
                    CardDrawTwoAction(gameGroup, hasCardWithColor, currentPlayer, res);
                }
                else if (hasCardWithType != null && hasCardWithType.CardType == CardType.DrawTwo)
                {
                    CardDrawTwoAction(gameGroup, hasCardWithType, currentPlayer, res);
                }
                else if (hasCardWithColor != null && hasCardWithColor.CardType == CardType.Reverse)
                {
                    CardReverseAction(gameGroup, hasCardWithColor, currentPlayer, res);
                }
                else if (hasCardWithType != null && hasCardWithType.CardType == CardType.Reverse)
                {
                    CardReverseAction(gameGroup, hasCardWithType, currentPlayer, res);
                }
                else if (hasWildCard != null && hasWildCard.CardType == CardType.Wild)
                {
                    CardWildAction(gameGroup, hasWildCard, currentPlayer, res);
                }
                else if (hasWildCard != null && hasWildCard.CardType == CardType.WildDrawFour)
                {
                    CardWildDrawFourAction(gameGroup, hasWildCard, currentPlayer, res);
                }
                else
                {
                    CardPassAction(gameGroup, currentPlayer, res);
                }
            }

            var saveToCached = await SaveGameGroupAsync(gameGroup);
            if (!saveToCached)
            {
                res.Message = @$"儲存GameGroups發生錯誤";
                return res;
            }

            return res;
        }

        public async Task<ResponseInfo> EndGameAsync(string groupId, Player player)
        {
            var res = new ResponseInfo();
            var gameGroup = await GetGameGroupAsync(groupId);
            if (gameGroup != null)
            {
                if (gameGroup.Host.Id != player.Id)
                {
                    res.Success = false;
                    res.Message = $@"強制結束遊戲必須由開局者 @{player.Username} 操作";
                    return res;
                }
            }

            var gameGroups = await GetOrInitGameGrouopsAsync();
            if (gameGroups != null)
            {
                if (gameGroups.ContainsKey(groupId))
                {
                    gameGroups.Remove(groupId);
                    var saveToCached = await SaveGameGroupsAsync(gameGroups);
                    if (!saveToCached)
                    {
                        res.Message = @$"儲存GameGroup發生錯誤";
                        res.Success = false;
                        return res;
                    }
                }
            }
            res.Success = true;
            res.Message = "遊戲結束";

            return res;
        }

        public async Task<ResponseInfo> GetPlayersAsync(string groupdId)
        {
            var response = new ResponseInfo();
            var gameGroup = await GetGameGroupAsync(groupdId);
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

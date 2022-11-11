﻿using CommonGameLib.Extensions;
using CommonGameLib.Services;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Linq.Expressions;
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
        public string GroupIdMapper { get => "GroupIdMapper"; }
        public string GameGroupsKey { get => "GameGroups"; }

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

        //public async Task<Dictionary<string, GameGroup>> GetGameGroups(string groupId)
        //{
        //    return await _cachedService.GetAsync<Dictionary<string, GameGroup>>(GameGroupsKey);
        //}

        public async Task<GameGroup?> GetGameGrouopByUserAsync(string userId)
        {
            var groupId = await GetGroupIdAsync(userId);
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
            var response = new ResponseInfo();
            if (string.IsNullOrEmpty(groupId))
            {
                response.AddPlayerAction("請先把機器人加入到群組");
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
                Players = new Queue<Player>(),
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
                    Players = new Queue<Player>(),
                    Cards = totalCards,
                    Host = host
                };
                currentGroup.Players.Enqueue(host);
                gameGrouops.Add(groupId, currentGroup);

                var saveToCached = await SaveGameGroupsAsync(gameGrouops);
                if (!saveToCached)
                {
                    response.AddPlayerAction(@$"儲存GameGroups發生錯誤");
                    return response;
                }
            }
            else
            {
                response.AddPlayerAction(@$"已開局，開局者：@{host.Username}");
                return response;
            }
            response.AddPlayerAction(@$"開始新遊戲，開局者：@{host.Username}");
            return response;
        }

        public async Task<ResponseInfo> JoinBotPlayerAsync(string groupId, int number)
        {
            var res = new ResponseInfo();
            var gameGroup = await GetGameGroupAsync(groupId);
            if (gameGroup != null)
            {
                Player newBotPlayer = new Player
                {
                    Index = gameGroup.Players.Count,
                    Id = Guid.NewGuid().ToString(),
                    IsBot = true,
                    Alias = $@"電腦玩家{number}",
                    Username = $@"電腦玩家{number}",
                };
                var currentGroup = await GetGameGroupAsync(groupId);
                if (currentGroup != null)
                {
                    currentGroup.Players.Enqueue(newBotPlayer);
                    var saveToCached = await SaveGameGroupAsync(currentGroup);
                    if (!saveToCached)
                    {
                        res.AddPlayerAction(@$"儲存GameGroup發生錯誤");
                        res.Success = false;
                        return res;
                    }

                    res.AddPlayerAction($@"已加入電腦玩家:{newBotPlayer.Username}");
                    res.Success = true;
                    return res;
                }
            }

            res.Success = false;
            res.AddPlayerAction("遊戲尚未開局，請先執行開局指令 /new");
            return res;
        }

        public async Task<ResponseInfo> JoinPlayerAsync(string groupId, Player player)
        {
            var res = new ResponseInfo();

            var currentGroup = await GetGameGroupAsync(groupId);
            if (currentGroup != null)
            {
                player.Index = currentGroup.Players.Count;
                if (currentGroup.Players.Any(p => p.Id == player.Id))
                {
                    res.Success = false;
                    res.AddPlayerAction(@$"玩家 @{player.Username} 已經加入遊戲了");
                    return res;
                }


                currentGroup.Players.Enqueue(player);

                var saveToCached = await SaveGameGroupAsync(currentGroup);
                if (!saveToCached)
                {
                    res.AddPlayerAction(@$"儲存GameGroup發生錯誤");
                    res.Success = false;
                    return res;
                }

                StringBuilder msgBuilder = new StringBuilder();
                res.AddPlayerAction(@$"玩家 @{player.Username}加入遊戲");
                return res;
            }

            res.Success = false;
            res.AddPlayerAction("遊戲尚未開局，請先執行開局指令 /new");
            return res;
        }

        public void CardNumberAction(GameGroup gameGroup, Card? throwCard, Player currentPlayer, ResponseInfo res)
        {
            gameGroup.Discards.Push(throwCard);
            currentPlayer.RemoveHandCards(throwCard);
            currentPlayer.NextCard = throwCard;

            gameGroup.Players.Dequeue();
            gameGroup.Players.Enqueue(currentPlayer);

            var nextPlayer = gameGroup.Players.Peek();
            nextPlayer.PrevCard = throwCard;

            res.AddPlayerAction($@"玩家 @{currentPlayer.Username} 出牌:");
            res.AddPlayerAction("", throwCard.FileId, currentPlayer?.Username, throwCard);
            res.AddPlayerAction($@"輪到玩家：@{nextPlayer.Username}");
        }

        public void CardSkipAction(GameGroup gameGroup, Card? throwCard, Player currentPlayer, ResponseInfo res)
        {
            gameGroup.Discards.Push(throwCard);
            currentPlayer.RemoveHandCards(throwCard);
            currentPlayer.NextCard = throwCard;

            gameGroup.Players.Dequeue();
            gameGroup.Players.Enqueue(currentPlayer);

            var skipNextPlayer = gameGroup.Players.Dequeue();
            gameGroup.Players.Enqueue(skipNextPlayer);

            var nextPlayer = gameGroup.Players.Peek();
            nextPlayer.PrevCard = throwCard;

            res.AddPlayerAction($@"玩家 @{currentPlayer.Username} 出牌:");
            res.AddPlayerAction("", throwCard.FileId);
            res.AddPlayerAction($@"跳過玩家 @{skipNextPlayer.Username}，輪到玩家：@{nextPlayer.Username}");

        }

        public void CardDrawTwoAction(GameGroup gameGroup, Card? throwCard, Player currentPlayer, ResponseInfo res)
        {
            gameGroup.Discards.Push(throwCard);
            currentPlayer.RemoveHandCards(throwCard);
            currentPlayer.NextCard = throwCard;

            gameGroup.Players.Dequeue();
            gameGroup.Players.Enqueue(currentPlayer);

            var skipNextPlayer = gameGroup.Players.Dequeue();
            skipNextPlayer.PrevCard = throwCard;
            for (int i = 0; i < 2; i++)
            {
                var card = gameGroup.Cards.Pop();
                skipNextPlayer.HandCards.Add(card);
            }
            gameGroup.Players.Enqueue(skipNextPlayer);

            var nextPlayer = gameGroup.Players.Peek();
            nextPlayer.PrevCard = throwCard;

            res.AddPlayerAction($@"玩家 @{currentPlayer.Username} 出牌:");
            res.AddPlayerAction("", throwCard.FileId);
            res.AddPlayerAction($@"玩家 @{skipNextPlayer.Username} 抽兩張牌並跳過，輪到玩家： @{nextPlayer.Username}");

        }

        public void CardReverseAction(GameGroup gameGroup, Card? throwCard, Player currentPlayer, ResponseInfo res)
        {
            gameGroup.Discards.Push(throwCard);
            currentPlayer.RemoveHandCards(throwCard);
            currentPlayer.NextCard = throwCard;

            gameGroup.Players = new Queue<Player>(gameGroup.Players.Reverse());

            var nextPlayer = gameGroup.Players.Peek();
            nextPlayer.PrevCard = throwCard;

            res.AddPlayerAction($@"玩家 @{currentPlayer.Username} 出牌:");
            res.AddPlayerAction("", throwCard.FileId);
            res.AddPlayerAction($@"反轉，輪到玩家 @{nextPlayer.Username}");
        }

        public void CardWildAction(GameGroup gameGroup, Card? throwCard, Player currentPlayer, ResponseInfo res, CardColor cardColor)
        {
            var copyCard = throwCard.CloneObj<Card>();

            gameGroup.Discards.Push(throwCard);
            currentPlayer.RemoveHandCards(throwCard);
            currentPlayer.NextCard = throwCard;

            gameGroup.Players.Dequeue();
            gameGroup.Players.Enqueue(currentPlayer);

            var nextPlayer = gameGroup.Players.Peek();
            copyCard.Color = cardColor;
            nextPlayer.PrevCard = copyCard;

            res.AddPlayerAction($@"玩家 @{currentPlayer.Username} 出牌:");
            res.AddPlayerAction("", copyCard.FileId);
            res.AddPlayerAction($@"顏色選擇{copyCard.Color}，輪到玩家 @{nextPlayer.Username}");
        }

        public void CardWildDrawFourAction(GameGroup gameGroup, Card? throwCard, Player currentPlayer, ResponseInfo res, CardColor color)
        {
            var copyCard = throwCard.CloneObj<Card>();

            gameGroup.Discards.Push(throwCard);
            currentPlayer.RemoveHandCards(throwCard);
            currentPlayer.NextCard = throwCard;

            gameGroup.Players.Dequeue();
            gameGroup.Players.Enqueue(currentPlayer);

            var skipNextPlayer = gameGroup.Players.Dequeue();
            skipNextPlayer.PrevCard = throwCard;
            for (int i = 0; i < 4; i++)
            {
                var card = gameGroup.Cards.Pop();
                skipNextPlayer.HandCards.Add(card);
            }
            gameGroup.Players.Enqueue(skipNextPlayer);

            var nextPlayer = gameGroup.Players.Peek();
            copyCard.Color = color;
            nextPlayer.PrevCard = copyCard;

            res.AddPlayerAction($@"玩家 @{currentPlayer.Username} 出牌:");
            res.AddPlayerAction("", copyCard.FileId);
            res.AddPlayerAction($@"顏色選擇{TGMapper.CardColorsMapper[copyCard.Color.Value]}，玩家 @{skipNextPlayer.Username} 抽四張牌並跳過，輪到玩家 @{nextPlayer.Username}");
        }

        public void CardPassAction(GameGroup gameGroup, Card? throwCard, Player currentPlayer, ResponseInfo res)
        {
            var newCard = gameGroup.Cards.Pop();
            currentPlayer.RemoveHandCards(throwCard);
            currentPlayer.NextCard = throwCard;

            var prevPlayer = gameGroup.Players.LastOrDefault();

            gameGroup.Players.Dequeue();
            gameGroup.Players.Enqueue(currentPlayer);

            var nextPlayer = gameGroup.Players.Peek();
            if (prevPlayer != null)
            {
                nextPlayer.PrevCard = prevPlayer.PrevCard;
            }
            else
            {
                nextPlayer.PrevCard = throwCard;
            }

            res.AddPlayerAction($@"玩家 @{currentPlayer.Username} Pass，輪到玩家 @{nextPlayer.Username}");

        }


        public async Task<ResponseInfo> HandlePlayerAction(string playerId, string uniqueFiledId, CardColor? color = null, bool isPass = false)
        {
            ResponseInfo res = new ResponseInfo();

            var gameGroup = await GetGameGrouopByUserAsync(playerId);
            var playerSendCard = await GetPlayerCardAsync(uniqueFiledId, playerId);
            var player = await GetPlayerAsync(playerId);
            if (player != null && playerSendCard != null && gameGroup != null)
            {
                Card cardOnBoard = player.PrevCard;
                if ((playerSendCard.Color == cardOnBoard.Color || playerSendCard.Number == cardOnBoard.Number) &&
                     playerSendCard.CardType == CardType.Number)
                {
                    CardNumberAction(gameGroup, playerSendCard, player, res);
                }
                else if ((playerSendCard.Color == cardOnBoard.Color || playerSendCard.CardType == cardOnBoard.CardType) &&
                     (playerSendCard.CardType == CardType.Skip))
                {
                    CardSkipAction(gameGroup, playerSendCard, player, res);
                }
                else if ((playerSendCard.Color == cardOnBoard.Color || playerSendCard.CardType == cardOnBoard.CardType) &&
                   playerSendCard.CardType == CardType.Reverse)
                {
                    CardReverseAction(gameGroup, playerSendCard, player, res);
                }
                else if ((playerSendCard.Color == cardOnBoard.Color || playerSendCard.CardType == cardOnBoard.CardType) &&
                   playerSendCard.CardType == CardType.DrawTwo)
                {
                    CardDrawTwoAction(gameGroup, playerSendCard, player, res);
                }
                else if ((playerSendCard.Color == cardOnBoard.Color) &&
                    playerSendCard.CardType == CardType.Wild)
                {
                    CardWildAction(gameGroup, playerSendCard, player, res, playerSendCard.Color.Value);
                }
                else if ((playerSendCard.Color == cardOnBoard.Color) &&
                  playerSendCard.CardType == CardType.WildDrawFour)
                {
                    CardWildDrawFourAction(gameGroup, playerSendCard, player, res, playerSendCard.Color.Value);
                }
                else if (playerSendCard.CardType == CardType.Wild)
                {
                    if (color != null)
                    {
                        CardWildAction(gameGroup, playerSendCard, player, res, color.Value);
                    }
                    else
                    {
                        res.NeedSelectedColor = true;
                    }
                }
                else if (playerSendCard.CardType == CardType.WildDrawFour)
                {
                    if (color != null)
                    {
                        CardWildDrawFourAction(gameGroup, playerSendCard, player, res, color.Value);
                    }
                    else
                    {
                        res.NeedSelectedColor = true;
                    }
                }
                else if (isPass)
                {
                    CardPassAction(gameGroup, playerSendCard, player, res);
                }
                else
                {
                    var currentColor = string.Empty;
                    if (cardOnBoard.Color.HasValue)
                    {
                        currentColor = TGMapper.CardColorsMapper[cardOnBoard.Color.Value];
                    }
                    res.AddPlayerAction(@$"不能出這張牌，必須與目前牌的顏色[{currentColor}]或數字相符的");
                }

                var playerHandCardCount = player.HandCards.Count;
                if (playerHandCardCount == 1)
                {
                    res.AddPlayerAction($@"玩家 @{player.Username} UNO !!!");
                }
                else if (playerHandCardCount == 1)
                {
                    res.AddPlayerAction($@"玩家 @{player.Username} 獲勝 !!!");
                }
            }

            if (gameGroup != null)
            {
                var totalCardCount = gameGroup.Cards.Count;
                if (totalCardCount == 0)
                {
                    for (int i = 0; i < gameGroup.Discards.Count; i++)
                    {
                        gameGroup.Cards.Push(gameGroup.Discards.Pop());
                    }
                    gameGroup.Cards = gameGroup.Cards.Shuffle();
                }
            }

            var saveToCached = await SaveGameGroupAsync(gameGroup);
            if (!saveToCached)
            {
                res.AddPlayerAction(@$"儲存GameGroups發生錯誤");
            }

            return res;
        }

        public async Task HandleBotActionAsync(GameGroup gameGroup, ResponseInfo res)
        {
            while (gameGroup.Players.Peek().IsBot)
            {
                //var newQueue = new Queue<Player>();
                var currentPlayerQueue = gameGroup.Players.Peek();
                var hasCardWithType = currentPlayerQueue.HandCards.FirstOrDefault(c => c.CardType == currentPlayerQueue?.PrevCard?.CardType);
                var hasCardWithColor = currentPlayerQueue.HandCards.FirstOrDefault(c => c.Color == currentPlayerQueue?.PrevCard?.Color);
                var hasWildCard = currentPlayerQueue.HandCards.FirstOrDefault(c => c.CardType == CardType.Wild || c.CardType == CardType.WildDrawFour);

                // 判斷出啥牌
                if (hasCardWithColor != null && hasCardWithColor.CardType == CardType.Number && hasCardWithColor.Number == currentPlayerQueue.PrevCard.Number)
                {
                    CardNumberAction(gameGroup, hasCardWithColor, currentPlayerQueue, res);
                }
                else if (hasCardWithType != null && hasCardWithType.CardType == CardType.Number && hasCardWithType.Number == currentPlayerQueue.PrevCard.Number)
                {
                    CardNumberAction(gameGroup, hasCardWithType, currentPlayerQueue, res);
                }
                else if (hasCardWithColor != null && hasCardWithColor.CardType == CardType.Skip)
                {
                    CardSkipAction(gameGroup, hasCardWithColor, currentPlayerQueue, res);
                }
                else if (hasCardWithType != null && hasCardWithType.CardType == CardType.Skip)
                {
                    CardSkipAction(gameGroup, hasCardWithType, currentPlayerQueue, res);
                }
                else if (hasCardWithColor != null && hasCardWithColor.CardType == CardType.DrawTwo)
                {
                    CardDrawTwoAction(gameGroup, hasCardWithColor, currentPlayerQueue, res);
                }
                else if (hasCardWithType != null && hasCardWithType.CardType == CardType.DrawTwo)
                {
                    CardDrawTwoAction(gameGroup, hasCardWithType, currentPlayerQueue, res);
                }
                else if (hasCardWithColor != null && hasCardWithColor.CardType == CardType.Reverse)
                {
                    CardReverseAction(gameGroup, hasCardWithColor, currentPlayerQueue, res);
                }
                else if (hasCardWithType != null && hasCardWithType.CardType == CardType.Reverse)
                {
                    CardReverseAction(gameGroup, hasCardWithType, currentPlayerQueue, res);
                }
                else if (hasWildCard != null && hasWildCard.CardType == CardType.Wild)
                {
                    var color = GameExtensions.RandomEnumValue<CardColor>();
                    CardWildAction(gameGroup, hasWildCard, currentPlayerQueue, res, color);
                }
                else if (hasWildCard != null && hasWildCard.CardType == CardType.WildDrawFour)
                {
                    var color = GameExtensions.RandomEnumValue<CardColor>();
                    CardWildDrawFourAction(gameGroup, hasWildCard, currentPlayerQueue, res, color);
                }
                else
                {
                    CardPassAction(gameGroup, currentPlayerQueue.PrevCard, currentPlayerQueue, res);
                }

                var currentPlayer = gameGroup.Players.FirstOrDefault(p => p.Id == currentPlayerQueue.Id);
                if (currentPlayer != null)
                {
                    currentPlayer.HandCards = currentPlayerQueue.HandCards;
                    currentPlayer.PrevCard = currentPlayerQueue.PrevCard;
                    currentPlayer.NextCard = currentPlayerQueue.NextCard;
                }

                var playerHandCardCount = currentPlayer.HandCards.Count;
                if (playerHandCardCount == 1)
                {
                    res.AddPlayerAction($@"玩家 @{currentPlayer.Username} UNO !!!");
                }
                else if (playerHandCardCount == 1)
                {
                    res.AddPlayerAction($@"玩家 @{currentPlayer.Username} 獲勝 !!!");
                }

                var totalCardCount = gameGroup.Cards.Count;
                if (totalCardCount == 0)
                {
                    for (int i = 0; i < gameGroup.Discards.Count; i++)
                    {
                        gameGroup.Cards.Push(gameGroup.Discards.Pop());
                    }
                    gameGroup.Cards = gameGroup.Cards.Shuffle();
                }
            }

            var saveToCached = await SaveGameGroupAsync(gameGroup);
            if (!saveToCached)
            {
                res.AddPlayerAction(@$"儲存GameGroups發生錯誤");
            }

        }

        public async Task<ResponseInfo> StartGameAsync(string groupId, Player player)
        {
            var res = new ResponseInfo();
            res.Success = true;
            var gameGroup = await GetGameGroupAsync(groupId);
            if (gameGroup == null)
            {
                res.Success = false;
                res.AddPlayerAction("遊戲尚未開局，請先執行開局指令 /new");
                return res;
            }

            if (player.Id != gameGroup.Host.Id)
            {
                res.Success = false;
                res.AddPlayerAction($@"必須由開局者 @{gameGroup.Host.Username}開始遊戲");
                return res;
            }

            if (gameGroup.Players.Count < 2)
            {
                res.Success = false;
                res.AddPlayerAction($@"遊戲人數不夠，至少要兩人");
                return res;
            }

            if (gameGroup.IsStart)
            {
                res.Success = false;
                res.AddPlayerAction($@"遊戲已經開始");
                return res;
            }


            gameGroup.Cards = gameGroup.Cards.Shuffle();
            gameGroup.IsStart = true;

            foreach (var p in gameGroup.Players.ToList())
            {
                for (int i = 0; i < 7; i++)
                {
                    p.HandCards.Add(gameGroup.Cards.Pop());
                }
            }
            gameGroup.Players = gameGroup.Players.Shuffle();

            while (gameGroup.Cards.Peek().CardType != CardType.Number)
            {
                gameGroup.Cards = gameGroup.Cards.Shuffle();
            }
            var firstCard = gameGroup.Cards.Pop();

            gameGroup.Discards.Push(firstCard);


            StringBuilder msgBuilder = new StringBuilder();
            msgBuilder.AppendLine($@"遊戲開始:");

            msgBuilder.AppendLine();
            msgBuilder.AppendLine($@"目前輪到玩家:");
            msgBuilder.AppendLine($@"@{gameGroup.Players.Peek().Username}");
            msgBuilder.AppendLine();

            msgBuilder.AppendLine("起始順序為:");
            msgBuilder.AppendLine();

            for (int i = 0; i < gameGroup.Players.Count; i++)
            {
                var playerQueue = gameGroup.Players.ToList()[i];
                msgBuilder.AppendLine($@"{i + 1}. @{playerQueue.Username}");
            }

            msgBuilder.AppendLine();
            msgBuilder.AppendLine("開局牌為:");
            res.AddPlayerAction(msgBuilder.ToString());
            res.AddPlayerAction(fileId: firstCard.FileId);

            StringBuilder botMsgBuilder = new StringBuilder();
            var firstPlayer = gameGroup.Players.Peek();
            firstPlayer.PrevCard = firstCard;

            await HandleBotActionAsync(gameGroup, res);

            var saveToCached = await SaveGameGroupAsync(gameGroup);
            if (!saveToCached)
            {
                res.AddPlayerAction(@$"儲存GameGroups發生錯誤");
                //res.Message = @$"儲存GameGroups發生錯誤";
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
                    res.AddPlayerAction($@"強制結束遊戲必須由開局者 @{player.Username} 操作");
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
                        res.AddPlayerAction(@$"儲存GameGroup發生錯誤");
                        res.Success = false;
                        return res;
                    }
                }
            }
            res.Success = true;
            res.AddPlayerAction("遊戲結束");

            return res;
        }
        public async Task<Card?> GetPlayerCardAsync(string uniqueFiledId, string playerId)
        {
            var res = new ResponseInfo();
            var player = await GetPlayerAsync(playerId);
            if (player != null)
            {
                var card = player.HandCards.FirstOrDefault(c => c.UniqueFileId == uniqueFiledId);
                return card;
            }
            return null;
        }

        public async Task<Player?> GetPlayerAsync(string playerId)
        {
            var gameGroup = await GetGameGrouopByUserAsync(playerId);
            if (gameGroup != null)
            {
                return gameGroup.Players.FirstOrDefault(p => p.Id == playerId);
            }
            return null;
        }

        public async Task<ResponseInfo> ShowGameStateAsync(string groupId)
        {
            ResponseInfo res = new ResponseInfo();
            StringBuilder gameStateBuilder = new StringBuilder();
            var gameGroup = await GetGameGroupAsync(groupId);
            if (gameGroup != null)
            {
                if (gameGroup.Players.Count > 0)
                {
                    var currentPlayer = gameGroup.Players.Peek();
                    gameStateBuilder.AppendLine($@"目前輪到玩家：@{currentPlayer.Username}");
                    gameStateBuilder.AppendLine();
                    string type = string.Empty;
                    if (currentPlayer.PrevCard != null)
                    {
                        if (currentPlayer.PrevCard.CardType == CardType.Number)
                        {
                            type = TGMapper.CardTypesMapper.ContainsKey(currentPlayer.PrevCard.CardType) ?
                                                        $"{TGMapper.CardTypesMapper[currentPlayer.PrevCard.CardType]}{currentPlayer.PrevCard.Number}" : string.Empty;
                        }
                        else
                        {
                            type = TGMapper.CardTypesMapper.ContainsKey(currentPlayer.PrevCard.CardType) ?
                                TGMapper.CardTypesMapper[currentPlayer.PrevCard.CardType] : string.Empty;
                        }

                        string color = string.Empty;
                        if (currentPlayer.PrevCard.Color.HasValue)
                        {
                            color = TGMapper.CardColorsMapper.ContainsKey(currentPlayer.PrevCard.Color.Value) ?
                               TGMapper.CardColorsMapper[currentPlayer.PrevCard.Color.Value] : string.Empty;
                        }

                        string cardName = $"{color}{type}";
                        gameStateBuilder.AppendLine($@"目前牌：{cardName}");
                    }
                    gameStateBuilder.AppendLine();
                    gameStateBuilder.AppendLine("目前玩家順序和手牌數量：");
                    gameStateBuilder.AppendLine();
                    for (int i = 0; i < gameGroup.Players.Count; i++)
                    {
                        var player = gameGroup.Players.ToList()[i];
                        gameStateBuilder.AppendLine($@"{i + 1}. 玩家名稱：@{player.Username} - 共{player.HandCards.Count}張");
                    }
                }
                else
                {
                    gameStateBuilder.AppendLine($@"目前玩家：");
                    for (int i = 0; i < gameGroup.Players.Count; i++)
                    {
                        var player = gameGroup.Players.ToList()[i];
                        gameStateBuilder.AppendLine($@"{i + 1}. 玩家名稱：@{player.Username} - 共{player.HandCards.Count}張");
                    }
                }


                gameStateBuilder.AppendLine();
                gameStateBuilder.AppendLine($@"牌疊數：{gameGroup.Cards.Count} 張");
                gameStateBuilder.AppendLine();
                gameStateBuilder.AppendLine($@"棄牌數：{gameGroup.Discards.Count} 張");
            }
            res.Success = true;
            //res.Message = gameStateBuilder.ToString();
            res.AddPlayerAction(gameStateBuilder.ToString());
            return res;
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

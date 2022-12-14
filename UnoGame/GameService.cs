using CommonGameLib.Extensions;
using CommonGameLib.Services;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Types;
using UnoGame.Extensions;
using UnoGame.GameComponents;
using UnoGame.Telegram.Models;

namespace UnoGame
{
    public class GameService : IGameService
    {
        private readonly ICachedService _cachedService;
        private readonly ILogger<GameService> _logger;
        //private Dictionary<string, string> _gameGroupIdMapper;
        public string GroupIdMapper { get => "GroupIdMapper"; }
        public string GameGroupsKey { get => "GameGroups"; }

        public GameService(ICachedService cachedService, ILogger<GameService> logger)
        {
            _cachedService = cachedService;
            _logger = logger;
        }

        public async Task<List<string>?> GetGroupIdAsync(string userId)
        {
            var groups = await GetGameGrouopsAsync();
            if (groups != null)
            {
                var groupsId = groups.Where(g => g.Players.Select(p => p.Id).Contains(userId)).ToList();
            }
            return null;
        }

        public async Task<List<GameGroup>?> GetGameGrouopByUserAsync(string userId)
        {

            var groups = await GetGameGrouopsAsync();
            if (groups != null)
            {
                return groups.Where(g => g.Players.Select(p => p.Id).Contains(userId)).ToList();
            }
            return null;
        }

        public async Task<GameGroup?> GetGameGroupAsync(string groupId)
        {
            var gameGroups = await GetGameGrouopsAsync();
            if (gameGroups != null)
            {
                return gameGroups.FirstOrDefault(g => g.GroupId == groupId);
            }
            return null;
        }

        public async Task<List<GameGroup>> GetGameGrouopsAsync()
        {
            return await _cachedService.GetAndSetAsync(GameGroupsKey, new List<GameGroup>());
        }

        public async Task<ResponseInfo> NewGameAsync(string groupId, string groupName, Player host, List<Card> baseCards)
        {
            var res = new ResponseInfo();
            if (string.IsNullOrEmpty(groupId))
            {
                res.AddPlayerAction("請先把機器人加入到群組");
                return res;
            }

            if (string.IsNullOrEmpty(groupName))
            {
                res.AddPlayerAction("群組名稱不能為空");
                return res;
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
                    totalCards.Push(card.CloneNewCard());
                    totalCards.Push(card.CloneNewCard());
                }
                else
                {
                    totalCards.Push(card.CloneNewCard());
                    totalCards.Push(card.CloneNewCard());
                    totalCards.Push(card.CloneNewCard());
                    totalCards.Push(card.CloneNewCard());
                }
            }

            var gameGroups = await GetGameGrouopsAsync();
            if (gameGroups != null)
            {
                var isGroupNameExist = gameGroups.Where(g => g.GroupName == groupName).Count() > 0;

                var gameGroup = await GetGameGroupAsync(groupId);
                if (gameGroup == null && !isGroupNameExist)
                {
                    gameGroup = new GameGroup
                    {
                        GroupName = groupName,
                        GroupId = groupId,
                        Players = new Queue<Player>(),
                        Cards = totalCards,
                        Host = host
                    };
                    gameGroup.Players.Enqueue(host);
                    gameGroups.Add(gameGroup);
                    await SaveGameGroupsAsync(gameGroups, res);
                }
                else
                {
                    if (gameGroup.IsGameStart)
                    {
                        res.AddPlayerAction(@$"遊戲進行中...");
                        var player = gameGroup.Players.Peek();
                        res.AddPlayerAction(@$"目前輪到玩家 @{player.Username}");
                    }
                    else
                    {
                        res.AddPlayerAction(@$"已開局，開局者：@{host.Username}");
                    }
                    return res;
                }
            }
            res.AddPlayerAction(@$"開始新遊戲，開局者：@{host.Username}");
            return res;
        }

        public async Task<ResponseInfo> JoinBotPlayerAsync(string groupId, int number)
        {
            var res = new ResponseInfo();
            var gameGroup = await GetGameGroupAsync(groupId);
            if (gameGroup != null)
            {
                if (gameGroup.IsGameStart)
                {
                    res.AddPlayerAction("遊戲已經在進行中，無法加入新玩家...");
                    return res;
                }

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
                    var saveToCached = await SaveGameGroupAsync(currentGroup, res);
                    if (!saveToCached)
                    {
                        return res;
                    }

                    res.AddPlayerAction($@"已加入電腦玩家:{newBotPlayer.Username}");
                    return res;
                }
            }
            res.AddPlayerAction("遊戲尚未開局，請先執行開局指令 /new");

            return res;
        }

        public async Task<ResponseInfo> JoinPlayerAsync(string groupId, Player player)
        {
            var res = new ResponseInfo();

            var gameGroup = await GetGameGroupAsync(groupId);
            if (gameGroup != null)
            {
                if (gameGroup.IsGameStart)
                {
                    res.AddPlayerAction("遊戲已經在進行中，無法加入新玩家...");
                    return res;
                }

                player.Index = gameGroup.Players.Count;
                if (gameGroup.Players.Any(p => p.Id == player.Id))
                {
                    res.AddPlayerAction(@$"玩家 @{player.Username} 已經加入遊戲了");
                    return res;
                }


                gameGroup.Players.Enqueue(player);

                var saveToCached = await SaveGameGroupAsync(gameGroup, res);
                if (!saveToCached)
                {
                    return res;
                }

                StringBuilder msgBuilder = new StringBuilder();
                res.AddPlayerAction(@$"玩家 @{player.Username}加入遊戲");
                return res;
            }

            res.AddPlayerAction("遊戲尚未開局，請先執行開局指令 /new");
            return res;
        }

        public void CardNumberAction(GameGroup gameGroup, Card? throwCard, Player currentPlayer, ResponseInfo res)
        {
            gameGroup.Discards.Push(throwCard);
            currentPlayer.RemoveHandCards(throwCard);

            gameGroup.Players.Dequeue();
            gameGroup.Players.Enqueue(currentPlayer);

            var nextPlayer = gameGroup.Players.Peek();

            res.AddPlayerAction($@"玩家 @{currentPlayer.Username} 出牌:");
            //res.AddPlayerAction("", currentPlayer?.Username, throwCard, throwCard.ImageFile);

            gameGroup.CardOnBoard = throwCard;
        }

        public void ShowThrowCard(Player currentPlayer, Card throwCard, ResponseInfo res)
        {
            if (currentPlayer.IsBot)
            {
                res.AddPlayerAction($@"玩家 @{currentPlayer.Username} 出牌:");
                res.AddPlayerAction(message: "", imgFile: throwCard.ImageFile);
            }
            else
            {
                res.AddPlayerAction($@"玩家 @{currentPlayer.Username} 出牌: {throwCard.Color.GetCardColorName()}{throwCard.CardType.GetCardTypeName()}");
            }
        }

        public void CardSkipAction(GameGroup gameGroup, Card? throwCard, Player currentPlayer, ResponseInfo res)
        {
            gameGroup.Discards.Push(throwCard);
            currentPlayer.RemoveHandCards(throwCard);

            gameGroup.Players.Dequeue();
            gameGroup.Players.Enqueue(currentPlayer);

            var skipNextPlayer = gameGroup.Players.Dequeue();
            gameGroup.Players.Enqueue(skipNextPlayer);

            var nextPlayer = gameGroup.Players.Peek();

            ShowThrowCard(currentPlayer, throwCard, res);
            //if (currentPlayer.IsBot)
            //{
            //    res.AddPlayerAction($@"玩家 @{currentPlayer.Username} 出牌:");
            //    res.AddPlayerAction(message: "", imgFile: throwCard.ImageFile);
            //}
            //else
            //{
            //    res.AddPlayerAction($@"玩家 @{currentPlayer.Username} 出牌: {throwCard.Color.GetCardColorName()}{throwCard.CardType.GetCardTypeName()}");
            //}
            res.AddPlayerAction($@"跳過玩家 @{skipNextPlayer.Username}");

            gameGroup.CardOnBoard = throwCard;

        }

        public void CardDrawTwoAction(GameGroup gameGroup, Card? throwCard, Player currentPlayer, ResponseInfo res)
        {
            gameGroup.Discards.Push(throwCard);
            currentPlayer.RemoveHandCards(throwCard);

            gameGroup.Players.Dequeue();
            gameGroup.Players.Enqueue(currentPlayer);

            var skipNextPlayer = gameGroup.Players.Dequeue();
            for (int i = 0; i < 2; i++)
            {
                var card = gameGroup.Cards.Pop();
                skipNextPlayer.HandCards.Add(card);
            }
            gameGroup.Players.Enqueue(skipNextPlayer);

            var nextPlayer = gameGroup.Players.Peek();
            ShowThrowCard(currentPlayer, throwCard, res);

            //if (currentPlayer.IsBot)
            //{
            //    res.AddPlayerAction($@"玩家 @{currentPlayer.Username} 出牌:");
            //    res.AddPlayerAction(message: "", imgFile: throwCard.ImageFile);
            //}
            //else
            //{
            //    res.AddPlayerAction($@"玩家 @{currentPlayer.Username} 出牌: {throwCard.Color.GetCardColorName()}{throwCard.CardType.GetCardTypeName()}");
            //}
            res.AddPlayerAction($@"玩家 @{skipNextPlayer.Username} 抽兩張牌並跳過");


            gameGroup.CardOnBoard = throwCard;

        }

        public void CardReverseAction(GameGroup gameGroup, Card? throwCard, Player currentPlayer, ResponseInfo res)
        {
            gameGroup.Discards.Push(throwCard);
            currentPlayer = gameGroup.Players.FirstOrDefault(p => p.Id == currentPlayer.Id);
            if (currentPlayer != null)
            {
                currentPlayer.RemoveHandCards(throwCard);
            }

            gameGroup.Players = new Queue<Player>(gameGroup.Players.Reverse());

            var nextPlayer = gameGroup.Players.Peek();

            ShowThrowCard(currentPlayer, throwCard, res);

            //res.AddPlayerAction($@"玩家 @{currentPlayer.Username} 出牌:");
            //res.AddPlayerAction(message: "", imgFile: throwCard.ImageFile);
            res.AddPlayerAction($@"反轉");

            gameGroup.CardOnBoard = throwCard;

        }

        public void CardWildAction(GameGroup gameGroup, Card? throwCard, Player currentPlayer, ResponseInfo res, CardColor cardColor)
        {

            gameGroup.Discards.Push(throwCard);
            currentPlayer.RemoveHandCards(throwCard);

            gameGroup.Players.Dequeue();
            gameGroup.Players.Enqueue(currentPlayer);

            var nextPlayer = gameGroup.Players.Peek();
            throwCard.Color = cardColor;
            var colorStr = throwCard.Color.GetCardColorName();

            ShowThrowCard(currentPlayer, throwCard, res);
            //res.AddPlayerAction($@"玩家 @{currentPlayer.Username} 出牌:");
            //res.AddPlayerAction(message: "", imgFile: throwCard.ImageFile);
            res.AddPlayerAction($@"顏色選擇{colorStr}");

            gameGroup.CardOnBoard = throwCard;

        }

        public void CardWildDrawFourAction(GameGroup gameGroup, Card? throwCard, Player currentPlayer, ResponseInfo res, CardColor color)
        {

            gameGroup.Discards.Push(throwCard);
            currentPlayer.RemoveHandCards(throwCard);

            gameGroup.Players.Dequeue();
            gameGroup.Players.Enqueue(currentPlayer);

            var skipNextPlayer = gameGroup.Players.Dequeue();
            for (int i = 0; i < 4; i++)
            {
                var card = gameGroup.Cards.Pop();
                skipNextPlayer.HandCards.Add(card);
            }
            gameGroup.Players.Enqueue(skipNextPlayer);
            throwCard.Color = color;
            var colorName = throwCard.Color.GetCardColorName();

            //res.AddPlayerAction($@"玩家 @{currentPlayer.Username} 出牌:");
            //res.AddPlayerAction(message: "", imgFile: throwCard.ImageFile);
            ShowThrowCard(currentPlayer, throwCard, res);

            res.AddPlayerAction($@"顏色選擇{colorName}，玩家 @{skipNextPlayer.Username} 抽四張牌並跳過");


            gameGroup.CardOnBoard = throwCard;
        }

        public void CardPassAction(GameGroup gameGroup, Player currentPlayer, ResponseInfo res)
        {
            var newCard = gameGroup.Cards.Pop();
            currentPlayer.HandCards.Add(newCard);

            gameGroup.Players.Dequeue();
            gameGroup.Players.Enqueue(currentPlayer);

            var nextPlayer = gameGroup.Players.Peek();

            var number = gameGroup.CardOnBoard.CardType == CardType.Number ? gameGroup.CardOnBoard.Number.ToString() : string.Empty;

            res.AddPlayerAction($@"玩家 @{currentPlayer.Username} 跳過");
            res.AddPlayerAction($@"目前場上的牌 {gameGroup.CardOnBoard.Color.GetCardColorName()}{gameGroup.CardOnBoard.CardType.GetCardTypeName()}{number}");

        }


        private async Task<bool> CheckPlayerState(GameGroup gameGroup, Player player, Player nextPlayer, ResponseInfo res)
        {
            var playerHandCardCount = player.HandCards.Count;
            if (playerHandCardCount == 1)
            {
                res.AddPlayerAction($@"玩家 @{player.Username} UNO !!!");
            }

            if (playerHandCardCount == 0)
            {
                res.AddPlayerAction($@"玩家 @{player.Username} 獲勝 !!!");
                var gameGroups = await GetGameGrouopsAsync();
                if (gameGroups != null)
                {
                    gameGroups = gameGroups.Where(g => g.GroupId != gameGroup.GroupId).ToList();
                    var saveToCached = await SaveGameGroupsAsync(gameGroups, res);
                    if (!saveToCached)
                    {
                        res.AddPlayerAction("儲存GameGroups發生錯誤");
                    }
                }
                res.AddPlayerAction("遊戲結束");
                return true;
            }
            else
            {
                res.AddPlayerAction($"輪到玩家：@{nextPlayer.Username}");
            }
            return false;
        }

        private void CheckCardNeedShuffle(GameGroup gameGroup)
        {
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
        }



        public async Task<ResponseInfo> HandlePlayerAction(string groupId, string playerId, ImageFileInfo imgFile, CardColor? color = null, bool isPass = false)
        {
            ResponseInfo res = new ResponseInfo();

            var gameGroup = await GetGameGroupAsync(groupId);
            var playerSendCard = await GetPlayerCardAsync(imgFile.FileUniqueId, groupId, playerId);
            var player = await GetPlayerAsync(groupId, playerId);


            if (isPass && player != null && gameGroup != null)
            {
                CardPassAction(gameGroup, player, res);
            }
            else if (player != null && playerSendCard != null && gameGroup != null)
            {
                Card cardOnBoard = gameGroup.CardOnBoard;
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
                    CardPassAction(gameGroup, player, res);
                }
                else
                {

                    var currentColorName = cardOnBoard.Color.GetCardColorName();
                    var currentTypeName = cardOnBoard.CardType.GetCardTypeName();
                    res.AddPlayerAction(@$"不能出這張牌，必須與目前牌的顏色為<i>{currentColorName}</i>，類別為<i>{currentTypeName}</i>或數字相符的");
                }

            }

            var nextPlayer = gameGroup.Players.Peek();
            var gameOver = await CheckPlayerState(gameGroup, player, nextPlayer, res);
            if (!gameOver)
            {
                CheckCardNeedShuffle(gameGroup);

                await SaveGameGroupAsync(gameGroup, res);
            }

            return res;
        }

        public async Task HandleBotActionAsync(GameGroup gameGroup, ResponseInfo res)
        {
            while (gameGroup.Players.Peek().IsBot)
            {
                var currentPlayerQueue = gameGroup.Players.Peek();
                var hasCardWithType = currentPlayerQueue.HandCards.FirstOrDefault(c => c.CardType == gameGroup.CardOnBoard?.CardType);
                var hasCardWithColor = currentPlayerQueue.HandCards.FirstOrDefault(c => c.Color == gameGroup.CardOnBoard?.Color);
                var hasWildCard = currentPlayerQueue.HandCards.FirstOrDefault(c => c.CardType == CardType.Wild || c.CardType == CardType.WildDrawFour);
                Card cardOnBoard = gameGroup.CardOnBoard;

                // 判斷出啥牌
                if (hasCardWithColor != null && hasCardWithColor.CardType == CardType.Number && hasCardWithColor.Number == gameGroup.CardOnBoard.Number)
                {
                    CardNumberAction(gameGroup, hasCardWithColor, currentPlayerQueue, res);
                }
                else if (hasCardWithType != null && hasCardWithType.CardType == CardType.Number && hasCardWithType.Number == gameGroup.CardOnBoard.Number)
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
                    CardPassAction(gameGroup, currentPlayerQueue, res);
                }
                var nextPlayer = gameGroup.Players.Peek();

                var gameOver = await CheckPlayerState(gameGroup, currentPlayerQueue, nextPlayer, res);

                if (!gameOver)
                {
                    CheckCardNeedShuffle(gameGroup);
                    await SaveGameGroupAsync(gameGroup, res);
                }
            }


        }

        public async Task<ResponseInfo> StartGameAsync(string groupId, Player player)
        {
            var res = new ResponseInfo();
            var gameGroup = await GetGameGroupAsync(groupId);
            if (gameGroup == null)
            {
                res.AddPlayerAction("遊戲尚未開局，請先執行開局指令 /new");
                return res;
            }

            if (player.Id != gameGroup.Host.Id)
            {
                res.AddPlayerAction($@"必須由開局者 @{gameGroup.Host.Username}開始遊戲");
                return res;
            }

            if (gameGroup.Players.Count < 2)
            {
                res.AddPlayerAction($@"遊戲人數不夠，至少要兩人");
                return res;
            }

            if (gameGroup.IsGameStart)
            {
                res.AddPlayerAction($@"遊戲已經開始");
                return res;
            }


            gameGroup.Cards = gameGroup.Cards.Shuffle();
            gameGroup.IsGameStart = true;

            foreach (var p in gameGroup.Players.ToList())
            {
                for (int i = 0; i < 1; i++)
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
            gameGroup.CardOnBoard = firstCard;
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
            res.AddPlayerAction(imgFile: firstCard.ImageFile);

            await HandleBotActionAsync(gameGroup, res);

            await SaveGameGroupAsync(gameGroup, res);


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
                    res.AddPlayerAction($@"強制結束遊戲必須由開局者 @{player.Username} 操作");
                    return res;
                }
            }
            else
            {
                res.AddPlayerAction("遊戲尚未開局，請先執行開局指令 /new");
                return res;
            }

            var gameGroups = await GetGameGrouopsAsync();
            if (gameGroups != null)
            {
                if (gameGroups.Any(g => g.GroupId == groupId))
                {
                    var index = gameGroups.FindIndex(g => g.GroupId == groupId);
                    //gameGroups = gameGroups.Where(g => g.GroupId != groupId).ToList();
                    gameGroups.RemoveAt(index);
                    var saveToCached = await SaveGameGroupsAsync(gameGroups, res);
                    if (!saveToCached)
                    {
                        return res;
                    }
                }
            }
            res.AddPlayerAction("遊戲結束");

            return res;
        }
        public async Task<Card?> GetPlayerCardAsync(string uniqueFiledId, string groupId, string playerId)
        {
            var res = new ResponseInfo();
            var player = await GetPlayerAsync(groupId, playerId);
            if (player != null)
            {
                var card = player.HandCards.FirstOrDefault(c => c.ImageFile.FileUniqueId == uniqueFiledId);
                return card;
            }
            return null;
        }

        public async Task<Player?> GetPlayerAsync(string groupId, string playerId)
        {
            var gameGroups = await GetGameGrouopsAsync();
            if (gameGroups != null)
            {
                var gameGroup = gameGroups.Where(g => g.GroupId == groupId).FirstOrDefault();
                if (gameGroup != null)
                {
                    var player = gameGroup.Players.FirstOrDefault(p => p.Id == playerId);
                    return player;
                }
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
                    if (gameGroup.CardOnBoard != null)
                    {
                        string colorName = gameGroup.CardOnBoard.Color.GetCardColorName();
                        string typeName = gameGroup.CardOnBoard.CardType.GetCardTypeName();

                        string cardName = $"{colorName}{typeName}";
                        gameStateBuilder.AppendLine($@"目前場上的牌：{cardName}");
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
            else
            {
                res.AddPlayerAction("遊戲尚未開局，請先執行開局指令 /new");
                return res;
            }
            res.AddPlayerAction(gameStateBuilder.ToString());
            return res;
        }

        public async Task<ResponseInfo> PassGameAsync(string groupId, string playerId)
        {

            var gameGroup = await GetGameGroupAsync(groupId);
            var res = new ResponseInfo();
            if (gameGroup != null)
            {
                if (gameGroup.IsGameStart)
                {
                    var currentPlayer = gameGroup.Players.Peek();
                    if (playerId == currentPlayer.Id)
                    {
                        res = await HandlePlayerAction(groupId, playerId, gameGroup.CardOnBoard.ImageFile, null, true);
                        gameGroup = await GetGameGroupAsync(groupId);
                        if (gameGroup != null && gameGroup.IsGameStart)
                        {
                            var nextPlayer = gameGroup.Players.Peek();
                            if (nextPlayer.IsBot)
                            {
                                await HandleBotActionAsync(gameGroup, res);
                            }
                        }
                    }
                    else
                    {
                        res.AddPlayerAction($@"現在輪到玩家是： @{currentPlayer.Username}");
                        //await BotClient.SendTextMessageAsync(groupId, $@"現在輪到玩家是： @{currentPlayer.Username}");
                    }
                }
            }
            else
            {
                res.AddPlayerAction("遊戲尚未開局，請先執行開局指令 /new");
            }
            return res;
        }

        /// <summary>
        /// Save GameGroup to cached
        /// </summary>
        /// <param name="gameGroup"></param>
        /// <param name="res"></param>
        /// <returns></returns>
        public async Task<bool> SaveGameGroupAsync(GameGroup gameGroup, ResponseInfo res)
        {
            try
            {
                var gameGroups = await GetGameGrouopsAsync();
                var newGameGroups = gameGroups.Where(g => g.GroupId != gameGroup.GroupId).ToList();
                if (gameGroup != null)
                {
                    newGameGroups.Add(gameGroup);
                    return await _cachedService.SetAsync(GameGroupsKey, newGameGroups);
                }
                res.AddPlayerAction(@$"儲存GameGroups發生錯誤");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
                return false;
            }
        }

        private async Task<bool> SaveGameGroupsAsync(List<GameGroup> gameGroups, ResponseInfo res)
        {
            try
            {
                var result = await _cachedService.SetAsync(GameGroupsKey, gameGroups);
                if (!result)
                {
                    res.AddPlayerAction(@$"儲存GameGroup發生錯誤");
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
                return false;
            }
        }

    }
}

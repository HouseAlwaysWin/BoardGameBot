using AutoMapper;
using CommonGameLib.Services;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using System.Data;
using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Types.ReplyMarkups;
using UnoGame.Extensions;
using UnoGame.GameComponents;
using UnoGame.Telegram.Models;
using IOFile = System.IO.File;
using SixLabors.ImageSharp.Processing;
using System.Globalization;

namespace UnoGame.Telegram
{
    public class UnoTGBotService : IUnoTGBotService
    {
        private readonly ILogger<UnoTGBotService> _logger;
        public ITelegramBotClient BotClient { get; set; }
        private readonly IGameService _gameService;
        private readonly ICachedService _cachedService;
        private IMapper _mapper;
        private Dictionary<string, BotCommandInfo> _commands;

        private string ImgSourceRootPath = @"Source/UnoGame/Images";

        public delegate Card GetCard(string id, string uniqueFiledId, string fileId, string name, int number, string imageUrl, CardColor? color);

        public UnoTGBotService(
            //ITelegramBotClient botClient,
            IGameService gameService,
            ICachedService cachedService,
            ILogger<UnoTGBotService> logger,
            string token)
        {
            _logger = logger;
            BotClient = new TelegramBotClient(token);
            _gameService = gameService;
            _mapper = TGMapper.CreateMap();
            _commands = InitCommandsMapperAsync().Result;
            _cachedService = cachedService;
        }

        private async Task<Dictionary<string, BotCommandInfo>> InitCommandsMapperAsync()
        {
            var botInfo = await BotClient.GetMeAsync();
            var botName = $"{botInfo.Username}";
            var botCommandInfos = await GetBotCommandInfosAsync();
            Dictionary<string, BotCommandInfo> commands = new Dictionary<string, BotCommandInfo>();
            foreach (var cmd in botCommandInfos)
            {
                commands.Add($@"/{cmd.Command}", cmd);
                commands.Add($@"/{cmd.Command}@{botName}", cmd);
            }
            return commands;
        }

        public async Task InitCommands()
        {
            var botCommandInfos = await GetBotCommandInfosAsync();
            await BotClient.SetMyCommandsAsync(botCommandInfos);
        }

        private async Task<List<BotCommandInfo>> GetBotCommandInfosAsync()
        {
            List<BotCommandInfo> botCommandInfos = new List<BotCommandInfo>()
            {
                new(@$"new", "開始新局", async m => await NewGameAsync(m)),
                new(@$"start", "開始遊戲", async m => await StartGameAsync(m)),
                new(@$"pass", "跳過這局", async m => await PassGameAsync(m)),
                //to do new(@$"handcards", "顯示目前手牌", async m => await EndGameAsync(m)),
                new(@$"gamestate", "顯示遊戲狀態", async m => await ShowGameStateAsync(m)),
                new(@$"join", "加入遊戲", async m => await JoinPlayerAsync(m)),
                new(@$"joinbot", "加入電腦玩家", async m => await JoinBotPlayerAsync(m)),
                new(@$"end", "強制結束遊戲", async m => await EndGameAsync(m)),
                //to do new(@$"settings", "遊戲設定", async m => await EndGameAsync(m)),
                new(@$"test",  "test",async m => await TestAsync(m))
            };
            return botCommandInfos;
        }



        public async Task TestAsync(Message message)
        {
            //await InitCardStickers(message);
            //await BotClient.SendChatActionAsync(message.Chat.Id, ChatAction.Typing);

            //// Simulate longer running task
            //await Task.Delay(500);

            //var red = JsonConvert.SerializeObject(new CallBackDataMapper(SelectedColorKey, CardColor.Red.ToString(), ""));
            //var blue = JsonConvert.SerializeObject(new CallBackDataMapper(SelectedColorKey, CardColor.Blue.ToString(), ""));
            //var green = JsonConvert.SerializeObject(new CallBackDataMapper(SelectedColorKey, CardColor.Green.ToString(), ""));
            //var yellow = JsonConvert.SerializeObject(new CallBackDataMapper(SelectedColorKey, CardColor.Yellow.ToString(), ""));
            //InlineKeyboardMarkup inlineKeyboard = new(
            //  new[]
            //  {
            //                    new []
            //                    {
            //                        InlineKeyboardButton.WithCallbackData("紅色", red),
            //                        InlineKeyboardButton.WithCallbackData("藍色", blue),
            //                    },
            //                    new []
            //                    {
            //                        InlineKeyboardButton.WithCallbackData("綠色", green),
            //                        InlineKeyboardButton.WithCallbackData("黃色", yellow),
            //                    },
            //  });

            //var groupId = await _gameService.GetGroupIdAsync(message.From.Id.ToString());
            //var gameGroup = await _gameService.GetGameGroupAsync(groupId);
            //var player = await _gameService.GetPlayerAsync(message.From.Id.ToString());

            //foreach (var card in player.HandCards)
            //{
            //    var msg = await BotClient.SendStickerAsync(message.From.Id, card.FileId);

            //    await BotClient.DeleteMessageAsync(message.From.Id, msg.MessageId);
            //}


            //var msg = await BotClient.SendTextMessageAsync(chatId: message.From.Id,
            //                                       text: "Choose",
            //                                       replyMarkup: inlineKeyboard);





            ReplyKeyboardMarkup replyKeyboardMarkup = new(
               new[]
               {
                        new KeyboardButton[] { "pass" },
                        new KeyboardButton[] { "2.1", "2.2" },
               })
            {
                ResizeKeyboard = true
            };

            await BotClient.SendTextMessageAsync(chatId: message.Chat.Id,
                                                 text: "Choose",
                                                 replyMarkup: replyKeyboardMarkup);
        }


        /// <summary>
        /// Chat Begin
        /// </summary>
        /// <param name="update"></param>
        /// <returns></returns>
        public async Task HandleChat(Update update)
        {
            var handler = update.Type switch
            {
                UpdateType.Message => BotOnMessageAsync(update.Message!),
                UpdateType.EditedMessage => BotOnEditedMessageAsync(update.EditedMessage!),
                UpdateType.CallbackQuery => CallbackQueryAsync(update.CallbackQuery!),
                UpdateType.InlineQuery => InlineQueryAsync(update.InlineQuery!),
                UpdateType.ChosenInlineResult => ChosenInlineResultAsync(update.ChosenInlineResult!),
                _ => UnknownUpdateHandlerAsync(update)
            };
        }

        public async Task<StickerSet> GetCardStickersAsync(Message message)
        {

            var botInfo = await BotClient.GetMeAsync();
            var stickerName = $"botUnoCard_by_{botInfo.Username}";
            StickerSet stickerSet = new StickerSet();
            try
            {

                stickerSet = await BotClient.GetStickerSetAsync(name: stickerName);
                if (stickerSet.Stickers.Length != 54)
                {
                    foreach (var item in stickerSet.Stickers)
                    {
                        await BotClient.DeleteStickerFromSetAsync(item.FileId);
                    }
                    stickerSet = await BotClient.GetStickerSetAsync(name: stickerName);
                }

                await _cachedService.GetAndSetAsync(stickerName, stickerSet);
                return stickerSet;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }

            var sourceRootPath = @$"{AppContext.BaseDirectory}\Source\Images";
            var filesPath = Directory.GetFiles(sourceRootPath);

            List<ImageFileInfo> tgFiles = new List<ImageFileInfo>();
            foreach (var path in filesPath)
            {
                var fileBytes = await IOFile.ReadAllBytesAsync(path);
                using (Image image = Image.Load(fileBytes))
                using (MemoryStream ms = new MemoryStream())
                {
                    if (image.Height != 512)
                    {
                        image.Mutate(m => m.Resize(image.Width, 512));
                    }
                    image.SaveAsPng(ms);
                    ms.Seek(0, SeekOrigin.Begin);
                    try
                    {
                        var fileInfo = new FileInfo(path);
                        var file = await BotClient.UploadStickerFileAsync(
                            userId: botInfo.Id,
                            pngSticker: new InputFileStream(ms));
                        tgFiles.Add(new ImageFileInfo()
                        {
                            TGFile = file,
                            FileInfo = fileInfo
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.Log(LogLevel.Error, $"{ex}");
                        return stickerSet;
                    }
                }
            }

            int firstEmojiUnicode = 0x1F601;
            var emojiString = char.ConvertFromUtf32(firstEmojiUnicode);

            await BotClient.CreateNewStaticStickerSetAsync(
                userId: message.From.Id,
                name: stickerName,
                title: "Uno遊戲卡牌",
                pngSticker: tgFiles[0].TGFile.FileId,
                emojis: emojiString);

            for (int i = 0; i < tgFiles.Count; i++)
            {
                var tgFile = tgFiles[i];
                emojiString = char.ConvertFromUtf32(firstEmojiUnicode + i);
                await BotClient.AddStaticStickerToSetAsync(
                    userId: message.From.Id,
                    name: stickerName,
                    pngSticker: tgFile.TGFile.FileId,
                    emojis: emojiString);
            }
            stickerSet = await BotClient.GetStickerSetAsync(stickerName);
            return stickerSet;
        }


        private async Task BotOnEditedMessageAsync(Message message)
        {
            return;
        }

        private async Task BotOnMessageAsync(Message message)
        {
            if (message.Type == MessageType.Text)
            {
                var text = string.IsNullOrEmpty(message.Text) ? "" : message.Text;
                if (_commands.ContainsKey(text))
                {
                    _commands[text].Action.Invoke(message);
                }
            }
            else if (message.Type == MessageType.Sticker && message.ViaBot != null)
            {
                var playerId = message?.From?.Id.ToString();
                var groupId = await _gameService.GetGroupIdAsync(playerId);
                var stickerId = message?.Sticker?.FileId;
                var uniqueFiledId = message?.Sticker?.FileUniqueId;
                if (!string.IsNullOrEmpty(playerId) && !string.IsNullOrEmpty(stickerId))
                {
                    var res = await _gameService.HandlePlayerAction(playerId, uniqueFiledId, null);

                    var gameGroup = await _gameService.GetGameGroupAsync(groupId);
                    if (gameGroup != null && gameGroup.IsGameStart)
                    {
                        var currentPlayer = gameGroup.Players.Peek();
                        if (currentPlayer.IsBot)
                        {
                            await _gameService.HandleBotActionAsync(gameGroup, res);
                        }

                        if (res.NeedSelectedColor)
                        {
                            InlineKeyboardMarkup inlineKeyboard = new(
                              new[]
                              {
                                new []
                                {
                                    // data: key:colorNumber;stickerId
                                    InlineKeyboardButton.WithCallbackData("紅色", $"color;1;{uniqueFiledId}"),
                                    InlineKeyboardButton.WithCallbackData("藍色", $"color;2;{uniqueFiledId}"),
                                },
                                new []
                                {
                                    InlineKeyboardButton.WithCallbackData("綠色", $"color;3;{uniqueFiledId}"),
                                    InlineKeyboardButton.WithCallbackData("黃色", $"color;4;{uniqueFiledId}"),
                                },
                              });
                            var player = await _gameService.GetPlayerAsync(playerId);

                            res.AddPlayerAction(
                                message: $"玩家 @{player.Username} 請選擇顏色：",
                                replyMarkup: inlineKeyboard);
                        }

                    }
                    await HandleResponseAsync(groupId, res);
                }

            }

        }

        public async Task<bool> CheckGroupType(ChatId id, ChatType chatType)
        {
            if (chatType != ChatType.Group)
            {
                await BotClient.SendTextMessageAsync(
                chatId: id,
                text: "請先把機器人加入到群組");
                return false;
            }
            return true;
        }

        public async Task<bool> CheckPlayerNumberAsync(ChatId id, Queue<Player> players)
        {
            if (players.Count == 10)
            {
                await BotClient.SendTextMessageAsync(
                chatId: id,
                text: "已達最大玩家人數");
                return false;
            }
            return true;
        }

        private Card GetCardType(string id, string uniqueFiledId, string fileId, string name, int number, string imageUrl, CardType cardType, CardColor? color)
        {
            return new Card
            {
                Id = id,
                UniqueFileId = uniqueFiledId,
                FileId = fileId,
                Name = name,
                CardType = cardType,
                Color = color,
                Image = imageUrl,
                Number = number,
            };
        }


        public async Task NewGameAsync(Message message)
        {
            var groupId = message?.Chat?.Id.ToString();
            if (!await CheckGroupType(groupId, message.Chat.Type)) return;

            var stickers = await GetCardStickersAsync(message);
            List<Card> baseCards = new List<Card>();
            var sourceRootPath = @$"{AppContext.BaseDirectory}\{ImgSourceRootPath}";
            var filesPath = Directory.GetFiles(sourceRootPath);
            var fileInfos = new List<FileInfo>();
            foreach (var path in filesPath)
            {
                fileInfos.Add(new FileInfo(path));
            }

            List<CardTypeMapper> cardTypeMapper = new List<CardTypeMapper>()
            {
                new( @"(?<color>\w+)(?<number>\d+)\.png",
                    (id,uniqueFiledId,fileId,name,number,imgUrl,cardType,color) => GetCardType(id,uniqueFiledId,fileId,name,number,imgUrl,CardType.Number,color)),
                new( @"(?<color>\w+)DrawTwo.png",
                     (id,uniqueFiledId,fileId,name,number,imgUrl,cardType,color) => GetCardType(id,uniqueFiledId,fileId,name,-1,imgUrl,CardType.DrawTwo,color)),
                new(@"(?<color>\w+)Skip.png",
                     (id,uniqueFiledId,fileId,name,number,imgUrl,cardType,color) => GetCardType(id, uniqueFiledId, fileId, name, -1, imgUrl, CardType.Skip, color)),
                new(@"(?<color>\w+)Reverse.png",
                    (id,uniqueFiledId,fileId,name,number,imgUrl,cardType,color) => GetCardType(id, uniqueFiledId, fileId, name, -1, imgUrl, CardType.Reverse, color)),
                new(@"wild.png",
                     (id,uniqueFiledId,fileId,name,number,imgUrl,cardType,color) => GetCardType(id, uniqueFiledId, fileId, name, -1, imgUrl, CardType.Wild, null)),
                new(@"wildDrawFour.png",
                     (id,uniqueFiledId,fileId,name,number,imgUrl,cardType,color) => GetCardType(id, uniqueFiledId, fileId, name, -1, imgUrl, CardType.WildDrawFour, null))
        };

            for (int i = 0; i < stickers.Stickers.Length; i++)
            {
                var sticker = stickers.Stickers[i];
                var fileInfo = fileInfos[i];

                var mapCard = cardTypeMapper.FirstOrDefault(c => Regex.IsMatch(fileInfo.Name, c.RegexPattern));

                var numberStr = Regex.Matches(fileInfo.Name, mapCard.RegexPattern).Select(r => r.Groups["number"].Value).FirstOrDefault();
                int number = -1;
                if (!int.TryParse(numberStr, out number))
                {
                    number = -1;
                }

                CardColor? color = null;
                var colorName = Regex.Matches(fileInfo.Name, mapCard.RegexPattern).Select(r => r.Groups["color"].Value).FirstOrDefault();
                if (!string.IsNullOrEmpty(colorName))
                {
                    var cardColorName = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(colorName.ToLower());
                    color = (CardColor)Enum.Parse(typeof(CardColor), cardColorName);
                }
                string imgUrl = @$"{ImgSourceRootPath}/{fileInfo.Name}";

                var baseCard = mapCard.GetCardAction.Invoke(
                    id: Guid.NewGuid().ToString("N"),
                    uniqueFiledId: sticker.FileUniqueId,
                    fileId: sticker.FileId,
                    name: fileInfo.Name,
                    number: number,
                    cardType: CardType.Number,
                    imageUrl: imgUrl,
                    color: color);
                baseCards.Add(baseCard);
            }

            var host = _mapper.Map<Player>(message.From);
            var res = await _gameService.NewGameAsync(groupId, host, baseCards);
            await HandleResponseAsync(groupId, res);
        }

        /// <summary>
        /// Start Game
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public async Task StartGameAsync(Message message)
        {
            var groupId = message?.Chat?.Id.ToString();
            var player = _mapper.Map<User, Player>(message.From);
            var res = await _gameService.StartGameAsync(groupId, player);
            await HandleResponseAsync(groupId, res);
        }

        /// <summary>
        /// Join Player
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public async Task JoinPlayerAsync(Message message)
        {
            var groupId = message?.Chat?.Id.ToString();
            if (!await CheckGroupType(groupId, message.Chat.Type)) return;

            var gameGroup = await _gameService.GetGameGroupAsync(groupId);
            if (gameGroup != null)
            {
                if (!await CheckPlayerNumberAsync(groupId, gameGroup.Players)) return;
                var newPlayer = _mapper.Map<Player>(message.From);
                var res = await _gameService.JoinPlayerAsync(groupId, newPlayer);
                await HandleResponseAsync(groupId, res);

            }
        }

        /// <summary>
        /// Join Bot Player
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public async Task JoinBotPlayerAsync(Message message)
        {
            var groupId = message?.Chat?.Id.ToString();
            if (!await CheckGroupType(groupId, message.Chat.Type)) return;

            var gameGroup = await _gameService.GetGameGroupAsync(groupId);
            if (gameGroup != null)
            {

                if (!await CheckPlayerNumberAsync(groupId, gameGroup.Players)) return;
                var botPlayerCount = gameGroup.Players.Where(p => p.IsBot).Count() + 1;
                var res = await _gameService.JoinBotPlayerAsync(groupId, botPlayerCount);
                await HandleResponseAsync(groupId, res);
            }
        }


        /// <summary>
        /// Force Game over
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public async Task EndGameAsync(Message message)
        {
            var groupId = message?.Chat?.Id.ToString();
            var player = _mapper.Map<Player>(message.From);
            var res = await _gameService.EndGameAsync(groupId, player);
            await HandleResponseAsync(groupId, res);
        }

        public async Task ShowGameStateAsync(Message message)
        {
            var groupId = message?.Chat?.Id.ToString();
            if (!await CheckGroupType(groupId, message.Chat.Type)) return;

            var res = await _gameService.ShowGameStateAsync(groupId);
            await HandleResponseAsync(groupId, res);
        }

        public async Task PassGameAsync(Message message)
        {
            var groupId = message.Chat?.Id.ToString();
            var playerId = message.From?.Id.ToString();
            var gameGroup = await _gameService.GetGameGroupAsync(groupId);
            if (gameGroup != null)
            {
                if (gameGroup.IsGameStart)
                {
                    var currentPlayer = gameGroup.Players.Peek();
                    if (playerId == currentPlayer.Id)
                    {
                        var res = await _gameService.HandlePlayerAction(playerId, gameGroup.CardOnBoard.UniqueFileId, null, true);
                        gameGroup = await _gameService.GetGameGroupAsync(groupId);
                        if (gameGroup != null && gameGroup.IsGameStart)
                        {
                            var nextPlayer = gameGroup.Players.Peek();
                            if (nextPlayer.IsBot)
                            {
                                await _gameService.HandleBotActionAsync(gameGroup, res);
                            }

                        }
                        await HandleResponseAsync(groupId, res);
                    }
                    else
                    {
                        await BotClient.SendTextMessageAsync(groupId, $@"現在輪到玩家是： @{currentPlayer.Username}");
                    }
                }
            }
        }

        private async Task HandleResponseAsync(ChatId chatId, ResponseInfo res)
        {
            if (res != null)
            {
                foreach (var action in res.PlayerActions)
                {
                    await Task.Delay(1000);
                    if (!string.IsNullOrEmpty(action.Message))
                    {
                        await BotClient.SendTextMessageAsync(
                                 chatId: chatId,
                                 parseMode: ParseMode.Html,
                                 text: action.Message,
                                 replyMarkup: action.ReplyMarkup);
                    }
                    else if (!string.IsNullOrEmpty(action.FileId))
                    {
                        await BotClient.SendStickerAsync(chatId, action.FileId);
                    }
                }
            }
        }

        private async Task CallbackQueryAsync(CallbackQuery callbackQuery)
        {
            var groupId = callbackQuery.Message?.Chat?.Id.ToString();
            var playerId = callbackQuery.From.Id.ToString();
            var gameGroup = await _gameService.GetGameGroupAsync(groupId);
            if (gameGroup != null)
            {
                var currentPlayer = gameGroup.Players.Peek();

                if (playerId == currentPlayer.Id)
                {
                    if (callbackQuery.Data != null)
                    {
                        var callbackData = callbackQuery.Data.Split(';');
                        var key = callbackData[0];

                        if (key == "color")
                        {
                            var colorNumber = int.Parse(callbackData[1]);
                            var uniqueFiledId = callbackData[2];

                            // remove selected keyboard
                            await BotClient.EditMessageReplyMarkupAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId, null);
                            var color = colorNumber.GetCallbackCardColor();
                            var res = await _gameService.HandlePlayerAction(playerId, uniqueFiledId, color);
                            gameGroup = await _gameService.GetGameGroupAsync(groupId);
                            if (gameGroup != null)
                            {
                                var nextPlayer = gameGroup.Players.Peek();
                                if (nextPlayer.IsBot)
                                {
                                    await _gameService.HandleBotActionAsync(gameGroup, res);
                                }
                            }
                            await HandleResponseAsync(groupId, res);
                        }
                    }
                }
            }
        }

        private async Task InlineQueryAsync(InlineQuery inlineQuery)
        {
            List<InlineQueryResultCachedSticker> cards = new List<InlineQueryResultCachedSticker>();
            var playerId = inlineQuery?.From?.Id.ToString();
            var groupId = await _gameService.GetGroupIdAsync(inlineQuery.From.Id.ToString());
            var gameGroup = await _gameService.GetGameGroupAsync(groupId);
            if (gameGroup != null)
            {
                var player = await _gameService.GetPlayerAsync(playerId);
                if (player != null)
                {
                    foreach (var c in player.HandCards)
                    {
                        cards.Add(new InlineQueryResultCachedSticker(Guid.NewGuid().ToString(), c.FileId));
                    }
                }
            }

            await BotClient.AnswerInlineQueryAsync(inlineQuery.Id, cards, 0, true);
        }

        private async Task ChosenInlineResultAsync(ChosenInlineResult chosenInlineResult)
        {
        }

        private Task UnknownUpdateHandlerAsync(Update update)
        {
            _logger.LogInformation("Unknown update type: {UpdateType}", update.Type);
            return Task.CompletedTask;
        }
    }
}

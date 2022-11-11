﻿using AutoMapper;
using CommonGameLib.Services;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Types.ReplyMarkups;
using UnoGame.Extensions;
using UnoGame.GameComponents;
using UnoGame.Telegram.Models;
using TGFile = Telegram.Bot.Types.File;
using IOFile = System.IO.File;
using SixLabors.ImageSharp.Processing;
using Microsoft.AspNetCore.Http.Connections;
using AutoMapper.Execution;
using System.Drawing;
using System.IO;
using System.Globalization;
using System.Xml.Linq;
using Telegram.Bot.Requests;
using Newtonsoft.Json;
using static System.Collections.Specialized.BitVector32;

namespace UnoGame.Telegram
{
    public class TelegramBotService : ITelegramBotService
    {
        private readonly ILogger<TelegramBotService> _logger;
        private readonly ITelegramBotClient _botClient;
        private readonly IGameService _gameService;
        private readonly ICachedService _cachedService;
        private IMapper _mapper;
        private Dictionary<string, BotCommandInfo> _commands;

        private string SelectedColorKey = "SelectedColor";
        private string ImgSourceRootPath = @"Source/Images";

        public delegate Card GetCard(string id, string uniqueFiledId, string fileId, string name, int number, string imageUrl, CardColor? color);

        public TelegramBotService(
            ITelegramBotClient botClient,
            IGameService gameService,
            ICachedService cachedService,
            ILogger<TelegramBotService> logger)
        {
            _logger = logger;
            _botClient = botClient;
            _gameService = gameService;
            _mapper = TGMapper.CreateMap();
            _commands = InitCommandsMapperAsync().Result;
            _cachedService = cachedService;
        }

        private async Task<Dictionary<string, BotCommandInfo>> InitCommandsMapperAsync()
        {
            var botInfo = await _botClient.GetMeAsync();
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
            await _botClient.SetMyCommandsAsync(botCommandInfos);
        }

        private async Task<List<BotCommandInfo>> GetBotCommandInfosAsync()
        {
            List<BotCommandInfo> botCommandInfos = new List<BotCommandInfo>()
            {
                new(@$"new", "開始新局", async m => await SendNewGameAsync(m)),
                new(@$"start", "開始遊戲", async m => await StartGameAsync(m)),
                new(@$"end", "強制結束遊戲", async m => await EndGameAsync(m)),
                new(@$"join", "加入遊戲", async m => await JoinPlayerAsync(m)),
                new(@$"joinbot", "加入電腦玩家", async m => await JoinBotPlayerAsync(m)),
                //new(@$"players", "顯示目前玩家", async m => await ShowPlayersAsync(m)),
                new(@$"gamestate", "顯示遊戲狀態", async m => await ShowGameStateAsync(m)),
                new(@$"pass", "跳過這局", async m => await PassGameAsync(m)),
                new(@$"test",  "test",async m => await TestAsync(m))
            };
            return botCommandInfos;
        }


        public async Task<StickerSet> GetCardStickersAsync(Message message)
        {

            var botInfo = await _botClient.GetMeAsync();
            var stickerName = $"botUnoCard_by_{botInfo.Username}";
            StickerSet stickerSet = new StickerSet();
            try
            {
                stickerSet = await _botClient.GetStickerSetAsync(name: stickerName);
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
                    //image.Mutate(m => m.Resize(512, 512));
                    image.SaveAsPng(ms);
                    ms.Seek(0, SeekOrigin.Begin);
                    try
                    {
                        var fileInfo = new FileInfo(path);
                        var file = await _botClient.UploadStickerFileAsync(
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

            await _botClient.CreateNewStaticStickerSetAsync(
                userId: message.From.Id,
                name: stickerName,
                title: "Uno遊戲卡牌",
                pngSticker: tgFiles[0].TGFile.FileId,
                emojis: emojiString);

            for (int i = 0; i < tgFiles.Count; i++)
            {
                var tgFile = tgFiles[i];
                emojiString = char.ConvertFromUtf32(firstEmojiUnicode + i);
                await _botClient.AddStaticStickerToSetAsync(
                    userId: message.From.Id,
                    name: stickerName,
                    pngSticker: tgFile.TGFile.FileId,
                    emojis: emojiString);
            }
            stickerSet = await _botClient.GetStickerSetAsync(stickerName);
            return stickerSet;

            //stickerSet = await _botClient.GetStickerSetAsync(
            //   name: stickerName);

            //await _botClient.SendStickerAsync(
            //    chatId: message.Chat.Id,
            //    sticker: stickerSet.Stickers[0].FileId);
        }

        public async Task TestAsync(Message message)
        {
            //await InitCardStickers(message);
            //await _botClient.SendChatActionAsync(message.Chat.Id, ChatAction.Typing);

            //// Simulate longer running task
            //await Task.Delay(500);

            var red = JsonConvert.SerializeObject(new CallBackDataMapper(SelectedColorKey, CardColor.Red.ToString(), ""));
            var blue = JsonConvert.SerializeObject(new CallBackDataMapper(SelectedColorKey, CardColor.Blue.ToString(), ""));
            var green = JsonConvert.SerializeObject(new CallBackDataMapper(SelectedColorKey, CardColor.Green.ToString(), ""));
            var yellow = JsonConvert.SerializeObject(new CallBackDataMapper(SelectedColorKey, CardColor.Yellow.ToString(), ""));
            InlineKeyboardMarkup inlineKeyboard = new(
              new[]
              {
                                new []
                                {
                                    InlineKeyboardButton.WithCallbackData("紅色", red),
                                    InlineKeyboardButton.WithCallbackData("藍色", blue),
                                },
                                new []
                                {
                                    InlineKeyboardButton.WithCallbackData("綠色", green),
                                    InlineKeyboardButton.WithCallbackData("黃色", yellow),
                                },
              });


            var msg = await _botClient.SendTextMessageAsync(chatId: message.Chat.Id,
                                                   text: "Choose",
                                                   replyMarkup: inlineKeyboard);


            //ReplyKeyboardMarkup replyKeyboardMarkup = new(
            //   new[]
            //   {
            //            new KeyboardButton[] { "1.1", "1.2","1.3","1.4" },
            //            new KeyboardButton[] { "2.1", "2.2" },
            //   })
            //{
            //    ResizeKeyboard = true
            //};

            //await _botClient.SendTextMessageAsync(chatId: message.Chat.Id,
            //                                     text: "Choose",
            //                                     replyMarkup: replyKeyboardMarkup);
        }


        public async Task EchoAsync(Update update)
        {
            var handler = update.Type switch
            {
                UpdateType.Message => BotOnMessageAsync(update.Message!),
                UpdateType.EditedMessage => BotOnEditedMessageAsync(update.EditedMessage!),
                UpdateType.CallbackQuery => BotOnCallbackQueryAsync(update.CallbackQuery!),
                UpdateType.InlineQuery => BotOnInlineQueryAsync(update.InlineQuery!),
                UpdateType.ChosenInlineResult => BotOnChosenInlineResultAsync(update.ChosenInlineResult!),
                _ => UnknownUpdateHandlerAsync(update)
            };
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
                    if (gameGroup != null)
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
                                    InlineKeyboardButton.WithCallbackData("綠色", $"color:3;{uniqueFiledId}"),
                                    InlineKeyboardButton.WithCallbackData("黃色", $"color;4;{uniqueFiledId}"),
                                },
                              });
                            var player = await _gameService.GetPlayerAsync(playerId);

                            res.AddPlayerAction(
                                message: $"玩家 @{player.Username} 請選擇顏色：",
                                replyMarkup: inlineKeyboard);
                        }

                        await HandleResponseAsync(groupId, res);
                    }
                }

            }

        }

        public async Task<bool> CheckChatTypeAsync(ChatId id, ChatType chatType)
        {
            if (chatType != ChatType.Group)
            {
                await _botClient.SendTextMessageAsync(
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
                await _botClient.SendTextMessageAsync(
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


        public async Task SendNewGameAsync(Message message)
        {
            var groupId = message?.Chat?.Id.ToString();
            if (!await CheckChatTypeAsync(groupId, message.Chat.Type)) return;

            var stickers = await GetCardStickersAsync(message);
            List<Card> baseCards = new List<Card>();
            var sourceRootPath = @$"{AppContext.BaseDirectory}\Source\Images";
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

        public async Task JoinPlayerAsync(Message message)
        {
            var groupId = message?.Chat?.Id.ToString();
            if (!await CheckChatTypeAsync(groupId, message.Chat.Type)) return;

            var gameGroup = await _gameService.GetGameGroupAsync(groupId);
            if (gameGroup != null)
            {
                if (!await CheckPlayerNumberAsync(groupId, gameGroup.Players)) return;
                var newPlayer = _mapper.Map<Player>(message.From);
                var res = await _gameService.JoinPlayerAsync(groupId, newPlayer);
                await HandleResponseAsync(groupId, res);

            }
        }

        public async Task JoinBotPlayerAsync(Message message)
        {
            var groupId = message?.Chat?.Id.ToString();
            if (!await CheckChatTypeAsync(groupId, message.Chat.Type)) return;

            var gameGroup = await _gameService.GetGameGroupAsync(groupId);
            if (gameGroup != null)
            {

                if (!await CheckPlayerNumberAsync(groupId, gameGroup.Players)) return;
                var botPlayerCount = gameGroup.Players.Where(p => p.IsBot).Count() + 1;
                var res = await _gameService.JoinBotPlayerAsync(groupId, botPlayerCount);
                await HandleResponseAsync(groupId, res);
            }
        }

        public async Task StartGameAsync(Message message)
        {
            var groupId = message?.Chat?.Id.ToString();
            var player = _mapper.Map<User, Player>(message.From);
            var res = await _gameService.StartGameAsync(groupId, player);
            await HandleResponseAsync(groupId, res);
        }

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
            if (!await CheckChatTypeAsync(groupId, message.Chat.Type)) return;

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
                var currentPlayer = gameGroup.Players.Peek();
                if (playerId == currentPlayer.Id)
                {
                    var res = await _gameService.HandlePlayerAction(playerId, gameGroup.CardOnBoard.UniqueFileId, null, true);
                    gameGroup = await _gameService.GetGameGroupAsync(groupId);
                    var nextPlayer = gameGroup.Players.Peek();
                    if (nextPlayer.IsBot)
                    {
                        await _gameService.HandleBotActionAsync(gameGroup, res);
                    }

                    await HandleResponseAsync(groupId, res);
                }
                else
                {
                    await _botClient.SendTextMessageAsync(groupId, $@"現在輪到玩家是： @{currentPlayer.Username}");
                }
            }
        }

        private async Task HandleResponseAsync(ChatId chatId, ResponseInfo res)
        {
            if (res != null)
            {
                foreach (var action in res.PlayerActions)
                {
                    if (!string.IsNullOrEmpty(action.Message))
                    {
                        await _botClient.SendTextMessageAsync(
                                 chatId: chatId,
                                 text: action.Message,
                                 replyMarkup: action.ReplyMarkup);
                    }
                    else if (!string.IsNullOrEmpty(action.FileId))
                    {
                        await _botClient.SendStickerAsync(chatId, action.FileId);
                    }
                }
            }
        }

        private async Task BotOnCallbackQueryAsync(CallbackQuery callbackQuery)
        {


            var groupId = callbackQuery.Message?.Chat?.Id.ToString();
            var playerId = callbackQuery.From.Id.ToString();
            var gameGroup = await _gameService.GetGameGroupAsync(groupId);
            var currentPlayer = gameGroup.Players.Peek();
            var callbackData = callbackQuery.Data.Split(';');
            var key = callbackData[0];
            var colorNumber = int.Parse(callbackData[1]);
            var uniqueFiledId = callbackData[2];

            if (playerId == currentPlayer.Id)
            {
                if (key == "color")
                {
                    // remove selected keyboard
                    await _botClient.EditMessageReplyMarkupAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId, null);
                    var color = TGMapper.CallbackColorMapper[colorNumber];
                    var res = await _gameService.HandlePlayerAction(playerId, uniqueFiledId, color);
                    gameGroup = await _gameService.GetGameGroupAsync(groupId);
                    var nextPlayer = gameGroup.Players.Peek();
                    if (nextPlayer.IsBot)
                    {
                        await _gameService.HandleBotActionAsync(gameGroup, res);
                    }
                    await HandleResponseAsync(groupId, res);
                }
            }
            else
            {
                await _botClient.SendTextMessageAsync(groupId, $@"現在輪到玩家是： @{currentPlayer.Username}");
            }
        }

        private async Task BotOnInlineQueryAsync(InlineQuery inlineQuery)
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

            await _botClient.AnswerInlineQueryAsync(inlineQuery.Id, cards, 0, true);
        }

        private async Task BotOnChosenInlineResultAsync(ChosenInlineResult chosenInlineResult)
        {
        }

        private Task UnknownUpdateHandlerAsync(Update update)
        {
            _logger.LogInformation("Unknown update type: {UpdateType}", update.Type);
            return Task.CompletedTask;
        }
    }
}

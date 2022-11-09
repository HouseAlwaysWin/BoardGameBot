using AutoMapper;
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
        private string StickerName = "UnoGameCard";
        private string ImgSourceRootPath = @"Source/Images";
        private StickerSet stickerSet;

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
            _mapper = TGDtoMapper.CreateMap();
            _commands = InitCommandsMapper().Result;
            _cachedService = cachedService;
        }

        private async Task<Dictionary<string, BotCommandInfo>> InitCommandsMapper()
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

        private async Task<List<BotCommandInfo>> GetBotCommandInfosAsync()
        {
            List<BotCommandInfo> botCommandInfos = new List<BotCommandInfo>()
            {
                new(@$"new", "開始新局", async m => await SendNewGame(m)),
                new(@$"join", "加入遊戲", async m => await JoinPlayer(m)),
                new(@$"players", "顯示目前玩家", async m => await ShowPlayers(m)),
                new(@$"test",  "test",async m => await TestAsync(m))
            };
            return botCommandInfos;
        }

        public async Task InitCommands()
        {
            var botCommandInfos = await GetBotCommandInfosAsync();
            await _botClient.SetMyCommandsAsync(botCommandInfos);
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

            //InlineKeyboardMarkup inlineKeyboard = new(
            //    new[]
            //    {
            //        // first row
            //        new []
            //        {
            //            InlineKeyboardButton.WithCallbackData("1.1", "11"),
            //            InlineKeyboardButton.WithCallbackData("1.2", "12"),
            //        },
            //        // second row
            //        new []
            //        {
            //            InlineKeyboardButton.WithCallbackData("2.1", "21"),
            //            InlineKeyboardButton.WithCallbackData("2.2", "22"),
            //        },
            //    });

            //await _botClient.SendTextMessageAsync(chatId: message.Chat.Id,
            //                                     text: "Choose",
            //                                     replyMarkup: inlineKeyboard);



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
                UpdateType.Message => BotOnMessageReceived(update.Message!),
                UpdateType.EditedMessage => BotOnMessageReceived(update.EditedMessage!),
                UpdateType.CallbackQuery => BotOnCallbackQueryReceived(update.CallbackQuery!),
                UpdateType.InlineQuery => BotOnInlineQueryReceived(update.InlineQuery!),
                UpdateType.ChosenInlineResult => BotOnChosenInlineResultReceived(update.ChosenInlineResult!),
                _ => UnknownUpdateHandlerAsync(update)
            };
        }

        private async Task BotOnMessageReceived(Message message)
        {
            if (message.Type != MessageType.Text)
                return;

            var text = string.IsNullOrEmpty(message.Text) ? "" : message.Text;
            if (_commands.ContainsKey(text))
            {
                _commands[text].Action.Invoke(message);
            }

        }

        public async Task<bool> CheckChatType(ChatId id, ChatType chatType)
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


        public async Task SendNewGame(Message message)
        {
            if (!await CheckChatType(message.Chat.Id, message.Chat.Type)) return;

            var stickers = await GetCardStickersAsync(message);
            List<Card> baseCards = new List<Card>();
            var sourceRootPath = @$"{AppContext.BaseDirectory}\Source\Images";
            var filesPath = Directory.GetFiles(sourceRootPath);
            var fileInfos = new List<FileInfo>();
            foreach (var path in filesPath)
            {
                fileInfos.Add(new FileInfo(path));
            }

            Dictionary<string, GetCard> cardTypeMapper = new Dictionary<string, GetCard>()
            {
                { @"(?<color>\w+)(?<number>\d+)\.png",
                    (id,  uniqueFiledId,  fileId, name,number,imgUrl,color) => GetCardType(id,uniqueFiledId,fileId,name,number,imgUrl,CardType.Number,color)
                },
                { @"(?<color>\w+)DrawTwo.png",
                     (id,  uniqueFiledId,  fileId, name,number,imgUrl,color) => GetCardType(id,uniqueFiledId,fileId,name,-1,imgUrl,CardType.DrawTwo,color)
                },
                { @"(?<color>\w+)Skip.png",
                     (id,  uniqueFiledId,  fileId, name,number,imgUrl,color) => GetCardType(id,uniqueFiledId,fileId,name,-1,imgUrl,CardType.Skip,color)
                },
                { @"(?<color>\w+)Reverse.png",
                    (id,  uniqueFiledId,  fileId, name,number,imgUrl,color)  => GetCardType(id,uniqueFiledId,fileId,name,-1,imgUrl,CardType.Reverse,color)
                },
                 { @"wild.png",
                     (id,  uniqueFiledId,  fileId, name,number,imgUrl,color)  =>  GetCardType(id,uniqueFiledId,fileId,name,-1,imgUrl,CardType.Wild,null)
                },
                { @"wildDrawFour.png",
                     (id,  uniqueFiledId,  fileId, name,number,imgUrl,color)  =>  GetCardType(id,uniqueFiledId,fileId,name,-1,imgUrl,CardType.WildDrawFour,null)
                }
            };

            for (int i = 0; i < stickers.Stickers.Length; i++)
            {
                var sticker = stickers.Stickers[i];
                var fileInfo = fileInfos[i];

                var mapCard = cardTypeMapper.FirstOrDefault(c => Regex.IsMatch(fileInfo.Name, c.Key));

                var numberStr = Regex.Matches(fileInfo.Name, mapCard.Key).Select(r => r.Groups["number"].Value).FirstOrDefault();
                int number = -1;
                if (!int.TryParse(numberStr, out number))
                {
                    number = -1;
                }

                CardColor? color = null;
                var colorName = Regex.Matches(fileInfo.Name, mapCard.Key).Select(r => r.Groups["color"].Value).FirstOrDefault();
                if (!string.IsNullOrEmpty(colorName))
                {
                    var cardColorName = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(colorName.ToLower());
                    color = (CardColor)Enum.Parse(typeof(CardColor), cardColorName);
                }
                string imgUrl = @$"{ImgSourceRootPath}/{fileInfo.Name}";

                var baseCard = mapCard.Value.Invoke(Guid.NewGuid().ToString("N"),
                    sticker.FileId, sticker.FileUniqueId, fileInfo.Name, number, imgUrl, color);
                baseCards.Add(baseCard);
            }

            var host = _mapper.Map<Player>(message.From);
            var res = await _gameService.NewGameAsync(message?.Chat?.Id.ToString(), host, baseCards);
            await _botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: res.Message);
        }

        public async Task JoinPlayer(Message message)
        {
            if (await CheckChatType(message.Chat.Id, message.Chat.Type)) return;

            var newPlayer = _mapper.Map<Player>(message.From);
            var res = await _gameService.JoinPlayerAsync(message?.Chat?.Id.ToString(), newPlayer);
            await _botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: res.Message);
        }

        public async Task ShowPlayers(Message message)
        {
            if (await CheckChatType(message.Chat.Id, message.Chat.Type)) return;

            var res = await _gameService.GetPlayersAsync(message?.Chat?.Id.ToString());
            await _botClient.SendTextMessageAsync(
              chatId: message.Chat.Id,
              text: res.Message);
        }


        private async Task BotOnCallbackQueryReceived(CallbackQuery callbackQuery)
        {
        }

        private async Task BotOnInlineQueryReceived(InlineQuery inlineQuery)
        {

            List<InlineQueryResultCachedSticker> cards = new List<InlineQueryResultCachedSticker>();
            var botInfo = await _botClient.GetMeAsync();
            var sourceRootPath = @$"{AppContext.BaseDirectory}\Source\Images";
            var gameGroups = await _cachedService.GetAsync<Dictionary<string, GameGroup>>("GameGroups");
            var groupId = await _gameService.GetGroupId(inlineQuery.From.Id.ToString());
            if (gameGroups.ContainsKey(groupId))
            {
                var gameGroup = gameGroups[groupId];
                foreach (var c in gameGroup.Cards)
                {
                    cards.Add(new InlineQueryResultCachedSticker(c.UniqueFileId, c.FileId));
                }





                //var filesPath = Directory.GetFiles(sourceRootPath);

                //var fileBytes = await IOFile.ReadAllBytesAsync(filesPath[0]);
                //using (Image image = Image.Load(fileBytes))
                //using (MemoryStream ms = new MemoryStream())
                //{
                //    image.SaveAsJpeg(ms);
                //    ms.Seek(0, SeekOrigin.Begin);

                //    //await _botClient.SendPhotoAsync(
                //    //    chatId: inlineQuery.,
                //    //    new InputOnlineFile(ms));

                //    var file = await _botClient.UploadStickerFileAsync(
                //        userId: botInfo.Id,
                //        pngSticker: new InputFileStream(ms));

                //    cards.Add(new InlineQueryResultCachedSticker(file.FileUniqueId, file.FileId));
                //    //await _botClient.SendPhotoAsync(
                //    //    chatId: botInfo.Id,
                //    //    file.FileId);
                //    //await _botClient.SendStickerAsync(
                //    //    chatId: message.Chat.Id,
                //    //    sticker: file.FileId);
                //}


                //List<InlineQueryResultArticle> cards = new List<InlineQueryResultArticle>
                //{
                //    new InlineQueryResultArticle(Guid.NewGuid().ToString(),"奶妹",new InputTextMessageContent("奶妹")),
                //    new InlineQueryResultArticle(Guid.NewGuid().ToString(),"坑妹1號",new InputTextMessageContent("坑妹1號")),
                //    new InlineQueryResultArticle(Guid.NewGuid().ToString(),"坑妹2號",new InputTextMessageContent("坑妹2號")),
                //    new InlineQueryResultArticle(Guid.NewGuid().ToString(),"坑妹3號",new InputTextMessageContent("坑妹3號"))
                //};

                await _botClient.AnswerInlineQueryAsync(inlineQuery.Id, cards, 0, true);
            }
        }

        private async Task BotOnChosenInlineResultReceived(ChosenInlineResult chosenInlineResult)
        {
        }

        private Task UnknownUpdateHandlerAsync(Update update)
        {
            _logger.LogInformation("Unknown update type: {UpdateType}", update.Type);
            return Task.CompletedTask;
        }
    }
}

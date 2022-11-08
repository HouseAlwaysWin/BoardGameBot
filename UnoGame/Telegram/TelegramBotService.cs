using AutoMapper;
using CommonGameLib.Services;
using Microsoft.Extensions.Logging;
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
using Telegram.Bot.Types.ReplyMarkups;
using UnoGame.Extensions;
using UnoGame.GameComponents;
using UnoGame.Telegram.Models;

namespace UnoGame.Telegram
{
    public class TelegramBotService : ITelegramBotService
    {
        private readonly ILogger<TelegramBotService> _logger;
        private readonly ITelegramBotClient _botClient;
        private readonly IGameService _gameService;
        private IMapper _mapper;
        private Dictionary<string, BotCommandInfo> _commands;

        public TelegramBotService(
            ITelegramBotClient botClient,
            IGameService gameService,
            ILogger<TelegramBotService> logger)
        {
            _logger = logger;
            _botClient = botClient;
            _gameService = gameService;
            _mapper = TGDtoMapper.CreateMap();
            _commands = InitCommandsMapper().Result;
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

        public async Task InitCardStickers()
        {
        }

        public async Task TestAsync(Message message)
        {
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
                // UpdateType.Unknown:
                // UpdateType.ChannelPost:
                // UpdateType.EditedChannelPost:
                // UpdateType.ShippingQuery:
                // UpdateType.PreCheckoutQuery:
                // UpdateType.Poll:
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

        public async Task SendNewGame(Message message)
        {
            if (await CheckChatType(message.Chat.Id, message.Chat.Type)) return;

            var host = _mapper.Map<Player>(message.From);
            var res = await _gameService.NewGameAsync(message?.Chat?.Id.ToString(), host);
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

            List<InlineQueryResultPhoto> cards = new List<InlineQueryResultPhoto>
            {
                //new InlineQueryResultPhoto(Guid.NewGuid().ToString(),)
            };

            //List<InlineQueryResultArticle> cards = new List<InlineQueryResultArticle>
            //{
            //    new InlineQueryResultArticle(Guid.NewGuid().ToString(),"奶妹",new InputTextMessageContent("奶妹")),
            //    new InlineQueryResultArticle(Guid.NewGuid().ToString(),"坑妹1號",new InputTextMessageContent("坑妹1號")),
            //    new InlineQueryResultArticle(Guid.NewGuid().ToString(),"坑妹2號",new InputTextMessageContent("坑妹2號")),
            //    new InlineQueryResultArticle(Guid.NewGuid().ToString(),"坑妹3號",new InputTextMessageContent("坑妹3號"))
            //};

            await _botClient.AnswerInlineQueryAsync(inlineQuery.Id, cards, 0, true);
        }

        private async Task BotOnChosenInlineResultReceived(ChosenInlineResult chosenInlineResult)
        {
        }

        private Task UnknownUpdateHandlerAsync(Update update)
        {
            _logger.LogInformation("Unknown update type: {UpdateType}", update.Type);
            return Task.CompletedTask;
        }

        public async Task CreateNewGame(Update update)
        {

        }
    }
}

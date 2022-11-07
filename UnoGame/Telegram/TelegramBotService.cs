using AutoMapper;
using CommonGameLib.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using UnoGame.Extensions;
using UnoGame.GameComponents;

namespace UnoGame.Telegram
{
    public class TelegramBotService : ITelegramBotService
    {
        private readonly ILogger<TelegramBotService> _logger;
        private readonly ITelegramBotClient _botClient;
        private readonly IGameService _gameService;
        private IMapper _mapper;
        private Dictionary<string, Action<Message>> _commands;

        public TelegramBotService(
            ITelegramBotClient botClient,
            IGameService gameService,
            ILogger<TelegramBotService> logger)
        {
            _logger = logger;
            _botClient = botClient;
            _gameService = gameService;
            _mapper = DTOMapper.CreateMap();

            InitCommands();
        }

        private void InitCommands()
        {
            var botInfo = _botClient.GetMeAsync().Result;
            var botName = $"{botInfo.Username}";
            _commands = new Dictionary<string, Action<Message>>
            {
                { @"/new",  async m => await SendNewGame(m) },
                { @$"/new@{botName}",  async m => await SendNewGame(m) },
                { @"/join",  async m => await JoinPlayer(m) },
                { @$"/join@{botName}",  async m => await JoinPlayer(m) }
            };
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
                _commands[text].Invoke(message);
            }

        }

        public async Task SendNewGame(Message message)
        {
            var host = _mapper.Map<Player>(message.From);
            var res = await _gameService.NewGameAsync(message?.Chat?.Id.ToString(), host);
            await _botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: res.Message);
        }

        public async Task JoinPlayer(Message message)
        {
            var newPlayer = _mapper.Map<Player>(message.From);
            var res = await _gameService.JoinPlayerAsync(message?.Chat?.Id.ToString(), newPlayer);
            await _botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: res.Message);
        }


        private async Task BotOnCallbackQueryReceived(CallbackQuery callbackQuery)
        {
        }

        private async Task BotOnInlineQueryReceived(InlineQuery inlineQuery)
        {
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

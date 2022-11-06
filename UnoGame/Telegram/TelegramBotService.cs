using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace UnoGame.Telegram
{
    public class TelegramBotService : ITelegramBotService
    {
        private readonly ILogger<TelegramBotService> _logger;
        private readonly ITelegramBotClient _botClient;

        public TelegramBotService(ITelegramBotClient botClient, ILogger<TelegramBotService> logger)
        {
            _logger = logger;
            _botClient = botClient;
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

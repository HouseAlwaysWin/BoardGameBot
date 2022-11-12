using Telegram.Bot;
using Telegram.Bot.Types;

namespace UnoGame.Telegram
{
    public interface IUnoTGBotService
    {
        ITelegramBotClient BotClient { get; set; }
        Task HandleChat(Update update);
        Task InitCommands();
    }
}
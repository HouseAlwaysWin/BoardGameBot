using Telegram.Bot.Types;

namespace UnoGame.Telegram
{
    public interface ITelegramBotService
    {
        Task EchoAsync(Update update);
        Task InitCommands();
    }
}
using Microsoft.AspNetCore.Mvc;
using Telegram.Bot.Types;
using TelegramGameBot.Services;
using UnoGame.Telegram;

namespace TelegramGameBot.Controllers
{
    [ApiController]
    [Route("[controller]/[action]")]
    public class BotController : ControllerBase
    {
        private ITelegramBotService _telegramBotService;
        public BotController(ITelegramBotService telegramBotService)
        {
            _telegramBotService = telegramBotService;
        }

        [HttpPost]
        public async Task<IActionResult> HandleRequest([FromBody] Update update)
        {
            await _telegramBotService.EchoAsync(update);
            return Ok();
        }
    }
}

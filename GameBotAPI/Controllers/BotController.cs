using Microsoft.AspNetCore.Mvc;
using Telegram.Bot.Types;
using GameBotAPI.Services;
using UnoGame.Telegram;

namespace GameBotAPI.Controllers
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
        public async Task<IActionResult> HandleTelegramRequest([FromBody] Update update)
        {
            await _telegramBotService.EchoAsync(update);
            return Ok();
        }
    }
}

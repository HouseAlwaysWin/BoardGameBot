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
        private IUnoTGBotService _unoTGBotService;
        public BotController(IUnoTGBotService unoTGBotService)
        {
            _unoTGBotService = unoTGBotService;
        }

        [HttpPost]
        public async Task<IActionResult> HandleUnoTGBot([FromBody] Update update)
        {
            await _unoTGBotService.HandleChat(update);
            return Ok();
        }
    }
}

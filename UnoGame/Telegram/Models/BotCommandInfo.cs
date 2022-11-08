using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types;

namespace UnoGame.Telegram.Models
{
    public class BotCommandInfo : BotCommand
    {
        public Action<Message> Action { get; set; }

        public BotCommandInfo(string command, string description, Action<Message> action)
        {
            Command = command;
            Description = description;
            Action = action;
        }
    }
}

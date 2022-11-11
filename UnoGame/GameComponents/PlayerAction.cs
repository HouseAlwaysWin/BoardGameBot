using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types.ReplyMarkups;

namespace UnoGame.GameComponents
{
    public class PlayerAction
    {
        public string UserName { get; set; }
        public string Message { get; set; }
        public string FileId { get; set; }
        public Card CurrentCard { get; set; }
        public IReplyMarkup? ReplyMarkup { get; set; }


        public PlayerAction(string message = null, string userName = null, string fileId = null, Card currentCard = null, IReplyMarkup replymarkUp = null)
        {
            UserName = userName;
            Message = message;
            FileId = fileId;
            CurrentCard = currentCard;
            ReplyMarkup = replymarkUp;
        }


    }
}

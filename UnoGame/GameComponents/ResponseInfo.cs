using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types.ReplyMarkups;
using UnoGame.Telegram.Models;

namespace UnoGame.GameComponents
{
    public class ResponseInfo
    {
        public Queue<PlayerAction> PlayerActions { get; set; }
        public Card Card { get; set; }
        public ImageFileInfo? ImgFile { get; set; }

        public bool NeedSelectedColor { get; set; }

        public ResponseInfo(Queue<PlayerAction> playerActions = null, Card card = null, bool needSelectedColor = false)
        {
            //Success = success;
            PlayerActions = playerActions;
            Card = card;
            PlayerActions = new Queue<PlayerAction>();
            NeedSelectedColor = needSelectedColor;
        }

        public void AddPlayerAction(string message = null, string userName = null, Card currentCard = null, ImageFileInfo? imgFile = null, IReplyMarkup? replyMarkup = null)
        {
            PlayerActions.Enqueue(new(message, userName, currentCard, imgFile, replyMarkup));
        }

    }
}

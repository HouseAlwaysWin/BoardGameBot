using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types.ReplyMarkups;
using UnoGame.Telegram.Models;

namespace UnoGame.GameComponents
{
    public class PlayerAction
    {
        public string UserName { get; set; }
        public string Message { get; set; }
        public ImageFileInfo? ImgFile { get; set; }
        public Card? CurrentCard { get; set; }
        public IReplyMarkup? ReplyMarkup { get; set; }


        public PlayerAction(string message = "", string userName = "", Card currentCard = null, ImageFileInfo? imgFile = null, IReplyMarkup replymarkUp = null)
        {
            UserName = userName;
            Message = message;
            ImgFile = imgFile;
            CurrentCard = currentCard;
            ReplyMarkup = replymarkUp;
        }


    }
}

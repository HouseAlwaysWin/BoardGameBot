using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnoGame.Telegram.Models
{
    public class CardStickerSet
    {
        public string Name { get; set; }
        public string Title { get; set; }
        public List<CardSticker>? CardStickers { get; set; }
    }
}

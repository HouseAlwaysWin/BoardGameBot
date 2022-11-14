using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnoGame.Telegram.Models;

namespace UnoGame.GameComponents
{
    public class Card
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public ImageFileInfo? ImageFile { get; set; }
        public CardType CardType { get; set; }
        public CardColor? Color { get; set; }

        private int _number;
        public int Number
        {
            set
            {
                _number = value;
            }
            get
            {
                if (CardType == CardType.Number)
                {
                    return _number;
                }
                return -1;
            }
        }
    }
}

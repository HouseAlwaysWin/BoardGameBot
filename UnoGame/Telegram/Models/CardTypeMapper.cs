using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnoGame.GameComponents;

namespace UnoGame.Telegram.Models
{

    public delegate Card GetCard(string id, string uniqueFiledId, string fileId, string name, int number, string imageUrl, CardType cardType, CardColor? color);
    public class CardTypeMapper
    {
        public string RegexPattern;
        public GetCard GetCardAction;

        public CardTypeMapper(string regex, GetCard action)
        {
            RegexPattern = regex;
            GetCardAction = action;
        }

    }
}

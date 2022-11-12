using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnoGame.GameComponents;

namespace UnoGame.Extensions
{
    public static class Extensions
    {
        public static Dictionary<CardType, string> CardTypesMapper = new Dictionary<CardType, string>()
        {
            { CardType.Number, "數字" },
            { CardType.Skip, "跳過" },
            { CardType.Reverse, "反轉" },
            { CardType.DrawTwo, "+2" },
            { CardType.Wild, "萬用卡" },
            { CardType.WildDrawFour, "+4萬用卡" },
        };

        public static Dictionary<CardColor, string> CardColorsMapper = new Dictionary<CardColor, string>()
        {
            { CardColor.Red, "紅色" },
            { CardColor.Blue, "藍色" },
            { CardColor.Yellow, "黃色" },
            { CardColor.Green, "綠色" },
        };

        public static Dictionary<int, CardColor> CallbackColorMapper = new Dictionary<int, CardColor>
        {
            { 1,CardColor.Red  },
            { 2,CardColor.Blue  },
            { 3,CardColor.Green  },
            { 4,CardColor.Yellow  }
        };

        public static string GetCardTypeName(this CardType cardType)
        {
            if (CardTypesMapper.TryGetValue(cardType, out var result))
            {
                return result;
            }
            return string.Empty;
        }

        public static string GetCardColorName(this CardColor cardColor)
        {
            if (CardColorsMapper.TryGetValue(cardColor, out var result))
            {
                return result;
            }
            return string.Empty;
        }

        public static CardColor GetCallbackCardColor(this int n)
        {
            if (CallbackColorMapper.TryGetValue(n, out var result))
            {
                return result;
            }
            return CardColor.Red;
        }

        public static Card CloneNewCard(this Card card)
        {
            var serializeObj = JsonConvert.SerializeObject(card);
            if (serializeObj != null)
            {
                var newCard = JsonConvert.DeserializeObject<Card>(serializeObj);
                newCard.Id = Guid.NewGuid().ToString();
                return newCard;
            }
            return null;
        }
    }
}

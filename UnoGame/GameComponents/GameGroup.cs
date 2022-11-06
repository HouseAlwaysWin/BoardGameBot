using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types;

namespace UnoGame.GameComponents
{
    public class GameGroup
    {
        public string GroupId { get; set; }
        public List<Player> Players { get; set; }
        public List<Card> Cards { get; set; }
        public List<Card> Discards { get; set; }

    }
}

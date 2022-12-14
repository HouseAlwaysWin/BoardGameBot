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
        public string GroupName { get; set; }
        //public List<Player> Players { get; set; }
        public Queue<Player> Players { get; set; }
        public bool IsGameStart { get; set; }
        public Stack<Card> Cards { get; set; } = new Stack<Card>();
        public Stack<Card> Discards { get; set; } = new Stack<Card>();
        public Card CardOnBoard { get; set; }
        public Player Host { get; set; }

    }
}

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnoGame.GameComponents
{
    public class Player
    {
        public string Id { get; set; }

        public bool IsBot { get; set; }
        public string Alias { get; set; }

        public string? Username { get; set; }

        public string? LanguageCode { get; set; }

        public bool? CanJoinGroups { get; set; }

        public bool? CanReadAllGroupMessages { get; set; }
        public bool? SupportsInlineQueries { get; set; }
        public List<Card> HandCards { get; set; } = new List<Card>();
        public Card NextCard { get; set; }
        public Card PrevCard { get; set; }
    }
}

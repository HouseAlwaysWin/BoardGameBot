using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnoGame.GameComponents;

namespace UnoGame
{
    public class GameState
    {
        public List<GameGroup> GameGroups = new List<GameGroup>();

        private Card AddNumberCard(int number, CardColor color)
        {
            return new Card
            {
                Id = Guid.NewGuid().ToString(),
                Name = $"{color}{number}",
                CardType = CardType.Number,
                Color = color,
                Image = $"Source/Images/unocards/{color.ToString().ToLower()}{number}.png",
                Number = number,
            };
        }

        private Card AddFunctionCard(CardType cardType, CardColor color)
        {
            return new Card
            {
                Id = Guid.NewGuid().ToString(),
                Name = $"{color}{cardType}",
                CardType = cardType,
                Color = color,
                Image = $"Source/Images/unocards/{color.ToString().ToLower()}{cardType}.png",
                Number = 0,
            };
        }

        private Card AddWildCard()
        {
            return new Card
            {
                Id = Guid.NewGuid().ToString(),
                Name = CardType.Wild.ToString(),
                CardType = CardType.Wild,
                Color = null,
                Image = $"Source/Images/unocards/wild.png",
                Number = 0,
            };
        }

        private Card AddWildDrawFourCard()
        {
            return new Card
            {
                Id = Guid.NewGuid().ToString(),
                Name = CardType.WildDrawFour.ToString(),
                CardType = CardType.WildDrawFour,
                Color = null,
                Image = $"Source/Images/unocards/wildDrawFour.png",
                Number = 0,
            };
        }


        public async Task StartNewGame(string groupId)
        {
            await Task.Run(() =>
            {
                var TotalCards = new List<Card>();
                Action<int> AddNumbers = (i) =>
               {
                   TotalCards.Add(AddNumberCard(i, CardColor.Red));
                   TotalCards.Add(AddNumberCard(i, CardColor.Blue));
                   TotalCards.Add(AddNumberCard(i, CardColor.Green));
                   TotalCards.Add(AddNumberCard(i, CardColor.Yellow));
               };

                Action<int, CardType> AddFuncs = (i, cardType) =>
                {
                    TotalCards.Add(AddFunctionCard(cardType, CardColor.Red));
                    TotalCards.Add(AddFunctionCard(cardType, CardColor.Blue));
                    TotalCards.Add(AddFunctionCard(cardType, CardColor.Green));
                    TotalCards.Add(AddFunctionCard(cardType, CardColor.Yellow));
                };


                for (int i = 0; i <= 14; i++)
                {
                    switch (i)
                    {
                        case 0:
                            AddNumbers(i);
                            break;
                        case 10:
                            AddFuncs(i, CardType.Skip);
                            AddFuncs(i, CardType.Skip);
                            break;
                        case 11:
                            AddFuncs(i, CardType.Reverse);
                            AddFuncs(i, CardType.Reverse);
                            break;
                        case 12:
                            AddFuncs(i, CardType.DrawTwo);
                            AddFuncs(i, CardType.DrawTwo);
                            break;
                        case 13:
                            TotalCards.Add(AddWildCard());
                            TotalCards.Add(AddWildCard());
                            TotalCards.Add(AddWildCard());
                            TotalCards.Add(AddWildCard());
                            break;
                        case 14:
                            TotalCards.Add(AddWildDrawFourCard());
                            TotalCards.Add(AddWildDrawFourCard());
                            TotalCards.Add(AddWildDrawFourCard());
                            TotalCards.Add(AddWildDrawFourCard());
                            break;
                        default:
                            AddNumbers(i);
                            AddNumbers(i);
                            break;
                    }
                }

                GameGroup newGameGroup = new GameGroup()
                {
                    GroupId = groupId,
                    Cards = TotalCards,
                    Discards = new List<Card>(),
                    Players = new List<Player>()
                };
                GameGroups.Add(newGameGroup);
            });
        }
    }
}

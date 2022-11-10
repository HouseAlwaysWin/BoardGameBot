using AutoMapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using UnoGame.GameComponents;

namespace UnoGame.Extensions
{
    public static class TGMapper
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

        public static IMapper CreateMap()
        {
            var configuration = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<User, Player>()
                    .ForMember(p => p.Id, option => option.MapFrom(u => u.Id.ToString()))
                    .ForMember(p => p.Alias, option => option.MapFrom(u => $@"{u.FirstName}{u.LastName}"))
                    .ForMember(p => p.Username, option => option.MapFrom(u => u.Username))
                    .ForMember(p => p.HandCards, option => option.Ignore())
                    .ForMember(p => p.NextCard, option => option.Ignore())
                    .ForMember(p => p.PrevCard, option => option.Ignore());
            });
#if DEBUG
            configuration.AssertConfigurationIsValid();
#endif
            return configuration.CreateMapper();
        }
    }
}

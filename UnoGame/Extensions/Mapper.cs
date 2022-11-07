﻿using AutoMapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using UnoGame.GameComponents;

namespace UnoGame.Extensions
{
    public static class DTOMapper
    {
        public static IMapper CreateMap()
        {
            var configuration = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<User, Player>()
                    .ForMember(p => p.Id, option => option.MapFrom(u => u.Id.ToString()))
                    .ForMember(p => p.Username, option => option.MapFrom(u => $@"{u.FirstName}{u.LastName}"))
                    .ForMember(p => p.HandCards, option => option.Ignore());
            });
#if DEBUG
            configuration.AssertConfigurationIsValid();
#endif
            return configuration.CreateMapper();
        }
    }
}
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnoGame.GameComponents
{
    public class ResponseInfo
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public List<BotPlayerAction> BotPlayActions { get; set; } = new List<BotPlayerAction>();
        public Card FirstCard { get; set; }
    }
}

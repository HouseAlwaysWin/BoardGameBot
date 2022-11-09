using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TGFile = Telegram.Bot.Types.File;
using IOFile = System.IO.File;

namespace UnoGame.Telegram.Models
{
    public class ImageFileInfo
    {
        public TGFile TGFile { get; set; }
        public FileInfo FileInfo { get; set; }
    }
}

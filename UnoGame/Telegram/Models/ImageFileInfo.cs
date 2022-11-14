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
        //public TGFile TGFile { get; set; }

        public ImageFileInfo(string name = null, string emojiString = null, string fileId = null, string fileUniqueId = null, string filePath = null, long? fileSize = null)
        {
            Name = name;
            FilePath = filePath;
            FileId = fileId;
            FileUniqueId = fileUniqueId;
            FileSize = fileSize;
            EmojiString = emojiString;
        }
        public string Name { get; set; } = default!;
        public string FilePath { get; set; } = default!;
        public string FileId { get; set; } = default!;

        public string FileUniqueId { get; set; } = default!;
        public long? FileSize { get; set; }
        public string EmojiString { get; set; }




        //public FileInfo FileInfo { get; set; } = default!;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnoGame.GameComponents
{
    public class CallBackDataMapper
    {
        public string Key { get; set; }
        public string Data { get; set; }
        public string UniqueFiledId { get; set; }
        //public string FileUniqueId { get; set; }

        public CallBackDataMapper(string key, string data,  string uniqueFiledId)
        {
            Key = key;
            Data = data;
            //FileUniqueId = fileId;
            UniqueFiledId = uniqueFiledId;
        }
    }
}

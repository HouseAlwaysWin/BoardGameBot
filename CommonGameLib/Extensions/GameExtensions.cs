using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace CommonGameLib.Extensions
{
    public static class ThreadSafeRandom
    {
        [ThreadStatic] private static Random Local;

        public static Random ThisThreadsRandom
        {
            get { return Local ?? (Local = new Random(unchecked(Environment.TickCount * 31 + Thread.CurrentThread.ManagedThreadId))); }
        }
    }

    public static class GameExtensions
    {
        private static Random rng = new Random();


        public static T CloneObj<T>(this object obj)
        {
            var serializeObj = JsonConvert.SerializeObject(obj);
            if (serializeObj != null)
            {
                return JsonConvert.DeserializeObject<T>(serializeObj);
            }
            return default(T);
        }

        public static T RandomEnumValue<T>()
        {
            var v = Enum.GetValues(typeof(T));
            return (T)v.GetValue(rng.Next(v.Length));
        }

        public static Stack<T> Shuffle<T>(this Stack<T> stack)
        {
            var list = stack.ToList();
            list.Shuffle();

            return new Stack<T>(list);
        }

        public static Queue<T> Shuffle<T>(this Queue<T> queue)
        {
            var list = queue.ToList();
            list.Shuffle();

            return new Queue<T>(list);
        }

        public static void Shuffle<T>(this IList<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = ThreadSafeRandom.ThisThreadsRandom.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }
    }
}

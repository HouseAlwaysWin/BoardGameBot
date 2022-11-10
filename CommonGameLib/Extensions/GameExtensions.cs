﻿using System;
using System.Collections.Generic;
using System.Linq;
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

        public static Stack<T> Shuffle<T>(this Stack<T> stack)
        {
            //int n = stack.Count();
            var list = stack.ToList();
            list.Shuffle();
            //while (n > 1)
            //{
            //    n--;
            //    int k = ThreadSafeRandom.ThisThreadsRandom.Next(n + 1);
            //    T value = list[k];
            //    list[k] = list[n];
            //    list[n] = value;
            //}

            return new Stack<T>(list);
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

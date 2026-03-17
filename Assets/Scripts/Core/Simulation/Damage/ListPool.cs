using System.Collections.Generic;

namespace MOBA.Core.Simulation
{
    public static class ListPool<T>
    {
        private static readonly Stack<List<T>> _pool = new Stack<List<T>>(8);

        public static List<T> Get()
        {
            if (_pool.Count > 0)
                return _pool.Pop();

            return new List<T>(8);
        }

        public static void Release(List<T> list)
        {
            list.Clear();
            _pool.Push(list);
        }
    }
}
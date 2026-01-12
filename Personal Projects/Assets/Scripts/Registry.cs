using System.Collections.Generic;
using System.Linq;

public delegate T SelectionStrategy<T>(IEnumerable<T> items);

namespace DefaultNamespace {
    public static class Registry<T> where T : class {
        private static HashSet<T> _items = new HashSet<T>();

        public static bool TryAdd(T item) {
            return item != null && _items.Add(item);
        }

        public static bool Remove(T item) {
            return item != null && _items.Remove(item);
        }

        public static T GetFirst() {
            return _items.FirstOrDefault();
        }

        public static T Get(SelectionStrategy<T> strategy) {
            return strategy(_items);
        }

        public static IEnumerable<T> GetAll() {
            return _items;
        }
    }
}
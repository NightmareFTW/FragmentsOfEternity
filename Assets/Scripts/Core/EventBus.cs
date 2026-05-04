using System;
using System.Collections.Generic;

namespace Core
{
    public static class EventBus
    {
        private static readonly Dictionary<Type, List<Delegate>> _handlers = new();

        public static void Subscribe<T>(Action<T> handler)
        {
            var type = typeof(T);
            if (!_handlers.ContainsKey(type)) _handlers[type] = new List<Delegate>();
            _handlers[type].Add(handler);
        }

        public static void Unsubscribe<T>(Action<T> handler)
        {
            var type = typeof(T);
            if (_handlers.TryGetValue(type, out var list)) list.Remove(handler);
        }

        public static void Raise<T>(T evt)
        {
            var type = typeof(T);
            if (!_handlers.TryGetValue(type, out var list)) return;
            foreach (var h in list.ToArray()) ((Action<T>)h).Invoke(evt);
        }

        public static void Clear() => _handlers.Clear();
    }
}

namespace Core.Engine.Utils
{
    using System.Collections.Generic;

    public class LFUCache<T>
    {
        private readonly int _capacity;
        private readonly Dictionary<string, (T Value, int Frequency)> _cache = new();
        private readonly Dictionary<int, LRU<int>> _frequencies = new();
        private int _minFrequency;
        private readonly object _lock = new object();

        public LFUCache(int capacity)
        {
            _capacity = capacity;
            _minFrequency = 0;
        }

        public T GetOrAdd(string key, Func<string, T> valueFactory)
        {
            lock (_lock)
            {
                if (_cache.TryGetValue(key, out var entry))
                {
                    Touch(key, entry.Value, entry.Frequency);
                    return entry.Value;
                }

                T value = valueFactory(key);
                if (_capacity <= 0)
                {
                    return value;
                }

                if (_cache.Count >= _capacity)
                {
                    RemoveLeastFrequent();
                }

                var lru = GetOrCreateLRU(1);
                lru.Touch(key);
                _cache[key] = (value, 1);
                _minFrequency = 1;
                return value;
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _cache.Clear();
                _frequencies.Clear();
                _minFrequency = 0;
            }
        }

        private void Touch(string key, T value, int frequency)
        {

            var keys = _frequencies[frequency];
            keys.TryRemove(key);

            if (keys.Count == 0)
            {
                _frequencies.Remove(frequency);
                if (_minFrequency == frequency)
                {
                    _minFrequency+=1;
                }
            }

            var lru = GetOrCreateLRU(frequency + 1);
            lru.Touch(key);
            _cache[key] = (value, frequency + 1);
        }

        private void RemoveLeastFrequent()
        {
            // return when cache is empty
            if (!_frequencies.TryGetValue(_minFrequency, out var lru) || lru.Count == 0 || !lru.TryRemoveLast(out var keyToRemove))
            {
                return;
            }

            _cache.Remove(keyToRemove);
            if (lru.Count == 0)
            {
                _frequencies.Remove(_minFrequency);
            }
        }

        // LRU cache for keys within the same frequency, use T as int to save memory because we only care about keys
        // capacity of lru is guaranteed to be <= overall capacity
        private LRU<int> GetOrCreateLRU(int frequency)
        {
            if (!_frequencies.TryGetValue(frequency, out var lru))
            {
                lru = new LRU<int>(_capacity);
                _frequencies[frequency] = lru;
            }

            return lru;
        }
    }
}
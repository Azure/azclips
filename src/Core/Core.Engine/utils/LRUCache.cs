// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Engine.Utils
{
    public class LRU<T>
    {
        class DoubleLinkedListNode
        {
            public string Key;
            public T Value;
            public DoubleLinkedListNode Prev = null!;
            public DoubleLinkedListNode Next = null!;

            public DoubleLinkedListNode(string key, T value)
            {
                Key = key;
                Value = value;
            }
        }

        private readonly int _capacity;
        private readonly Dictionary<string, DoubleLinkedListNode> _cache;
        private DoubleLinkedListNode Head;
        private DoubleLinkedListNode Tail;

        public int Count => _cache.Count;

        public LRU(int capacity)
        {
            _capacity = capacity;
            _cache = new Dictionary<string, DoubleLinkedListNode>();
            Head = new DoubleLinkedListNode("", default!);
            Tail = new DoubleLinkedListNode("", default!);
            Head.Next = Tail;
            Tail.Prev = Head;
        }

        public T? Get(string key)
        {
            if (_cache.TryGetValue(key, out var node))
            {
                MoveToHead(node);
                return node.Value;
            }
            return default;
        }

        public void Put(string key, T value)
        {
            if (_cache.TryGetValue(key, out var node))
            {
                node.Value = value;
                MoveToHead(node);
            }
            else
            {
                if (_cache.Count >= _capacity)
                {
                    RemoveLast();
                }
                var newNode = new DoubleLinkedListNode(key, value);
                InsertToHead(newNode);
                _cache[key] = newNode;
            }
        }

        public void Touch(string key)
        {
            if (_cache.TryGetValue(key, out var node))
            {
                MoveToHead(node);
                return;
            }

            var newNode = new DoubleLinkedListNode(key, default!);
            InsertToHead(newNode);
            _cache[key] = newNode;
        }

        public bool TryRemoveLast(out string key)
        {
            var last = Tail.Prev;
            if (last == Head)
            {
                key = string.Empty;
                return false;
            }

            key = last.Key;
            Tail.Prev = last.Prev;
            last.Prev.Next = Tail;
            _cache.Remove(key);
            return true;
        }

        public bool TryRemove(string key)
        {
            if (!_cache.Remove(key, out var node))
            {
                return false;
            }

            node.Prev.Next = node.Next;
            node.Next.Prev = node.Prev;
            return true;
        }

        private void RemoveLast()
        {
            var last = Tail.Prev;
            if (last == Head) return; // No element to remove
            Tail.Prev = last.Prev;
            last.Prev.Next = Tail;
            _cache.Remove(last.Key);
        }

        private void InsertToHead(DoubleLinkedListNode node)
        {
            node.Next = Head.Next;
            node.Prev = Head;
            Head.Next.Prev = node;
            Head.Next = node;
        }

        private void MoveToHead(DoubleLinkedListNode node)
        {
            node.Prev.Next = node.Next;
            node.Next.Prev = node.Prev;
            InsertToHead(node);
        }
    }
}
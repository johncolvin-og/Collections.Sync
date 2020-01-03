using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Collections.Sync.Collections.Impl {
   class FilteredDictionary<TKey, TValue> : IReadOnlyDictionary<TKey, TValue> {
      readonly IReadOnlyDictionary<TKey, TValue> _source;
      readonly Func<KeyValuePair<TKey, TValue>, bool> _predicate;
      /// Note: once initialized (lazily), <see cref="_count"/> may become invalid if the underlying source changes.
      int? _count;

      public FilteredDictionary(IReadOnlyDictionary<TKey, TValue> source, Func<KeyValuePair<TKey, TValue>, bool> predicate) {
         _source = source;
         _predicate = predicate;
      }

      public TValue this[TKey key] =>
         _source.TryGetValue(key, out var value) && _predicate(new KeyValuePair<TKey, TValue>(key, value)) ?
            value : throw new KeyNotFoundException();

      public IEnumerable<TKey> Keys => this.Select(kv => kv.Key);
      public IEnumerable<TValue> Values => this.Select(kv => kv.Value);
      public int Count => _count ?? (_count = this.Count()).Value;

      public bool ContainsKey(TKey key) => _source.TryGetValue(key, out var value) && _predicate(new KeyValuePair<TKey, TValue>(key, value));
      public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _source.Where(_predicate).GetEnumerator();
      public bool TryGetValue(TKey key, out TValue value) => _source.TryGetValue(key, out value) && _predicate(new KeyValuePair<TKey, TValue>(key, value));
      IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
   }
}

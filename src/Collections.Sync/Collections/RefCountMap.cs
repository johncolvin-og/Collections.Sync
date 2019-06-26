using Collections.Sync.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace Collections.Sync.Special {
   public class RefCountMap<TKey> : IDictionary<TKey, int>, IReadOnlyDictionary<TKey, int> { 
      static readonly Func<KeyRefCount, int> _count_selector = krc => krc.count;

      readonly IEqualityComparer<TKey> _key_comparer;
      readonly Dictionary<TKey, KeyRefCount> _dictionary;
      readonly IReadOnlyCollection<int> _ro_values;
      readonly ICollection<int> _values;

      public RefCountMap()
         : this(EqualityComparer<TKey>.Default) { }

      public RefCountMap(IEqualityComparer<TKey> key_comparer)
         : this(new Dictionary<TKey, KeyRefCount>(key_comparer), key_comparer) { }

      RefCountMap(Dictionary<TKey, KeyRefCount> dictionary, IEqualityComparer<TKey> key_comparer) {
         _dictionary = dictionary;
         _key_comparer = key_comparer;
         CollectionHelper.wrap_selector(dictionary.Values, _count_selector, out _ro_values, out _values);
      }

      public int this[TKey key] {
         get => _dictionary[key].count;
         set {
            if (!_dictionary.TryGetValue(key, out var krc))
               _dictionary[key] = krc = new KeyRefCount(key);
            krc.count = value;
         }
      }

      public int Count => _dictionary.Count;
      public IReadOnlyCollection<TKey> Keys => _dictionary.Keys;
      public IReadOnlyCollection<int> Values => _ro_values;
      public bool IsReadOnly => false;
      public bool IsSynchronized => false;
      public object SyncRoot => null;
      // explicit
      ICollection<TKey> IDictionary<TKey, int>.Keys => _dictionary.Keys;
      ICollection<int> IDictionary<TKey, int>.Values => _values;
      IEnumerable<TKey> IReadOnlyDictionary<TKey, int>.Keys => _dictionary.Keys;
      IEnumerable<int> IReadOnlyDictionary<TKey, int>.Values => _values;

      public void Add(TKey key, int value) =>
         _dictionary.Add(key, new KeyRefCount(key) { count = value });

      public void Add(KeyValuePair<TKey, int> item) =>
         _dictionary.Add(item.Key, new KeyRefCount(item.Key) { count = item.Value });

      public void Clear() => _dictionary.Clear();

      public RefCountMap<TKey> clone() =>
         new RefCountMap<TKey>(new Dictionary<TKey, KeyRefCount>(_dictionary, _key_comparer), _key_comparer);

      public bool Contains(KeyValuePair<TKey, int> item) =>
         _dictionary.TryGetValue(item.Key, out var krc) && krc.count == item.Value;

      public bool ContainsKey(TKey key) => _dictionary.ContainsKey(key);

      public void CopyTo(KeyValuePair<TKey, int>[] array, int arrayIndex) {
         foreach (var kv in _dictionary)
            array[arrayIndex++] = new KeyValuePair<TKey, int>(kv.Key, kv.Value.count);
      }

      public void CopyTo(Array array, int index) {
         foreach (var kv in _dictionary)
            array.SetValue(new KeyValuePair<TKey, int>(kv.Key, kv.Value.count), index++);
      }

      public bool decrement(TKey key) =>
         decrement(key, 1);

      public bool decrement(TKey key, int count) {
         if (_dictionary.TryGetValue(key, out KeyRefCount krc)) {
            if (krc.count > count) {
               krc.count -= count;
            } else {
               // count cannot be negative
               krc.count = 0;
               _dictionary.Remove(key);
            }
            return true;
         }
         return false;
      }

      public IEnumerator<KeyValuePair<TKey, int>> GetEnumerator() {
         foreach (var kv in _dictionary)
            yield return new KeyValuePair<TKey, int>(kv.Key, kv.Value.count);
      }

      public void increment(TKey key) =>
         increment(key, 1);

      public void increment(TKey key, int count) {
         if (!_dictionary.TryGetValue(key, out KeyRefCount krc))
            _dictionary.Add(key, krc = new KeyRefCount(key) { count = count });
         else krc.count += count;
      }

      public bool Remove(TKey key) => _dictionary.Remove(key);

      public bool Remove(KeyValuePair<TKey, int> item) {
         if (_dictionary.TryGetValue(item.Key, out var krc) && krc.count == item.Value) {
            _dictionary.Remove(item.Key);
            return true;
         } else return false;
      }

      public bool TryGetValue(TKey key, out int value) {
         if (_dictionary.TryGetValue(key, out var krc)) {
            value = krc.count;
            return true;
         }
         value = 0;
         return false;
      }
      // explicit
      IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

      class KeyRefCount {
         public KeyRefCount(TKey key) =>
            this.key = key;

         public TKey key { get; }
         public int count { get; set; } = 1;
      }
   }

   class ThreadSafeRefCountMap<TKey> {
      readonly IEqualityComparer<TKey> _key_comparer;
      readonly Dictionary<TKey, KeyRefCount> _dictionary;
      int _op_count = 0;

      IDisposable _execute_op() {
         while (_op_count > 0)
            Thread.Yield();
         if (Interlocked.Increment(ref _op_count) > 1) {
            Interlocked.Decrement(ref _op_count);
            return _execute_op();
         }
         return Disposable.Create(() => Interlocked.Decrement(ref _op_count));
      }

      public ThreadSafeRefCountMap()
         : this(EqualityComparer<TKey>.Default) { }

      public ThreadSafeRefCountMap(IEqualityComparer<TKey> key_comparer)
         : this(new Dictionary<TKey, KeyRefCount>(), key_comparer) { }

      ThreadSafeRefCountMap(Dictionary<TKey, KeyRefCount> dictionary, IEqualityComparer<TKey> key_comparer) {
         _dictionary = dictionary;
         _key_comparer = key_comparer;
      }

      public int Count => _dictionary.Count;
      public object SyncRoot => null;
      public bool IsSynchronized => false;
      public int this[TKey key] => _dictionary[key].count;
      public ICollection<TKey> keys => _dictionary.Keys;
      public bool contains_key(TKey key) {
         using (_execute_op())
            return _dictionary.ContainsKey(key);
      }

      public void CopyTo(Array array, int index) {
         using (_execute_op())
            ((ICollection)_dictionary).CopyTo(array, index);
      }

      public void increment(TKey key) {
         using (_execute_op())
            _increment_core(key);
      }

      public bool increment_adds_key(TKey key) {
         using (_execute_op()) {
            int count_before = Count;
            _increment_core(key);
            return Count > count_before;
         }
      }

      void _increment_core(TKey key) {
         if (!_dictionary.TryGetValue(key, out KeyRefCount krc))
            _dictionary.Add(key, krc = new KeyRefCount(key));
         else krc.count++;
      }

      public bool decrement(TKey key) {
         using (_execute_op())
            return _decrement_core(key);
      }

      public bool decrement_removes_key(TKey key) {
         using (_execute_op()) {
            int count_before = Count;
            return _decrement_core(key) && Count < count_before;
         }
      }

      bool _decrement_core(TKey key) {
         if (_dictionary.TryGetValue(key, out KeyRefCount krc)) {
            if ((--krc.count) == 0)
               _dictionary.Remove(key);
            return true;
         } else {
            return false;
         }
      }

      public bool remove(TKey key) {
         using (_execute_op())
            return _dictionary.Remove(key);
      }

      public void clear() {
         using (_execute_op())
            _dictionary.Clear();
      }

      class KeyRefCount {
         public KeyRefCount(TKey key) =>
            this.key = key;

         public TKey key { get; }
         public int count { get; set; } = 1;
      }
   }
 
   static class RefCountMapExtensions {
      public static bool decrement_removes_key<TKey>(this RefCountMap<TKey> ref_count_map, TKey key) {
         int count_before = ref_count_map.Count;
         if (ref_count_map.decrement(key))
            return ref_count_map.Count < count_before;
         return false;
      }

      public static bool increment_adds_key<TKey>(this RefCountMap<TKey> ref_count_map, TKey key) {
         int count_before = ref_count_map.Count;
         ref_count_map.increment(key);
         return ref_count_map.Count > count_before;
      }
   }
}

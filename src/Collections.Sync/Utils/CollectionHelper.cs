using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;

namespace Collections.Sync.Utils {
   public static class CollectionHelper {
      internal const string indexer_name = "Item[]";

      public static int binary_search<T>(IList<T> items, T value, IComparer<T> comparer) =>
         binary_search(items, value, 0, items.Count, comparer);

      public static int binary_search<T>(IList<T> items, T value, int index, int length, IComparer<T> comparer) {
         if (comparer == null) comparer = Comparer<T>.Default;
         int lo = index;
         int hi = index + length - 1;
         while (lo <= hi) {
            int i = lo + ((hi - lo) >> 1);
            int c = comparer.Compare(items[i], value);
            if (c == 0) return i;
            if (c < 0) {
               lo = i + 1;
            } else {
               hi = i - 1;
            }
         }
         return ~lo;
      }

      public static void reflect_change<T, TData>(IList<T> target, IEnumerable<TData> source, Func<TData, T> create_item, Action<T> on_removed, NotifyCollectionChangedEventArgs e) {
         switch (e.Action) {
            case NotifyCollectionChangedAction.Add:
               int i = e.NewStartingIndex;
               foreach (TData d in e.NewItems) {
                  T item = create_item(d);
                  target.Insert(i++, item);
               }
               break;
            case NotifyCollectionChangedAction.Move:
               if (e.OldStartingIndex > e.NewStartingIndex) {
                  i = e.OldStartingIndex;
                  foreach (TData d in e.OldItems) {
                     var v = target[i];
                     target.RemoveAt(i++);
                     target.Insert(e.NewStartingIndex, v);
                  }
               } else {
                  foreach (TData d in e.OldItems) {
                     var v = target[e.OldStartingIndex];
                     target.RemoveAt(e.OldStartingIndex);
                     target.Insert(e.NewStartingIndex, v);
                  }
               }
               break;
            case NotifyCollectionChangedAction.Remove:
               for (int x = 0; x < e.OldItems.Count; x++) {
                  T item = target[e.OldStartingIndex];
                  target.RemoveAt(e.OldStartingIndex);
                  on_removed?.Invoke(item);
               }
               break;
            case NotifyCollectionChangedAction.Replace:
               i = e.NewStartingIndex;
               foreach (TData d in e.NewItems) {
                  T old_item = target[i];
                  target[i++] = create_item(d);
                  on_removed?.Invoke(old_item);
               }
               break;
            case NotifyCollectionChangedAction.Reset:
               var old_views = target.ToArray();
               target.Clear();
               if (on_removed != null) foreach (T item in old_views) on_removed(item);
               foreach (TData d in source) target.Add(create_item(d));
               break;
         }
      }

      public static void process_new_items<T>(IEnumerable<T> source, Action<T> on_new_item, NotifyCollectionChangedEventArgs e) {
         switch (e.Action) {
            case NotifyCollectionChangedAction.Add:
            case NotifyCollectionChangedAction.Replace:
               foreach (T v in e.NewItems)
                  on_new_item(v);
               break;
            case NotifyCollectionChangedAction.Reset:
               foreach (T v in source)
                  on_new_item(v);
               break;
         }
      }

      // Note: this is not optimized, be wary of use with large collections
      public static void sync_with_never_duplicate<T>(IList<T> dest, IEnumerable<T> src) =>
         sync_with_never_duplicate(dest, src, EqualityComparer<T>.Default);

      // Note: this is not optimized, be wary of use with large collections
      public static void sync_with_never_duplicate<T>(IList<T> dest, IEnumerable<T> src, IEqualityComparer<T> comp) {
         var src_list = src as IList<T> ?? new List<T>(src);
         var src_set = src as ISet<T> ?? new HashSet<T>(src_list);
         for (int i = 0; i < dest.Count; i++) {
            if (!src_set.Contains(dest[i]))
               dest.RemoveAt(i);
         }
         for (int i = 0; i < src_list.Count; i++) {
            int curr_index = dest.IndexOf(src_list[i]);
            if (curr_index == i) {
               continue;
            } else if (curr_index >= 0 && curr_index < dest.Count) {
               dest.RemoveAt(curr_index);
            }
            dest.Insert(i, src_list[i]);
         }
      }

      public static void sync_with_never_duplicate<T, TSource, TKey>(IList<T> dest, IEnumerable<TSource> src, Func<TSource, T> create, Func<T, TKey> key, Func<TSource, TKey> src_key) =>
         sync_with_never_duplicate(dest, src, create, key, src_key, EqualityComparer<TKey>.Default);

      // Note: this is not optimized, be wary of use with large collections
      public static void sync_with_never_duplicate<T, TSource, TKey>(IList<T> dest, IEnumerable<TSource> src, Func<TSource, T> create, Func<T, TKey> key_selector, Func<TSource, TKey> src_key_selector, IEqualityComparer<TKey> key_comparer) {
         var src_list = src as IList<TSource> ?? new List<TSource>(src);
         var src_map = src_list.ToDictionary(src_key_selector);
         for (int i = 0; i < dest.Count; i++) {
            var k = key_selector(dest[i]);
            if (!src_map.ContainsKey(k))
               dest.RemoveAt(i);
         }
         var dest_map = dest.ToDictionary(key_selector);
         for (int i = 0; i < src_list.Count; i++) {
            TKey key = src_key_selector(src_list[i]);
            if (dest_map.TryGetValue(key, out T dval)) {
               int curr_index = dest.IndexOf(dval);
               if (curr_index != i) {
                  dest.RemoveAt(curr_index);
                  dest.Insert(i, dval);
               }
            } else {
               dest.Insert(i, create(src_list[i]));
            }
         }
      }

      public static TResult[] convert_array<T, TResult>(T[] source, Func<T, TResult> selector) {
         TResult[] rv = new TResult[source.Length];
         for (int i = 0; i < rv.Length; i++)
            rv[i] = selector(source[i]);
         return rv;
      }

      public static void wrap_selector<T, TResult>(IReadOnlyCollection<T> source, Func<T, TResult> selector, out IReadOnlyCollection<TResult> as_ro_collection, out ICollection<TResult> as_collection) {
         var wrapper = new ROCollectionWrapper<T, TResult>(source, selector);
         as_ro_collection = wrapper;
         as_collection = wrapper;
      }

      class ROCollectionWrapper<TIn, TOut> : IReadOnlyCollection<TOut>, ICollection<TOut> {
         readonly Func<TIn, TOut> _convert;
         readonly IReadOnlyCollection<TIn> _source;

         public ROCollectionWrapper(IReadOnlyCollection<TIn> source, Func<TIn, TOut> convert) {
            _source = source;
            _convert = convert;
         }

         public int Count => _source.Count;
         public bool IsReadOnly => true;

         void ICollection<TOut>.Add(TOut item) => throw new NotImplementedException();
         bool ICollection<TOut>.Remove(TOut item) => throw new NotImplementedException();
         void ICollection<TOut>.Clear() => throw new NotImplementedException();

         public bool Contains(TOut item) {
            foreach (var v in _source) {
               if (Equals(v, _convert(v)))
                  return true;
            }
            return false;
         }

         public void CopyTo(TOut[] array, int arrayIndex) {
            foreach (var v in _source)
               array[arrayIndex++] = _convert(v);
         }

         public IEnumerator<TOut> GetEnumerator() {
            foreach (var v in _source)
               yield return _convert(v);
         }

         IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
      }
   }
}
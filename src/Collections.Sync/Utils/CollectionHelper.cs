using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
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

      /// <summary>
      /// Like <see cref="binary_search{T}(IList{T}, T, IComparer{T})"/>, but doesn't require deterministic sort
      /// (i.e., a comparison of adjacent items in the collection may yield '0').
      /// </summary>
      public static int binary_search<T>(IList<T> items, T value, IComparer<T> comparer, IEqualityComparer<T> eq_comparer) =>
         binary_search(items, value, 0, items.Count, comparer, eq_comparer);

      /// <summary>
      /// Like <see cref="binary_search{T}(IList{T}, T, int, int, IComparer{T})"/>, but doesn't require deterministic sort
      /// (i.e., a comparison of adjacent items in the collection may yield '0').
      /// </summary>
      public static int binary_search<T>(IList<T> items, T value, int index, int length, IComparer<T> comparer, IEqualityComparer<T> eq_comparer) {
         if (comparer == null) comparer = Comparer<T>.Default;
         int lo = index;
         int hi = index + length - 1;
         while (lo <= hi) {
            int i = lo + ((hi - lo) >> 1);
            int c = comparer.Compare(items[i], value);
            if (c == 0) {
               if (eq_comparer.Equals(items[i], value)) {
                  return i;
               } else {
                  lo = i;
                  goto try_find_exact;
               }
            }
            if (c < 0) {
               lo = i + 1;
            } else {
               hi = i - 1;
            }
         }
         return ~lo;
         try_find_exact:
         // move right
         for (hi = lo + 1; hi < items.Count && comparer.Compare(items[hi], value) == 0; hi++) {
            if (eq_comparer.Equals(items[hi], value))
               return hi;
         }
         hi = lo;
         // move left
         while (--lo >= 0 && comparer.Compare(items[lo], value) == 0) {
            if (eq_comparer.Equals(items[lo], value))
               return lo;
         }
         return ~hi;
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

      /// <summary>
      /// An optimized 'InsertSorted' implementation that leverages the fact that the items to be inserted are themselves sorted.
      /// </summary>
      /// <typeparam name="T">The type of elements.</typeparam>
      /// <param name="target">The list into which the specified sorted items will be inserted.</param>
      /// <param name="items">The presorted items to insert.</param>
      /// <param name="comparer">The comparer used to sort the presorted items and the target list.</param>
      internal static void insert_presorted_items<T>([DisallowNull]IList<T> target, [DisallowNull]IList<T> items, [DisallowNull]IComparer<T> comparer) {
         if (target is null)
            throw new ArgumentNullException(nameof(target));
         if (items is null)
            throw new ArgumentNullException(nameof(items));
         if (comparer is null)
            throw new ArgumentNullException(nameof(comparer));
         int lo_i = 0, hi_i = items.Count - 1;
         int lo_thresh = 0, size = target.Count;
         while (lo_i < hi_i) {
            if (size == 0) {
               do target.Insert(lo_thresh++, items[lo_i++]);
               while (lo_i <= hi_i);
            }
            // lo
            int lo_insert_pos = insert_item(lo_i);
            size -= (++lo_insert_pos - lo_thresh);
            lo_thresh = lo_insert_pos;
            lo_i++;
            // hi
            int hi_insert_pos = insert_item(hi_i);
            size = hi_insert_pos - lo_thresh;
            hi_i--;
         }
         // if the item count is odd, the middle item was skipped in the loop and must be added explicitly.
         if ((items.Count & 1) == 1) {
            var v = items[lo_i];
            int pos = binary_search(target, v, comparer);
            target.Insert(pos < 0 ? ~pos : pos, v);
         }
         // local methods
         int insert_item(int source_index) {
            var v = items[source_index];
            int insert_pos = binary_search(target, v, lo_thresh, size, comparer);
            if (insert_pos < 0)
               insert_pos = ~insert_pos;
            target.Insert(insert_pos, v);
            return insert_pos;
         }
      }

      /// <summary>
      /// An optimized 'InsertSorted' implementation that leverages the fact that the items to be inserted are themselves sorted.
      /// </summary>
      /// <typeparam name="T">The type of elements.</typeparam>
      /// <param name="target">The list into which the specified sorted items will be inserted.</param>
      /// <param name="items">The presorted items to insert.</param>
      /// <param name="comparer">The comparer used to sort the presorted items and the target list.</param>
      internal static void insert_presorted_items<T>([DisallowNull]IList<T> target, [DisallowNull]IList<T> items, [DisallowNull]IComparer<T> comparer, Func<T, bool> predicate) {
         if (predicate == null)
            insert_presorted_items(target, items, comparer);
         if (target is null)
            throw new ArgumentNullException(nameof(target));
         if (items is null)
            throw new ArgumentNullException(nameof(items));
         if (comparer is null)
            throw new ArgumentNullException(nameof(comparer));
         int lo_i = 0, hi_i = items.Count - 1;
         int lo_thresh = 0, size = target.Count;
         while (lo_i < hi_i) {
            if (size == 0) {
               do {
                  var v = items[lo_i];
                  if (predicate(v))
                     target.Insert(lo_thresh++, v);
               }
               while (++lo_i <= hi_i);
               return;
            }
            // lo
            var lo_v = items[lo_i];
            if (predicate(lo_v)) {
               int lo_insert_pos = insert_item(lo_v);
               size -= (++lo_insert_pos - lo_thresh);
               lo_thresh = lo_insert_pos;
            }
            lo_i++;
            // hi
            var hi_v = items[hi_i];
            if (predicate(hi_v)) {
               int hi_insert_pos = insert_item(hi_v);
               size = hi_insert_pos - lo_thresh;
            }
            hi_i--;
         }
         // if the item count is odd, the middle item was skipped in the loop and must be added explicitly.
         if ((items.Count & 1) == 1) {
            var v = items[lo_i];
            int pos = binary_search(target, v, comparer);
            target.Insert(pos < 0 ? ~pos : pos, v);
         }
         // local methods
         int insert_item(T v) {
            int insert_pos = binary_search(target, v, lo_thresh, size, comparer);
            if (insert_pos < 0)
               insert_pos = ~insert_pos;
            target.Insert(insert_pos, v);
            return insert_pos;
         }
      }

      /// <summary>
      /// An optimized 'RemoveSorted' implementation that leverages the fact that the items to be removed are themselves sorted.
      /// </summary>
      /// <typeparam name="T">The type of elements.</typeparam>
      /// <param name="target">The list from which the specified sorted items will be removed.</param>
      /// <param name="items">The presorted items to remove.</param>
      /// <param name="comparer">The comparer used to sort the presorted items and the target list.</param>
      internal static void remove_presorted_items<T>([DisallowNull]IList<T> target, [DisallowNull]IList<T> items, [DisallowNull]IComparer<T> comparer) {
         if (target is null)
            throw new ArgumentNullException(nameof(target));
         if (items is null)
            throw new ArgumentNullException(nameof(items));
         if (comparer is null)
            throw new ArgumentNullException(nameof(comparer));
         if (target.Count < items.Count)
            throw new ArgumentException(nameof(items), "The number of items to remove is greater than the number of items in the target list.");
         int lo_i = 0, hi_i = items.Count - 1;
         int lo_thresh = 0, size = target.Count;
         while (lo_i < hi_i) {
            if (size == 0) {
               // remove from the hi end because in typical IList implementations, it will require less array copying.
               for (int i = lo_thresh + hi_i - lo_i + 1; i >= lo_thresh; i--)
                  target.RemoveAt(i);
               return;
            }
            // lo
            int lo_remove_pos = remove_item(lo_i);
            size -= (lo_thresh - lo_remove_pos - 1);
            lo_thresh = lo_remove_pos;
            lo_i++;
            // hi
            int hi_remove_pos = remove_item(hi_i);
            size = hi_remove_pos - lo_thresh;
            hi_i--;
         }
         if ((items.Count & 1) == 1) {
            remove_item(lo_i);
         }
         // local methods
         int remove_item(int source_index) {
            var v = items[source_index];
            int pos = binary_search(target, v, lo_thresh, size, comparer);
            if (pos < 0)
               throw new InvalidOperationException("Binary search failed to find item to remove.");
            target.RemoveAt(pos);
            return pos;
         }
      }

      /// <summary>
      /// An optimized 'RemoveSorted' implementation that leverages the fact that the items to be removed are themselves sorted.
      /// </summary>
      /// <typeparam name="T">The type of elements.</typeparam>
      /// <param name="target">The list from which the specified sorted items will be removed.</param>
      /// <param name="items">The presorted items to remove.</param>
      /// <param name="comparer">The comparer used to sort the presorted items and the target list.</param>
      internal static void remove_presorted_items<T>([DisallowNull]IList<T> target, [DisallowNull]IList<T> items, [DisallowNull]IComparer<T> comparer, Func<T, bool> predicate) {
         if (predicate == null) {
            remove_presorted_items(target, items, comparer);
            return;
         }
         if (target is null)
            throw new ArgumentNullException(nameof(target));
         if (items is null)
            throw new ArgumentNullException(nameof(items));
         if (comparer is null)
            throw new ArgumentNullException(nameof(comparer));
         if (target.Count < items.Count)
            throw new ArgumentException(nameof(items), "The number of items to remove is greater than the number of items in the target list.");
         int lo_i = 0, hi_i = items.Count - 1;
         int lo_thresh = 0, size = target.Count;
         while (lo_i < hi_i) {
            if (size == 0) {
               do {
                  var v = items[hi_i];
                  if (predicate(v)) {
                     // Verify that the specified item to be removed is comparatively equal to the item in the target list that will actually be removed.
                     // While this should be unneccesary, this check is most likely cheap enough that it is worthwhile because it may avoid a silent failures.
                     if (comparer.Compare(target[lo_thresh], v) != 0) {
                        throw new InvalidOperationException("Target list is out of sync with the items to remove.  Common causes for such behavior are: " +
                           "1) the target list and/or the items to remove are not properly sorted, " +
                           "2) the items to remove includes value(s) that are not present in the target list.");
                     }
                     target.RemoveAt(lo_thresh);
                  }
               } while (lo_i <= --hi_i);
               return;
            }
            // lo
            var lo_val = items[lo_i];
            if (predicate(lo_val)) {
               int lo_remove_pos = remove_item(lo_val);
               size -= (lo_thresh - lo_remove_pos - 1);
               lo_thresh = lo_remove_pos;
            }
            lo_i++;
            // hi
            var hi_val = items[hi_i];
            if (predicate(hi_val)) {
               int hi_remove_pos = remove_item(hi_val);
               size = hi_remove_pos - lo_thresh;
            }
            hi_i--;
         }
         if ((items.Count & 1) == 1) {
            remove_item(items[lo_i]);
         }
         // local methods
         int remove_item(T v) {
            int pos = binary_search(target, v, lo_thresh, size, comparer);
            if (pos < 0)
               throw new InvalidOperationException("Binary search failed to find item to remove.");
            target.RemoveAt(pos);
            return pos;
         }
      }

      internal static void process_new_items<T>(IEnumerable<T> source, Action<T> on_new_item, NotifyCollectionChangedEventArgs e) {
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


using Collections.Sync.Collections.Impl;
using Collections.Sync.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Collections.Sync.Extensions {
   public static class ListExtensions {
      public static IEnumerable<T> ExceptAt<T>(this List<T> list, int index) {
         for (int i = 0; i < index; i++)
            yield return list[i];
         for (int i = index + 1; i < list.Count; i++)
            yield return list[i];
      }

      public static void InsertSorted<T>(this IList<T> list, T value, IComparer<T> comparer) {
         int i = CollectionHelper.binary_search(list, value, comparer);
         list.Insert(i < 0 ? ~i : i, value);
      }

      public static void InsertSorted<T>(this IList<T> list, T value, IComparer<T> comparer, out int index) {
         index = CollectionHelper.binary_search(list, value, comparer);
         if (index < 0)
            index = ~index;
         list.Insert(index, value);
      }

      public static void InsertSorted<T>(this IList<T> list, IEnumerable<T> items, IComparer<T> comparer) {
         foreach (var v in items) {
            int i = CollectionHelper.binary_search(list, v, comparer);
            list.Insert(i < 0 ? ~i : i, v);
         }
      }

      public static bool InsertSortedDistinct<T>(this IList<T> list, T value, IComparer<T> comparer) {
         int i = CollectionHelper.binary_search(list, value, comparer);
         if (i < 0) {
            list.Insert(~i, value);
            return true;
         } else return false;
      }

      public static bool InsertSortedDistinct<T>(this IList<T> list, T value, IComparer<T> comparer, out int index) {
         index = CollectionHelper.binary_search(list, value, comparer);
         if (index < 0) {
            list.Insert(~index, value);
            return true;
         } else return false;
      }

      public static void InsertSortedDistinct<T>(this IList<T> list, IEnumerable<T> items, IComparer<T> comparer) {
         foreach (var v in items) {
            int i = CollectionHelper.binary_search(list, v, comparer);
            if (i < 0)
               list.Insert(~i, v);
         }
      }

      public static bool RemoveSorted<T>(this IList<T> list, T value, IComparer<T> comparer) {
         int i = CollectionHelper.binary_search(list, value, comparer);
         if (i >= 0) {
            list.RemoveAt(i);
            return true;
         } else return false;
      }

      public static bool RemoveSorted<T>(this IList<T> list, T value, IComparer<T> comparer, out int index) {
         index = CollectionHelper.binary_search(list, value, comparer);
         if (index >= 0) {
            list.RemoveAt(index);
            return true;
         } else return false;
      }

      public static void RemoveSorted<T>(this IList<T> list, IEnumerable<T> items, IComparer<T> comparer) {
         foreach (var v in items) {
            int i = CollectionHelper.binary_search(list, v, comparer);
            if (i >= 0)
               list.RemoveAt(i);
         }
      }

      public static void RemoveWhere<T>(this IList<T> list, Func<T, bool> where) { foreach (var r in list.Where(where).ToList()) list.Remove(r); }

      public static void RemoveWhere<T>(this IList<T> list, Func<T, bool> where, Action<T> on_remove) {
         int count = list.Count;
         for (int i = 0; i < count; i++) {
            var t = list[i];
            if (where(t)) {
               list.RemoveAt(i--);
               --count;
               on_remove(t);
            }
         }
      }

      public static bool TryGetAt<T>(this IList<T> list, int index, out T value) {
         if (index >= 0 && index < list.Count) {
            value = list[index];
            return true;
         }
         value = default;
         return false;
      }

      public static IList<T> WrapValue<T>(T value) =>
         new ValueListWrapper<T>(value);

      public static IList<T> WrapType<T>(this IList list) =>
         new TypeWrapper<T>(list);

      public static IList<T> WrapSubRange<T>(this IList<T> list, int start) =>
         new SubRangeWrapper<T>(list, start, list.Count - start);

      public static IList<T> WrapSubRange<T>(this IList<T> list, int start, int length) =>
         new SubRangeWrapper<T>(list, start, length);

      public static IList<T> WrapSelector<T>(this IList list, Func<object, T> convert, Func<T, object> convert_back = null) =>
         new SelectorWrapper<T>(list, convert, convert_back);

      public static IList WrapSelectorHidden<T>(this IList list, Func<object, T> convert, Func<T, object> convert_back = null) =>
         new SelectorWrapper<T>(list, convert, convert_back);

      public static IList<TResult> WrapSelector<T, TResult>(this IList<T> list, Func<T, TResult> convert, Func<TResult, T> convert_back) =>
         new SelectorWrapper<T, TResult>(list, convert, convert_back);

      public static IReadOnlyList<TResult> WrapSelectorRO<T, TResult>(this IList<T> list, Func<T, TResult> convert) =>
         new SelectorWrapper<T, TResult>(list, convert, null);

      private static Random rng = new Random();

      public static void Shuffle<T>(this IList<T> list) {
         int n = list.Count;
         while (n > 1) {
            n--;
            int k = rng.Next(n + 1);
            T value = list[k];
            list[k] = list[n];
            list[n] = value;
         }
      }

      public static void RemoveAt<T>(this IList<T> list, int index, int count) {
         for (int i = 0; i < count; i++)
            list.RemoveAt(index);
      }

      public static int IndexOf<T>(IReadOnlyList<T> list, T value) {
         for (int i = 0; i < list.Count; i++)
            if (value.Equals(list[i]))
               return i;
         return -1;
      }
   }
}

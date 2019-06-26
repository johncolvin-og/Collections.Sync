using Collections.Sync.Utils;
using System;
using System.Collections.Generic;

namespace Collections.Sync.Extensions {
   static class EnumerableExtensions {
      public static bool CountAtLeast<T>(this IEnumerable<T> source, int count) {
         if (count < 0)
            throw new ArgumentException(ExceptionBuilder.Format.LessThan(0), nameof(count));
         if (count == 0)
            return true;
         foreach (var v in source) {
            if (--count == 0)
               return true;
         }
         return false;
      }

      public static bool CountAtMost<T>(this IEnumerable<T> source, int count) {
         if (count < 0)
            throw new ArgumentException(ExceptionBuilder.Format.LessThan(0), nameof(count));
         foreach (var v in source) {
            if (--count == -1)
               return false;
         }
         return true;
      }


      public static IEnumerable<T> DistinctOn<T, TKey>(this IEnumerable<T> source, Func<T, TKey> key_selector) {
         var distinct_keys = new HashSet<TKey>();
         foreach (var v in source) {
            if (distinct_keys.Add(key_selector(v)))
               yield return v;
         }
      }

      public static IEnumerable<T> ExceptNull<T>(this IEnumerable<T> source) where T : class {
         foreach (var v in source)
            if (v != null)
               yield return v;
      }

      public static bool TryGetFirst<T>(this IEnumerable<T> source, out T result) {
         foreach (var v in source) {
            result = v;
            return true;
         }
         result = default;
         return false;
      }

      public static bool TryGetFirst<T>(this IEnumerable<T> source, Func<T, bool> predicate, out T result) {
         foreach (var v in source) {
            if (predicate(v)) {
               result = v;
               return true;
            }
         }
         result = default;
         return false;
      }
   }
}
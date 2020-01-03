using Collections.Sync.Collections.Impl;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Collections.Sync.Extensions {
   public static class DictionaryExtensions {
      public static void AddOrUpdate<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, Func<TKey, TValue> add_value_factory, Action<TKey, TValue> update_value) {
         if (!dictionary.TryGetValue(key, out TValue value))
            dictionary.Add(key, add_value_factory(key));
         else update_value(key, value);
      }

      public static void AddOrReplace<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, Func<TKey, TValue> add_value_factory, Func<TKey, TValue, TValue> replace_value) {
         if (!dictionary.TryGetValue(key, out TValue value))
            dictionary.Add(key, add_value_factory(key));
         else dictionary[key] = replace_value(key, value);
      }

      public static void AddOrUpdate<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, Func<TKey, TValue> add_value_factory, Func<TKey, TValue, TValue> update_value) {
         if (!dictionary.TryGetValue(key, out TValue value))
            dictionary.Add(key, add_value_factory(key));
         else dictionary[key] = update_value(key, value);
      }

      public static void AddOrUpdate<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, Action<TValue> update_existing, Func<TValue> create_new) {
         if (dictionary.TryGetValue(key, out TValue curr)) update_existing(curr);
         else dictionary.Add(key, create_new());
      }

      public static IReadOnlyDictionary<TKey, TValue> Filtered<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> source, Func<KeyValuePair<TKey, TValue>, bool> predicate) =>
         new FilteredDictionary<TKey, TValue>(source, predicate);

      public static TValue? GetNullable<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key) where TValue : struct {
         if (dictionary.TryGetValue(key, out TValue result))
            return result;
         else return null;
      }

      public static TValue GetOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, Func<TKey, TValue> add_value_factory) {
         if (!dictionary.TryGetValue(key, out TValue value))
            dictionary.Add(key, value = add_value_factory(key));
         return value;
      }

      #region GetValueOrDefault
      public static TValue GetValueOrDefault<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dictionary, TKey key) =>
         dictionary.TryGetValue(key, out TValue result) ? result : default;

      public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key) =>
         dictionary.TryGetValue(key, out TValue result) ? result : default;

      public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue default_value) =>
         dictionary.TryGetValue(key, out TValue result) ? result : default_value;

      public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, Func<TKey, TValue> default_factory) =>
         dictionary.TryGetValue(key, out TValue result) ? result : default_factory(key);

      public static TValue GetValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key) =>
         dictionary.TryGetValue(key, out TValue result) ? result : default;
      #endregion

      public static IReadOnlyDictionary<TKey, TResult> LeftJoin<TKey, TValue1, TValue2, TResult>(this IReadOnlyDictionary<TKey, TValue1> dictionary, IReadOnlyDictionary<TKey, TValue2> merge_target,
         Func<TValue1, TValue2, TResult> select, Func<TValue1, TResult> select_left_only) =>
         dictionary.ToDictionary(kv => kv.Key, kv => {
            if (merge_target.TryGetValue(kv.Key, out TValue2 v2))
               return select(kv.Value, v2);
            else
               return select_left_only(kv.Value);
         });

      public static IEnumerable<TValue> SelectValues<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, IEnumerable<TKey> value_keys) {
         foreach (TKey k in value_keys) {
            if (dictionary.TryGetValue(k, out TValue v))
               yield return v;
         }
      }

      public static IReadOnlyDictionary<TKey, (TValue1, TValue2)> SuperJoin<TKey, TValue1, TValue2>(this IReadOnlyDictionary<TKey, TValue1> dictionary, IReadOnlyDictionary<TKey, TValue2> merge_target) {
         var eks1 = dictionary.Keys.Except(merge_target.Keys);
         var eks2 = merge_target.Keys.Except(dictionary.Keys);
         var iks = dictionary.Keys.Intersect(merge_target.Keys);
         return new ReadOnlyDictionary<TKey, (TValue1, TValue2)>(eks1
            .Select(k => (k, dictionary[k], default(TValue2)))
            .Concat(eks2.Select(k => (k, default(TValue1), merge_target[k])))
            .Concat(iks.Select(k => (k, dictionary[k], merge_target[k])))
            .ToDictionary(vals => vals.k, vals => (vals.Item2, vals.Item3))
         );
      }

      public static bool TryAdd<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, TValue value) {
         if (dict.ContainsKey(key)) return false;
         else {
            dict[key] = value;
            return true;
         }
      }

      /// <summary>
      /// Gets the value associated with the specified key, then removes the key if the lookup was successful.
      /// </summary>
      /// <typeparam name="TKey">The type of keys in the dictionary.</typeparam>
      /// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
      /// <param name="dictionary">A dictionary to try to get/remove a KeyValuePair from.</param>
      /// <param name="key">The key of the value to get/remove.</param>
      /// <param name="result">When this method returns, contains the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter.  This parameter is passed uninitialized.</param>
      /// <returns>true if the dictionary contains an element with the specified key; otherwise, false.</returns>
      public static bool TryPopValue<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, out TValue result) {
         if (dictionary.TryGetValue(key, out result)) {
            dictionary.Remove(key);
            return true;
         } else return false;
      }

      /// <summary>
      /// Gets the value associated with the specified key, then calls <paramref name="should_pop"/> with the result if the lookup was successful.  If <paramref name="should_pop"/> returns true, the key is removed.
      /// </summary>
      /// <typeparam name="TKey">The type of keys in the dictionary.</typeparam>
      /// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
      /// <param name="dictionary">A dictionary to try to get/remove a KeyValuePair from.</param>
      /// <param name="key">The key of the value to get/remove.</param>
      /// <param name="result">When this method returns, contains the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter.  This parameter is passed uninitialized.</param>
      /// <returns>true if the dictionary contains an element with the specified key, AND <paramref name="should_pop"/> returns true; otherwise, false.</returns>
      public static bool TryPopValueIf<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, Func<TValue, bool> should_pop) =>
         dictionary.TryGetValue(key, out TValue result) && should_pop(result) && dictionary.Remove(key);


      public static T? TryGetValue<TKey, T>(this IDictionary<TKey, T> src, TKey key) where T : struct {
         if (src.TryGetValue(key, out T value)) {
            return value;
         }
         return null;
      }
      public static T? TryGetValue<TKey, T>(this IReadOnlyDictionary<TKey, T> src, TKey key) where T : struct {
         if (src.TryGetValue(key, out T value)) {
            return value;
         }
         return null;
      }
   }

   public static class DictionaryHelper {
      public static IReadOnlyDictionary<TKey, TValue> empty<TKey, TValue>() =>
         Empty<TKey, TValue>.value;

      static class Empty<TKey, TValue> {
         public static readonly IReadOnlyDictionary<TKey, TValue> value =
            new ReadOnlyDictionary<TKey, TValue>(new Dictionary<TKey, TValue>(0));
      }
   }
}

using System;
using System.Collections.Generic;

namespace Collections.Sync.Utils {
   public static class ArrayHelper {
      public static void trim<T>(ref T[] array, int size) {
         if (array.Length > size) {
            T[] trimmed = new T[size];
            Array.Copy(array, trimmed, size);
         }
      }

      public static void ensure_capacity<T>(ref T[] array, int min) {
         if (array.Length < min) {
            int twice_cap = array.Length * 2;
            expand(ref array, min > twice_cap ? min : twice_cap);
         }
      }

      public static void expand<T>(ref T[] array) => expand(ref array, array.Length * 2);

      public static void expand<T>(ref T[] array, int new_capacity) {
         if (array.Length < new_capacity) {
            T[] expanded = new T[new_capacity];
            Array.Copy(array, expanded, array.Length);
            array = expanded;
         }
      }

      public static TResult[] safe_convert<T, TResult>(T[] array, Func<T, TResult> convert) =>
         array == null ? Array.Empty<TResult>() : Array.ConvertAll(array, new Converter<T, TResult>(convert));

      public static TResult[] convert<T, TResult>(ICollection<T> source, Func<T, TResult> convert) {
         var array = new TResult[source.Count];
         int i = 0;
         foreach (var v in source)
            array[i++] = convert(v);
         return array;
      }

      public static T[] clone<T>(T[] array) {
         T[] rv = new T[array.Length];
         Array.Copy(array, rv, array.Length);
         return rv;
      }

      public static T[] remove<T>(T[] source, T value) {
         int i = Array.IndexOf(source, value);
         return i < 0 ? source : remove_at(source, i);
      }

      public static T[] remove_at<T>(T[] array, int index) {
         T[] clone = new T[array.Length - 1];
         Array.Copy(array, 0, clone, 0, index);
         Array.Copy(array, index + 1, clone, index, clone.Length - index);
         return clone;
      }

      public static void remove_sorted<T>(ref T[] array, ref int size, T value, IComparer<T> comparer) =>
         remove_sorted(ref array, ref size, value, comparer);

      public static void remove_sorted<T>(ref T[] array, ref int size, T value, IComparer<T> comparer, out int? pos) {
         int p = Array.BinarySearch(array, 0, size, value, comparer);
         if (p < 0) {
            pos = null;
         } else {
            pos = p;
            size--;
            Array.Copy(array, p + 1, array, p, size - p);
            array[size] = default;
         }
      }

      public static T[] add<T>(T[] array, T value) {
         T[] clone = new T[array.Length + 1];
         Array.Copy(array, clone, array.Length);
         clone[array.Length] = value;
         return clone;
      }

      public static void add_sorted<T>(ref T[] array, ref int size, T value, IComparer<T> comparer) =>
         add_sorted(ref array, ref size, value, comparer, out int pos);

      public static void add_sorted<T>(ref T[] array, ref int size, T value, IComparer<T> comparer, out int pos) {
         pos = peek_sorted_index(array, size, value, comparer);
         ensure_capacity(ref array, size + 1);
         Array.Copy(array, pos, array, pos + 1, size - pos);
         array[pos] = value;
         size++;
      }

      public static int peek_sorted_index<T>(T[] array, int size, T value, IComparer<T> comparer) {
         int index = Array.BinarySearch(array, 0, size, value, comparer);
         if (index < 0)
            index = ~index;
         return index;
      }
   }
}
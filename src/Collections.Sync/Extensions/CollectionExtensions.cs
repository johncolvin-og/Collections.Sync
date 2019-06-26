using Collections.Sync.Utils;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace Collections.Sync.Extensions {
   public static class CollectionExtensions {
      internal static void SetItems<T>(this ICollection<T> collection, IEnumerable<T> items) {
         collection.Clear();
         foreach (var v in items)
            collection.Add(v);
      }

      public static IDisposable Subscribe(this INotifyCollectionChanged source, NotifyCollectionChangedEventHandler handler) {
         source.CollectionChanged += handler;
         return Disposable.Create(() => source.CollectionChanged -= handler);
      }

      public static IDisposable SuspendHandler(this INotifyCollectionChanged source, NotifyCollectionChangedEventHandler handler) {
         source.CollectionChanged -= handler;
         return Disposable.Create(() => source.CollectionChanged += handler);
      }

      public static void SyncWith<T>(this IList<T> dest, IEnumerable<T> src) =>
         SyncWith(dest, src, EqualityComparer<T>.Default);

      public static void SyncWith<T>(this IList<T> dest, IEnumerable<T> src, IEqualityComparer<T> comp) {
         if (src == null) {
            dest.Clear();
            return;
         }
         var src_list = src as IList<T> ?? src.ToList();
         var changes = Diff.diff(dest, src_list, comp).ToList();
         // Apply the diff
         for (int i = 0; i < changes.Count; ++i) {
            var c = changes[i];
            // removes
            for (var x = 0; x < c.deleted_left; ++x)
               dest.RemoveAt(c.start_left);
            // inserts
            for (var x = 0; x < c.inserted_right; ++x)
               dest.Insert(c.start_left + x, src_list[c.start_right + x]);
            // Adjust start index of remaining changes
            for (int ii = i + 1; ii < changes.Count; ++ii) {
               var cc = changes[ii];
               var delta = c.inserted_right - c.deleted_left;
               if (cc.start_left >= c.start_left) {
                  cc.start_left += delta;
                  changes[ii] = cc;//remember: struct
               }
            }
         }
      }
   }
}
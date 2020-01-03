using Collections.Sync.Collections.Impl;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace Collections.Sync {
   public interface INotifyCollectionChanged<T> : INotifyCollectionChanged {
      new event NotifyCollectionChangedEventHandler<T> CollectionChanged;
   }

   public delegate void NotifyCollectionChangedEventHandler<T>(object sender, NotifyCollectionChangedEventArgs<T> e);

   public class NotifyCollectionChangedEventArgs<T> {
      /// <summary>
      /// Construct a NotifyCollectionChangedEventArgs that describes a reset change.
      /// </summary>
      /// <param name="action">The action that caused the event (must be Reset).</param>
      public NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction action, IList<T> new_items, int new_starting_index, IList<T> old_items, int old_starting_index) {
         Action = action;
         NewItems = new_items;
         NewStartingIndex = new_starting_index;
         OldItems = old_items;
         OldStartingIndex = old_starting_index;
      }

      public NotifyCollectionChangedAction Action { get; }
      public IList<T> NewItems { get; }
      public IList<T> OldItems { get; }
      public int NewStartingIndex { get; }
      public int OldStartingIndex { get; }

      public static NotifyCollectionChangedEventArgs<T> from_non_generic(NotifyCollectionChangedEventArgs e) {
         List<T> new_items = null, old_items = null;
         if (e.NewItems != null) {
            new_items = new List<T>(e.NewItems.Count);
            for (int i = 0; i < e.NewItems.Count; i++)
               new_items.Add((T)e.NewItems[i]);
         }
         if (e.OldItems != null) {
            old_items = new List<T>(e.OldItems.Count);
            for (int i = 0; i < e.OldItems.Count; i++)
               old_items.Add((T)e.OldItems[i]);
         }
         return new NotifyCollectionChangedEventArgs<T>(e.Action, new_items, e.NewStartingIndex, old_items, e.OldStartingIndex);
      }

      public static NotifyCollectionChangedEventArgs<T> create_added(T new_item, int new_index) =>
         new NotifyCollectionChangedEventArgs<T>(NotifyCollectionChangedAction.Add, new List<T>(1) { new_item }, new_index, null, -1);

      public static NotifyCollectionChangedEventArgs<T> create_added(IList<T> new_items, int new_index) =>
         new NotifyCollectionChangedEventArgs<T>(NotifyCollectionChangedAction.Add, new_items, new_index, null, -1);

      public static NotifyCollectionChangedEventArgs<T> create_removed(T old_item, int old_index) =>
         new NotifyCollectionChangedEventArgs<T>(NotifyCollectionChangedAction.Remove, null, -1, new List<T>(1) { old_item }, old_index);

      public static NotifyCollectionChangedEventArgs<T> create_removed(IList<T> old_items, int old_index) =>
         new NotifyCollectionChangedEventArgs<T>(NotifyCollectionChangedAction.Remove, null, -1, old_items, old_index);

      public static NotifyCollectionChangedEventArgs<T> create_reset() =>
         new NotifyCollectionChangedEventArgs<T>(NotifyCollectionChangedAction.Reset, null, -1, null, -1);
   }

   static class NotifyCollectionChangedEventArgsExtensions {
      public static NotifyCollectionChangedEventArgs ToNonGeneric<T>(this NotifyCollectionChangedEventArgs<T> args) =>
         args.Action switch {
            NotifyCollectionChangedAction.Add => new NotifyCollectionChangedEventArgs(
               NotifyCollectionChangedAction.Add,
               NonGenericListHelper.CastOrWrap(args.NewItems),
               args.NewStartingIndex),
            NotifyCollectionChangedAction.Remove => new NotifyCollectionChangedEventArgs(
               NotifyCollectionChangedAction.Remove,
               NonGenericListHelper.CastOrWrap(args.OldItems),
               args.OldStartingIndex),
            NotifyCollectionChangedAction.Replace => new NotifyCollectionChangedEventArgs(
               NotifyCollectionChangedAction.Replace,
               NonGenericListHelper.CastOrWrap(args.NewItems),
               NonGenericListHelper.CastOrWrap(args.OldItems),
               args.NewStartingIndex),
            NotifyCollectionChangedAction.Move => new NotifyCollectionChangedEventArgs(
               NotifyCollectionChangedAction.Move,
               NonGenericListHelper.CastOrWrap(args.NewItems),
               args.NewStartingIndex,
               args.OldStartingIndex),
            NotifyCollectionChangedAction.Reset => new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset),
            _ => throw new ArgumentException($"Unexpected action type '{args.Action}.'", nameof(args)),
         };
   }
}

using Collections.Sync.Collections.Impl;
using Collections.Sync.Extensions;
using Collections.Sync.Special;
using Collections.Sync.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Disposable = Collections.Sync.Utils.Disposable;

namespace Collections.Sync {
   public static class ObservableAutoWrapper {
      public static IReadOnlyObservableCollection<T> CreateReadOnly<T>(ObservableCollection<T> source) =>
         new ReadOnlyWrapper<ObservableCollection<T>, T>(source);

      public static IStrongReadOnlyObservableCollection<T> CreateFiltered<T>(IReadOnlyObservableCollection<T> source, IComparer<T> comparer, Func<T, bool> predicate) =>
         CreateFiltered(source as IStrongReadOnlyObservableCollection<T> ?? ObservableCollectionFactory.CreateStrongReadOnly(source), comparer, predicate);

      public static IStrongReadOnlyObservableCollection<T> CreateFiltered<T>(IStrongReadOnlyObservableCollection<T> source, IComparer<T> comparer, Func<T, bool> predicate) =>
         new FilteredObservableCollectionWrapper<T>(source, comparer, predicate);

      public static IReadOnlyObservableCollection<T> CreateSorted<T>(ObservableCollection<T> source, IComparer<T> comparer) =>
         new SortedObservableCollectionWrapper<T>(source, source, comparer);

      public static IDisposable ConnectItemActions<T>(IReadOnlyObservableCollection<T> source, Action<T> action) =>
         ConnectItemActions(source, source, action);

      public static IDisposable ConnectItemActions<T>(ObservableCollection<T> source, Action<T> action) =>
         ConnectItemActions(source, source, action);

      public static IDisposable ConnectItemActions<T>(INotifyCollectionChanged notifier, IEnumerable<T> collection, Action<T> action) {
         foreach (T v in collection)
            action(v);
         return notifier.Subscribe((s, e) => CollectionHelper.process_new_items(collection, action, e));
      }

      public static IDisposable ConnectItemHooks<T>(IReadOnlyObservableCollection<T> source, Func<T, IDisposable> get_item_hook) {
         var item_hooks = source.Select(get_item_hook).ToList();
         return Disposable.Create(
            Disposable.wrap_collection(item_hooks),
            source.Subscribe((s, e) => CollectionHelper.reflect_change(item_hooks, source, get_item_hook, d => d.Dispose(), e)));
      }

      public static IDisposable ConnectItemHooks<T>(ObservableCollection<T> source, Func<T, IDisposable> get_item_hook, bool clear_source_on_dispose = false) {
         var item_hooks = source.Select(get_item_hook).ToList();
         return Disposable.Create(
            clear_source_on_dispose ? Disposable.Create(source.Clear) : Disposable.wrap_collection(item_hooks),
            source.Subscribe((s, e) => CollectionHelper.reflect_change(item_hooks, source, get_item_hook, d => d.Dispose(), e)));
      }

      public static IDisposable ConnectItems<T, TResult>(
         ObservableCollection<T> source,
         IList<TResult> target,
         Func<T, TResult> create,
         Action<TResult> on_removed = null) =>
         CreateReadOnly(source).ConnectItems(target, create, on_removed);

      public static IDisposable ConnectItems<T, TResult>(
         this IReadOnlyObservableCollection<T> source,
         IList<TResult> target,
         Func<T, TResult> create,
         Action<TResult> on_removed = null) {
         on_collection_changed(source, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, (IList)source));
         source.CollectionChanged += on_collection_changed;
         return Disposable.Create(() => source.CollectionChanged -= on_collection_changed);
         void on_collection_changed(object sender, NotifyCollectionChangedEventArgs e) {
            switch (e.Action) {
               case NotifyCollectionChangedAction.Add:
                  if (e.NewStartingIndex < 0) {
                     for (int i = 0; i < e.NewItems.Count; i++)
                        target.Add(create((T)e.NewItems[i]));
                  } else {
                     for (int i = 0; i < e.NewItems.Count; i++)
                        target.Insert(e.NewStartingIndex + i, create((T)e.NewItems[i]));
                  }
                  break;
               case NotifyCollectionChangedAction.Remove:
                  validate_old_index();
                  for (int i = 0; i < e.OldItems.Count; i++) {
                     on_removed?.Invoke(target[e.OldStartingIndex]);
                     target.RemoveAt(e.OldStartingIndex);
                  }
                  break;
               case NotifyCollectionChangedAction.Replace:
                  validate_old_index();
                  // remove
                  for (int i = 0; i < e.OldItems.Count; i++) {
                     on_removed?.Invoke(target[e.OldStartingIndex]);
                     target.RemoveAt(e.OldStartingIndex);
                  }
                  // add
                  if (e.NewStartingIndex != e.OldStartingIndex)
                     throw new InvalidOperationException(fmt_error($"{nameof(e.OldStartingIndex)} is not equal to {nameof(e.NewStartingIndex)}"));
                  if (e.NewStartingIndex < 0) {
                     for (int i = 0; i < e.NewItems.Count; i++)
                        target.Add(create((T)e.NewItems[i]));
                  } else {
                     for (int i = 0; i < e.NewItems.Count; i++)
                        target.Insert(e.NewStartingIndex + i, create((T)e.NewItems[i]));
                  }
                  break;
               case NotifyCollectionChangedAction.Move:
                  validate_old_index();
                  validate_new_index();
                  if (e.OldStartingIndex < 0)
                     throw new InvalidOperationException($"Cannot process '{e.Action}' event when e.OldStartingIndex is less than 0.");
                  for (int i = 0; i < e.OldItems.Count; i++) {
                     target.RemoveAt(e.OldStartingIndex);
                  }
                  if (e.NewStartingIndex < 0)
                     throw new InvalidOperationException($"Cannot process '{e.Action}' event when e.NewStartingIndex is less than 0.");
                  for (int i = 0; i < e.NewItems.Count; i++) {
                  }
                  break;
               case NotifyCollectionChangedAction.Reset:
                  if (on_removed != null) {
                     for (int i = 0; i < target.Count; i++)
                        on_removed(target[i]);
                  }
                  target.Clear();
                  for (int i = 0; i < source.Count; i++)
                     target.Add(create(source[i]));
                  break;
            }
            void validate_old_index() => validate_index(e.OldStartingIndex, nameof(NotifyCollectionChangedEventArgs.OldStartingIndex));
            void validate_new_index() => validate_index(e.NewStartingIndex, nameof(NotifyCollectionChangedEventArgs.NewStartingIndex));
            void validate_index(int i, string name) {
               if (i < 0)
                  throw new InvalidOperationException(fmt_error($"{name} is less than 0."));
            }
            string fmt_error(string msg) => $"Cannot process '{e.Action}' event when {msg}";
         }
      }

      public static IDisposable DisposeOnRemoved<T>(IReadOnlyObservableCollection<T> source) where T : IDisposable =>
         ConnectItemHooks(source, v => v);

      public static IDisposable DisposeOnRemoved<T>(ObservableCollection<T> source, bool clear_source_on_dispose) where T : IDisposable =>
         ConnectItemHooks(source, v => v, clear_source_on_dispose);

      public static IDisposable Synchronize<T>(ObservableCollection<T> source, ObservableCollection<T> target) {
         target.SyncWith(source);
         return source.Subscribe(source_changed);
         void source_changed(object sender, NotifyCollectionChangedEventArgs e) =>
            CollectionHelper.reflect_change(target, source, _ => _, null, e);
      }

      public static IDisposable Synchronize<T>(IObservable<ObservableCollection<T>> source, ObservableCollection<T> target) {
         IDisposable curr_connection = null;
         IDisposable source_sub = source.Subscribe(new AnonymousObserver<ObservableCollection<T>>(c => {
            Disposable.dispose(ref curr_connection);
            curr_connection = Synchronize(c, target);
         }));
         return Disposable.Create(() => {
            Disposable.dispose(ref source_sub);
            Disposable.dispose(ref curr_connection);
         });
      }

      public static IDisposable SynchronizeSorted<T>(ObservableCollection<T> source, ObservableCollection<T> target, IComparer<T> comparer) {
         _reset();
         return source.Subscribe(on_source_collection_changed);
         // local methods
         void on_source_collection_changed(object sender, NotifyCollectionChangedEventArgs e) {
            switch (e.Action) {
               case NotifyCollectionChangedAction.Add:
                  foreach (T v in e.NewItems) {
                     int new_pos = peek_pos(v);
                     target.Insert(new_pos, v);
                  }
                  break;
               case NotifyCollectionChangedAction.Remove:
                  foreach (T v in e.OldItems) {
                     int pos = assert_pos(v);
                     target.RemoveAt(pos);
                  }
                  break;
               case NotifyCollectionChangedAction.Move:
                  // ignore moves - target collection is sorted
                  break;
               case NotifyCollectionChangedAction.Replace:
                  // if the old/new sorted indices are different, this will be a Remove/re-Add op instead of a Replace op
                  using (var old_en = e.OldItems.Cast<T>().GetEnumerator())
                  using (var new_en = e.NewItems.Cast<T>().GetEnumerator()) {
                     while (old_en.MoveNext()) {
                        if (!new_en.MoveNext())
                           throw new InvalidOperationException("e.OldItems and e.NewItems do not have same number of elements in a 'Replace' op, target collection is now out of sync with its source.");
                        int old_pos = assert_pos(old_en.Current);
                        int new_pos = peek_pos(new_en.Current);
                        if (old_pos == new_pos) {
                           target[new_pos] = new_en.Current;
                        } else {
                           target.RemoveAt(old_pos);
                           target.Insert(new_pos, new_en.Current);
                        }
                     }
                  }
                  break;
               case NotifyCollectionChangedAction.Reset:
                  _reset();
                  break;
            }
         }

         int assert_pos(T value) {
            int pos = CollectionHelper.binary_search(target, value, comparer);
            if (pos < 0)
               throw new InvalidOperationException("Failed to remove value, target collection is now out of sync with its source.");
            return pos;
         }

         int peek_pos(T value) {
            int pos = CollectionHelper.binary_search(target, value, comparer);
            if (pos < 0)
               pos = ~pos;
            return pos;
         }

         void _reset() {
            var sorted = new List<T>(source);
            sorted.Sort(comparer);
            target.SyncWith(sorted);
         }
      }

      public static IDisposable SynchronizeSortedDistinct<T, TResult>(IList<T> source_list, INotifyCollectionChanged source_notifier, IList<TResult> target, Func<T, TResult> selector, IComparer<TResult> comparer) =>
         SynchronizeSortedDistinct(source_list, source_notifier, target, selector, comparer, EqualityComparer<TResult>.Default);

      public static IDisposable SynchronizeSortedDistinct<T, TResult>(IList<T> source_list, INotifyCollectionChanged source_notifier, IList<TResult> target, Func<T, TResult> selector, IComparer<TResult> comparer, IEqualityComparer<TResult> eq_comparer) {
         var ref_count = new RefCountMap<TResult>(eq_comparer);
         if (target.Count > 0)
            target.Clear();
         foreach (var v in source_list) {
            var r = selector(v);
            target.InsertSortedDistinct(r, comparer);
         }
         return source_notifier.Subscribe(on_collection_changed);
         void on_collection_changed(object sender, NotifyCollectionChangedEventArgs e) {
            switch (e.Action) {
               case NotifyCollectionChangedAction.Add:
                  target.InsertSorted(e.NewItems.Cast<T>().Select(selector).Where(ref_count.Increment), comparer);
                  break;
               case NotifyCollectionChangedAction.Remove:
                  target.RemoveSorted(e.OldItems.Cast<T>().Select(selector).Where(ref_count.Decrement), comparer);
                  break;
               case NotifyCollectionChangedAction.Replace:
                  target.RemoveSorted(e.OldItems.Cast<T>().Select(selector).Where(ref_count.Decrement), comparer);
                  target.InsertSorted(e.NewItems.Cast<T>().Select(selector).Where(ref_count.Increment), comparer);
                  break;
               case NotifyCollectionChangedAction.Reset:
                  target.Clear();
                  ref_count.Clear();
                  target.InsertSorted(source_list.Select(selector).Where(ref_count.Increment), comparer);
                  break;
            }
         }
      }

      /// <summary>
      /// Establishes a 2-way connection between a target collection and a source collection.
      /// First, the target is mutated to contain the exact same elements as the source.
      /// After that, subsequent changes to either collection are replicated in the other.
      /// </summary>
      public static IDisposable SynchronizeTwoWay<T>(IObservable<ObservableCollection<T>> source, ObservableCollection<T> target) {
         IDisposable curr_connection = null;
         IDisposable source_sub = source.Subscribe(new AnonymousObserver<ObservableCollection<T>>(c => {
            Disposable.dispose(ref curr_connection);
            curr_connection = SynchronizeTwoWay(c, target);
         }));
         return Disposable.Create(() => {
            Disposable.dispose(ref source_sub);
            Disposable.dispose(ref curr_connection);
         });
      }

      /// <summary>
      /// Establishes a 2-way connection between a target collection and a source collection.
      /// First, the target is mutated to contain the exact same elements as the source.
      /// After that, subsequent changes to either collection are replicated in the other.
      /// </summary>
      public static IDisposable SynchronizeTwoWay<T>(ObservableCollection<T> source, ObservableCollection<T> target) {
         target.SyncWith(source);
         source.CollectionChanged += source_changed;
         target.CollectionChanged += target_changed;
         return Disposable.Create(() => {
            source.CollectionChanged -= source_changed;
            target.CollectionChanged -= target_changed;
         });
         void source_changed(object sender, NotifyCollectionChangedEventArgs e) {
            using (target.SuspendHandler(target_changed))
               CollectionHelper.reflect_change(target, source, _ => _, null, e);
         }
         void target_changed(object sender, NotifyCollectionChangedEventArgs e) {
            using (source.SuspendHandler(source_changed))
               CollectionHelper.reflect_change(target, source, _ => _, null, e);
         }
      }

      public static IDisposable TryDisposeOnRemoved<T>(IReadOnlyObservableCollection<T> source) =>
         ConnectItemHooks(source, v => v as IDisposable ?? Disposable.empty);

      public static IDisposable TryDisposeOnRemoved<T>(ObservableCollection<T> source, bool clear_source_on_dispose = false) =>
         ConnectItemHooks(source, v => v as IDisposable ?? Disposable.empty, clear_source_on_dispose);

      class ReadOnlyWrapper<TSource, T> : IReadOnlyObservableCollection<T> where TSource : IReadOnlyList<T>, INotifyCollectionChanged {
         readonly TSource _source;

         public ReadOnlyWrapper(TSource source) =>
            _source = source;

         public event NotifyCollectionChangedEventHandler CollectionChanged {
            add => _source.CollectionChanged += value;
            remove => _source.CollectionChanged -= value;
         }

         public T this[int index] => _source[index];
         public int Count => _source.Count;

         public IEnumerator<T> GetEnumerator() => _source.GetEnumerator();
         IEnumerator IEnumerable.GetEnumerator() => _source.GetEnumerator();
      }
   }
}

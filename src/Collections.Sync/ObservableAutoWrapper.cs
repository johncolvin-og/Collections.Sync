using Collections.Sync.Extensions;
using Collections.Sync.Special;
using Collections.Sync.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive.Disposables;
using Disposable = Collections.Sync.Utils.Disposable;

namespace Collections.Sync {
   public static class ObservableAutoWrapper {
      public static IReadOnlyObservableCollection<T> create_read_only_wrapper<T>(ObservableCollection<T> source) =>
         new ReadOnlyWrapper<ObservableCollection<T>, T>(source);

      public static IReadOnlyObservableCollection<T> create_sorted_wrapper<T>(ObservableCollection<T> source, IComparer<T> comparer) =>
         new SortedWrapper<T>(source, source, comparer);

      public static IDisposable connect<T, TResult>(ObservableCollection<T> source, IList<TResult> target, Func<T, TResult> create, Action<TResult> on_removed = null) =>
         connect<ObservableCollection<T>, T, TResult>(source, target, create, null);

      public static IDisposable connect<TCollection, T, TResult>(TCollection source, IList<TResult> target, Func<T, TResult> create, Action<TResult> on_removed = null)
         where TCollection : IReadOnlyList<T>, INotifyCollectionChanged {
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

      public static IDisposable connect<T>(IObservable<ObservableCollection<T>> source, ObservableCollection<T> target) {
         IDisposable curr_connection = null;
         IDisposable source_sub = source.Subscribe(c => {
            Disposable.dispose(ref curr_connection);
            curr_connection = connect(c, target);
         });
         return Disposable.Create(() => {
            Disposable.dispose(ref source_sub);
            Disposable.dispose(ref curr_connection);
         });
      }

      public static IDisposable connect<T>(ObservableCollection<T> source, ObservableCollection<T> target) {
         target.SyncWith(source);
         return source.Subscribe(source_changed);
         void source_changed(object sender, NotifyCollectionChangedEventArgs e) =>
            CollectionHelper.reflect_change(target, source, _ => _, null, e);
      }

      public static IDisposable connect_set<T>(IList<T> source_list, INotifyCollectionChanged source_notifier, ISet<T> target) {
         return source_notifier.Subscribe(callback);
         void callback(object sender, NotifyCollectionChangedEventArgs e) {
            switch (e.Action) {
               case NotifyCollectionChangedAction.Add:
                  foreach (T v in e.NewItems)
                     target.Add(v);
                  break;
               case NotifyCollectionChangedAction.Remove:
                  foreach (T v in e.OldItems)
                     target.Remove(v);
                  break;
               case NotifyCollectionChangedAction.Replace:
                  foreach (T v in e.OldItems)
                     target.Remove(v);
                  foreach (T v in e.NewItems)
                     target.Add(v);
                  break;
               case NotifyCollectionChangedAction.Reset:
                  target.Clear();
                  for (int i = 0; i < source_list.Count; i++)
                     target.Add(source_list[i]);
                  break;
            }
         }
      }

      public static IDisposable connect_sorted<T>(ObservableCollection<T> source, ObservableCollection<T> target, IComparer<T> comparer) {
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

      public static IDisposable connect_sorted_distinct<T, TResult>(IList<T> source_list, INotifyCollectionChanged source_notifier, IList<TResult> target, Func<T, TResult> selector, IComparer<TResult> comparer) =>
         connect_sorted_distinct(source_list, source_notifier, target, selector, comparer, EqualityComparer<TResult>.Default);

      public static IDisposable connect_sorted_distinct<T, TResult>(IList<T> source_list, INotifyCollectionChanged source_notifier, IList<TResult> target, Func<T, TResult> selector, IComparer<TResult> comparer, IEqualityComparer<TResult> eq_comparer) {
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
                  target.InsertSorted(e.NewItems.Cast<T>().Select(selector).Where(ref_count.increment_adds_key), comparer);
                  break;
               case NotifyCollectionChangedAction.Remove:
                  target.RemoveSorted(e.OldItems.Cast<T>().Select(selector).Where(ref_count.decrement_removes_key), comparer);
                  break;
               case NotifyCollectionChangedAction.Replace:
                  target.RemoveSorted(e.OldItems.Cast<T>().Select(selector).Where(ref_count.decrement_removes_key), comparer);
                  target.InsertSorted(e.NewItems.Cast<T>().Select(selector).Where(ref_count.increment_adds_key), comparer);
                  break;
               case NotifyCollectionChangedAction.Reset:
                  target.Clear();
                  ref_count.Clear();
                  target.InsertSorted(source_list.Select(selector).Where(ref_count.increment_adds_key), comparer);
                  break;
            }
         }
      }

      public static IDisposable connect_sorted_distinct_mux<T, TResult>(IList<T> source_list, INotifyCollectionChanged source_notifier, IList<TResult> target, Func<T, IEnumerable<TResult>> selector, IComparer<TResult> comparer) =>
         connect_sorted_distinct_mux(source_list, source_notifier, target, selector, comparer, EqualityComparer<TResult>.Default);

      public static IDisposable connect_sorted_distinct_mux<T, TResult>(IList<T> source_list, INotifyCollectionChanged source_notifier, IList<TResult> target, Func<T, IEnumerable<TResult>> selector, IComparer<TResult> comparer, IEqualityComparer<TResult> eq_comparer) {
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
                  target.InsertSorted(e.NewItems.Cast<T>().SelectMany(selector).Where(ref_count.increment_adds_key), comparer);
                  break;
               case NotifyCollectionChangedAction.Remove:
                  target.RemoveSorted(e.OldItems.Cast<T>().SelectMany(selector).Where(ref_count.decrement_removes_key), comparer);
                  break;
               case NotifyCollectionChangedAction.Replace:
                  target.RemoveSorted(e.OldItems.Cast<T>().SelectMany(selector).Where(ref_count.decrement_removes_key), comparer);
                  target.InsertSorted(e.NewItems.Cast<T>().SelectMany(selector).Where(ref_count.increment_adds_key), comparer);
                  break;
               case NotifyCollectionChangedAction.Reset:
                  target.Clear();
                  ref_count.Clear();
                  target.InsertSorted(source_list.SelectMany(selector).Where(ref_count.increment_adds_key), comparer);
                  break;
            }
         }
      }

      /// <summary>
      /// Establishes a 2-way connection between a target collection and a source collection.
      /// First, the target is mutated to contain the exact same elements as the source.
      /// After that, subsequent changes to either collection are replicated in the other.
      /// </summary>
      public static IDisposable connect_two_way<T>(IObservable<ObservableCollection<T>> source, ObservableCollection<T> target) {
         IDisposable curr_connection = null;
         IDisposable source_sub = source.Subscribe(c => {
            Disposable.dispose(ref curr_connection);
            curr_connection = connect_two_way(c, target);
         });
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
      public static IDisposable connect_two_way<T>(ObservableCollection<T> source, ObservableCollection<T> target) {
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

      public static IDisposable connect_item_actions<T>(IReadOnlyObservableCollection<T> source, Action<T> action) =>
         connect_item_actions(source, source, action);

      public static IDisposable connect_item_actions<T>(ObservableCollection<T> source, Action<T> action) =>
         connect_item_actions(source, source, action);

      public static IDisposable connect_item_actions<T>(INotifyCollectionChanged notifier, IEnumerable<T> collection, Action<T> action) {
         foreach (T v in collection)
            action(v);
         return notifier.Subscribe((s, e) => CollectionHelper.process_new_items(collection, action, e));
      }

      public static IDisposable connect_item_hooks<T>(IReadOnlyObservableCollection<T> source, Func<T, IDisposable> get_item_hook) {
         var item_hooks = source.Select(get_item_hook).ToList();
         return new CompositeDisposable(
            Disposable.wrap_collection(item_hooks),
            source.Subscribe((s, e) => CollectionHelper.reflect_change(item_hooks, source, get_item_hook, d => d.Dispose(), e)));
      }

      public static IDisposable connect_item_hooks<T>(ObservableCollection<T> source, Func<T, IDisposable> get_item_hook, bool clear_source_on_dispose = false) {
         var item_hooks = source.Select(get_item_hook).ToList();
         return new CompositeDisposable(
            clear_source_on_dispose ? Disposable.Create(source.Clear) : Disposable.wrap_collection(item_hooks),
            source.Subscribe((s, e) => CollectionHelper.reflect_change(item_hooks, source, get_item_hook, d => d.Dispose(), e)));
      }

      public static IDisposable dispose_on_removed<T>(IReadOnlyObservableCollection<T> source) where T : IDisposable =>
         connect_item_hooks(source, v => v);

      public static IDisposable dispose_on_removed<T>(ObservableCollection<T> source, bool clear_source_on_dispose) where T : IDisposable =>
         connect_item_hooks(source, v => v, clear_source_on_dispose);

      public static IDisposable try_dispose_on_removed<T>(IReadOnlyObservableCollection<T> source) =>
         connect_item_hooks(source, v => v as IDisposable ?? Disposable.empty);

      public static IDisposable try_dispose_on_removed<T>(ObservableCollection<T> source, bool clear_source_on_dispose = false) =>
         connect_item_hooks(source, v => v as IDisposable ?? Disposable.empty, clear_source_on_dispose);

      public static INotifyCollectionChanged<TResult> wrap_notifier<T, TResult>(INotifyCollectionChanged notifier, Func<T, TResult> selector) =>
         new NotifierWrapper<T, TResult>(notifier, selector);

      class NotifierWrapper<T, TResult> : INotifyCollectionChanged<TResult> {
         public NotifierWrapper(INotifyCollectionChanged source, Func<T, TResult> selector) {
            source.CollectionChanged += on_collection_changed;
            void on_collection_changed(object sender, NotifyCollectionChangedEventArgs e) {
               if (_collection_changed == null && _nongen_collection_changed == null)
                  return;
               var new_items = e.NewItems == null ? null : ListExtensions.WrapSelector(e.NewItems, obj => selector((T)obj), null);
               var old_items = e.OldItems == null ? null : ListExtensions.WrapSelector(e.OldItems, obj => selector((T)obj), null);
               _collection_changed?.Invoke(this, new NotifyCollectionChangedEventArgs<TResult>(e.Action, new_items, e.NewStartingIndex, old_items, e.OldStartingIndex));
               if (_nongen_collection_changed != null) {
                  NotifyCollectionChangedEventArgs nongen_wrapper;
                  switch (e.Action) {
                     case NotifyCollectionChangedAction.Add:
                        nongen_wrapper = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, (IList)new_items, e.NewStartingIndex);
                        break;
                     case NotifyCollectionChangedAction.Remove:
                        nongen_wrapper = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, (IList)old_items, e.OldStartingIndex);
                        break;
                     case NotifyCollectionChangedAction.Move:
                        nongen_wrapper = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Move, (IList)new_items, e.NewStartingIndex, e.OldStartingIndex);
                        break;
                     case NotifyCollectionChangedAction.Replace:
                        nongen_wrapper = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Move, (IList)new_items, (IList)old_items, e.NewStartingIndex);
                        break;
                     default:
                        nongen_wrapper = e;
                        break;
                  }
                  _nongen_collection_changed(this, nongen_wrapper);
               }
            }
         }

         event NotifyCollectionChangedEventHandler<TResult> _collection_changed;
         event NotifyCollectionChangedEventHandler<TResult> INotifyCollectionChanged<TResult>.CollectionChanged {
            add => _collection_changed += value;
            remove => _collection_changed -= value;
         }

         event NotifyCollectionChangedEventHandler _nongen_collection_changed;
         event NotifyCollectionChangedEventHandler INotifyCollectionChanged.CollectionChanged {
            add => _nongen_collection_changed += value;
            remove => _nongen_collection_changed -= value;
         }
      }

      class SortedWrapper<T> : IReadOnlyObservableCollection<T> {
         const int _min_capacity = 4;
         readonly IComparer<T> _comparer;
         T[] _values;
         int _size;
         readonly INotifyCollectionChanged _src_notifier;
         readonly ICollection<T> _src_collection;

         public SortedWrapper(INotifyCollectionChanged notifier, ICollection<T> collection, IComparer<T> comparer) {
            _src_notifier = notifier;
            _src_collection = collection;
            _comparer = comparer;
            if (collection.Count == 0) {
               _values = new T[_min_capacity];
            } else {
               _values = new T[Math.Max(_min_capacity, collection.Count)];
               collection.CopyTo(_values, 0);
               Array.Sort(_values, 0, collection.Count, comparer);
            }
            notifier.CollectionChanged += on_collection_changed;
            void on_collection_changed(object sender, NotifyCollectionChangedEventArgs e) {
               switch (e.Action) {
                  case NotifyCollectionChangedAction.Add:
                     foreach (T v in e.NewItems)
                        _add_and_notify(v);
                     break;
                  case NotifyCollectionChangedAction.Remove:
                     foreach (T v in e.OldItems)
                        _remove_and_notify(v);
                     break;
                  case NotifyCollectionChangedAction.Move:
                     // ignore moves - this is a sorted collection.
                     break;
                  case NotifyCollectionChangedAction.Replace:
                     // if the old/new sorted indices are different, this will be a Remove/re-Add op (by definition, can only be a Replace op if they are the same)
                     using (var old_en = e.OldItems.Cast<T>().GetEnumerator())
                     using (var new_en = e.NewItems.Cast<T>().GetEnumerator()) {
                        while (old_en.MoveNext()) {
                           if (!new_en.MoveNext())
                              throw new InvalidOperationException("e.OldItems and e.NewItems do not have same number of elements in a 'Replace' op, SortedWrapper is now out of sync with its source.");
                           int old_pos = _assert_remove(old_en.Current);
                           int new_pos = _add(new_en.Current);
                           if (CollectionChanged != null) {
                              if (old_pos == new_pos) {
                                 CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, old_en.Current, new_en.Current, new_pos));
                              } else {
                                 CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, old_en.Current, old_pos));
                                 CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, new_en.Current, new_pos));
                              }
                           }
                        }
                     }
                     break;
                  case NotifyCollectionChangedAction.Reset:
                     ArrayHelper.ensure_capacity(ref _values, _src_collection.Count);
                     if (_size > _src_collection.Count) {
                        Array.Clear(_values, _src_collection.Count, _size - _src_collection.Count);
                     }
                     _src_collection.CopyTo(_values, 0);
                     _size = _src_collection.Count;
                     CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                     break;
               }
            }
         }

         public event NotifyCollectionChangedEventHandler CollectionChanged;

         public T this[int index] => _values[index];
         public int Count => _size;

         public IEnumerator<T> GetEnumerator() {
            for (int i = 0; i < _size; i++)
               yield return _values[i];
         }
         IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

         int _assert_remove(T value) {
            ArrayHelper.remove_sorted(ref _values, ref _size, value, _comparer, out int? pos);
            if (!pos.HasValue)
               throw new InvalidOperationException("Failed to remove value, SortedWrapper is now out of sync with its source.");
            return pos.Value;
         }

         void _remove_and_notify(T value) {
            int pos = _assert_remove(value);
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, value, pos));
         }

         int _add(T value) {
            ArrayHelper.add_sorted(ref _values, ref _size, value, _comparer, out int pos);
            return pos;
         }

         void _add_and_notify(T value) {
            int pos = _add(value);
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, value, pos));
         }
      }

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
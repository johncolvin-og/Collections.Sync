using Collections.Sync.Extensions;
using Collections.Sync.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Collections.Sync.Collections.Impl {
   interface IIncrementalChangeNotifier<T> {
      IEnumerable<T> Items { get; }
      event Action<IEnumerable<T>> Added;
      event Action<IEnumerable<T>> Removed;
      event Action Reset;
   }

   static class IncrementalChangeNotifier {
      public static IIncrementalChangeNotifier<T> ToNotifier<T>(this IStrongReadOnlyObservableCollection<T> source) =>
         new Impl<T>(source);

      public static IIncrementalChangeNotifier<T> ToNotifier<T>(this IStrongReadOnlyObservableCollection<T> source, Func<T, bool> predicate) =>
         new FilteredImpl<T>(source, predicate);

      class Impl<T> : IIncrementalChangeNotifier<T> {
         readonly IStrongReadOnlyObservableCollection<T> _source;

         public Impl([DisallowNull]IStrongReadOnlyObservableCollection<T> source) {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _source.CollectionChanged += on_collection_changed;
            void on_collection_changed(object sender, NotifyCollectionChangedEventArgs<T> e) {
               switch (e.Action) {
                  case NotifyCollectionChangedAction.Add: Added?.Invoke(e.NewItems); break;
                  case NotifyCollectionChangedAction.Remove: Removed?.Invoke(e.OldItems); break;
                  case NotifyCollectionChangedAction.Replace:
                     Removed?.Invoke(e.OldItems);
                     Added?.Invoke(e.NewItems);
                     break;
                  case NotifyCollectionChangedAction.Reset: Reset?.Invoke(); break;
               }
            }
         }

         public event Action<IEnumerable<T>> Added;
         public event Action<IEnumerable<T>> Removed;
         public event Action Reset;

         public IEnumerable<T> Items => _source;
      }

      class FilteredImpl<T> : IIncrementalChangeNotifier<T> {
         readonly IStrongReadOnlyObservableCollection<T> _source;

         public FilteredImpl([DisallowNull]IStrongReadOnlyObservableCollection<T> source, [DisallowNull]Func<T, bool> predicate) {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _source.CollectionChanged += on_collection_changed;
            void on_collection_changed(object sender, NotifyCollectionChangedEventArgs<T> e) {
               switch (e.Action) {
                  case NotifyCollectionChangedAction.Add: Added?.Invoke(e.NewItems.Where(predicate)); break;
                  case NotifyCollectionChangedAction.Remove: Removed?.Invoke(e.OldItems.Where(predicate)); break;
                  case NotifyCollectionChangedAction.Replace:
                     Removed?.Invoke(e.OldItems.Where(predicate));
                     Added?.Invoke(e.NewItems.Where(predicate));
                     break;
                  case NotifyCollectionChangedAction.Reset: Reset?.Invoke(); break;
               }
            }
         }

         public event Action<IEnumerable<T>> Added;
         public event Action<IEnumerable<T>> Removed;
         public event Action Reset;

         public IEnumerable<T> Items => _source;
      }

      class SortedImpl<T> : IStrongReadOnlyObservableCollection<T> {
         readonly IIncrementalChangeNotifier<T> _notifier;
         readonly IComparer<T> _comparer;
         readonly List<T> _sorted_items = new List<T>();

         public SortedImpl([DisallowNull]IIncrementalChangeNotifier<T> notifier, [DisallowNull]IComparer<T> comparer) {
            _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
            _comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
         }

         public event NotifyCollectionChangedEventHandler<T> CollectionChanged;
         event NotifyCollectionChangedEventHandler _ng_collection_changed;
         public event PropertyChangedEventHandler PropertyChanged;

         public int Count => _sorted_items.Count;

         public T this[int index] => _sorted_items[index];

         event NotifyCollectionChangedEventHandler INotifyCollectionChanged.CollectionChanged {
            add => _ng_collection_changed += value;
            remove => _ng_collection_changed -= value;
         }

         public IEnumerator<T> GetEnumerator() => _sorted_items.GetEnumerator();
         IEnumerator IEnumerable.GetEnumerator() => _sorted_items.GetEnumerator();

         void _on_reset() {
            if (Count > 0) {
               CollectionChanged?.Invoke(this, NotifyCollectionChangedEventArgs<T>.create_reset());
               _ng_collection_changed?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
               _on_count_changed();
            }
         }
         void _on_removed(IEnumerable<T> items) {
            foreach (var v in items) {
               if (_sorted_items.RemoveSorted(v, _comparer, out int old_pos)) {
                  CollectionChanged?.Invoke(this, NotifyCollectionChangedEventArgs<T>.create_removed(v, old_pos));
                  _ng_collection_changed?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, v, old_pos));
                  _on_count_changed();
               }
            }
         }
         void _on_added(IEnumerable<T> items) {
            foreach (var v in items) {
               _sorted_items.InsertSorted(v, _comparer, out int new_pos);
               CollectionChanged?.Invoke(this, NotifyCollectionChangedEventArgs<T>.create_added(v, new_pos));
               _ng_collection_changed?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, v, new_pos));
               _on_count_changed();
            }
         }
         void _on_count_changed() {
            if (PropertyChanged != null) {
               PropertyChanged(this, new PropertyChangedEventArgs(nameof(Count)));
               PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(CollectionHelper.indexer_name));
            }
         }
      }

      class SortedCollectionImpl<T> : IStrongReadOnlyObservableCollection<T>, INotifyPropertyChanged {
         readonly IStrongReadOnlyObservableCollection<T> _source;
         readonly IComparer<T> _comparer;
         readonly List<T> _sorted_items = new List<T>();

         public SortedCollectionImpl([DisallowNull]IStrongReadOnlyObservableCollection<T> source, IComparer<T> comparer) {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
            _sorted_items.InsertSorted(source, _comparer);
            _source.CollectionChanged += on_source_collection_changed;
            void on_source_collection_changed(object sender, NotifyCollectionChangedEventArgs<T> e) {
               switch (e.Action) {
                  case NotifyCollectionChangedAction.Add: _on_added(e.NewItems); break;
                  case NotifyCollectionChangedAction.Remove: _on_removed(e.OldItems);break;
                  case NotifyCollectionChangedAction.Replace:
                     _on_removed(e.OldItems);
                     _on_added(e.NewItems);
                     break;
                  case NotifyCollectionChangedAction.Reset:
                     _on_reset();
                     break;
               }
            }
         }

         public event NotifyCollectionChangedEventHandler<T> CollectionChanged;
         event NotifyCollectionChangedEventHandler _ng_collection_changed;
         public event PropertyChangedEventHandler PropertyChanged;

         public int Count => _sorted_items.Count;

         public T this[int index] => _sorted_items[index];

         event NotifyCollectionChangedEventHandler INotifyCollectionChanged.CollectionChanged {
            add => _ng_collection_changed += value;
            remove => _ng_collection_changed -= value;
         }

         public IEnumerator<T> GetEnumerator() => _sorted_items.GetEnumerator();
         IEnumerator IEnumerable.GetEnumerator() => _sorted_items.GetEnumerator();

         void _on_reset() {
            if (Count > 0) {
               CollectionChanged?.Invoke(this, NotifyCollectionChangedEventArgs<T>.create_reset());
               _ng_collection_changed?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
               _on_count_changed();
            }
         }
         void _on_removed(IEnumerable<T> items) {
            foreach (var v in items) {
               if (_sorted_items.RemoveSorted(v, _comparer, out int old_pos)) {
                  CollectionChanged?.Invoke(this, NotifyCollectionChangedEventArgs<T>.create_removed(v, old_pos));
                  _ng_collection_changed?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, v, old_pos));
                  _on_count_changed();
               }
            }
         }
         void _on_added(IEnumerable<T> items) {
            foreach (var v in items) {
               _sorted_items.InsertSorted(v, _comparer, out int new_pos);
               CollectionChanged?.Invoke(this, NotifyCollectionChangedEventArgs<T>.create_added(v, new_pos));
               _ng_collection_changed?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, v, new_pos));
               _on_count_changed();
            }
         }
         void _on_count_changed() {
            if (PropertyChanged != null) {
               PropertyChanged(this, new PropertyChangedEventArgs(nameof(Count)));
               PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(CollectionHelper.indexer_name));
            }
         }
      }
   }
}

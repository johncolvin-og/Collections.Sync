using EqualityComparer.Extensions;
using Collections.Sync.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace Collections.Sync.Collections.Impl {
   /// <summary>
   /// A readonly observable collection that applies a predicate (or filter) to an underlying observable collection.
   /// The filtered items are sorted with a key comparer, which also facilitates log(n) lookups when responding to changes in the source collection.
   /// </summary>
   /// <typeparam name="T">The type of elements.</typeparam>
   /// <typeparam name="TKey">The type of immutable keys that uniquely identify each element.</typeparam>
      class SortedImpl<T> : IStrongReadOnlyObservableCollection<T> {
         readonly IIncrementalChangeNotifier<T> _notifier;
         readonly IComparer<T> _comparer;
         readonly List<T> _sorted_items = new List<T>();

         public SortedImpl([DisallowNull]IIncrementalChangeNotifier<T> notifier, [DisallowNull]IComparer<T> comparer) {
            _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
            _comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
            _notifier.Added += _on_added;
            _notifier.Removed += _on_removed;
            _notifier.Reset += _on_reset;
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

         void _on_removed(IEnumerable<T> items) {
            foreach (var v in items) {
               if (_sorted_items.RemoveSorted(v, _comparer, out int old_pos)) {
                  CollectionChanged?.Invoke(this, NotifyCollectionChangedEventArgs<T>.create_removed(v, old_pos));
                  _ng_collection_changed?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, v, old_pos));
                  _on_count_changed();
               }
            }
         }

         void _on_reset() {
            if (Count > 0) {
               CollectionChanged?.Invoke(this, NotifyCollectionChangedEventArgs<T>.create_reset());
               _ng_collection_changed?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
               _on_count_changed();
            }
         }
      }
}

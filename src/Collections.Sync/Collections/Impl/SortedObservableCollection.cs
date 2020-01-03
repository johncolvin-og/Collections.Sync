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
   class SortedObservableCollection<T, TKey> : IStrongReadOnlyObservableCollection<T>, INotifyPropertyChanged {
      readonly IStrongReadOnlyObservableCollection<T> _source;
      readonly IComparer<T> _item_comparer;
      readonly IEqualityComparer<T> _eq_comparer;
      readonly Func<T, TKey> _key_fn;
      readonly List<T> _sorted_items = new List<T>();

      public SortedObservableCollection(
         [DisallowNull]IStrongReadOnlyObservableCollection<T> source,
         [DisallowNull]IComparer<TKey> key_comparer,
         [DisallowNull]IEqualityComparer<TKey> key_eq_comparer,
         [DisallowNull]Func<T, TKey> key_fn) {
         //
         _source = source ?? throw new ArgumentNullException(nameof(source));
         if (key_comparer is null)
            throw new ArgumentNullException(nameof(key_comparer));
         _key_fn = key_fn ?? throw new ArgumentNullException(nameof(key_fn));
         _item_comparer = Comparer<T>.Create((a, b) => key_comparer.Compare(_key_fn(a), _key_fn(b)));
         _eq_comparer = (key_eq_comparer ?? throw new ArgumentNullException(nameof(key_eq_comparer))).Wrap(_key_fn);
         _key_fn = key_fn;
         _source.CollectionChanged += _on_source_collection_changed;
      }

      public T this[int index] =>
         _sorted_items[index];

      public int Count =>
         _sorted_items.Count;

      public event NotifyCollectionChangedEventHandler<T> CollectionChanged;

      event NotifyCollectionChangedEventHandler INotifyCollectionChanged.CollectionChanged {
         add => _ng_collection_changed += value;
         remove => _ng_collection_changed -= value;
      }

      event NotifyCollectionChangedEventHandler _ng_collection_changed;
      public event PropertyChangedEventHandler PropertyChanged;

      public IEnumerator<T> GetEnumerator() =>
         _sorted_items.GetEnumerator();

      IEnumerator IEnumerable.GetEnumerator() =>
         _sorted_items.GetEnumerator();

      void _on_source_collection_changed(object sender, NotifyCollectionChangedEventArgs<T> e) {
         switch (e.Action) {
            case NotifyCollectionChangedAction.Remove: _on_removed(e.OldItems); break;
            case NotifyCollectionChangedAction.Add: _on_added(e.NewItems); break;
            case NotifyCollectionChangedAction.Replace:
               _on_removed(e.OldItems);
               _on_added(e.NewItems);
               break;
            case NotifyCollectionChangedAction.Reset:
               if (_source.Count > 0)
                  throw new InvalidOperationException($"Source collection should be empty during a 'reset' CollectionChanged event (acually has {_source.Count} elements).");
               _sorted_items.Clear();
               CollectionChanged?.Invoke(this, NotifyCollectionChangedEventArgs<T>.create_reset());
               break;
         }
      }

      void _on_added(IList<T> items) {
         var batch = new List<T>(1);
         int batch_start = -1;
         foreach (var v in items) {
            int i = CollectionHelper.binary_search(_sorted_items, v, _item_comparer, _eq_comparer);
            if (i < 0)
               i = ~i;
            if (batch.Count == 0) {
               batch_start = i;
               batch.Add(v);
            } else if (batch_start == i - batch.Count) {
               batch.Add(v);
            } else {
               // fire current batch then start a new one
               _insert_range(batch_start, batch);
               batch = new List<T>();
            }
         }
         if (batch.Count > 0)
            _insert_range(batch_start, batch);
      }

      void _on_removed(IList<T> items) {
         var batch = new List<T>(1);
         int batch_start = -1;
         foreach (var v in items) {
            int i = CollectionHelper.binary_search(_sorted_items, v, _item_comparer, _eq_comparer);
            if (i < 0)
               throw new InvalidOperationException("Binary search failed to find item to remove.");
            if (batch.Count == 0) {
               batch_start = i;
               batch.Add(v);
            } else if (batch_start == i) {
               batch.Add(v);
            } else {
               // fire current batch then start a new one
               _remove_range(batch_start, batch);
               batch = new List<T>();
            }
         }
         if (batch.Count > 0)
            _remove_range(batch_start, batch);
      }

      void _insert_range(int index, List<T> items) {
         _sorted_items.InsertRange(index, items);
         CollectionChanged?.Invoke(this, NotifyCollectionChangedEventArgs<T>.create_added(items, index));
         _ng_collection_changed?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, items, index));
         _on_count_changed();
      }

      void _remove_range(int index, IList<T> items) {
         _sorted_items.RemoveRange(index, items.Count);
         CollectionChanged(this, NotifyCollectionChangedEventArgs<T>.create_removed(items, index));
         _ng_collection_changed?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, items, index));
         _on_count_changed();
      }

      void _on_count_changed() {
         if (PropertyChanged != null) {
            PropertyChanged(this, new PropertyChangedEventArgs(nameof(Count)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(CollectionHelper.indexer_name));
         }
      }
   }
}

﻿using Collections.Sync.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;

namespace Collections.Sync.Collections.Impl {
   class FilteredObservableCollectionWrapper<T> : IStrongReadOnlyObservableCollection<T>, INotifyPropertyChanged {
      readonly IStrongReadOnlyObservableCollection<T> _source;
      readonly IComparer<T> _comparer;
      readonly IEqualityComparer<T> _eq_comparer;
      readonly Func<T, bool> _predicate;
      readonly List<T> _filtered_items = new List<T>();
      readonly HashSet<string> _observed_properties_set;
      //readonly IStrongObservableCollection<T> _filtered_items = ObservableCollectionFactory.Create<T>();

      public FilteredObservableCollectionWrapper(
         IStrongReadOnlyObservableCollection<T> source,
         IComparer<T> comparer,
         IEqualityComparer<T> eq_comparer,
         Func<T, bool> predicate,
         string[] observed_properties) {
         //
         _source = source;
         _comparer = comparer;
         _eq_comparer = eq_comparer;
         _predicate = predicate;
         _observed_properties_set = new HashSet<string>(observed_properties);
         _source.CollectionChanged += _on_source_collection_changed;
      }

      public T this[int index] =>
         _filtered_items[index];

      public int Count =>
         _filtered_items.Count;

      public event NotifyCollectionChangedEventHandler<T> CollectionChanged;

      event NotifyCollectionChangedEventHandler INotifyCollectionChanged.CollectionChanged {
         add => _ng_collection_changed += value;
         remove => _ng_collection_changed -= value;
      }

      event NotifyCollectionChangedEventHandler _ng_collection_changed;
      public event PropertyChangedEventHandler PropertyChanged;

      public IEnumerator<T> GetEnumerator() =>
         _filtered_items.GetEnumerator();

      IEnumerator IEnumerable.GetEnumerator() =>
         _filtered_items.GetEnumerator();

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
               _filtered_items.Clear();
               CollectionChanged?.Invoke(this, NotifyCollectionChangedEventArgs<T>.create_reset());
               break;
         }
      }

      void _on_added(IList<T> items) {
         var batch = new List<T>(1);
         int batch_start = -1;
         foreach (var v in items) {
            if (_predicate(v)) {
               int i = CollectionHelper.binary_search(_filtered_items, v, _comparer, _eq_comparer);
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
            if (_observed_properties_set.Count > 0 && v is INotifyPropertyChanged npc) {
               npc.PropertyChanged += _on_item_property_changed;
            }
         }
         if (batch.Count > 0)
            _insert_range(batch_start, batch);
      }

      void _on_removed(IList<T> items) {
         var batch = new List<T>(1);
         int batch_start = -1;
         foreach (var v in items) {
            if (_predicate(v)) {
               int i = CollectionHelper.binary_search(_filtered_items, v, _comparer, _eq_comparer);
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
            if (_observed_properties_set.Count > 0 && v is INotifyPropertyChanged npc) {
               npc.PropertyChanged -= _on_item_property_changed;
            }
         }
         if (batch.Count > 0)
            _remove_range(batch_start, batch);
      }

      void _on_item_property_changed(object sender, PropertyChangedEventArgs e) {
         if (_observed_properties_set.Contains(e.PropertyName) && sender is T item) {
            int pos = CollectionHelper.binary_search(_filtered_items, item, _comparer);
            if (_predicate(item)) {
               if (pos < 0)
                  _insert(~pos, item);
            } else if (pos >= 0) {
               _remove(pos);
            }
         }
      }

      void _insert(int index, T item) {
         _filtered_items.Insert(index, item);
         CollectionChanged?.Invoke(this, NotifyCollectionChangedEventArgs<T>.create_added(item, index));
         _on_count_changed();
      }

      void _insert_range(int index, IList<T> items) {
         _filtered_items.InsertRange(index, items);
         CollectionChanged?.Invoke(this, NotifyCollectionChangedEventArgs<T>.create_added(items, index));
         _on_count_changed();
      }

      void _remove(int index) {
         if (CollectionChanged != null) {
            var old_item = _filtered_items[index];
            _filtered_items.RemoveAt(index);
            CollectionChanged(this, NotifyCollectionChangedEventArgs<T>.create_removed(old_item, index));
         } else {
            _filtered_items.RemoveAt(index);
         }
         _on_count_changed();
      }

      void _remove_range(int index, IList<T> items) {
         _filtered_items.RemoveRange(index, items.Count);
         CollectionChanged(this, NotifyCollectionChangedEventArgs<T>.create_removed(items, index));
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
using Collections.Sync.Extensions;
using Collections.Sync.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Collections.Sync.Collections.Impl {
   class SortedObservableCollectionWrapper<T> : IReadOnlyObservableCollection<T> {
      const int _min_capacity = 4;
      readonly IComparer<T> _comparer;
      T[] _values;
      int _size;
      readonly INotifyCollectionChanged _src_notifier;
      readonly ICollection<T> _src_collection;

      public SortedObservableCollectionWrapper(INotifyCollectionChanged notifier, ICollection<T> collection, IComparer<T> comparer) {
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
}

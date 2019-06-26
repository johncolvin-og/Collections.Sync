using Collections.Sync.Extensions;
using Collections.Sync.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace Collections.Sync {
   public class ObservableKeyedCollection<T, TKey> : ObservableCollection<T>, IObservableCollection<T>, IDictionary<TKey, T>, IReadOnlyObservableKeyedCollection<T, TKey> {
      readonly Func<T, TKey> _key;
      readonly Dictionary<TKey, T> _map;

      public ObservableKeyedCollection(Func<T, TKey> key)
         : this(key, EqualityComparer<TKey>.Default) { }

      public ObservableKeyedCollection(Func<T, TKey> key, IEqualityComparer<TKey> key_comparer) {
         _key = key;
         _map = new Dictionary<TKey, T>(key_comparer);
      }

      public ICollection<TKey> Keys => _map.Keys;
      public ICollection<T> Values => _map.Values;
      public bool IsReadOnly => false;
      public T this[TKey key] {
         get => _map[key];
         set {
            TKey item_key = _key(value);
            if (!key.Equals(item_key))
               throw new ArgumentException("Specified key does not match that of specified value.", nameof(value));
            if (_map.TryGetValue(key, out T curr)) {
               if (ReferenceEquals(value, curr))
                  return;
               int index = Items.IndexOf(curr);
               Items[index] = value;
               _map[key] = value;
               OnPropertyChanged(new PropertyChangedEventArgs(CollectionHelper.indexer_name));
               OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, value, curr, index));
            } else {
               Items.Add(value);
               _map.Add(key, value);
               _notify_props_changed();
               OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, value, Count - 1));
            }
         }
      }

      IEnumerable<TKey> IReadOnlyDictionary<TKey, T>.Keys => Keys;
      IEnumerable<T> IReadOnlyDictionary<TKey, T>.Values => Values;

      public IDictionary<TKey, T> AsDictionary() => this;
      public ObservableCollection<T> AsCollection() => this;

      public bool TryGetValue(TKey key, out T result) =>
         _map.TryGetValue(key, out result);

      public new bool Add(T item) {
         CheckReentrancy();
         TKey k = _key(item);
         if (_map.ContainsKey(k))
            return false;
         _map.Add(k, item);
         Items.Add(item);
         _notify_props_changed();
         OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, Count - 1));
         return true;
      }

      public bool Remove(TKey key) {
         if (_map.TryGetValue(key, out T value)) {
            int index = Items.IndexOf(value);
            if (index < 0) throw new InvalidOperationException("Map out of sync with items.");
            _map.Remove(key);
            Items.RemoveAt(index);
            _notify_props_changed();
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, value, index));
            return true;
         }
         return false;
      }

      public bool Replace(TKey old_key, T new_value) {
         if (_map.TryGetValue(old_key, out T old_value)) {
            int index = Items.IndexOf(old_value);
            if (index < 0) throw new InvalidOperationException("Map out of sync with items.");
            _map.Remove(old_key);
            _map[_key(new_value)] = new_value;
            Items[index] = new_value;
            OnPropertyChanged(new PropertyChangedEventArgs(CollectionHelper.indexer_name));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, new_value, old_value, index));
            return true;
         }
         return false;
      }

      public void SyncWithKeys(IEnumerable<TKey> keys, Func<TKey, T> factory) {
         var keys_list = new List<TKey>(keys);
         var changes = Diff.diff(this.Keys, keys).ToList();
         int ch_count = changes.Count;
         for (int ch_pos = 0; ch_pos < ch_count; ch_pos++) {
            var ch = changes[ch_pos];
            for (int i = ch.start_left; i < (ch.start_left + ch.deleted_left); i++)
               RemoveAt(i);
            for (int i = 0; i < ch.inserted_right; i++) {
               var k = keys_list[ch.start_right + i];
               var v = factory(k);
               int index = ch.start_left + i;
               Items.Insert(index, v);
               _map.Add(k, v);
               _notify_props_changed();
               OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, v, index));
            }
            for (int i = ch_pos + 1; i < ch_count; i++) {
               // (f)orward (ch)ange
               var fch = changes[i];
               if (fch.start_left >= ch.start_left) {
                  fch.start_left += (fch.inserted_right - fch.deleted_left);
                  changes[i] = fch;
               }
            }
         }
      }

      public void SyncWithDictionary<TModel>(IReadOnlyDictionary<TKey, TModel> dictionary, Func<TModel, T> factory) {
         foreach (var k in _map.Keys.Except(dictionary.Keys).ToList())
            Remove(k);
         foreach (var kv in dictionary) {
            if (!ContainsKey(kv.Key))
               Add(factory(kv.Value));
         }
      }

      public void SyncWithDictionary<TModel>(IReadOnlyDictionary<TKey, TModel> dictionary, Func<TModel, T> factory, Action<TModel, T> update) {
         foreach (var k in _map.Keys.Except(dictionary.Keys).ToList())
            Remove(k);
         foreach (var kv in dictionary) {
            if (TryGetValue(kv.Key, out T curr))
               update(kv.Value, curr);
            else
               Add(factory(kv.Value));
         }
      }

      protected override void ClearItems() {
         _map.Clear();
         base.ClearItems();
      }

      protected override void InsertItem(int index, T item) {
         CheckReentrancy();
         TKey k = _key(item);
         if (_map.ContainsKey(k))
            throw new InvalidOperationException("[Item]Key already added.");
         _map.Add(k, item);
         Items.Insert(index, item);
         _notify_props_changed();
         OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
      }

      protected override void SetItem(int index, T item) {
         _remove(index);
         base.SetItem(index, item);
         _set(item);
      }

      protected override void RemoveItem(int index) {
         _remove(index);
         base.RemoveItem(index);
      }

      void _remove(int index) => _map.Remove(_key(Items[index]));
      void _set(T item) => _map[_key(item)] = item;
      void _notify_props_changed() {
         OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
         OnPropertyChanged(new PropertyChangedEventArgs(CollectionHelper.indexer_name));
      }

      public bool ContainsKey(TKey key) => _map.ContainsKey(key);
      public void Add(TKey key, T value) => this[key] = value;
      public void Add(KeyValuePair<TKey, T> item) => this[item.Key] = item.Value;
      public bool Contains(KeyValuePair<TKey, T> item) => _map.Contains(item);
      public void CopyTo(KeyValuePair<TKey, T>[] array, int arrayIndex) {
         int len = Count;
         for(int i = 0; i < len; i++) {
            T v = Items[i];
            array[arrayIndex + i] = new KeyValuePair<TKey, T>(_key(v), v);
         }
      }
      public bool Remove(KeyValuePair<TKey, T> item) => _key(item.Value).Equals(item.Key) ?
         Remove(item.Value) : throw new ArgumentException("KeyValuePair.Key does not match KeyValuePair.Value's functional key.");
      IEnumerator<KeyValuePair<TKey, T>> IEnumerable<KeyValuePair<TKey, T>>.GetEnumerator() {
         /// Don't iterate through <see cref="_map"/>; item-order should match the collection's.
         int len = Count;
         for (int i = 0; i < len; i++) {
            T v = Items[i];
            yield return new KeyValuePair<TKey, T>(_key(v), v);
         }
      }
   }
   
   static class ObservableKeyedCollectionExtensions {
      public static IReadOnlyObservableKeyedCollection<TResult, TKey> WrapSelector<T, TKey, TResult>(this IReadOnlyObservableKeyedCollection<T, TKey> source, Func<T, TResult> selector) =>
         new SelectorWrapper<T, TKey, TResult>(source, selector);

      class SelectorWrapper<T, TKey, TResult> : IReadOnlyObservableKeyedCollection<TResult, TKey> {
         readonly IReadOnlyObservableKeyedCollection<T, TKey> _src;
         readonly IReadOnlyList<T> _src_list;
         readonly IReadOnlyDictionary<TKey, T> _src_dictionary;
         readonly Func<T, TResult> _selector;

         public SelectorWrapper(IReadOnlyObservableKeyedCollection<T, TKey> source, Func<T, TResult> selector) {
            _src = source;
            _src_list = source;
            _src_dictionary = source;
            _selector = selector;
            _src.CollectionChanged += on_collection_changed;
            void on_collection_changed(object sender, NotifyCollectionChangedEventArgs e) {
               if (CollectionChanged == null)
                  return;
               NotifyCollectionChangedEventArgs wrapped;
               switch (e.Action) {
                  case NotifyCollectionChangedAction.Add:
                     wrapped = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add,
                        e.NewItems.WrapSelectorHidden(from_obj), e.NewStartingIndex);
                     break;
                  case NotifyCollectionChangedAction.Remove:
                     wrapped = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove,
                        e.OldItems.WrapSelectorHidden(from_obj), e.OldStartingIndex);
                     break;
                  case NotifyCollectionChangedAction.Replace:
                     wrapped = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace,
                        e.OldItems.WrapSelectorHidden(from_obj), e.OldItems.WrapSelectorHidden(from_obj), e.OldStartingIndex);
                     break;
                  case NotifyCollectionChangedAction.Move:
                     wrapped = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace,
                        e.OldItems.WrapSelectorHidden(from_obj), e.OldItems.WrapSelectorHidden(from_obj), e.OldStartingIndex);
                     break;
                  case NotifyCollectionChangedAction.Reset:
                     wrapped = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);
                     break;
                  default: throw new InvalidOperationException($"Unexpected {nameof(NotifyCollectionChangedAction)} '{e.Action}.'");
               }
               CollectionChanged(this, wrapped);
            }
            TResult from_obj(object obj) => selector((T)obj);
         }

         public event NotifyCollectionChangedEventHandler CollectionChanged;

         public TResult this[int index] => _selector(_src_list[index]);
         public TResult this[TKey key] => _selector(_src_dictionary[key]);

         public int Count => _src_list.Count;
         public IEnumerable<TKey> Keys => _src.Keys;
         public IEnumerable<TResult> Values => _src.Values.Select(_selector);

         public bool ContainsKey(TKey key) => _src.ContainsKey(key);
         public IEnumerator<TResult> GetEnumerator() {
            foreach (var v in _src_list)
               yield return _selector(v);
         }
         public bool TryGetValue(TKey key, out TResult value) {
            if (_src.TryGetValue(key, out T sv)) {
               value = _selector(sv);
               return true;
            }
            value = default(TResult);
            return false;
         }
         IEnumerator<KeyValuePair<TKey, TResult>> IEnumerable<KeyValuePair<TKey, TResult>>.GetEnumerator() {
            foreach (var kv in _src_dictionary)
               yield return new KeyValuePair<TKey, TResult>(kv.Key, _selector(kv.Value));
         }
         IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
      }
   }
}
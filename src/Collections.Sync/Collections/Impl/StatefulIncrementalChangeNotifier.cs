using Collections.Sync.Extensions;
using Collections.Sync.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace Collections.Sync.Collections.Impl {
   /// <summary>
   /// This interface extends <see cref="IIncrementalChangeNotifier{T}"/> 
   /// by broadcasting immutable item state changes (with the <see cref="Changed"/> event).
   /// Typical agents that require such item-state change notifications are ones that apply sorting/filtering.
   /// </summary>
   /// <typeparam name="T"></typeparam>
   /// <typeparam name="TState"></typeparam>
   public interface IStatefulIncrementalChangeNotifier<T, TState> : IIncrementalChangeNotifier<T> {
      event Action<IEnumerable<(T item, TState old_state, TState new_state)>> Changed;
      //TState GetState(T item);
   }

   public static class StatefulIncrementalChangeNotifier {
      public static IStrongReadOnlyObservableCollection<T> ToSortedObservableCollection<T, TState>(this IStatefulIncrementalChangeNotifier<T, TState> source, IComparer<TState> comparer) =>
         new SortedImpl<T, TState>(source, comparer);

      public static IStatefulIncrementalChangeNotifier<T, TState> ToStateful<T, TState, TKey>(this IIncrementalChangeNotifier<T> source, Func<T, TState> state_fn, Func<T, TKey> key_fn) where T : INotifyPropertyChanged {
         return new StatefulImpl<T, TState, TKey>(source, state_fn, key_fn, attach_state_listener);
         IDisposable attach_state_listener(T item, Action<(TState old_state, TState new_state)> state_changed_callback) {
            var state = state_fn(item);
            item.PropertyChanged += on_property_changed;
            return Disposable.Create(() => item.PropertyChanged -= on_property_changed);
            void on_property_changed(object sender, PropertyChangedEventArgs e) {
               var old_state = state;
               state = state_fn(item);
               state_changed_callback((old_state, state));
            }
         }
      }

      public static IStatefulIncrementalChangeNotifier<T, TState> ToStateful<T, TState, TKey>(
         this IIncrementalChangeNotifier<T> source,
         Func<T, TState> state_fn,
         Func<T, TKey> key_fn,
         Func<T, Action<(TState old_state, TState new_state)>, IDisposable> attach_state_listener) =>
         //
         new StatefulImpl<T, TState, TKey>(source, state_fn, key_fn, attach_state_listener);

      class FilteredStatefulImpl<T, TState> : IStatefulIncrementalChangeNotifier<T, TState> {
         readonly IStatefulIncrementalChangeNotifier<T, TState> _source;
         readonly Func<TState, bool> _predicate;

         public FilteredStatefulImpl(IStatefulIncrementalChangeNotifier<T, TState> source, Func<TState, bool> predicate) {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
            _source.Added += _on_added;
            _source.Removed += _on_removed;
            _source.Reset += _on_reset;
            _source.Changed += _on_changed;
         }

         public event Action<IEnumerable<(T item, TState old_state, TState new_state)>> Changed;
         public event Action<IEnumerable<T>> Added;
         public event Action<IEnumerable<T>> Removed;
         public event Action Reset;

         public IEnumerable<T> Items =>
            _source.Items.Where(i => _predicate(GetState(i)));

         void _on_added(IEnumerable<T> items) =>
            Added?.Invoke(items.Where(i => _predicate(GetState(i))));

         void _on_removed(IEnumerable<T> items) =>
            Removed?.Invoke(items.Where(i => _predicate(GetState(i))));

         void _on_reset() => throw new NotImplementedException();

         void _on_changed(IEnumerable<(T item, TState old_state, TState new_state)> changes) {
            foreach (var (item, old_state, new_state) in changes) {
               if (_predicate(old_state)) {
                  if (_predicate(new_state)) {
                     Changed?.Invoke(new[] { (item, old_state, new_state) });
                  } else {
                     Removed?.Invoke(new[] { item });
                  }
               } else if (_predicate(new_state)) {
                  Added?.Invoke(new[] { item });
               }
            }
         }
      }

      class StatefulImpl<T, TState, TKey> : IStatefulIncrementalChangeNotifier<T, TState> {
         readonly IIncrementalChangeNotifier<T> _source;
         readonly Func<T, TKey> _key_fn;
         readonly Func<T, TState> _state_fn;
         readonly Dictionary<TKey, IDisposable> _cache = new Dictionary<TKey, IDisposable>();
         //readonly Dictionary<TKey, (T item, Action<(TState old_state, TState new_state)> state_changed_callback)> _cache =
         //   new Dictionary<TKey, (T item, Action<(TState old_state, TState new_state)> state_changed_callback)>();
         readonly Func<T, Action<(TState old_state, TState new_state)>, IDisposable> _attach_state_listener;
         //readonly Action<T, Action<(TState old_state, TState new_state)>> _attach_state_listener, _detach_state_listener;

         public StatefulImpl(
            IIncrementalChangeNotifier<T> source,
            Func<T, TState> state_fn,
            Func<T, TKey> key_fn,
            Func<T, Action<(TState old_state, TState new_state)>, IDisposable> attach_state_listener) {
            //Action<T, Action<(TState old_state, TState new_state)>> attach_state_listener,
            //Action<T, Action<(TState old_state, TState new_state)>> detach_state_listener) {
            //
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _state_fn = state_fn ?? throw new ArgumentNullException(nameof(state_fn));
            _key_fn = key_fn ?? throw new ArgumentNullException(nameof(key_fn));
            _attach_state_listener = attach_state_listener ?? throw new ArgumentNullException(nameof(attach_state_listener));
            //_detach_state_listener = detach_state_listener ?? throw new ArgumentNullException(nameof(detach_state_listener));
            _source.Added += _on_added;
            _source.Removed += _on_removed;
            _source.Reset += _on_reset;
         }

         public IEnumerable<T> Items => _source.Items;

         public event Action<IEnumerable<T>> Added { add => _source.Added += value; remove => _source.Added -= value; }
         public event Action<IEnumerable<T>> Removed { add => _source.Removed += value; remove => _source.Removed -= value; }
         public event Action Reset { add => _source.Reset += value; remove => _source.Reset -= value; }

         public event Action<IEnumerable<(T item, TState old_state, TState new_state)>> Changed;

         public TState GetState(T item) => _state_fn(item);

         void _on_added(IEnumerable<T> items) {
            foreach (var item in items) {
               TState state = default;
               var key = _key_fn(item);
               _cache.Add(key, _attach_state_listener(item, on_item_property_changed));
               _attach_state_listener(item, on_item_property_changed);
               void on_item_property_changed((TState old_state, TState new_state) ch) {
                  var old_state = state;
                  state = ch.new_state;
                  Changed?.Invoke(new[] { (item, old_state, state) });
               }
            }
         }

         void _on_removed(IEnumerable<T> items) {
            foreach (var item in items) {
               if (!_cache.Remove(_key_fn(item), out var dispose))
                  throw new InvalidOperationException("The source notifier fired a remove event with an item who's key was not present in the cache " +
                     "(either the key mutated, or the item was added to the source without a corresponding Add event).");
               dispose.Dispose();
            }
         }

         void _on_reset() {
            foreach (var d in _cache.Values)
               d.Dispose();
            _cache.Clear();
         }
      }

      class SortedImpl<T, TState> : IStrongReadOnlyObservableCollection<T> {
         readonly IStatefulIncrementalChangeNotifier<T, TState> _source;
         readonly IComparer<TState> _comparer;
         readonly IComparer<T> _item_comparer;
         readonly List<(T item, TState state)> _item_states = new List<(T item, TState state)>();
         readonly IList<T> _items_wrapper;
         readonly IList<TState> _states_wrapper;

         public SortedImpl(IStatefulIncrementalChangeNotifier<T, TState> source, IComparer<TState> comparer) {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
            _states_wrapper = _item_states.WrapSelector(tup => tup.state, null);
            _items_wrapper = _item_states.WrapSelector(tup => tup.item, item => (item, _source.GetState(item)));
            _source.Added += _on_added;
            _source.Changed += _on_changed;
            _source.Removed += _on_removed;
            _source.Reset += _on_reset;
         }

         public event NotifyCollectionChangedEventHandler<T> CollectionChanged;
         public event PropertyChangedEventHandler PropertyChanged;
         event NotifyCollectionChangedEventHandler _ng_collection_changed;

         public int Count => _item_states.Count;

         public T this[int index] => _item_states[index].item;

         event NotifyCollectionChangedEventHandler INotifyCollectionChanged.CollectionChanged {
            add => _ng_collection_changed += value;
            remove => _ng_collection_changed -= value;
         }

         public IEnumerator<T> GetEnumerator() => _items_wrapper.GetEnumerator();
         IEnumerator IEnumerable.GetEnumerator() => _items_wrapper.GetEnumerator();

         void _on_added(IEnumerable<T> items) {
            foreach (var v in items) {
               int index = CollectionHelper.binary_search(_items_wrapper, v, _item_comparer);
               _insert(index < 0 ? ~index : index, v);
            }
         }

         void _on_changed(IEnumerable<(T item, TState old_state, TState new_state)> changes) {
            foreach (var (item, old_state, new_state) in changes) {
               int old_pos = CollectionHelper.binary_search(_states_wrapper, old_state, _comparer);
               if (old_pos < 0)
                  throw new InvalidOperationException("States are out of sync (binary search failed to find the old state of a changed item).");
               int new_pos = CollectionHelper.binary_search(_states_wrapper, new_state, _comparer);
               if (new_pos < 0)
                  new_pos = ~new_pos;
               if (Math.Abs(new_pos - old_pos) > 1) {
                  _remove_at(old_pos);
                  _insert(new_pos, item);
               }
            }
         }

         void _on_count_changed() {
            if (PropertyChanged != null) {
               PropertyChanged(this, new PropertyChangedEventArgs(nameof(Count)));
               PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(CollectionHelper.indexer_name));
            }
         }

         void _on_removed(IEnumerable<T> items) {
            foreach (var v in items)
               _remove_at(CollectionHelper.binary_search(_items_wrapper, v, _item_comparer));
         }

         void _on_reset() {
            if (Count > 0) {
               CollectionChanged?.Invoke(this, NotifyCollectionChangedEventArgs<T>.create_reset());
               _ng_collection_changed?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
               _on_count_changed();
            }
         }

         void _insert(int index, T item) {
            _items_wrapper.Insert(index, item);
            CollectionChanged?.Invoke(this, NotifyCollectionChangedEventArgs<T>.create_added(item, index));
            _ng_collection_changed?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
            _on_count_changed();
         }

         void _remove_at(int index) {
            var v = _items_wrapper[index];
            CollectionChanged?.Invoke(this, NotifyCollectionChangedEventArgs<T>.create_removed(v, index));
            _ng_collection_changed?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, v, index));
            _on_count_changed();
         }
      }
   }
}

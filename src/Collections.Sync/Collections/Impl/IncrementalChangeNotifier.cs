using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Collections.Sync.Collections.Impl {
   /// <summary>
   /// Broadcasts incremental change events, without relative positions (item indices).
   /// This provides the infrastructure for lazy 'linq style' operator chains to power observable collections.
   /// </summary>
   /// <typeparam name="T"></typeparam>
   public interface IIncrementalChangeNotifier<T> {
      IEnumerable<T> Items { get; }
      event Action<IEnumerable<T>> Added;
      event Action<IEnumerable<T>> Removed;
      event Action Reset;
   }

   public static class IncrementalChangeNotifier {
      public static IIncrementalChangeNotifier<T> ToNotifier<T>(this IStrongReadOnlyObservableCollection<T> source) =>
         new Impl<T>(source);

      public static IIncrementalChangeNotifier<T> ToNotifier<T>(this IStrongReadOnlyObservableCollection<T> source, Func<T, bool> predicate) =>
         predicate == null ? ToNotifier(source) : new FilteredImpl<T>(source.ToNotifier(), predicate);

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
         readonly IIncrementalChangeNotifier<T> _source;
         readonly Func<T, bool> _predicate;

         public FilteredImpl([DisallowNull]IIncrementalChangeNotifier<T> source, [DisallowNull]Func<T, bool> predicate) {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
            _source.Added += _on_added;
            _source.Removed += _on_removed;
         }

         public event Action<IEnumerable<T>> Added;
         public event Action<IEnumerable<T>> Removed;
         public event Action Reset {
            add => _source.Reset += value;
            remove => _source.Reset -= value;
         }

         public IEnumerable<T> Items => _source.Items.Where(_predicate);

         void _on_added(IEnumerable<T> items) => Added?.Invoke(items.Where(_predicate));
         void _on_removed(IEnumerable<T> items) => Removed?.Invoke(items.Where(_predicate));
      }
   }
}

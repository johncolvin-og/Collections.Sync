using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;

namespace Collections.Sync {
   public static class ObservableCollectionFactory {
      public static IStrongObservableCollection<T> Create<T>() =>
         new Impl<T>();

      public static IStrongReadOnlyObservableCollection<T> CreateStrongReadOnly<T>([DisallowNull]IReadOnlyObservableCollection<T> source) =>
         new ReadOnlyImpl<T>(source);

      class Impl<T> : ObservableCollection<T>, IStrongObservableCollection<T> {
         event NotifyCollectionChangedEventHandler<T> _collection_changed;

         event NotifyCollectionChangedEventHandler<T> INotifyCollectionChanged<T>.CollectionChanged {
            add => _collection_changed += value;
            remove => _collection_changed -= value;
         }

         protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e) {
            if (_collection_changed != null) {
               using (BlockReentrancy())
                  _collection_changed(this, NotifyCollectionChangedEventArgs<T>.from_non_generic(e));
            }
            base.OnCollectionChanged(e);
         }
      }

      class ReadOnlyImpl<T> : IStrongReadOnlyObservableCollection<T> {
         readonly IReadOnlyObservableCollection<T> _source;

         public ReadOnlyImpl([DisallowNull]IReadOnlyObservableCollection<T> source) =>
            _source = source ?? throw new ArgumentNullException(nameof(source));

         public T this[int index] => _source[index];

         public int Count => _source.Count;

         public event NotifyCollectionChangedEventHandler<T> CollectionChanged {
            add {
               bool init = _strong_delegate == null;
               _strong_delegate += value;
               if (init)
                  _source.CollectionChanged += _forward_to_strong_delegate;
            }
            remove {
               _strong_delegate -= value;
               if (_strong_delegate == null)
                  _source.CollectionChanged -= _forward_to_strong_delegate;
            }
         }

         event NotifyCollectionChangedEventHandler INotifyCollectionChanged.CollectionChanged {
            add => _source.CollectionChanged += value;
            remove => _source.CollectionChanged -= value;
         }

         event NotifyCollectionChangedEventHandler<T> _strong_delegate;

         public IEnumerator<T> GetEnumerator() =>
            _source.GetEnumerator();

         IEnumerator IEnumerable.GetEnumerator() =>
            _source.GetEnumerator();

         void _forward_to_strong_delegate(object sender, NotifyCollectionChangedEventArgs e) =>
            _strong_delegate?.Invoke(sender, NotifyCollectionChangedEventArgs<T>.from_non_generic(e));
      }
   }
}

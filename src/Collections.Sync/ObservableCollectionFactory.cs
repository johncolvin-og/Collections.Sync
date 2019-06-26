using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace Collections.Sync {
   public static class ObservableCollectionFactory {
      public static IObservableCollection<T> Create<T>() =>
         new Impl<T>();

      class Impl<T> : ObservableCollection<T>, IObservableCollection<T>, INotifyCollectionChanged<T> {
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
   }
}
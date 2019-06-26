using System.Collections.Generic;
using System.Collections.Specialized;

namespace Collections.Sync {
   public interface IReadOnlyObservableKeyedCollection<T, TKey> : IReadOnlyObservableCollection<T>, IReadOnlyDictionary<TKey, T>, INotifyCollectionChanged { }
}
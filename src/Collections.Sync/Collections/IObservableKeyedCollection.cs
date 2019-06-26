using System.Collections.Generic;
using System.Collections.Specialized;

namespace Collections.Sync {
   public interface IObservableKeyedCollection<T, TKey> : IObservableCollection<T>, IDictionary<T, TKey> { }
}
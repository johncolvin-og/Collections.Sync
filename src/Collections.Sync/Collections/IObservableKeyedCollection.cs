using System.Collections.Generic;

namespace Collections.Sync {
   public interface IObservableKeyedCollection<T, TKey> : IObservableCollection<T>, IDictionary<T, TKey> { }
}
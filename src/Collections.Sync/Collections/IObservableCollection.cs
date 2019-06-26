using System.Collections.Generic;
using System.Collections.Specialized;

namespace Collections.Sync {
   public interface IObservableCollection<T> : IList<T>, INotifyCollectionChanged { }
}
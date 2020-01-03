using System.Collections.Generic;
using System.Collections.Specialized;

namespace Collections.Sync {
   public interface IReadOnlyObservableCollection<out T> : IReadOnlyList<T>, IReadOnlyCollection<T>, INotifyCollectionChanged { }
   public interface IStrongReadOnlyObservableCollection<T> : IReadOnlyObservableCollection<T>, INotifyCollectionChanged<T> { }
}
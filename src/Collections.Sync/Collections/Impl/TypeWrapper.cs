using System.Collections;
using System.Collections.Generic;

namespace Collections.Sync.Collections.Impl {
   class TypeWrapper<T> : IList<T>, IReadOnlyList<T> {
      readonly IList _source;

      public TypeWrapper(IList source) =>
         _source = source;

      public T this[int index] {
         get => _source[index] is T v ? v : default;
         set => _source[index] = value;
      }
      public int Count => _source.Count;
      public bool IsReadOnly => _source.IsReadOnly;

      public void Add(T item) => _source.Add(item);
      public void Clear() => _source.Clear();
      public bool Contains(T item) => _source.Contains(item);
      public void CopyTo(T[] array, int array_index) => _source.CopyTo(array, array_index);
      public IEnumerator<T> GetEnumerator() {
         for (int i = 0; i < _source.Count; i++)
            yield return _source[i] is T v ? v : default;
      }
      public int IndexOf(T item) => _source.IndexOf(item);
      public void Insert(int index, T item) => _source.Insert(index, item);
      public bool Remove(T item) {
         int index = _source.IndexOf(item);
         if (index >= 0) {
            _source.RemoveAt(index);
            return true;
         } else return false;
      }
      public void RemoveAt(int index) => _source.RemoveAt(index);
      IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
   }
}

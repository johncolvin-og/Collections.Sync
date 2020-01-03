using System;
using System.Collections;
using System.Collections.Generic;

namespace Collections.Sync.Collections.Impl {
   // unlike new T[] { value }, this struct prohibits changing the value at index 0.
   struct ValueListWrapper<T> : IList<T> {
      readonly T _value;

      public ValueListWrapper(T value) =>
         _value = value;

      public T this[int index] {
         get => index == 0 ? _value : throw new IndexOutOfRangeException();
         set => throw new NotSupportedException();
      }
      public int Count => 1;
      public bool IsReadOnly => true;

      void ICollection<T>.Add(T item) => throw new NotSupportedException();
      void ICollection<T>.Clear() => throw new NotSupportedException();
      public bool Contains(T item) => _value.Equals(item);
      public void CopyTo(T[] array, int arrayIndex) => array[arrayIndex] = _value;
      public IEnumerator<T> GetEnumerator() {
         yield return _value;
      }
      public int IndexOf(T item) => _value.Equals(item) ? 0 : -1;
      void IList<T>.Insert(int index, T item) => throw new NotSupportedException();
      bool ICollection<T>.Remove(T item) => throw new NotSupportedException();
      void IList<T>.RemoveAt(int index) => throw new NotSupportedException();
      IEnumerator IEnumerable.GetEnumerator() {
         yield return _value;
      }
   }
}

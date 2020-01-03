using System;
using System.Collections;
using System.Collections.Generic;

namespace Collections.Sync.Collections.Impl {
   class SubRangeWrapper<T> : IList<T>, IReadOnlyList<T> {
      readonly IList<T> _source;
      readonly int _start;

      public SubRangeWrapper(IList<T> source, int start, int count) {
         _source = source;
         _start = start;
         Count = count;
      }

      public T this[int index] {
         get => _source[_convert_index(index)];
         set => _source[_convert_index(index)] = value;
      }
      public int Count { get; private set; }
      public bool IsReadOnly => _source.IsReadOnly;

      public void Add(T item) {
         int index = _start + Count;
         if (index < _source.Count) {
            _source.Insert(index, item);
         } else _source.Add(item);
         Count++;
      }
      public void Clear() {
         if (_source is List<T> l) {
            /// Favor <see cref="List{T}.RemoveRange(int, int)"/>; it is optimized.
            l.RemoveRange(_start, Count);
            Count = 0;
         } else {
            while (Count > 0) {
               _source.RemoveAt(_start);
               Count--;
            }
         }
      }
      public bool Contains(T item) => IndexOf(item) >= 0;
      public void CopyTo(T[] array, int arrayIndex) {
         int i = 0;
         foreach (T v in this)
            array[arrayIndex + i] = v;
      }
      public IEnumerator<T> GetEnumerator() {
         int stop = _start + Count;
         for (int i = _start; i < stop; i++)
            yield return _source[i];
      }
      public int IndexOf(T item) {
         int i = 0;
         foreach (T v in this) {
            if (v.Equals(item))
               return i;
            i++;
         }
         return -1;
      }
      public void Insert(int index, T item) {
         _source.Insert(_convert_index(index), item);
         Count++;
      }
      public bool Remove(T item) {
         int i = IndexOf(item);
         if (i >= 0) {
            _source.RemoveAt(i);
            Count--;
            return true;
         }
         return false;
      }
      public void RemoveAt(int index) {
         _source.RemoveAt(_convert_index(index));
         Count--;
      }
      IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

      int _convert_index(int index) {
         if (index >= Count || index < 0) throw new ArgumentOutOfRangeException(nameof(index));
         return _start + index;
      }
   }
}

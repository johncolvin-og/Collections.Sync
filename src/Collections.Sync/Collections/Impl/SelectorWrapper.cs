using System;
using System.Collections;
using System.Collections.Generic;

namespace Collections.Sync.Collections.Impl {
   class SelectorWrapper<T> : IList<T>, IList, IReadOnlyList<T> {
      readonly Func<object, T> _convert;
      readonly Func<T, object> _convert_back;
      readonly IList _source;

      public SelectorWrapper(IList source, Func<object, T> convert, Func<T, object> convert_back) {
         _source = source;
         _convert = convert;
         _convert_back = convert_back;
      }

      public T this[int index] {
         get => _convert(_source[index]);
         set => _source[index] = _convert_back(value);
      }
      public int Count => _source.Count;
      public bool IsReadOnly => _source.IsReadOnly && _convert_back != null;

      public void Add(T item) => _source.Add(_convert_back(item));
      public void Clear() => _source.Clear();
      public bool Contains(T item) => _source.Contains(_convert_back(item));
      public void CopyTo(T[] array, int arrayIndex) {
         for (int i = 0; i < _source.Count; i++)
            array[arrayIndex + i] = _convert(_source[i]);
      }
      public IEnumerator<T> GetEnumerator() {
         for (int i = 0; i < _source.Count; i++)
            yield return _convert(_source[i]);
      }
      public int IndexOf(T item) => _source.IndexOf(_convert_back(item));
      public void Insert(int index, T item) => _source.Insert(index, _convert_back(item));
      public bool Remove(T item) {
         int i = _source.IndexOf(_convert_back(item));
         if (i >= 0) {
            _source.RemoveAt(i);
            return true;
         } else return false;
      }
      public void RemoveAt(int index) => _source.RemoveAt(index);

      #region IList (non-generic)
      object IList.this[int index] {
         get => this[index];
         set {
            if (value is T v)
               this[index] = v;
         }
      }
      bool IList.IsFixedSize => _source.IsFixedSize;
      bool IList.IsReadOnly => IsReadOnly;
      bool ICollection.IsSynchronized => _source.IsSynchronized;
      object ICollection.SyncRoot => _source.SyncRoot;
      int IList.Add(object value) {
         if (value is T v) {
            return _source.Add(_convert_back(v));
         } else throw new ArgumentException(nameof(value), $"Unexpected type '{value?.GetType()}.'");
      }
      bool IList.Contains(object value) => value is T v && _source.Contains(_convert_back(v));
      void ICollection.CopyTo(Array array, int index) {
         for (int i = 0; i < _source.Count; i++)
            array.SetValue(_convert(_source[index]), index + i);
      }
      IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
      int IList.IndexOf(object value) => value is T v ? IndexOf(v) : -1;
      void IList.Insert(int index, object value) {
         if (value is T v)
            Insert(index, v);
      }
      void IList.Remove(object value) {
         if (value is T v)
            Remove(v);
      }
      void IList.RemoveAt(int index) => RemoveAt(index);
      #endregion
   }

   class SelectorWrapper<TIn, TOut> : IList<TOut>, IReadOnlyList<TOut> {
      readonly Func<TIn, TOut> _convert;
      readonly Func<TOut, TIn> _convert_back;
      readonly IList<TIn> _source;

      public SelectorWrapper(IList<TIn> source, Func<TIn, TOut> convert, Func<TOut, TIn> convert_back) {
         _source = source;
         _convert = convert;
         _convert_back = convert_back;
      }

      public TOut this[int index] {
         get => _convert(_source[index]);
         set => _source[index] = _convert_back(value);
      }
      public int Count => _source.Count;
      public bool IsReadOnly => _source.IsReadOnly && _convert_back != null;
      public void Add(TOut item) => _source.Add(_convert_back(item));
      public void Clear() => _source.Clear();
      public bool Contains(TOut item) => _source.Contains(_convert_back(item));
      public void CopyTo(TOut[] array, int arrayIndex) {
         for (int i = 0; i < _source.Count; i++)
            array[arrayIndex + i] = _convert(_source[i]);
      }
      public IEnumerator<TOut> GetEnumerator() {
         for (int i = 0; i < _source.Count; i++)
            yield return _convert(_source[i]);
      }
      public int IndexOf(TOut item) => _source.IndexOf(_convert_back(item));
      public void Insert(int index, TOut item) => _source.Insert(index, _convert_back(item));
      public bool Remove(TOut item) => _source.Remove(_convert_back(item));
      public void RemoveAt(int index) => _source.RemoveAt(index);
      IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
   }
}

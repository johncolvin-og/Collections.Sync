using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Collections.Sync.Collections.Impl {
   static class NonGenericListHelper {
      public static IList CastOrWrap<T>(IList<T> source) =>
         source as IList ?? new NonGenericListWrapper<T>(source, null);
   }

   class NonGenericListWrapper<T> : IList<T>, IList {
      readonly IList<T> _source;
      readonly IList _ng_source;

      public NonGenericListWrapper([DisallowNull]IList<T> source)
         : this(source, source as IList) { }

      public NonGenericListWrapper([DisallowNull]IList<T> source, IList non_generic_source) {
         _source = source ?? throw new ArgumentNullException(nameof(source));
         _ng_source = non_generic_source;
      }

      public T this[int index] {
         get => _source[index];
         set => _source[index] = value;
      }

      object IList.this[int index] {
         get => _ng_source == null ?
            _source[index] :
            _ng_source[index];
         set {
            if (_ng_source == null) {
               _source[index] = (T)value;
            } else {
               _ng_source[index] = value;
            }
         }
      }

      public int Count => _source.Count;

      public bool IsReadOnly => _source.IsReadOnly;

      public bool IsFixedSize =>
         _ng_source != null && _ng_source.IsFixedSize;

      public bool IsSynchronized =>
         _ng_source != null && _ng_source.IsSynchronized;

      public object SyncRoot =>
         _ng_source?.SyncRoot;

      public void Add(T item) =>
         _source.Add(item);

      public int Add(object value) {
         if (_ng_source == null) {
            _source.Add((T)value);
            return _source.Count - 1;
         } else {
            return _ng_source.Add(value);
         }
      }

      public void Clear() =>
         _source.Clear();

      public bool Contains(T item) =>
         _source.Contains(item);

      public bool Contains(object value) =>
         _ng_source == null ?
            _source.Contains((T)value) :
            _ng_source.Contains(value);

      public void CopyTo(T[] array, int arrayIndex) =>
         _source.CopyTo(array, arrayIndex);

      public void CopyTo(Array array, int index) {
         if (_ng_source == null) {
            for (int i = 0; i < _source.Count; i++)
               array.SetValue(_source[i], index + i);
         } else {
            _ng_source.CopyTo(array, index);
         }
      }

      public IEnumerator<T> GetEnumerator() =>
         _source.GetEnumerator();

      public int IndexOf(T item) =>
         _source.IndexOf(item);

      public int IndexOf(object value) =>
         _ng_source == null ?
            _source.IndexOf((T)value) :
            _ng_source.IndexOf(value);

      public void Insert(int index, T item) =>
         _source.Insert(index, item);

      public void Insert(int index, object value) {
         if (_ng_source == null) {
            _source.Insert(index, (T)value);
         } else {
            _ng_source.Insert(index, value);
         }
      }

      public bool Remove(T item) =>
         _source.Remove(item);

      public void Remove(object value) {
         if (_ng_source == null) {
            _source.Remove((T)value);
         } else {
            _ng_source.Remove(value);
         }
      }

      public void RemoveAt(int index) =>
         _source.RemoveAt(index);

      IEnumerator IEnumerable.GetEnumerator() =>
         _ng_source == null ?
            _source.GetEnumerator() :
            _ng_source.GetEnumerator();
   }
}

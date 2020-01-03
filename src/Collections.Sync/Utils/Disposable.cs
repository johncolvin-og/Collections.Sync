using System;
using System.Collections.Generic;

namespace Collections.Sync.Utils {
   static class Disposable {
      public static readonly IDisposable empty = new AnonymousDisposable(null);

      public static IDisposable Create(Action action) =>
         new AnonymousDisposable(action);

      public static IDisposable Create(params IDisposable[] disposables) {
         return Create(impl);
         void impl() {
            for (int i = 0; i < disposables.Length; i++)
               disposables[i]?.Dispose();
         }
      }

      public static void dispose<T>(ref T item) where T : class, IDisposable {
         if (item != null) {
            item.Dispose();
            item = null;
         }
      }

      public static IDisposable wrap_collection<T>(IEnumerable<T> values) where T : IDisposable =>
         Create(() => {
            foreach (var v in values)
               v.Dispose();
         });

      class AnonymousDisposable : IDisposable {
         Action _action;

         public AnonymousDisposable(Action action) =>
            _action = action;

         public void Dispose() =>
            _action?.Invoke();
      }
   }
}

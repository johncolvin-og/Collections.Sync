using System;

namespace Collections.Sync.Utils {
   class AnonymousObserver<T> : IObserver<T> {
      readonly Action<T> _on_next;
      readonly Action _on_completed;
      readonly Action<Exception> _on_error;

      public AnonymousObserver(
         Action<T> on_next,
         Action on_completed = null,
         Action<Exception> on_error = null) {
         _on_next = on_next ?? throw new ArgumentException(nameof(on_next));
         _on_completed = on_completed;
         _on_error = on_error;
      }

      public void OnCompleted() => _on_completed?.Invoke();
      public void OnError(Exception error) => (_on_error ?? throw error).Invoke(error);
      public void OnNext(T value) => _on_next(value);
   }
}

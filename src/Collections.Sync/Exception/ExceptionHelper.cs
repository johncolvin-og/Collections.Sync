using System;
using System.Collections.Generic;
using System.Linq;

namespace Collections.Sync.Utils {
   static class ExceptionHelper {
      public static string FlattenMessage(Exception ex) {
         if (ex is AggregateException) return _flatten_message((AggregateException)ex);
         if (ex.InnerException == null) return ex.Message;
         string message = FlattenMessage(ex.InnerException);
         return message + "\n" + ex.Message;
      }

      public static NotImplementedException unexpected_oneof_case<T>(T oneof_case) where T : struct =>
         new NotImplementedException($"Unexpected {typeof(T)}: {oneof_case}");

      private static string _flatten_message(AggregateException ex) { return _flatten_message(ex.Flatten().InnerExceptions); }

      private static string _flatten_message(IEnumerable<Exception> aggregate) {
         if (!aggregate.Any()) return string.Empty;
         string message = aggregate.First().Message;
         string next = _flatten_message(aggregate.Skip(1));
         if (!string.IsNullOrEmpty(next)) message += "\n" + next;
         return message;
      }
   }
}
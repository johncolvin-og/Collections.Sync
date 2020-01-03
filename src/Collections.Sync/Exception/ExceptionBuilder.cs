using System;

namespace Collections.Sync.Utils {
   static class ExceptionBuilder {
      public static class Argument {
         public static ArgumentException must_be(string description, string name) =>
            new ArgumentException($"Must be {description}.", name);

         public static ArgumentException unexpected_case<T>(T value, string name) =>
            new ArgumentException($"Unexpected {typeof(T)} '{value}.'", name);

         public static ArgumentException not_of_type<T>(object value, string name) =>
            not_of_type(value, name, typeof(T));

         public static ArgumentException not_of_type(object value, string name, Type t) =>
            new ArgumentException($"Value is not of the expected type '{t}.'");
      }

      public static class IndexOutOfRange {
         public static IndexOutOfRangeException lt_zero() =>
            new IndexOutOfRangeException("Index is less than 0");

         public static IndexOutOfRangeException gte_length() =>
            new IndexOutOfRangeException("Index is greater than or equal to the number of elements.");
      }

      public static class ArgumentOutOfRange {
         public static ArgumentOutOfRangeException lt_zero(string name) =>
            new ArgumentOutOfRangeException(name, "Index is less than 0");

         public static ArgumentOutOfRangeException gte_length(string name) =>
            new ArgumentOutOfRangeException(name, "Index is greater than or equal to the number of elements.");
      }

      public static class Format {
         public static string LessThan(int value) => $"{Messages.LessThan} {value}.";
         public static string LessThanOrEqualTo(int value) => $"{Messages.LessThanOrEqualTo} {value}.";
         public static string GreaterThan(int value) => $"{Messages.GreaterThan} {value}.";
         public static string GreaterThanOrEqualTo(int value) => $"{Messages.GreaterThanOrEqualTo} {value}.";
      }

      public static class Messages {
         public const string
            LessThan = "Less than",
            LessThanOrEqualTo = "Less than or equal to",
            GreaterThan = "Greater than",
            GreaterThanOrEqualTo = "Greater than or equal to";
      }
   }
}

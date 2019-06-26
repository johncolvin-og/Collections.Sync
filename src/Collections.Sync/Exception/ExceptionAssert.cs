using System;
using System.Collections.Generic;
using System.Linq;

namespace Collections.Sync.Utils {
   static class ExceptionAssert {
      public static class Argument {
         public static T Is<T>(object argument, string name) {
            if (argument is T t) return t;
            else throw new ArgumentException($"Must be of type {typeof(T)}.", name);
         }

         public static void IsNot<T>(object argument, string name) {
            if (argument is T)
               throw new ArgumentException($"Must not be of type {typeof(T)}.", name);
         }

         public static void must_be_so(bool condition, string condition_description, string name) {
            if (!condition)
               throw ExceptionBuilder.Argument.must_be(condition_description, name);
         }

         public static void NotNull(object argument, string name) {
            if (argument == null)
               throw new ArgumentNullException(name);
         }

         public static void Equals(object argument, object value, string name) {
            if (!argument.Equals(value))
               throw new ArgumentException($"Must equal {value}.", name);
         }

         public static void NotNullAndEquals(object argument, object value, string name) {
            NotNull(argument, name);
            if (!argument.Equals(value))
               throw new ArgumentException($"Must equal {value}.", name);
         }

         public static void GreaterThan<T>(T argument, T value, string name) where T : IComparable<T> {
            if (argument.CompareTo(value) <= 0)
               throw new ArgumentException($"Must be greater than {value}.", name);
         }

         public static void GreaterThanEqualTo<T>(T argument, T value, string name) where T : IComparable<T> {
            if (argument.CompareTo(value) < 0)
               throw new ArgumentException($"Must be greater than {value}.", name);
         }

         public static void NotNullGreaterThan<T>(T argument, T value, string name) where T : class, IComparable<T> {
            NotNull(argument, name);
            GreaterThan(argument, value, name);
         }

         public static void NotNullGreaterThanEqualTo<T>(T argument, T value, string name) where T : class, IComparable<T> {
            NotNull(argument, name);
            GreaterThanEqualTo(argument, value, name);
         }

         public static void LessThan<T>(T argument, T value, string name) where T : IComparable<T> {
            if (argument.CompareTo(value) >= 0)
               throw new ArgumentException($"Must be less than {value}.", name);
         }

         public static void LessThanEqualTo<T>(T argument, T value, string name) where T : IComparable<T> {
            if (argument.CompareTo(value) > 0)
               throw new ArgumentException($"Must be less than {value}.", name);
         }

         public static void NotNullLessThan<T>(T argument, T value, string name) where T : class, IComparable<T> {
            NotNull(argument, name);
            LessThan(argument, value, name);
         }

         public static void HasElements<T>(IEnumerable<T> argument, string name) {
            if (!argument.Any())
               throw new ArgumentException("Must have elements.", name);
         }

         public static void NotNullAndHasElements<T>(IEnumerable<T> argument, string name) {
            NotNull(argument, name);
            HasElements(argument, name);
         }
      }

      public static class InvalidOperation {
         public static void That(bool condition, string message) {
            if (!condition)
               throw new InvalidOperationException(message);
         }

         public static void PropertyNotNull(object property_value, string property_name) {
            if (property_value == null)
               throw new InvalidOperationException($"{property_name} must not be null.");
         }

         public static void PropertyNotNull(params (object value, string name)[] properties) {
            foreach ((object value, string name) prop in properties)
               PropertyNotNull(prop.value, prop.name);
         }
      }
   }
}
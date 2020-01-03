using System;
using System.Collections.Generic;
using System.Linq;

namespace Collections.Sync.Utils {
   // See http://xmailserver.org/diff2.pdf
   static class Diff {
      public static IEnumerable<Item> diff<T>(IEnumerable<T> left, IEnumerable<T> right, IEqualityComparer<T> comp = null) {
         var left_list = left as IList<T> ?? left.ToList();
         var right_list = right as IList<T> ?? right.ToList();

         Dictionary<T, int> cache = new Dictionary<T, int>(comp ?? EqualityComparer<T>.Default);
         var left_data = new DiffData(_diff_codes(left_list, cache));
         var right_data = new DiffData(_diff_codes(right_list, cache));

         int MAX = left_list.Count + right_list.Count + 1;
         int[] downVector = new int[2 * MAX + 2];
         int[] upVector = new int[2 * MAX + 2];

         _lcs(left_data, 0, left_data.length, right_data, 0, right_data.length, downVector, upVector);
         return _create_diffs(left_data, right_data);
      }

      static IEnumerable<Item> _create_diffs(DiffData data_left, DiffData data_right) {
         int start_left, start_right;
         int item_left = 0, item_right = 0;
         while (item_left < data_left.length || item_right < data_right.length) {
            if ((item_left < data_left.length) && (!data_left.modified[item_left]) &&
                (item_right < data_right.length) && (!data_right.modified[item_right])) {
               // Equal entries
               item_left++;
               item_right++;
            } else {
               start_left = item_left;
               start_right = item_right;
               while (item_left < data_left.length && (item_right >= data_right.length || data_left.modified[item_left]))
                  item_left++;
               while (item_right < data_right.length && (item_left >= data_left.length || data_right.modified[item_right]))
                  item_right++;
               if ((start_left < item_left) || (start_right < item_right))
                  yield return new Item(start_left, start_right, item_left - start_left, item_right - start_right);
            }
         }
      }

      static int[] _diff_codes<T>(IList<T> input, Dictionary<T, int> cache) {
         var length = input.Count;
         int[] codes = new int[length];
         int lastCode = cache.Count;

         for (int x = 0; x < length; ++x) {
            var item = input[x];
            if (cache.TryGetValue(item, out int temp)) {
               codes[x] = temp;
            } else {
               lastCode++;
               cache[item] = lastCode;
               codes[x] = lastCode;
            }
         }
         return codes;
      }

      // Longest common substring algorithm
      static void _lcs(DiffData data_left, int lower_left, int upper_left, DiffData data_right, int lower_right, int upper_right, int[] down_vector, int[] up_vector) {
         // Fast walkthrough equal lines at the start
         while (lower_left < upper_left &&
               lower_right < upper_right &&
               data_left.data[lower_left] == data_right.data[lower_right]) {
            lower_left++; lower_right++;
         }
         // Fast walkthrough equal lines at the end
         while (lower_left < upper_left &&
               lower_right < upper_right &&
               data_left.data[upper_left - 1] == data_right.data[upper_right - 1]) {
            --upper_left; --upper_right;
         }
         if (lower_left == upper_left) {
            // mark as inserted lines.
            while (lower_right < upper_right)
               data_right.modified[lower_right++] = true;
         } else if (lower_right == upper_right) {
            // mark as deleted lines.
            while (lower_left < upper_left)
               data_left.modified[lower_left++] = true;
         } else {
            // Find the middle snake and length of an optimal path for A and B
            var smsrd = _sms(data_left, lower_left, upper_left, data_right, lower_right, upper_right, down_vector, up_vector);
            // The path is from LowerX to (x,y) and (x,y) to UpperX
            _lcs(data_left, lower_left, smsrd.x, data_right, lower_right, smsrd.y, down_vector, up_vector);
            _lcs(data_left, smsrd.x, upper_left, data_right, smsrd.y, upper_right, down_vector, up_vector);
         }
      }

      // Shortest Middle Snake algorithm
      static ShortestMiddleSnake _sms(
         DiffData data_left, int lower_left, int upper_left,
         DiffData data_right, int lower_right, int upper_right,
         int[] down_vector, int[] up_vector) {

         int MAX = data_left.length + data_right.length + 1;
         int downk = lower_left - lower_right;
         int upk = upper_left - upper_right;
         int delta = (upper_left - lower_left) - (upper_right - lower_right);
         bool delta_is_odd = (delta & 1) != 0;
         int down_offset = MAX - downk;
         int upOffset = MAX - upk;
         int maxd = ((upper_left - lower_left + upper_right - lower_right) / 2) + 1;

         down_vector[down_offset + downk + 1] = lower_left;
         up_vector[upOffset + upk - 1] = upper_left;

         for (int d = 0; d <= maxd; ++d) {
            // Extend the forward path
            for (int k = downk - d; k <= downk + d; k += 2) {
               // Find the starting point
               int x, y;
               if (k == downk - d) {
                  x = down_vector[down_offset + k + 1];
               } else {
                  x = down_vector[down_offset + k - 1] + 1;
                  if ((k < downk + d) && (down_vector[down_offset + k + 1] >= x)) {
                     x = down_vector[down_offset + k + 1];
                  }
               }
               y = x - k;

               // find the end of the furthest reaching forward d-path in diagonal k
               while ((x < upper_left) && (y < upper_right) && (data_left.data[x] == data_right.data[y])) {
                  x++; y++;
               }
               down_vector[down_offset + k] = x;

               // detect overlap
               if (delta_is_odd && (upk - d < k) && (k < upk + d)) {
                  if (up_vector[upOffset + k] <= down_vector[down_offset + k]) {
                     return new ShortestMiddleSnake(x = down_vector[down_offset + k], y = down_vector[down_offset + k] - k);
                  }
               }
            }
            // extend reverse path
            for (int k = upk - d; k <= upk + d; k += 2) {
               // find the starting point
               int x, y;
               if (k == upk + d) {
                  x = up_vector[upOffset + k - 1]; // up
               } else {
                  x = up_vector[upOffset + k + 1] - 1; // left
                  if ((k > upk - d) && (up_vector[upOffset + k - 1] < x)) {
                     x = up_vector[upOffset + k - 1]; // up
                  }
               }
               y = x - k;

               while ((x > lower_left) && (y > lower_right) && (data_left.data[x - 1] == data_right.data[y - 1])) {
                  x--; y--; // diagonal
               }
               up_vector[upOffset + k] = x;

               // detect overlap
               if (!delta_is_odd && (downk - d <= k) && (k <= downk + d)) {
                  if (up_vector[upOffset + k] <= down_vector[down_offset + k]) {
                     return new ShortestMiddleSnake(down_vector[down_offset + k], down_vector[down_offset + k] - k);
                  }
               }
            }
         }
         throw new ApplicationException("Unexpected code path");
      }

      public struct Item {
         public int start_left;
         public int start_right;
         public int deleted_left;
         public int inserted_right;

         public Item(int start_left, int start_right, int deleted_left, int inserted_right) {
            this.start_left = start_left;
            this.start_right = start_right;
            this.deleted_left = deleted_left;
            this.inserted_right = inserted_right;
         }

         public override string ToString() =>
            $"{start_left} {start_right} {deleted_left} {inserted_right}";
      }

      readonly struct ShortestMiddleSnake {
         public readonly int x, y;

         public ShortestMiddleSnake(int x, int y) {
            this.x = x;
            this.y = y;
         }
      }

      // Represents one collection
      class DiffData {
         public readonly int length;
         public readonly int[] data;
         public readonly bool[] modified;

         public DiffData(int[] data) {
            this.data = data;
            length = data.Length;
            modified = new bool[length + 2];
         }
      }
   }
}

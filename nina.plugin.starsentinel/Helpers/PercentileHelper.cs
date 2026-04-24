using System;
using System.Collections.Generic;
using System.Linq;

namespace Michelegz.NINA.StarSentinel.Helpers
{
    public static class PercentileHelper
    {
        public static int GetPercentileValue(IEnumerable<int> values, double percentile)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            var array = values.ToArray();
            if (array.Length == 0)
            {
                throw new ArgumentException("Sequence contains no elements.", nameof(values));
            }

            percentile = Math.Clamp(percentile, 0.0, 1.0);
            int index = (int)Math.Floor(percentile * (array.Length - 1));
            return QuickSelect(array, index);
        }

        private static int QuickSelect(int[] values, int k)
        {
            int left = 0;
            int right = values.Length - 1;

            while (left < right)
            {
                int pivotIndex = Partition(values, left, right, left + (right - left) / 2);
                if (k == pivotIndex)
                {
                    break;
                }
                else if (k < pivotIndex)
                {
                    right = pivotIndex - 1;
                }
                else
                {
                    left = pivotIndex + 1;
                }
            }

            return values[k];
        }

        private static int Partition(int[] values, int left, int right, int pivotIndex)
        {
            int pivotValue = values[pivotIndex];
            Swap(values, pivotIndex, right);
            int storeIndex = left;

            for (int i = left; i < right; i++)
            {
                if (values[i] < pivotValue)
                {
                    Swap(values, i, storeIndex);
                    storeIndex++;
                }
            }

            Swap(values, storeIndex, right);
            return storeIndex;
        }

        private static void Swap(int[] values, int left, int right)
        {
            int temp = values[left];
            values[left] = values[right];
            values[right] = temp;
        }
    }
}

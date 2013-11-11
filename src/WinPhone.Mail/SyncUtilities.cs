using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WinPhone.Mail
{
    public class SyncUtilities
    {
        public static async Task CompareListsAsync<T>(IEnumerable<T> list1, IEnumerable<T> list2,
            Func<T, string> selector, Func<T, T, Task> match, Func<T, Task> firstOnly, Func<T, Task> secondOnly)
        {
            IOrderedEnumerable<T> orderedList1 = list1.OrderBy(selector);
            IOrderedEnumerable<T> orderedList2 = list2.OrderBy(selector);

            IEnumerator<T> list1Enumerator = orderedList1.GetEnumerator();
            IEnumerator<T> list2Enumerator = orderedList2.GetEnumerator();

            bool moreInList1 = list1Enumerator.MoveNext();
            bool moreInList2 = list2Enumerator.MoveNext();
            T item1 = moreInList1 ? list1Enumerator.Current : default(T);
            T item2 = moreInList2 ? list2Enumerator.Current : default(T);

            while (moreInList1 && moreInList2)
            {
                int rank = selector(item1).CompareTo(selector(item2));
                if (rank == 0)
                {
                    await match(item1, item2);
                    moreInList1 = list1Enumerator.MoveNext();
                    moreInList2 = list2Enumerator.MoveNext();
                    item1 = moreInList1 ? list1Enumerator.Current : default(T);
                    item2 = moreInList2 ? list2Enumerator.Current : default(T);
                }
                else if (rank < 0)
                {
                    // Found in the first but not in the second.
                    await firstOnly(item1);
                    moreInList1 = list1Enumerator.MoveNext();
                    item1 = moreInList1 ? list1Enumerator.Current : default(T);
                }
                else
                {
                    // Found in the second list but not the first.
                    await secondOnly(item2);
                    moreInList2 = list2Enumerator.MoveNext();
                    item2 = moreInList2 ? list2Enumerator.Current : default(T);
                }
            }

            while (moreInList1)
            {
                await firstOnly(item1);
                moreInList1 = list1Enumerator.MoveNext();
                item1 = moreInList1 ? list1Enumerator.Current : default(T);
            }

            while (moreInList2)
            {
                await secondOnly(item2);
                moreInList2 = list2Enumerator.MoveNext();
                item2 = moreInList2 ? list2Enumerator.Current : default(T);
            }
        }
    }
}

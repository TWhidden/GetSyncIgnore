using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;

namespace BitTorrentSyncIgnore.Collections
{

    public static class SortedInsertExtensions
    {
        public static void InsertSorted<T>(this ObservableCollection<T> collection, T item, Comparison<T> comparison)
        {
            if (collection.Count == 0)
                collection.Add(item);
            else
            {
                bool last = true;
                for (int i = 0; i < collection.Count; i++)
                {
                    int result = comparison.Invoke(collection[i], item);
                    if (result >= 1)
                    {
                        collection.Insert(i, item);
                        last = false;
                        break;
                    }
                }
                if (last)
                    collection.Add(item);
            }
        }

        public static void InsertSorted<T>(this ObservableCollection<T> collection, T item, IComparer<T> comparison)
        {
            if (collection.Count == 0)
                collection.Add(item);
            else
            {
                bool last = true;
                for (int i = 0; i < collection.Count; i++)
                {
                    int result = comparison.Compare(collection[i], item);
                    if (result >= 1)
                    {
                        collection.Insert(i, item);
                        last = false;
                        break;
                    }
                }
                if (last)
                    collection.Add(item);
            }
        }
    }

    /// <summary>
    /// Use InsertSorted instead, this will insert into the collection already sorted. Much better then post-sorting.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="TSort"></typeparam>
    public class SortableObservableCollection<T, TSort> : ObservableCollection<T>, IReadOnlyObservableCollection<T> where TSort : IComparer<T>
    {

        private bool _disableNotifyCollectionChanged = false;

        public SortableObservableCollection(TSort defaultSort)
        {
            DefaultSort = defaultSort;
        }

        public TSort DefaultSort { get; private set; }

        public new void Add(T item)
        {
            InsertSorted(item, DefaultSort);
        }

        private void InsertSorted(T item, IComparer<T> comparison)
        {


            bool last = true;

            // insert at:
            var startAt = BinarySearch(item, comparison);

            if (startAt == Count) // add to the end, no need to search.
            {
                base.Add(item);
                return;
            }

            if (startAt == -1) // detected that it should be inserted at the first location.
            {
                this.Insert(0, item);
                return;
            }

            // In the event that there are multiple names in the list,
            // we need to start at the index found by the binary search,
            // and work our way to the end where the value will be >=1
            for (int i = startAt; i < base.Count; i++)
            {
                int result = comparison.Compare(this[i], item);
                if (result >= 1)
                {
                    this.Insert(i, item);
                    last = false;
                    break;
                }
            }
            if (last)
                base.Add(item);

        }

        /// <summary>
        /// Becasue we do not want a liniar search when adding items
        /// to the collection, we will first use a binary search to find
        /// the location closest to where we will start our search
        /// </summary>
        /// <param name="searchFor"></param>
        /// <param name="comparer"></param>
        /// <returns></returns>
        public int BinarySearch(T searchFor, IComparer<T> comparer)
        {
            if (this.Count == 0) return 0;
            var high = this.Count - 1;
            var low = 0;

            if (comparer.Compare(this[0], searchFor) > 0)
                return -1; // the item compared needs to be inserted at record 0;
            if (comparer.Compare(this[high], searchFor) < 0)
                return high + 1; // the item compared needs to be inserted at the end of the collection.
            if (this[high].Equals(searchFor))
                return high;

            while (low <= high)
            {
                var mid = (high + low) / 2;
                if (comparer.Compare(this[mid], searchFor) == 0)
                    return mid; // tell the search to start at this index in the compare loop.

                if (comparer.Compare(this[mid], searchFor) > 0)
                    high = mid - 1;
                else
                    low = mid + 1;
            }
            return low;  // tell the search to start at this index in the loop
        }

        public void ChangeSort(TSort sorter)
        {
            // Gets a temporary copy of the collection (and sorted just so the items will add to the bottom of the list)
            var collection = this.OrderBy(x => x, sorter).ToList();

            // Clears the collection
            this.Clear();

            // Changes the default sorter, so the next add will use this one instead.
            DefaultSort = sorter;

            // Loop over each item and sends the add.
            foreach (var s in collection)
            {
                Add(s);
            }
        }

        public void AddRange(IEnumerable<T> collection)
        {

            AddRange(collection, false);


        }

        public void RemoveRange(IEnumerable<T> collection)
        {
            RemoveRange(collection, false);
        }

        public void AddRange(IEnumerable<T> collection, bool disableCollectionChanged)
        {
            if (disableCollectionChanged)
                _disableNotifyCollectionChanged = true;

            var sw = new Stopwatch();
            sw.Start();
            var i = 0;
            foreach (var itm in collection)
            {
                Add(itm);
                i++;
            }

            if (sw.ElapsedMilliseconds > 100)
                Debug.WriteLine(string.Format("SortableObservableCollection took {0}ms to execute with disableCollectionChanged Flag set to {1} and {2} new items", sw.ElapsedMilliseconds, disableCollectionChanged, i));

            if (disableCollectionChanged)
            {
                _disableNotifyCollectionChanged = false;
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
        }

        public void RemoveRange(IEnumerable<T> collection, bool disableCollectionChanged)
        {
            if (disableCollectionChanged)
                _disableNotifyCollectionChanged = true;

            foreach (var itm in collection)
            {
                Remove(itm);
            }

            if (disableCollectionChanged)
            {
                _disableNotifyCollectionChanged = false;
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
        }

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (!_disableNotifyCollectionChanged)
                base.OnCollectionChanged(e);
        }

    }

}

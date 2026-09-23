using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace ArcadeStick.Models
{
    // [SECTION: RangeObservableCollection]
    // A small ObservableCollection extension adding a bulk-replace operation that fires a single Reset
    // notification instead of one CollectionChanged event per item. Standard ObservableCollection.Add()
    // in a loop becomes a real bottleneck at real scale (e.g. rebuilding FlatVisibleRows against a
    // 30k+ item tree) - this bypasses that by writing directly into the protected Items list and
    // notifying once at the end, which is the standard pattern for "the whole collection changed."
    public class RangeObservableCollection<T> : ObservableCollection<T>
    {
        public void ReplaceAll(IEnumerable<T> items)
        {
            Items.Clear();
            foreach (var item in items)
            {
                Items.Add(item);
            }

            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
    // [END SECTION: RangeObservableCollection]
}
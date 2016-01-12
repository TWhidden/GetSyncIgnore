using System.Collections.Generic;
using System.Collections.Specialized;

namespace BitTorrentSyncIgnore.Collections
{
    public interface IReadOnlyObservableCollection<T> : IEnumerable<T>, INotifyCollectionChanged
    {
    }
}

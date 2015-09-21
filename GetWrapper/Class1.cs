using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace GetWrapper
{

    public class SynchronizedObservableCollection<T> : ObservableCollection<T>
    {
        private readonly Object syncRoot = new object();

        protected Object SyncRoot
        {
            get { return this.syncRoot; }
        }

        protected override void ClearItems()
        {
            lock (this.syncRoot)
            {
                base.ClearItems();
            }
        }

        protected override void InsertItem(int index, T item)
        {
            lock (this.syncRoot)
            {
                base.InsertItem(index, item);
            }
        }

        protected override void MoveItem(int oldIndex, int newIndex)
        {
            lock (this.syncRoot)
            {
                base.MoveItem(oldIndex, newIndex);
            }
        }

        protected override void RemoveItem(int index)
        {
            lock (this.syncRoot)
            {
                base.RemoveItem(index);
            }
        }

        protected override void SetItem(int index, T item)
        {
            lock (this.syncRoot)
            {
                base.SetItem(index, item);
            }
        }
    }

    // then we make it bindable

    public class BindableObservableCollection<T> : SynchronizedObservableCollection<T>
    {
        private readonly Dispatcher dispatcher = Dispatcher.CurrentDispatcher;

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            this.dispatcher.Invoke(new Action(() => base.OnCollectionChanged(e)), null);
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            this.dispatcher.Invoke(new Action(() => base.OnPropertyChanged(e)), null);
        }

    }
}




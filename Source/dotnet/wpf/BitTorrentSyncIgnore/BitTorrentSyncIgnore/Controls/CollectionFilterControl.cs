using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using BitTorrentSyncIgnore.Collections;

namespace BitTorrentSyncIgnore.Controls
{
    [TemplatePart(Name = "PART_SearchBox", Type = typeof(TextBox))]
    [TemplatePart(Name = "PART_CollectionFilterContainer", Type=typeof(FrameworkElement))]
    public class CollectionFilterControl : Control
    {
        private TextBox _searchBox;

        private object _filterLock = new object();

        private DateTime _lastKeyDown = DateTime.MinValue;
        private FrameworkElement _searchIcon;
        private DispatcherTimerContainingAction _lastTimer = null;

        public CollectionFilterControl()
        {
            FilteredCollection = new SortableObservableCollection<object, IComparer<object>>(new SortObject(""));

            DefaultStyleKey = typeof(CollectionFilterControl);
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            _searchIcon = GetTemplateChild("PART_SearchIcon") as FrameworkElement;
            _searchBox = GetTemplateChild("PART_SearchBox") as TextBox;
            if(_searchBox == null)
                throw new Exception("CollectionFilterControl expect template PART_SEARCHBOX to be TextBox");
            _searchBox.KeyUp += _searchBox_KeyUp;
            _searchBox.GotFocus += _searchBox_GotFocus;
            _searchBox.LostFocus += _searchBox_LostFocus;
        }

        void _searchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            SetSearchIconVisibility();
        }

        void SetSearchIconVisibility()
        {
            if (_searchBox.Text.Length == 0)
            {
                _searchIcon.Visibility = Visibility.Visible;
            }
            else
            {
                _searchIcon.Visibility = Visibility.Collapsed;
            }
         
        }

        void _searchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            _searchIcon.Visibility = Visibility.Collapsed;
        }

        private void _searchBox_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            SetSearchIconVisibility();
            RunFilter();
        }

        #region SourceCollection

        /// <summary>
        /// SourceCollection Dependency Property
        /// </summary>
        public static readonly DependencyProperty SourceCollectionProperty =
            DependencyProperty.Register("SourceCollection", typeof(IList), typeof(CollectionFilterControl),
                new FrameworkPropertyMetadata(null, OnSourceCollectionChanged));

        /// <summary>
        /// Gets or sets the SourceCollection property. This dependency property 
        /// indicates ....
        /// </summary>
        public IList SourceCollection
        {
            get { return (IList)GetValue(SourceCollectionProperty); }
            set
            {
                SetValue(SourceCollectionProperty, value);
            }
        }

        /// <summary>
        /// Handles changes to the SourceCollection property.
        /// </summary>
        private static void OnSourceCollectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            CollectionFilterControl target = (CollectionFilterControl)d;
            IList oldSourceCollection = (IList)e.OldValue;
            IList newSourceCollection = target.SourceCollection;
            target.OnSourceCollectionChanged(oldSourceCollection, newSourceCollection);
        }

        /// <summary>
        /// Provides derived classes an opportunity to handle changes to the SourceCollection property.
        /// </summary>
        protected virtual void OnSourceCollectionChanged(IList oldSourceCollection, IList newSourceCollection)
        {
            //Debug.WriteLine("CollectionFilterControl: OnSourceCollectionChanged");

            // watch the collection if it support INotifyCollectionChanged
            if(oldSourceCollection != null)
            {
                var cc = oldSourceCollection as INotifyCollectionChanged;
                if(cc != null)
                    cc.CollectionChanged -= cc_CollectionChanged;
            }

            if(newSourceCollection != null)
            {
                var cc = newSourceCollection as INotifyCollectionChanged;
                if (cc != null)
                    cc.CollectionChanged += cc_CollectionChanged;
            }

            

            FilteredCollection.Clear(); // Drop all the results
            RunFilter(); // Rerun the filter
        }

        void cc_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            RunFilter();
        }

        private void RunFilter()
        {
            if (_lastTimer != null)
            {
                _lastTimer.Stop();
                _lastTimer = null;
            }

            //if (_searchBox == null) return;

            Action execute = (() => { 
            lock (_filterLock)
            {
                var currentText = ((_searchBox != null) ? _searchBox.Text : String.Empty);
                Debug.WriteLine(string.Format("RunFilter() for {0}", currentText));

                // Remove any items in the filtered collection that are no longer available in the primary collection
                var oldItems = (from x in FilteredCollection where !SourceCollection.Cast<object>().Contains(x) select x).ToList();
                oldItems.ForEach(x=>FilteredCollection.Remove(x));

                // look at existing filtered items and remove those that do not match the filter
                oldItems = (from x in FilteredCollection where !DoesContainString(x, currentText) select x).ToList();
                oldItems.ForEach(x => FilteredCollection.Remove(x));

                // Look at the source colleciton and find out what needs to be in the filtered collection
                var newItems = (from x in SourceCollection.Cast<object>() where DoesContainString(x, currentText) && !FilteredCollection.Contains(x) select x).ToList();
                FilteredCollection.AddRange(newItems, true); 

                //FilteredCollection.Sort();

                _lastTimer = null;
            }
            });

            _lastTimer = SetTimeout(500, execute);

        }

       private class SortObject : IComparer<object>
        {
            private readonly string _fieldName;

            public SortObject(string fieldName)
            {
                _fieldName = fieldName;
            }

            public int Compare(object x,
                               object y)
            {


                return GetStringValue(x).CompareTo(GetStringValue(y));
            }

            private string GetStringValue(object o)
            {
                if (o == null) return null;

                var property = o.GetType().GetProperty(_fieldName);
                if (property == null) return string.Empty;

                if (property.PropertyType == typeof(string))
                {
                    var s = property.GetValue(o, null) as string;
                    if (s == null)
                        return string.Empty;

                    return s;
                }

                return string.Empty;
            }
        }

        

        private bool DoesContainString(object o, string filter)
        {
            if (o == null) return false;

            foreach (var field in FieldName)
            {
                var property = o.GetType().GetProperty(field);
                if (property == null) continue;

                //if(property.PropertyType == typeof(string))
                {
                    var sv = property.GetValue(o, null);
                    if (sv == null)
                        continue;

                    string s = sv.ToString();

                    if (s.Trim().ToLower().Contains(filter.Trim().ToLower()))
                        return true;
                }

            }
           
            return false;
        }

        #endregion

        private SortableObservableCollection<object, IComparer<object>> _filteredCollection;
        public SortableObservableCollection<object, IComparer<object>> FilteredCollection
        {
            get { return _filteredCollection; }
            private set { _filteredCollection = value; }
        }

        #region FieldName

        /// <summary>
        /// FieldName Dependency Property
        /// </summary>
        public static readonly DependencyProperty FieldNameProperty =
            DependencyProperty.Register("FieldName", typeof(StringList), typeof(CollectionFilterControl),
                new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnFieldNameChanged)));

        /// <summary>
        /// Gets or sets the FieldName property. This dependency property 
        /// indicates ....
        /// </summary>
        public StringList FieldName
        {
            get { return (StringList)GetValue(FieldNameProperty); }
            set { SetValue(FieldNameProperty, value); }
        }

        /// <summary>
        /// Handles changes to the FieldName property.
        /// </summary>
        private static void OnFieldNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            CollectionFilterControl target = (CollectionFilterControl)d;
            StringList oldFieldName = (StringList)e.OldValue;
            StringList newFieldName = (StringList) e.NewValue;
            target.OnFieldNameChanged(oldFieldName, newFieldName);
        }

        /// <summary>
        /// Provides derived classes an opportunity to handle changes to the FieldName property.
        /// </summary>
        protected virtual void OnFieldNameChanged(StringList oldFieldName, StringList newFieldName)
        {
            string field = string.Empty;

            if (newFieldName != null && newFieldName.Count > 0)
            {
                field = newFieldName[0];
            }
            FilteredCollection.ChangeSort(new SortObject(field));
        }

        #endregion


        #region SorterOverride

        /// <summary>
        /// SorterOverride Dependency Property
        /// </summary>
        public static readonly DependencyProperty SorterOverrideProperty =
            DependencyProperty.Register("SorterOverride", typeof(IComparer<object>), typeof(CollectionFilterControl),
                new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnSorterOverrideChanged)));

        /// <summary>
        /// Gets or sets the SorterOverride property. This dependency property 
        /// indicates ....
        /// </summary>
        public IComparer<object> SorterOverride
        {
            get { return (IComparer<object>)GetValue(SorterOverrideProperty); }
            set { SetValue(SorterOverrideProperty, value); }
        }

        /// <summary>
        /// Handles changes to the SorterOverride property.
        /// </summary>
        private static void OnSorterOverrideChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            CollectionFilterControl target = (CollectionFilterControl)d;
            IComparer<object> oldSorterOverride = (IComparer<object>)e.OldValue;
            IComparer<object> newSorterOverride = (IComparer<object>)e.NewValue;
            target.OnSorterOverrideChanged(oldSorterOverride, newSorterOverride);
        }

        /// <summary>
        /// Provides derived classes an opportunity to handle changes to the SorterOverride property.
        /// </summary>
        protected virtual void OnSorterOverrideChanged(IComparer<object> oldSorterOverride, IComparer<object> newSorterOverride)
        {
            FilteredCollection.ChangeSort(newSorterOverride);
        }

        #endregion


        private static DispatcherTimerContainingAction SetTimeout(int milliseconds, Action func)
        {
            //Debug.Write("SetTimeout");
            var timer = new DispatcherTimerContainingAction
            {
                Interval = new TimeSpan(0, 0, 0, 0, milliseconds),
                Action = func
            };
            timer.Tick += _onTimeout;
            timer.Start();
            return timer;
        }

        private static void _onTimeout(object sender, EventArgs arg)
        {
            //Debug.Write("_onTimeout");

            var t = sender as DispatcherTimerContainingAction;
            t.Stop();
            t.Action();
            t.Tick -= _onTimeout;
        }

        private class DispatcherTimerContainingAction : DispatcherTimer
        {
            /// <summary>
            /// uncomment this to see when the DispatcherTimerContainingAction is collected
            /// if you remove  t.Tick -= _onTimeout; line from _onTimeout method
            /// you will see that the timer is never collected
            /// </summary>
            //~DispatcherTimerContainingAction()
            //{
            //    throw new Exception("DispatcherTimerContainingAction is disposed");
            //}

            public Action Action { get; set; }
        }
    }

    public class StringListTypeConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, 
            Type sourceType)
        {
          return sourceType == typeof(string);
        }
 
        public override object ConvertFrom(ITypeDescriptorContext context,
            System.Globalization.CultureInfo culture, object value)
        {
          return new StringList((string)value);
        }
 
        public override bool CanConvertTo(ITypeDescriptorContext context, 
            Type destinationType)
        {
          return destinationType == typeof(string);
        }
 
        public override object ConvertTo(ITypeDescriptorContext context,
            System.Globalization.CultureInfo culture, object value, Type destinationType)
        {
          return value == null ? null : string.Join(", ", (StringList)value);
        }
    }

    [TypeConverter(typeof(StringListTypeConverter))]
    public class StringList : List<string>
    {
        private readonly string _original;
 
        public StringList(string value)
        {
          _original = value;
          if (!string.IsNullOrEmpty(value))
          {
              var items = value.Split(",;".ToCharArray()).
                  Where(i => !string.IsNullOrWhiteSpace(i)).Select(i => i.Trim());

              AddRange(items);
          }
        }
        
 
        protected StringList(string[] value)
        {
          AddRange(value);
          _original = string.Join(", ", value);
        }
 
        public StringList() { }
 
 
        public override string ToString()
        {
          return _original;
        }
    }
}

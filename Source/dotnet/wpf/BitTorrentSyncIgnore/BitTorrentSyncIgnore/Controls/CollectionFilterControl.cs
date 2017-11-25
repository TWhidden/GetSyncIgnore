using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using BitTorrentSyncIgnore.Collections;

namespace BitTorrentSyncIgnore.Controls
{
    [TemplatePart(Name = PART_SearchBox, Type = typeof(TextBox))]
    [TemplatePart(Name = PART_GridContainer, Type = typeof(Grid))]
    [TemplatePart(Name = PART_SearchIcon, Type = typeof(FrameworkElement))]
    public class CollectionFilterControl : Control
    {
        public const string PART_GridContainer = "PART_GridContainer";
        public const string PART_SearchBox = "PART_SearchBox";
        public const string PART_SearchIcon = "PART_SearchIcon";

        private TextBox _searchBox;

        private CancellationTokenSource _delay;

        private FrameworkElement _searchIcon;

        public CollectionFilterControl() 
        {
            FilteredCollection = new SortableObservableCollection<FileContainer, IComparer<FileContainer>>(new SorterName());
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            _searchIcon = GetTemplateChild(PART_SearchIcon) as FrameworkElement;
            _searchBox = GetTemplateChild(PART_SearchBox) as TextBox;

            if(_searchBox == null)
                throw new Exception("CollectionFilterControl expect template PART_SEARCHBOX to be TextBox");
            _searchBox.KeyUp += _searchBox_KeyUp;
            _searchBox.GotFocus += _searchBox_GotFocus;
            _searchBox.LostFocus += _searchBox_LostFocus;

            if (_searchIcon != null)
            {
                _searchIcon.Height = ControlHeight;
            }
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
            var target = (CollectionFilterControl)d;
            var oldSourceCollection = (IList)e.OldValue;
            var newSourceCollection = target.SourceCollection;
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

        private async void RunFilter()
        {
            _delay?.Cancel();

            _delay = new CancellationTokenSource();
            try
            {
                await Task.Delay(500, _delay.Token);
            }
            catch (TaskCanceledException)
            {
                // request was canceled, return;
                return;
            }

            var currentText = _searchBox?.Text ?? string.Empty;

            var sourceCollection = SourceCollection.Cast<FileContainer>().ToList();

            if (string.IsNullOrWhiteSpace(currentText))
            {
                // When nothing is being filtered, we dont need to filter any thing.
                FilteredCollection.Clear();
                FilteredCollection.AddRange(sourceCollection, true);
            }
            else
            {
                // Remove any items in the filtered collection that are no longer available in the primary collection
                var oldItems = (from x in FilteredCollection where !sourceCollection.Contains(x) select x).ToList();
                oldItems.ForEach(x => FilteredCollection.Remove(x));

                // look at existing filtered items and remove those that do not match the filter
                oldItems = (from x in FilteredCollection where !DoesContainString(x, currentText) select x).ToList();
                oldItems.ForEach(x => FilteredCollection.Remove(x));

                // Look at the source colleciton and find out what needs to be in the filtered collection
                var newItems = (from x in sourceCollection where DoesContainString(x, currentText) && !FilteredCollection.Contains(x) select x).ToList();
                FilteredCollection.AddRange(newItems, true);
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

                    var s = sv.ToString();

                    if (s.Trim().ToLower().Contains(filter.Trim().ToLower()))
                        return true;
                }

            }
           
            return false;
        }

        #endregion

        private SortableObservableCollection<FileContainer, IComparer<FileContainer>> _filteredCollection;
        public SortableObservableCollection<FileContainer, IComparer<FileContainer>> FilteredCollection
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
            var target = (CollectionFilterControl)d;
            var oldFieldName = (StringList)e.OldValue;
            var newFieldName = (StringList) e.NewValue;
            target.OnFieldNameChanged(oldFieldName, newFieldName);
        }

        /// <summary>
        /// Provides derived classes an opportunity to handle changes to the FieldName property.
        /// </summary>
        protected virtual void OnFieldNameChanged(StringList oldFieldName, StringList newFieldName)
        {
            var field = string.Empty;

            if (newFieldName != null && newFieldName.Count > 0)
            {
                field = newFieldName[0];
            }
            FilteredCollection.ChangeSort(new SorterName());
        }

        #endregion

        #region SorterOverride

        /// <summary>
        /// SorterOverride Dependency Property
        /// </summary>
        public static readonly DependencyProperty SorterOverrideProperty =
            DependencyProperty.Register("SorterOverride", typeof(IComparer), typeof(CollectionFilterControl),
                new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnSorterOverrideChanged)));

        /// <summary>
        /// Gets or sets the SorterOverride property. This dependency property 
        /// indicates ....
        /// </summary>
        public IComparer SorterOverride
        {
            get { return (IComparer)GetValue(SorterOverrideProperty); }
            set { SetValue(SorterOverrideProperty, value); }
        }

        /// <summary>
        /// Handles changes to the SorterOverride property.
        /// </summary>
        private static void OnSorterOverrideChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var target = (CollectionFilterControl)d;
            var oldSorterOverride = (IComparer<FileContainer>)e.OldValue;
            var newSorterOverride = (IComparer<FileContainer>)e.NewValue;
            target.OnSorterOverrideChanged(oldSorterOverride, newSorterOverride);
        }

        /// <summary>
        /// Provides derived classes an opportunity to handle changes to the SorterOverride property.
        /// </summary>
        protected virtual void OnSorterOverrideChanged(IComparer<FileContainer> oldSorterOverride, IComparer<FileContainer> newSorterOverride)
        {
            FilteredCollection.ChangeSort(newSorterOverride);
        }

        #endregion

        #region ControlHeight

        /// <summary>
        /// ControlHeight Dependency Property
        /// </summary>
        public static readonly DependencyProperty ControlHeightProperty =
            DependencyProperty.Register("ControlHeight", typeof(double), typeof(CollectionFilterControl),
                new FrameworkPropertyMetadata((double)20,
                    FrameworkPropertyMetadataOptions.None,
                    new PropertyChangedCallback(OnControlHeightChanged),
                    new CoerceValueCallback(CoerceControlHeight)));

        /// <summary>
        /// Gets or sets the ControlHeight property. This dependency property 
        /// indicates ....
        /// </summary>
        public double ControlHeight
        {
            get { return (double)GetValue(ControlHeightProperty); }
            set { SetValue(ControlHeightProperty, value); }
        }

        /// <summary>
        /// Handles changes to the ControlHeight property.
        /// </summary>
        private static void OnControlHeightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var target = (CollectionFilterControl)d;
            var oldControlHeight = (double)e.OldValue;
            var newControlHeight = target.ControlHeight;
            target.OnControlHeightChanged(oldControlHeight, newControlHeight);
        }

        /// <summary>
        /// Provides derived classes an opportunity to handle changes to the ControlHeight property.
        /// </summary>
        protected virtual void OnControlHeightChanged(double oldControlHeight, double newControlHeight)
        {
            if (_searchIcon != null)
            {
                _searchIcon.Height = newControlHeight;
            }
        }

        /// <summary>
        /// Coerces the ControlHeight value.
        /// </summary>
        private static object CoerceControlHeight(DependencyObject d, object value)
        {
            var target = (CollectionFilterControl)d;
            var desiredControlHeight = (double)value;

            return desiredControlHeight;
        }

        #endregion

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

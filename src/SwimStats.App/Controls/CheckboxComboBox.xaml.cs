using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace SwimStats.App.Controls
{
    public partial class CheckboxComboBox : ComboBox
    {
        public static readonly DependencyProperty MaxDisplayLengthProperty =
            DependencyProperty.Register(nameof(MaxDisplayLength), typeof(int), typeof(CheckboxComboBox), 
                new PropertyMetadata(int.MaxValue));

        public static readonly DependencyProperty SelectedItemsTextProperty =
            DependencyProperty.Register(nameof(SelectedItemsText), typeof(string), typeof(CheckboxComboBox),
                new PropertyMetadata("Select items..."));

        public int MaxDisplayLength
        {
            get => (int)GetValue(MaxDisplayLengthProperty);
            set => SetValue(MaxDisplayLengthProperty, value);
        }

        public string SelectedItemsText
        {
            get => (string)GetValue(SelectedItemsTextProperty);
            set => SetValue(SelectedItemsTextProperty, value);
        }

        public CheckboxComboBox()
        {
            InitializeComponent();
            
            // Bind the Text property to SelectedItemsText dependency property
            this.SetBinding(TextProperty, new Binding(nameof(SelectedItemsText)) { Source = this, Mode = BindingMode.OneWay });
        }

        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            // Keep dropdown open when clicking checkboxes
            this.IsDropDownOpen = true;
            
            // Update the display text to show selected items count
            UpdateDisplayText();
        }

        protected override void OnItemsSourceChanged(IEnumerable oldValue, IEnumerable newValue)
        {
            base.OnItemsSourceChanged(oldValue, newValue);
            
            // Unsubscribe from old collection
            if (oldValue is INotifyCollectionChanged oldCollection)
            {
                oldCollection.CollectionChanged -= ItemsSource_CollectionChanged;
            }
            
            // Unsubscribe from old items' property changes
            if (oldValue is IEnumerable oldItems)
            {
                foreach (var item in oldItems)
                {
                    if (item is INotifyPropertyChanged notifyOld)
                    {
                        notifyOld.PropertyChanged -= Item_PropertyChanged;
                    }
                }
            }
            
            // Subscribe to new collection
            if (newValue is INotifyCollectionChanged newCollection)
            {
                newCollection.CollectionChanged += ItemsSource_CollectionChanged;
            }
            
            // Subscribe to new items' property changes
            if (newValue is IEnumerable newItems)
            {
                foreach (var item in newItems)
                {
                    if (item is INotifyPropertyChanged notifyNew)
                    {
                        notifyNew.PropertyChanged += Item_PropertyChanged;
                    }
                }
            }
            
            UpdateDisplayText();
        }

        private void ItemsSource_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // Unsubscribe from removed items
            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems)
                {
                    if (item is INotifyPropertyChanged notify)
                    {
                        notify.PropertyChanged -= Item_PropertyChanged;
                    }
                }
            }

            // Subscribe to new items
            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems)
                {
                    if (item is INotifyPropertyChanged notify)
                    {
                        notify.PropertyChanged += Item_PropertyChanged;
                    }
                }
            }

            UpdateDisplayText();
        }

        private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // When any item's IsSelected property changes, update the display text
            if (e.PropertyName == "IsSelected")
            {
                UpdateDisplayText();
            }
        }

        private void UpdateDisplayText()
        {
            if (ItemsSource is IEnumerable items)
            {
                var selectedItems = new List<string>();
                foreach (var item in items)
                {
                    dynamic dynamicItem = item;
                    if (dynamicItem?.IsSelected == true)
                    {
                        selectedItems.Add(dynamicItem.Name ?? string.Empty);
                    }
                }

                if (selectedItems.Any())
                {
                    var displayText = $"{string.Join(", ", selectedItems)} ({selectedItems.Count} selected)";
                    
                    // Truncate if exceeds MaxDisplayLength
                    if (displayText.Length > MaxDisplayLength && MaxDisplayLength > 0)
                    {
                        displayText = displayText.Substring(0, MaxDisplayLength - 3) + "...";
                    }
                    
                    SelectedItemsText = displayText;
                }
                else
                {
                    SelectedItemsText = "Select items...";
                }
            }
        }
    }
}

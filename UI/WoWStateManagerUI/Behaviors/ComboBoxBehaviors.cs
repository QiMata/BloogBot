using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;

namespace WoWStateManagerUI.Behaviors
{
    /// <summary>
    /// Attached behaviors for <see cref="ComboBox"/>.
    /// <see cref="AutoSelectFirstProperty"/> auto-selects index 0 after the
    /// ItemsSource fills, but only when nothing else is selected — preserves
    /// user picks and bound values.
    /// </summary>
    public static class ComboBoxBehaviors
    {
        public static readonly DependencyProperty AutoSelectFirstProperty =
            DependencyProperty.RegisterAttached(
                "AutoSelectFirst",
                typeof(bool),
                typeof(ComboBoxBehaviors),
                new PropertyMetadata(false, OnAutoSelectFirstChanged));

        public static bool GetAutoSelectFirst(ComboBox d) => (bool)d.GetValue(AutoSelectFirstProperty);
        public static void SetAutoSelectFirst(ComboBox d, bool value) => d.SetValue(AutoSelectFirstProperty, value);

        private static void OnAutoSelectFirstChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not ComboBox cb) return;
            if ((bool)e.NewValue)
            {
                cb.Loaded += OnComboBoxLoaded;
                ((INotifyCollectionChanged)cb.Items).CollectionChanged += (_, _) => TryDefaultSelect(cb);
            }
        }

        private static void OnComboBoxLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is ComboBox cb)
                TryDefaultSelect(cb);
        }

        private static void TryDefaultSelect(ComboBox cb)
        {
            if (cb.Items.Count == 0) return;
            if (cb.SelectedIndex >= 0) return;
            // For editable ComboBoxes (parameter Value cells), don't override a
            // non-empty Text — that's a bound value the user might be typing.
            if (cb.IsEditable && !string.IsNullOrEmpty(cb.Text))
                return;
            cb.SelectedIndex = 0;
        }
    }
}

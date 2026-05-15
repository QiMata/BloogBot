using System.Windows;
using System.Windows.Controls;
using WoWStateManagerUI.Models;

namespace WoWStateManagerUI.Views
{
    public partial class ConfigEditorView : UserControl
    {
        public ConfigEditorView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Search-button click handler for the 🔍 button rendered by the
        /// SearchParamTemplate in the Parameters grid. Opens
        /// <see cref="SearchPickerDialog"/> against the parameter's
        /// <see cref="ActivityParameter.SearchKind"/> (Item / Quest / Spell);
        /// on OK writes the picked Name into the parameter's Value and stashes
        /// the entry id in Description for runtime resolution.
        ///
        /// The dialog queries name LIKE %q% plus entry = parsed-id in parallel
        /// against the matching mangos.* table — the user can type either the
        /// item name OR its entry id.
        /// </summary>
        private void OnParameterSearchClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            if (btn.DataContext is not ActivityParameter param) return;
            if (param.SearchKind == SearchKind.None) return;

            var dialog = new SearchPickerDialog(param.SearchKind, param.Value)
            {
                Owner = Window.GetWindow(this)
            };
            var ok = dialog.ShowDialog();
            if (ok == true && dialog.PickedResult != null)
            {
                // Store the human-readable Name so the config is readable; stash
                // the entry id in Description so the runtime can resolve unambiguously.
                param.Value = dialog.PickedResult.Name;
                param.Description = $"→ id {dialog.PickedResult.Id} ({dialog.PickedResult.Extra})";
            }
        }
    }
}

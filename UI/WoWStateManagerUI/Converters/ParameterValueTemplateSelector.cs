using System.Windows;
using System.Windows.Controls;
using WoWStateManagerUI.Models;

namespace WoWStateManagerUI.Converters
{
    /// <summary>
    /// Selects the editor template for a parameter row's Value cell.
    /// Renders a ComboBox bound to <see cref="ActivityParameter.Choices"/>
    /// when the parameter has a fixed value set; otherwise renders a free-form
    /// TextBox. Hooked from the Parameters DataGrid's
    /// <c>DataGridTemplateColumn.CellTemplateSelector</c>.
    /// </summary>
    public sealed class ParameterValueTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? ChoiceTemplate { get; set; }
        public DataTemplate? StringTemplate { get; set; }
        public DataTemplate? SearchTemplate { get; set; }

        public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        {
            if (item is not ActivityParameter p) return StringTemplate;
            if (p.HasSearch) return SearchTemplate;
            if (p.HasChoices) return ChoiceTemplate;
            return StringTemplate;
        }
    }
}

using System.Windows;
using System.Windows.Controls;

namespace SwimStats.App.Controls
{
    public partial class CheckboxComboBox : ComboBox
    {
        public CheckboxComboBox()
        {
            InitializeComponent();
        }

        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            // Keep dropdown open when clicking checkboxes
            this.IsDropDownOpen = true;
        }
    }
}

using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using SwimStats.Core.Models;
using SwimStats.App.Resources;
using SwimStats.App.Services;
using OxyPlot;
using OxyPlot.Wpf;

namespace SwimStats.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        
        // Load saved language
        var settings = AppSettings.Instance;
        LocalizationManager.Instance.SetLanguage(settings.Language);
        
        this.DataContext = new ViewModels.MainViewModel();
        // Set DataContext to the viewmodel (uses default ctor)
        this.Loaded += MainWindow_Loaded;
        
        // Configure PlotView for zoom and pan
        ConfigurePlotView();
    }
    
    private void ConfigurePlotView()
    {
        // Configure the chart to enable zoom and pan with proper mouse bindings
        var controller = new PlotController();
        
        // Add default bindings for zoom and pan
        controller.UnbindAll();
        
        // Left mouse button: zoom by rectangle
        controller.BindMouseDown(OxyMouseButton.Left, OxyPlot.PlotCommands.ZoomRectangle);
        
        // Right mouse button: pan
        controller.BindMouseDown(OxyMouseButton.Right, OxyPlot.PlotCommands.PanAt);
        
        // Mouse wheel: zoom in/out
        controller.BindMouseWheel(OxyPlot.PlotCommands.ZoomWheel);
        
        // Hover for tracker (tooltip)
        controller.BindMouseEnter(OxyPlot.PlotCommands.HoverPointsOnlyTrack);
        
        // Double click: reset
        controller.Bind(new OxyMouseDownGesture(OxyMouseButton.Left, clickCount: 2), OxyPlot.PlotCommands.ResetAt);
        
        // Right click: reset  
        controller.Bind(new OxyMouseDownGesture(OxyMouseButton.Right, clickCount: 1), OxyPlot.PlotCommands.ResetAt);
        
        ChartPlotView.Controller = controller;
    }
    
    private async void MainWindow_Loaded(object? sender, RoutedEventArgs e)
    {
        try
        {
            // Set the language dropdown to match saved setting
            var settings = AppSettings.Instance;
            var languageCombo = FindLanguageComboBox(this);
            if (languageCombo != null)
            {
                foreach (ComboBoxItem item in languageCombo.Items)
                {
                    if (item.Tag?.ToString() == settings.Language)
                    {
                        languageCombo.SelectedItem = item;
                        break;
                    }
                }
            }
            
            // Bring the window to the foreground
            this.Topmost = true;
            this.Activate();
            await System.Threading.Tasks.Task.Delay(200);
            this.Topmost = false;
        }
        catch
        {
            // ignore
        }
    }

    private void CheckBox_Click(object sender, RoutedEventArgs e)
    {
        // Keep the dropdown open when clicking checkboxes
        var checkBox = sender as CheckBox;
        if (checkBox != null)
        {
            var comboBox = FindParent<ComboBox>(checkBox);
            if (comboBox != null)
            {
                comboBox.IsDropDownOpen = true;
            }
        }
    }

    private void Language_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var comboBox = sender as ComboBox;
        if (comboBox?.SelectedItem is ComboBoxItem selectedItem)
        {
            var languageCode = selectedItem.Tag?.ToString();
            if (!string.IsNullOrEmpty(languageCode))
            {
                LocalizationManager.Instance.SetLanguage(languageCode);
                
                // Save language preference
                var settings = AppSettings.Instance;
                settings.Language = languageCode;
                settings.Save();
                
                // Refresh the chart title and axis labels
                if (this.DataContext is ViewModels.MainViewModel viewModel)
                {
                    viewModel.RefreshChart();
                }
            }
        }
    }

    private ComboBox? FindLanguageComboBox(DependencyObject parent)
    {
        int childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            
            if (child is ComboBox combo && combo.Items.Count > 0)
            {
                // Check if first item is a ComboBoxItem with a Tag (language combo)
                if (combo.Items[0] is ComboBoxItem item && item.Tag != null)
                {
                    var tag = item.Tag?.ToString();
                    if (tag == "en" || tag == "nl")
                    {
                        return combo;
                    }
                }
            }
            
            var result = FindLanguageComboBox(child);
            if (result != null) return result;
        }
        return null;
    }

    private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        DependencyObject? parentObject = VisualTreeHelper.GetParent(child);
        if (parentObject == null) return null;
        if (parentObject is T parent) return parent;
        return FindParent<T>(parentObject);
    }
}

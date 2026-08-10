using Wpf.Ui.Controls;

namespace YtDlpGui.Views;

public partial class MainWindow : FluentWindow
{
    public MainWindow()
    {
        InitializeComponent();
        
        if (App.IsPlusVersion)
        {
            this.Width = 950;
            this.Height = 770;
        }

        this.MinWidth = this.Width;
        this.MinHeight = this.Height;

        var vm = new ViewModels.MainViewModel();
        DataContext = vm;
        vm.PropertyChanged += (s, e) => 
        {
            if (e.PropertyName == nameof(ViewModels.MainViewModel.IsRemuxerEnabled))
            {
                // Trigger a layout update so columns resize and hide/show properly
                FormatListView_SizeChanged(FormatListView, null);
            }
            else if (e.PropertyName == nameof(ViewModels.MainViewModel.CurrentPage))
            {
                if (vm.IsAudioOptionsPage) AudioOptionsScrollViewer.ScrollToTop();
                if (vm.IsVideoOptionsPage) VideoOptionsScrollViewer.ScrollToTop();
                if (vm.IsManualOptionsPage) ManualOptionsScrollViewer.ScrollToTop();
            }
        };
    }

    private void FormatListView_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
    {
        if (sender is System.Windows.Controls.ListView listView && listView.View is System.Windows.Controls.GridView gridView)
        {
            double totalWidth = listView.ActualWidth - System.Windows.SystemParameters.VerticalScrollBarWidth - 10;
            if (totalWidth > 0)
            {
                if (gridView.Columns.Count == 7)
                {
                    // Video table weights
                    double[] weights = { 45, 65, 75, 70, 110, 75, 70 };
                    double totalWeight = 510; // Sum of weights

                    for (int i = 0; i < weights.Length; i++)
                    {
                        gridView.Columns[i].Width = (weights[i] / totalWeight) * totalWidth;
                    }
                }
                else if (gridView.Columns.Count == 6)
                {
                    // Audio table weights
                    double[] weights = { 45, 75, 90, 130, 85, 85 };
                    double totalWeight = 510; // Sum of weights

                    for (int i = 0; i < weights.Length; i++)
                    {
                        gridView.Columns[i].Width = (weights[i] / totalWeight) * totalWidth;
                    }
                }
            }
        }
    }
}

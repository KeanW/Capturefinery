using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CapturefineryViewExtension
{
  /// <summary>
  /// Interaction logic for CapturefineryWindow.xaml
  /// </summary>
  public partial class CapturefineryWindow : Window
  {
    private StudyInfo _study;
    private HallOfFame _hof;

    public CapturefineryWindow()
    {
      InitializeComponent();

      // Hide the options pane until something is selected

      TaskOptions.Visibility = Visibility.Hidden;
      TaskOptions.Height = 0;
      _study = null;
      _hof = null;
    }

    private async void Grid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      var viewModel = MainGrid.DataContext as CapturefineryWindowViewModel;
      if (e.AddedItems.Count > 0)
      {
        _study = e.AddedItems[0] as StudyInfo;

        // Display the options pane with automatic height

        TaskOptions.Visibility = Visibility.Visible;
        TaskOptions.Height = double.NaN;

        if (_study != null && viewModel != null)
        {
          _hof = viewModel.GetHallOfFame(_study);
          var max = _hof.solutions.Length;
          viewModel.MaxItems = max;
          viewModel.Start = 0;
          viewModel.Items = max;
        }
      }
    }

    private async void ExecuteButton_Click(object sender, RoutedEventArgs e)
    {
      var viewModel = MainGrid.DataContext as CapturefineryWindowViewModel;
      if (
        _study != null && viewModel != null
      )
      {
        this.Focus();
        this.Hide();
        await viewModel.RunTasks(_study, _hof);
        this.Show();
        this.Focus();
      }
    }
  }
}

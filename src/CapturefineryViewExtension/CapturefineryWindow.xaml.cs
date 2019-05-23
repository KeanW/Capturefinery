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
    private int _items;

    public CapturefineryWindow()
    {
      InitializeComponent();

      TaskOptions.Visibility = Visibility.Hidden;
      TaskOptions.Height = 0;
      _study = null;
      _hof = null;
      _items = 0;
    }

    private async void Grid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      var viewModel = MainGrid.DataContext as CapturefineryWindowViewModel;
      if (e.AddedItems.Count > 0)
      {
        _study = e.AddedItems[0] as StudyInfo;

        TaskOptions.Visibility = Visibility.Visible;
        TaskOptions.Height = double.NaN;

        if (_study != null && viewModel != null)
        {
          _hof = viewModel.GetHallOfFame(_study);
          _items = _hof.solutions.Length;
          TaskLabel.Content = string.Format("Selected task contains {0} items.", _items);
          StartText.Text = "0";
          ItemsText.Text = _items.ToString();
        }
      }
    }

    private async void ExecuteButton_Click(object sender, RoutedEventArgs e)
    {
      var viewModel = MainGrid.DataContext as CapturefineryWindowViewModel;
      if (_study != null && viewModel != null)
      {
        var errors = ErrorCheck.IsChecked.HasValue ? ErrorCheck.IsChecked.Value : false;
        this.Focus();
        this.Hide();
        await viewModel.RunTasks(_study, Int32.Parse(StartText.Text), Int32.Parse(ItemsText.Text), errors, _hof);
        this.Show();
        this.Focus();
      }
    }
  }
}

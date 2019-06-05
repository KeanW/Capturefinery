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

    const int numberOfSortParameters = 4;
    const int startSortRow = 8;

    public CapturefineryWindow()
    {
      InitializeComponent();

      // Hide the options pane until something is selected

      TaskOptions.Visibility = Visibility.Hidden;
      TaskOptions.Height = 0;
      _study = null;
      _hof = null;
    }

    private async void OnDataGridSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      var viewModel = MainGrid.DataContext as CapturefineryWindowViewModel;
      if (e.AddedItems.Count > 0)
      {
        _study = e.AddedItems[0] as StudyInfo;

        // Display the options pane with automatic height

        TaskOptions.Visibility = Visibility.Visible;
        TaskOptions.Height = double.NaN;
        TaskOptions.Margin = new Thickness(10);

        if (_study != null && viewModel != null)
        {
          _hof = viewModel.GetHallOfFame(_study);
          viewModel.InitProperties(_hof.solutions.Length);

          DisplayOrHideControls(true, true);
        }
      }
    }

    private void OnDataGridMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
      if (sender != null)
      {
        _study = null;
        TaskOptions.Visibility = Visibility.Hidden;
        TaskOptions.Height = 0;
        TaskOptions.Margin = new Thickness(0);
        var grid = sender as DataGrid;
        if (grid != null && grid.SelectedItems != null && grid.SelectedItems.Count == 1)
        {
          var dgr = grid.ItemContainerGenerator.ContainerFromItem(grid.SelectedItem) as DataGridRow;
          if (dgr.IsMouseOver)
          {
            (dgr as DataGridRow).IsSelected = false;
          }
        }
      }
    }

    private async void OnExecuteButtonClick(object sender, RoutedEventArgs e)
    {
      var viewModel = MainGrid.DataContext as CapturefineryWindowViewModel;
      if (_study != null && viewModel != null)
      {
        ShowProgress(true);
        await viewModel.RunTasks(_study, _hof);
        ShowProgress(false);
      }
    }

    private async void OnCancelButtonClick(object sender, RoutedEventArgs e)
    {
      var viewModel = MainGrid.DataContext as CapturefineryWindowViewModel;
      if (viewModel != null)
      {
        viewModel.Escape = true;
        ShowProgress(false);
        viewModel.DisableExecute(true);
      }
    }

    private void OnAnimateChecked(object sender, RoutedEventArgs e)
    {
      var checkBox = sender as CheckBox;
      if (checkBox != null)
      {
        var check = checkBox.IsChecked.Value;
        DisplayOrHideControls(check);
      }
    }

    private void DisplayOrHideControls(bool check, bool forceHide = false)
    {
      var val = check ? new GridLength(0, GridUnitType.Auto) : new GridLength(0);

      // Show/hide the load image checkbox, root filename and first sort combo

      TaskOptions.RowDefinitions[5].Height = val;
      TaskOptions.RowDefinitions[6].Height = val;
      TaskOptions.RowDefinitions[7].Height = val;

      // Optionally hide the sort combos after the first one

      if (!check || forceHide)
      {
        var hide = new GridLength(0);
        for (int i = 0; i < numberOfSortParameters - 1; i++)
        {
          TaskOptions.RowDefinitions[startSortRow + i].Height = hide;
        }
      }
    }

    private void ShowProgress(bool showProgress)
    {
      var show = showProgress ? Visibility.Visible : Visibility.Hidden;
      var hide = showProgress ? Visibility.Hidden : Visibility.Visible;
      TaskOptions.Visibility = hide;
      StudyList.Visibility = hide;
      ProgressGrid.Visibility = show;
    }

    private void OnSortComboSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      var combo = sender as ComboBox;

      const string prefix = "SortLevelComboBox";

      if (combo != null)
      {
        var name = combo.Name;
        if (name.StartsWith(prefix))
        {
          var number = Int32.Parse(name.Substring(prefix.Length)) - 1;
          if (number >= 0 && number < numberOfSortParameters)
          {
            var viewModel = MainGrid.DataContext as CapturefineryWindowViewModel;
            if (viewModel != null)
            {
              // Check whether the item selected is an empty value

              if (e.AddedItems.Count > 0 && e.AddedItems[0] == viewModel.EmptyComboValue)
              {
                // Clear the values

                viewModel.ClearSortParameters(number);

                // Hide the grid rows

                var hide = new GridLength(0);
                for (int i = startSortRow + number; i < startSortRow + numberOfSortParameters - 1; i++)
                {
                  TaskOptions.RowDefinitions[i].Height = hide;
                }
              }
              else
              {
                if (number < numberOfSortParameters - 1)
                {
                  var show = new GridLength(0, GridUnitType.Auto);
                  TaskOptions.RowDefinitions[startSortRow + number].Height = show;
                }
              }
            }
          }
        }
      }
    }
  }
}

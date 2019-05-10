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
    public CapturefineryWindow()
    {
      InitializeComponent();
    }

    private async void Grid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      if (e.AddedItems.Count > 0)
      {
        var study = e.AddedItems[0] as StudyInfo;
        var viewModel = MainGrid.DataContext as CapturefineryWindowViewModel;
        if (study != null && viewModel != null)
        {
          this.Focus();
          this.Hide();
          await viewModel.RunTasks(study);
          this.Show();
          this.Focus();          
        }
      }
    }
  }
}

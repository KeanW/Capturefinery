using Dynamo.ViewModels;
using Dynamo.Wpf.Extensions;
using System;
using System.Windows;
using System.Windows.Controls;

namespace CapturefineryViewExtension
{
  public class CapturefineryViewExtension : IViewExtension
  {
    private MenuItem menuItem;

    public void Dispose() {}

    public void Startup(ViewStartupParams p)
    {
      ToolTipService.ShowOnDisabledProperty.OverrideMetadata(
        typeof(Control),
        new FrameworkPropertyMetadata(true)
      );
    }

    public void Loaded(ViewLoadedParams p)
    {
      menuItem = new MenuItem { Header = "Capture Refinery Study..." };
      menuItem.Click += (sender, args) =>
      {
        var dynViewModel = p.DynamoWindow.DataContext as DynamoViewModel;
        var viewModel = new CapturefineryWindowViewModel(p, dynViewModel);
        var window = new CapturefineryWindow
        {
          // Set the data context for the main grid in the window

          MainGrid = { DataContext = viewModel },

          // Set the owner of the window to the Dynamo window

          Owner = p.DynamoWindow
        };

        window.Left = window.Owner.Left + window.Owner.Width / 2 - window.Width / 2;
        window.Top = window.Owner.Top + window.Owner.Height / 2 - window.Height / 2;

        // Show our modeless window

        window.Show();
      };
      p.AddMenuItem(MenuBarType.View, menuItem);
    }

    public void Shutdown()
    {
    }

    public string UniqueId
    {
      get { return Guid.NewGuid().ToString(); }
    }

    public string Name
    {
      get { return "Capturefinery View Extension"; }
    }
  }
}

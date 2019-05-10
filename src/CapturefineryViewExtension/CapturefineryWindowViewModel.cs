using CoreNodeModels.Input;
using Dynamo.Core;
using Dynamo.Extensions;
using Dynamo.Graph.Nodes;
using Dynamo.Graph.Nodes;
using Dynamo.Graph.Workspaces;
using Dynamo.Models;
using Dynamo.ViewModels;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Media;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace CapturefineryViewExtension
{
  public class HallOfFame
  {
    public string[] goals;
    public string[] ids;
    public string[][] solutions;
    public string[] variables;
  }
  public class FileOfFame
  {
    public int currentGeneration;
    public HallOfFame hallOfFame;
    public string id;
    public int maxGeneration;
    public int population_count;
    public long size;
    public string status;
    public string task_solver;
    public string endpoint;
  }
  public class CapturefineryWindowViewModel : NotificationObject, IDisposable
  {
    private ObservableCollection<StudyInfo> _refineryStudies;
    private ReadyParams _readyParams;
    private DynamoViewModel _dynamoViewModel;
    private string _file = "";
    private bool _waiting = false;
    private List<Bitmap> _images;

    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    public static extern bool DeleteObject(IntPtr hObject);

    // Construction & disposal

    public CapturefineryWindowViewModel(ReadyParams p, DynamoViewModel dynamoVM)
    {
      _readyParams = p;
      _dynamoViewModel = dynamoVM;
      _file = p.CurrentWorkspaceModel.FileName;
      _images = new List<Bitmap>();
    }

    public void Dispose()
    {
      foreach (var image in _images)
      {
        image.Dispose();
      }
    }

    public ObservableCollection<StudyInfo> RefineryTasks
    {
      get
      {
        _refineryStudies = getRefineryTasks(_file);
        return _refineryStudies;
      }
      set
      {
        _refineryStudies = value;
      }
    }

    public ObservableCollection<StudyInfo> getRefineryTasks(string fileName)
    {
      var nodeList = new List<StudyInfo>();

      var i = 0;
      var folder = fileName.Replace("dyn", "RefineryResults");
      if (System.IO.Directory.Exists(folder))
      {
        var start = folder.Length + 1;
        foreach (var file in System.IO.Directory.EnumerateDirectories(folder))
        {
          nodeList.Add(new StudyInfo(++i, file.Substring(start), file));
        }
      }

      // Return a bindable collection

      return new ObservableCollection<StudyInfo>(nodeList);
    }

    public async Task RunTasks(StudyInfo study)
    {
      _waiting = false;
      _images.Clear();
      int counter = 0;

      var folder = study.Folder + "\\screenshots";
      if (!System.IO.Directory.Exists(folder))
      {
        System.IO.Directory.CreateDirectory(folder);
      }

      Dynamo.Events.ExecutionEvents.GraphPostExecution +=
        async (e) =>
        {
          DoEvents();
          await Task.Delay(3000);

          _waiting = false;

          _images.Add(SaveScreenshot(folder + "\\" + counter++.ToString() + ".jpg"));
        };

      var hof = GetHallOfFame(study);

      var nodeMap = GetDynamoNodesForInputParameters(hof.variables, _readyParams.CurrentWorkspaceModel.Nodes);

      // Optionally choose a max number of solutions to capture

      int? max = null;

      var runs = max != null && max.HasValue ? hof.solutions.Take<string[]>(max.Value) : hof.solutions;
      foreach (var parameters in runs)
      {
        for (var i = 0; i < hof.variables.Length; i++)
        {
          SetDynamoInputParameter(nodeMap, hof.variables[i], parameters[hof.goals.Length + i]);
        }

        _waiting = true;

        StartDynamoRun();

        while (_waiting)
        {
          await Task.Delay(1000);
        }
      }

      await Task.Delay(5000);

      SaveAnimation(_images, folder + "\\animation.gif");
      SaveAnimation(_images, folder + "\\animation-small.gif", 1000);
      SaveAnimation(_images, folder + "\\animation-tiny.gif", 500);
    }

    private HallOfFame GetHallOfFame(StudyInfo study)
    {
      var fof = JsonConvert.DeserializeObject<FileOfFame>(File.ReadAllText(study.Folder + "\\RefineryResults.json"));
      return fof.hallOfFame;
    }

    private Dictionary<string, NodeModel> GetDynamoNodesForInputParameters(string[] variables, IEnumerable<NodeModel> nodes)
    {
      var nodeMap = new Dictionary<string, NodeModel>();
      foreach (var variable in variables)
      {
        foreach (var node in nodes)
        {
          if (node.Name == variable && node.IsSetAsInput)
          {
            foreach (var nodeViewModel in _dynamoViewModel.CurrentSpaceViewModel.Nodes)
            {
              var nodeModel = nodeViewModel.NodeModel;
              if (node.GUID == nodeModel.GUID)
              {
                nodeMap.Add(variable, nodeModel);
              }
            }
          }
        }
      }
      return nodeMap;
    }

    private void SetDynamoInputParameter(Dictionary<string, NodeModel> nodeMap, string parameterName, string parameterValue)
    {
      var node = nodeMap[parameterName];
      if (node != null)
      {
        var nodeType = node.GetType();

        if (nodeType == typeof(DoubleSlider))
        {
          var slider = node as DoubleSlider;
          slider.Value = Convert.ToDouble(parameterValue, CultureInfo.InvariantCulture);
        }
        else if (nodeType == typeof(IntegerSlider))
        {
          var slider = node as IntegerSlider;
          slider.Value = Convert.ToInt32(parameterValue, CultureInfo.InvariantCulture);
        }
        else if (nodeType == typeof(BoolSelector))
        {
          var selector = node as BoolSelector;
          selector.Value = parameterValue == "1" || parameterValue == "true";
        }
      }
    }

    private void StartDynamoRun()
    {
      var cmd = new Dynamo.Models.DynamoModel.RunCancelCommand(false, false);
      _dynamoViewModel.ExecuteCommand(cmd);
    }

    private Bitmap SaveScreenshot(string file)
    {
      var bitmap = new Bitmap(Screen.PrimaryScreen.Bounds.Width,
                              Screen.PrimaryScreen.Bounds.Height);
      using (var g = Graphics.FromImage(bitmap))
      {
        g.CopyFromScreen(Screen.PrimaryScreen.Bounds.X,
                         Screen.PrimaryScreen.Bounds.Y,
                         0, 0,
                         bitmap.Size,
                         CopyPixelOperation.SourceCopy);
        bitmap.Save(file, ImageFormat.Jpeg);
      }
      return bitmap;
    }

    private void SaveAnimation(IEnumerable<Bitmap> images, string path, int? width = null)
    {
      // If a width has been provided, scale the source images down to that width

      IEnumerable<Bitmap> smallImages = null;

      if (width != null && width.HasValue)
      {
        var first = images.First<System.Drawing.Bitmap>();
        var w = width != null && width.HasValue ? width.Value : first.Width;
        var h = first.Height * w / first.Width;

        smallImages = from img in images
                      select new Bitmap(img, w, h);
        images = smallImages;
      }

      // From: https://stackoverflow.com/questions/1196322/how-to-create-an-animated-gif-in-net

      var enc = new GifBitmapEncoder();

      foreach (var bmpImage in images)
      {
        var bmp = bmpImage.GetHbitmap();
        var src = Imaging.CreateBitmapSourceFromHBitmap(
            bmp,
            IntPtr.Zero,
            Int32Rect.Empty,
            BitmapSizeOptions.FromEmptyOptions());
        enc.Frames.Add(BitmapFrame.Create(src));
        DeleteObject(bmp); // recommended, handle memory leak
      }

      // Hack to make the image loop
      // From: https://stackoverflow.com/questions/18719302/net-creating-a-looping-gif-using-gifbitmapencoder

      using (var ms = new MemoryStream())
      {
        enc.Save(ms);
        var fileBytes = ms.ToArray();
        // This is the NETSCAPE2.0 Application Extension.
        var applicationExtension = new byte[] { 33, 255, 11, 78, 69, 84, 83, 67, 65, 80, 69, 50, 46, 48, 3, 1, 0, 0, 0 };
        var newBytes = new List<byte>();
        newBytes.AddRange(fileBytes.Take(13));
        newBytes.AddRange(applicationExtension);
        newBytes.AddRange(fileBytes.Skip(13));
        File.WriteAllBytes(path, newBytes.ToArray());
      }

      if (smallImages != null)
      {
        foreach (var image in smallImages)
        {
          image.Dispose();
        }
      }
    }

    private void DoEvents()
    {
      System.Windows.Application.Current.Dispatcher.Invoke(
        DispatcherPriority.Background, new Action(delegate { })
      );
    }
  }
}

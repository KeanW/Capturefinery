using CoreNodeModels.Input;
using Dynamo.Core;
using Dynamo.Events;
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
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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

  public class CapturefineryWindowViewModel : NotificationObject, INotifyPropertyChanged, IDisposable
  {
    private ObservableCollection<StudyInfo> _refineryStudies;
    private ReadyParams _readyParams;
    private DynamoViewModel _dynamoViewModel;
    private string _file = "";

    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    public static extern bool DeleteObject(IntPtr hObject);

    private int _start;
    private int _items;
    private int _maxItems;
    private bool _captureErrors;
    private bool _createAnimations;
    private bool _loadImages;
    private bool _escapePressed;

    public int Start
    {
      get { return _start; }
      set
      {
        if (value < 0 || value >= _maxItems)
        {
          throw new ArgumentException("Start must be greater than or equal to zero and less than the maximum number of items.");
        }
        _start = value;
        OnPropertyChanged();
      }
    }

    public int Items
    {
      get { return _items; }
      set
      {
        if (value < 0 || value > _maxItems)
        {
          throw new ArgumentException("Items must be greater than or equal to zero and less than or equal to the maximum number of items.");
        }
        _items = value;
        OnPropertyChanged();
      }
    }

    public int MaxItems
    {
      get { return _maxItems; }
      set {
        _maxItems = value;
        OnPropertyChanged();
      }
    }

    public bool CaptureErrors
    {
      get { return _captureErrors; }
      set
      {
        _captureErrors = value;
        OnPropertyChanged();
      }
    }

    public bool CreateAnimations
    {
      get { return _createAnimations; }
      set
      {
        _createAnimations = value;
        if (!value)
        {
          // If toggling animation creation off, clear the loading of images

          LoadImages = false;
        }
        OnPropertyChanged();
      }
    }

    public bool LoadImages
    {
      get { return _loadImages; }
      set
      {
        _loadImages = value;
        OnPropertyChanged();
      }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public void OnPropertyChanged([CallerMemberName]string propertyName = null)
    {
      if (PropertyChanged != null)
      {
        PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
      }
    }

    // Construction & disposal

    public CapturefineryWindowViewModel(ReadyParams p, DynamoViewModel dynamoVM)
    {
      _readyParams = p;
      _dynamoViewModel = dynamoVM;
      _file = p.CurrentWorkspaceModel.FileName;
      _captureErrors = true;
      _createAnimations = true;
      _loadImages = false;
      _escapePressed = false;
    }

    public void Dispose()
    {
    }

    public ObservableCollection<StudyInfo> RefineryTasks
    {
      get
      {
        _refineryStudies = GetRefineryTasks(_file);
        return _refineryStudies;
      }
      set
      {
        _refineryStudies = value;
      }
    }

    public ObservableCollection<StudyInfo> GetRefineryTasks(string fileName)
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

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
      if (e.KeyCode == Keys.Escape)
      {
        _escapePressed = true;
      }
    }

    public async Task RunTasks(StudyInfo study, HallOfFame hof = null)
    {
      if (
        Start >= 0 && Start < _maxItems &&
        Items > 0 && Items <= _maxItems &&
        Start + Items <= _maxItems
      )
      {
        bool waiting = false;
        int counter = Start;
        var images = new List<Bitmap>();
        var errorImages = new List<Bitmap>();
        var runsWithErrors = new List<int>();

        var folder = study.Folder + "\\screenshots";
        if (!System.IO.Directory.Exists(folder))
        {
          System.IO.Directory.CreateDirectory(folder);
        }

        // Attach a handler to check for the use of Escape

        InterceptKeys.OnKeyDown += new KeyEventHandler(OnKeyDown);
        InterceptKeys.Start();

        // Pre-load any existing images that come before the chosen range, if this option was selected

        if (_createAnimations && _loadImages && counter > 0)
        {
          LoadExistingImages(images, _captureErrors ? errorImages : null, folder, 0, counter);
        }

        // Define and attach the main post-execution handler

        ExecutionStateHandler postExecution =
          async (e) =>
          {
            if (!_escapePressed)
            {
              DoEvents();
              await Task.Delay(3000);

              waiting = false;

              var isError = false;
              if (_captureErrors)
              {
                // Does the graph contain any nodes in an error state?

                var errorNodes =
                  (from n in _readyParams.CurrentWorkspaceModel.Nodes
                   where n.State != ElementState.Active && n.State != ElementState.Dead
                   select n);
                if (errorNodes.Count<NodeModel>() > 0)
                {
                  isError = true;
                  runsWithErrors.Add(counter);
                }
              }

              var img = SaveScreenshot(GetImageFilename(folder, counter, isError));
              if (isError)
              {
                errorImages.Add(img);
              }
              else
              {
                images.Add(img);
              }

              counter++;
            }
          };

        ExecutionEvents.GraphPostExecution += postExecution;

        if (hof == null)
        {
          hof = GetHallOfFame(study);
        }

        var nodeMap = GetDynamoNodesForInputParameters(hof.variables, _readyParams.CurrentWorkspaceModel.Nodes);

        for (int i = Start; i < Start + Items; i++)
        {
          if (_escapePressed)
          {
            break;
          }

          var parameters = hof.solutions[i];
          for (var j = 0; j < hof.variables.Length; j++)
          {
            SetDynamoInputParameter(nodeMap, hof.variables[j], parameters[hof.goals.Length + j]);
          }

          waiting = true;

          StartDynamoRun();

          while (waiting)
          {
            await Task.Delay(1000);
          }
        }

        // Post-load any existing images that come after the chosen range, if this option was selected

        if (_createAnimations && _loadImages && counter + 1 < _maxItems)
        {
          LoadExistingImages(images, _captureErrors ? errorImages : null, folder, counter + 1, _maxItems);
        }

        if (!_escapePressed)
        {
          if (_createAnimations)
          {
            if (images.Count > 0)
            {
              SaveAnimation(images, folder + "\\animation.gif");
              SaveAnimation(images, folder + "\\animation-small.gif", 1000);
              SaveAnimation(images, folder + "\\animation-tiny.gif", 500);
            }

            if (errorImages.Count > 0)
            {
              SaveAnimation(errorImages, folder + "\\animation-errors.gif");
              SaveAnimation(errorImages, folder + "\\animation-errors-small.gif", 1000);
              SaveAnimation(errorImages, folder + "\\animation-errors-tiny.gif", 500);
            }
          }

          // If errors were found in any of the runs, create a new run with just the problematic runs

          const string errorsSuffix = "-errors";
          if (_captureErrors && runsWithErrors.Count > 0 && !study.Folder.EndsWith(errorsSuffix))
          {
            SaveFilteredHallOfFame(study, study.Folder + errorsSuffix, runsWithErrors);
            RaisePropertyChanged("RefineryTasks");
          }
        }

        foreach (var image in images)
        {
          image.Dispose();
        }
        foreach (var image in errorImages)
        {
          image.Dispose();
        }

        InterceptKeys.Stop();
        InterceptKeys.OnKeyDown -= new KeyEventHandler(OnKeyDown);
        ExecutionEvents.GraphPostExecution -= postExecution;
      }
    }

    private string GetImageFilename(string folder, int counter, bool isError)
    {
      return folder + "\\" + counter.ToString() + (isError ? "-error" : "") + ".jpg";
    }

    private void LoadExistingImages(List<Bitmap> images, List<Bitmap> errors, string folder, int start, int end)
    {
      for (var i = start; i < end; i++)
      {
        var name = GetImageFilename(folder, i, false);
        if (File.Exists(name))
        {
          var image = new Bitmap(name);
          if (image != null)
          {
            images.Add(image);
          }
        }
        else
        {
          // Also check for images flagged as errors...
          // Add these either to the special error list or the main one

          var errName = GetImageFilename(folder, i, true);
          if (File.Exists(errName))
          {
            var errImage = new Bitmap(errName);
            if (errImage != null)
            {
              (errors == null ? images : errors).Add(errImage);
            }
          }
        }
      }
    }

    public HallOfFame GetHallOfFame(StudyInfo study)
    {
      var fof = JsonConvert.DeserializeObject<FileOfFame>(File.ReadAllText(study.Folder + "\\RefineryResults.json"));
      return fof.hallOfFame;
    }

    private void SaveFilteredHallOfFame(StudyInfo study, string folder, List<int> subset)
    {
      var fof = JsonConvert.DeserializeObject<FileOfFame>(File.ReadAllText(study.Folder + "\\RefineryResults.json"));
      var sols = fof.hallOfFame.solutions;
      var solutionSubset = new string[subset.Count][];
      for (int i = 0; i < subset.Count; i++)
      {
        solutionSubset[i] = sols[subset[i]];
      }
      fof.hallOfFame.solutions = solutionSubset;
      System.IO.Directory.CreateDirectory(folder);
      File.WriteAllText(folder + "\\RefineryResults.json", JsonConvert.SerializeObject(fof));
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

    private Bitmap SaveScreenshot(string file, bool fullScreen = false)
    {
      Bitmap bitmap;
      if (fullScreen)
      {
        bitmap = new Bitmap(Screen.PrimaryScreen.Bounds.Width,
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
      }
      else
      {
        // We need to perform the capture on the main thread

        System.Windows.Application.Current.Dispatcher.Invoke(
          () =>
          {
            _dynamoViewModel.OnRequestSave3DImage(_dynamoViewModel, new ImageSaveEventArgs(file));
          }
        );
        bitmap = new Bitmap(file);
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

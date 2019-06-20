using CoreNodeModels.Input;
using Dynamo.Events;
using Dynamo.Extensions;
using Dynamo.Graph.Nodes;
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
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Clipboard = System.Windows.Clipboard;
using MessageBox = System.Windows.MessageBox;

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

  public class SortLevel : ObservableObject
  {
    private string _name;
    private string _parameter;
    private int _number;
    private string[] _parameters;

    public string Name
    {
      get { return _name; }
      set
      {
        _name = value;
        OnPropertyChanged();
      }
    }
    public string Parameter
    {
      get { return _parameter; }
      set
      {
        _parameter = value;
        OnPropertyChanged();
      }
    }
    public int Number
    {
      get { return _number; }
      set
      {
        _number = value;
        OnPropertyChanged();
      }
    }
    public string[] Parameters
    {
      get { return _parameters; }
      set
      {
        _parameters = value;
        OnPropertyChanged();
      }
    }
  }

  public abstract class ObservableObject : INotifyPropertyChanged
  {
    public event PropertyChangedEventHandler PropertyChanged;

    public void OnPropertyChanged([CallerMemberName]string propertyName = null)
    {
      if (PropertyChanged != null)
      {
        PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
      }
    }
  }

  public class CapturefineryWindowViewModel : ObservableObject, IDisposable
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
    private double _progress;
    private bool _captureErrors;
    private bool _createAnimations;
    private string _rootName;
    private bool _loadImages;
    private bool _escapePressed;
    private bool _executeEnabled;
    private string _executeText;
    private List<string> _parameterList;
    private Dispatcher _dispatcherUIThread;
    private string _folder;
    private Dynamo.Models.RunType _previousRunType;

    const string enableText = "Click here to launch a capture run. It may take some time, but can be canceled.";
    const string disableText = "Capture canceled; another can be started when current run completes.";
    public readonly string EmptyComboValue = "   ";

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
      set
      {
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
        OnPropertyChanged();
      }
    }

    public string RootName
    {
      get { return _rootName; }
      set
      {
        _rootName = value;
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

    public double Progress
    {
      get { return _progress; }
      set
      {
        _progress = value;
        OnPropertyChanged();
      }
    }

    public bool Escape
    {
      get { return _escapePressed; }
      set
      {
        _escapePressed = value;
        OnPropertyChanged();
      }
    }

    public bool ExecuteEnabled
    {
      get { return _executeEnabled; }
      set
      {
        _executeEnabled = value;
        OnPropertyChanged();
      }
    }

    public string ExecuteText
    {
      get { return _executeText; }
      set
      {
        _executeText = value;
        OnPropertyChanged();
      }
    }

    public ObservableCollection<SortLevel> SortLevels { get; set; }

    // Construction & disposal

    public CapturefineryWindowViewModel(ReadyParams p, DynamoViewModel dynamoVM)
    {
      _readyParams = p;
      _dynamoViewModel = dynamoVM;
      _file = p.CurrentWorkspaceModel.FileName;
      _captureErrors = true;
      _createAnimations = true;
      _rootName = "animation";
      _loadImages = false;
      _escapePressed = false;
      _executeEnabled = true;
      _progress = 0.0;
      _executeText = enableText;
      _parameterList = new List<string>();
      _dispatcherUIThread = System.Windows.Application.Current.Dispatcher;
      SortLevels = new ObservableCollection<SortLevel>();
    }

    public void Dispose()
    {
    }

    internal void LogException(Exception ex)
    {
      using (var sw = new StreamWriter(_folder + "\\errors.log", true))
      {
        sw.WriteLine(System.DateTime.Now + ": " + ex.Message);
        sw.WriteLine(System.DateTime.Now + ": " + ex.StackTrace);
        sw.Close();
      }
    }

    internal void InitProperties(int max)
    {
      MaxItems = max;
      Start = 0;
      Items = max;
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

    public async Task RunTasks(StudyInfo study, HallOfFame hof = null)
    {
      _folder = study.Folder + "\\screenshots";
      if (!System.IO.Directory.Exists(_folder))
      {
        System.IO.Directory.CreateDirectory(_folder);
      }

      if (
        Start >= 0 && Start < _maxItems &&
        Items >= 0 && Items <= _maxItems &&
        Start + Items <= _maxItems
      )
      {
        bool waiting = false;
        int counter = Start;
        var images = new Bitmap[_maxItems];
        var errorImages = new Bitmap[_maxItems];
        var runsWithErrors = new List<int>();
        Progress = 0;
        Escape = false;

        // Pre-load any existing images that come before the chosen range, if this option was selected

        if (_createAnimations && _loadImages && counter > 0)
        {
          LoadExistingImages(images, _captureErrors ? errorImages : null, _folder, 0, counter);
        }

        _previousRunType = _dynamoViewModel.HomeSpace.RunSettings.RunType;
        _dynamoViewModel.HomeSpace.RunSettings.RunType = Dynamo.Models.RunType.Manual;

        // Define and attach the main post-execution handler

        ExecutionStateHandler postExecution =
          async (e) =>
          {
            DoEvents();
            await Task.Delay(3000);

            waiting = false;

            if (!_escapePressed)
            {
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

              var img = SaveScreenshot(GetImageFilename(_folder, counter, isError));
              if (isError)
              {
                errorImages[counter] = img;
              }
              else
              {
                images[counter] = img;
              }

              counter++;

              var captured = counter - Start;
              Progress = 100 * captured / Items;
            }
          };

        ExecutionEvents.GraphPostExecution += postExecution;

        if (hof == null)
        {
          hof = GetHallOfFame(study);
        }

        if (Items == 0)
        {
          Progress = 100;
        }
        else
        {
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
        }

        _dynamoViewModel.HomeSpace.RunSettings.RunType = _previousRunType;

        // Post-load any existing images that come after the chosen range, if this option was selected

        if (_createAnimations && _loadImages && counter + 1 < _maxItems)
        {
          LoadExistingImages(images, _captureErrors ? errorImages : null, _folder, counter + 1, _maxItems);
        }

        if (!_escapePressed)
        {
          if (_createAnimations)
          {
            var levels = GetSortLevels();
            var order = GetSolutionOrder(hof, levels);

            var rootName = StripInvalidFileAndPathCharacters(_rootName);

            if (!images.All<Bitmap>((b) => b == null))
            {
              SaveAnimation(images, order, _folder + "\\" + rootName + ".gif");
              SaveAnimation(images, order, _folder + "\\" + rootName + "-small.gif", 1000);
              SaveAnimation(images, order, _folder + "\\" + rootName + "-tiny.gif", 500);
            }

            if (!errorImages.All<Bitmap>((b) => b == null))
            {
              SaveAnimation(errorImages, order, _folder + "\\" + rootName + "-errors.gif");
              SaveAnimation(errorImages, order, _folder + "\\" + rootName + "-errors-small.gif", 1000);
              SaveAnimation(errorImages, order, _folder + "\\" + rootName + "-errors-tiny.gif", 500);
            }
          }

          // If errors were found in any of the runs, create a new run with just the problematic runs

          const string errorsSuffix = "-errors";
          if (_captureErrors && runsWithErrors.Count > 0 && !study.Folder.EndsWith(errorsSuffix))
          {
            SaveFilteredHallOfFame(study, study.Folder + errorsSuffix, runsWithErrors);
            OnPropertyChanged("RefineryTasks");
          }
        }

        foreach (var image in images)
        {
          if (image != null)
          {
            image.Dispose();
          }
        }
        foreach (var image in errorImages)
        {
          if (image != null)
          {
            image.Dispose();
          }
        }

        DisableExecute(false);
        ExecutionEvents.GraphPostExecution -= postExecution;

        var result = MessageBox.Show("Capture complete. Copy output path to the clipboard?",
                                      "Confirmation",
                                      MessageBoxButton.YesNo,
                                      MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
          Clipboard.SetText(_folder);
        }
      }
    }

    public string StripInvalidFileAndPathCharacters(string filename)
    {
      var valid = filename;
      var invalid = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
      foreach (char c in invalid)
      {
        valid = valid.Replace(c.ToString(), "");
      }
      return valid;
    }

    public void DisableExecute(bool disable)
    {
      ExecuteEnabled = !disable;
      ExecuteText = disable ? disableText : enableText;
    }

    private string GetImageFilename(string folder, int counter, bool isError)
    {
      return folder + "\\" + counter.ToString() + (isError ? "-error" : "") + ".jpg";
    }

    private void LoadExistingImages(Bitmap[] images, Bitmap[] errors, string folder, int start, int end)
    {
      for (var i = start; i < end; i++)
      {
        var name = GetImageFilename(folder, i, false);
        if (File.Exists(name))
        {
          var image = new Bitmap(name);
          if (image != null)
          {
            images[i] = image;
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
              (errors == null ? images : errors)[i] = errImage;
            }
          }
        }
      }
    }

    public HallOfFame GetHallOfFame(StudyInfo study)
    {
      var fof = JsonConvert.DeserializeObject<FileOfFame>(File.ReadAllText(study.Folder + "\\RefineryResults.json"));
      if (fof != null && fof.hallOfFame != null)
      {
        var hof = fof.hallOfFame;
        _parameterList.Clear();
        _parameterList.Add(EmptyComboValue); // This means we clear the setting
        _parameterList.AddRange(hof.goals);
        _parameterList.AddRange(hof.variables);
        RemoveSortLevels(0);
        AddSortLevel();
        OnPropertyChanged("SortLevels");
      }
      return fof.hallOfFame;
    }
    
    public void UpdateSortLevels()
    {
      var existing = from level in SortLevels select level.Parameter;
      foreach (var level in SortLevels)
      {
        var pars = from param in _parameterList where param == level.Parameter || !existing.Contains(param) select param;
        level.Parameters = pars.ToArray();
      }
    }

    public string[] GetSortLevels()
    {
      var existing = from level in SortLevels select level.Parameter;
      return existing.ToArray();
    }

    public void AddSortLevel()
    {
      var levelNumber = SortLevels.Count;
      if (levelNumber < _parameterList.Count - 1)
      {
        var existing = from level in SortLevels where level.Number < levelNumber select level.Parameter;
        var pars = from param in _parameterList where !existing.Contains(param) select param;
        SortLevels.Add(new SortLevel { Name = Ordinal(levelNumber + 1) + " sorting level", Number = levelNumber, Parameter = null, Parameters = pars.ToArray() });
      }
      UpdateSortLevels();
    }

    public void RemoveSortLevels(int startLevel)
    {
      while (SortLevels.Count > startLevel)
      {
        SortLevels.RemoveAt(startLevel);
      }
      if (startLevel > 0)
      {
        SortLevels[startLevel - 1].Parameter = null;
      }
      UpdateSortLevels();
    }

    public static string Ordinal(int number)
    {
      string suffix = string.Empty;

      int ones = number % 10;
      int tens = (int)Math.Floor(number / 10M) % 10;

      if (tens == 1)
      {
        suffix = "th";
      }
      else
      {
        switch (ones)
        {
          case 1:
            suffix = "st";
            break;

          case 2:
            suffix = "nd";
            break;

          case 3:
            suffix = "rd";
            break;

          default:
            suffix = "th";
            break;
        }
      }
      return string.Format("{0}{1}", number, suffix);
    }

    public int[] GetSolutionOrder(HallOfFame hof, string[] parameters)
    {
      // Populate default array with sequential order

      var maxItems = hof.solutions.Length;
      var res = new int[maxItems];
      for (int i = 0; i < maxItems; i++)
      {
        res[i] = i;
      }

      if (parameters.Length > 0 && !(parameters.Length == 1 && parameters[0] == null))
      {
        var idx = GetParameterIndex(hof, parameters[0]);
        var selected = hof.solutions.Select((item, index) => new { item, index });
        var ordered = selected.OrderBy(a => Extract(a.item[idx]));

        for (int i = 1; i < parameters.Length; i++)
        {
          var parameter = parameters[i];
          if (parameter == null)
          {
            break;
          }
          var idx2 = GetParameterIndex(hof, parameter);
          ordered = ordered.ThenBy(a => Extract(a.item[idx2]));
        }
        return ordered.Select(a => a.index).ToArray<int>();
      }
      return res;
    }

    private double Extract(string str)
    {
      return Math.Round(double.Parse(str), 6);
    }

    private int GetParameterIndex(HallOfFame hof, string param)
    {
      int idx = -1;
      var goalIdx = Array.IndexOf(hof.goals, param);
      if (goalIdx >= 0)
      {
        idx = goalIdx;
      }
      var varIdx = Array.IndexOf(hof.variables, param);
      if (varIdx >= 0)
      {
        idx = hof.goals.Length + varIdx;
      }
      return idx;
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

        if (node is DoubleSlider)
        {
          var slider = (DoubleSlider)node;
          slider.Value = Convert.ToDouble(parameterValue, CultureInfo.InvariantCulture);
        }
        else if (node is IntegerSlider)
        {
          var slider = (IntegerSlider)node;
          slider.Value = Convert.ToInt32(parameterValue, CultureInfo.InvariantCulture);
        }
        else if (node is BoolSelector)
        {
          var selector = (BoolSelector)node;
          selector.Value = parameterValue == "1" || parameterValue == "true";
        }
      }
    }

    private void StartDynamoRun()
    {
      var cmd = new Dynamo.Models.DynamoModel.ForceRunCancelCommand(false, false);
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

        _dispatcherUIThread.Invoke(
          () =>
          {
            _dynamoViewModel.OnRequestSave3DImage(_dynamoViewModel, new ImageSaveEventArgs(file));
          }
        );
        bitmap = new Bitmap(file);
      }
      return bitmap;
    }

    private void SaveAnimation(Bitmap[] images, IEnumerable<int> order, string path, int? width = null)
    {
      // If a width has been provided, scale the source images down to that width

      var smallImages = new Bitmap[images.Length];

      if (width != null && width.HasValue)
      {
        Bitmap first = null;
        for (int i = 0; i < images.Length; i++)
        {
          if (images[i] != null)
          {
            first = images[i];
            break;
          }
        }
        if (first != null)
        {
          var w = width != null && width.HasValue ? width.Value : first.Width;
          var h = first.Height * w / first.Width;

          for (int i = 0; i < images.Length; i++)
          {
            smallImages[i] = images[i] != null ? new Bitmap(images[i], w, h) : null;
          }
          images = smallImages;
        }
      }

      // From: https://stackoverflow.com/questions/1196322/how-to-create-an-animated-gif-in-net

      var enc = new GifBitmapEncoder();

      foreach (var idx in order)
      {
        var bmpImage = images[idx];
        if (bmpImage != null)
        {
          var bmp = bmpImage.GetHbitmap();
          if (bmp != null)
          {
            var src = Imaging.CreateBitmapSourceFromHBitmap(
                bmp,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            enc.Frames.Add(BitmapFrame.Create(src));
            DeleteObject(bmp); // recommended, handle memory leak
          }
        }
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
          if (image != null)
          {
            image.Dispose();
          }
        }
      }
    }

    private void DoEvents()
    {
      _dispatcherUIThread.Invoke(
        DispatcherPriority.Background, new Action(delegate { })
      );
    }
  }
}

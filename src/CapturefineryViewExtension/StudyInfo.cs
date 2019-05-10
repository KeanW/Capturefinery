using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CapturefineryViewExtension
{
  public class StudyInfo : INotifyPropertyChanged
  {
    private int _id;
    private string _name;
    private string _folder;

    public event PropertyChangedEventHandler PropertyChanged;

    private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
    {
      if (PropertyChanged != null)
      {
        PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
      }
    }

    public StudyInfo(int id, string name, string folder)
    {
      _id = id;
      _name = name;
      _folder = folder;
      NotifyPropertyChanged();
    }

    public int ID
    {
      get { return _id; }
      set
      {
        _id = value;
        NotifyPropertyChanged();
      }
    }

    public string Name
    {
      get { return _name; }
      set
      {
        _name = value;
        NotifyPropertyChanged();
      }
    }

    public string Folder
    {
      get { return _folder; }
      set
      {
        _folder = value;
        NotifyPropertyChanged();
      }
    }
  }
}

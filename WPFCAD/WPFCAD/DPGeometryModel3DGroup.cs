using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Xml;

namespace WPFCAD
{
  public sealed class DPGeometryModel3DGroup : DependencyObject
  {
    private readonly List<DPGeometryModel3D> _geometryModel3DList = new List<DPGeometryModel3D>();
    public List<DPGeometryModel3D> GeometryModel3DList { get { return _geometryModel3DList; } }

    public Model3D GetModel3D()
    {
      try
      {
        var ret = new Model3DGroup();

        using (var currentWork = new StepWork("Create 3D Mode", this) { MaxSteps = GeometryModel3DList.Count })
        {
          foreach (var item in GeometryModel3DList)
          {
            var geometryModel3D = item.CreateGeometryModel3D();

            if (geometryModel3D != null)
              ret.Children.Add(geometryModel3D);

            currentWork.CurrentStep++;
          }
        }
        return ret;
      }
      catch (Exception)
      {
        return null;
      }
    }

    private Rect3D GetBounds()
    {
      var ret = Rect3D.Empty;

      foreach (var item in GeometryModel3DList)
      {
        ret.Union(item.GetBounds());
      }

      return ret;
    }

    public DPGeometryModel3DGroup GetSimplifiedGeometryModel3DGroup(UInt32 resolution)
    {
      var bounds = GetBounds();
      if (bounds.IsEmpty)
        return null;
      if (Double.IsNaN(bounds.SizeX) || Double.IsNaN(bounds.SizeY) || Double.IsNaN(bounds.SizeZ))
        return null;

      var max = Math.Max(bounds.SizeX, bounds.SizeY);
      var unitSize = Math.Max(max, bounds.SizeZ) / resolution;

      var ret = new DPGeometryModel3DGroup();

      using (var currentWork = new StepWork("Compress 3D Mode", this) { MaxSteps = GeometryModel3DList.Count })
      {
        foreach (var item in GeometryModel3DList)
        {
          var geometryModel3D = item.GetSimplifiedGeometryModel3D(unitSize);

          if (geometryModel3D != null)
            ret.GeometryModel3DList.Add(geometryModel3D);

          currentWork.CurrentStep++;
        }
      }

      return ret;
    }

    public void SaveToFile(string filePathName)
    {
      try
      {
        using (var fileStream = new FileStream(filePathName, FileMode.Create, FileAccess.Write, FileShare.None))
        {
          using (var currentWork = new StepWork("Save 3D Mode to file", this) { MaxSteps = GeometryModel3DList.Count })
          {
            var writer = new XmlTextWriter(fileStream, Encoding.ASCII);
            writer.Formatting = Formatting.Indented;

            writer.WriteStartElement("Model3DGroup", @"http://schemas.microsoft.com/winfx/2006/xaml/presentation");
            foreach (var item in GeometryModel3DList)
            {
              writer.WriteStartElement("GeometryModel3D");

              #region GeometryModel3D.Geometry

              writer.WriteStartElement("GeometryModel3D.Geometry");

              #region MeshGeometry3D

              writer.WriteStartElement("MeshGeometry3D");
              writer.WriteAttributeString("Positions", item.GetPositionsString());
              writer.WriteAttributeString("Normals", item.GetNormalsString());
              writer.WriteAttributeString("TriangleIndices", item.GetTriangleIndicesString());
              writer.WriteEndElement();

              #endregion

              writer.WriteEndElement();

              #endregion

              #region GeometryModel3D.MaterialColor

              writer.WriteStartElement("GeometryModel3D.Material");
              writer.WriteStartElement("DiffuseMaterial");
              writer.WriteStartElement("DiffuseMaterial.Brush");
              writer.WriteStartElement("SolidColorBrush");
              writer.WriteAttributeString("Color", item.MaterialColor.ToString());
              writer.WriteEndElement();
              writer.WriteEndElement();
              writer.WriteEndElement();
              writer.WriteEndElement();

              #endregion

              #region GeometryModel3D.BackMaterialColor

              writer.WriteStartElement("GeometryModel3D.BackMaterial");
              writer.WriteStartElement("DiffuseMaterial");
              writer.WriteStartElement("DiffuseMaterial.Brush");
              writer.WriteStartElement("SolidColorBrush");
              writer.WriteAttributeString("Color", item.BackMaterialColor.ToString());
              writer.WriteEndElement();
              writer.WriteEndElement();
              writer.WriteEndElement();
              writer.WriteEndElement();

              #endregion

              writer.WriteEndElement();

              currentWork.CurrentStep++;
            }
            writer.WriteEndElement();

            writer.Close();
          }
        }
      }
      catch (Exception)
      {
      }
    }
  }
  public sealed class DPGeometryModel3D : DependencyObject
  {
    public DPMeshGeometry3D MeshGeometry { get; set; }
    public Color MaterialColor { get; set; }
    public Color BackMaterialColor { get; set; }

    public Rect3D GetBounds()
    {
      if (MeshGeometry == null || MeshGeometry.Positions == null)
        return Rect3D.Empty;

      return this.MeshGeometry.GetBounds();
    }

    public DPGeometryModel3D GetSimplifiedGeometryModel3D(double unitSize)
    {
      var meshGeometry3D = MeshRefactor.GetSimplifiedMeshGeometry3D(MeshGeometry, unitSize);
      if (meshGeometry3D == null || meshGeometry3D.TriangleIndices == null || !meshGeometry3D.TriangleIndices.Any())
        return null;

      var ret = new DPGeometryModel3D();
      ret.MeshGeometry = meshGeometry3D;
      ret.MaterialColor = MaterialColor;
      ret.BackMaterialColor = BackMaterialColor;
      return ret;
    }

    public GeometryModel3D CreateGeometryModel3D()
    {
      try
      {
        if (!this.CheckAccess())
        {
          var ret = default(GeometryModel3D);
          this.Dispatcher.Invoke(new Action(delegate { ret = CreateGeometryModel3D(); }));
          return ret;
        }

        {
          if (MeshGeometry == null)
            return null;

          var meshGeometry3D = MeshGeometry.CreateMeshGeometry3D();
          if (meshGeometry3D == null)
            return null;

          var ret = new GeometryModel3D(meshGeometry3D, new DiffuseMaterial(new SolidColorBrush(MaterialColor)));
          ret.BackMaterial = (MaterialColor == BackMaterialColor) ? ret.Material : new DiffuseMaterial(new SolidColorBrush(BackMaterialColor));
          return ret;
        }
      }
      catch (System.Exception)
      {
        return null;
      }
    }

    public string GetPositionsString()
    {
      var ret = new StringBuilder();
      var count = 0;
      foreach (var item in MeshGeometry.Positions)
      {
        if (count > 0)
        {
          ret.Append(' ');
          ret.Append(' ');
        }

        ret.Append(item.X.ToString());
        ret.Append(' ');
        ret.Append(item.Y.ToString());
        ret.Append(' ');
        ret.Append(item.Z.ToString());
        count++;
      }
      return ret.ToString();
    }

    public string GetNormalsString()
    {
      var ret = new StringBuilder();
      var count = 0;
      foreach (var item in MeshGeometry.Normals)
      {
        if (count > 0)
        {
          ret.Append(' ');
          ret.Append(' ');
        }

        ret.Append(item.X.ToString());
        ret.Append(' ');
        ret.Append(item.Y.ToString());
        ret.Append(' ');
        ret.Append(item.Z.ToString());
        count++;
      }
      return ret.ToString();
    }

    public string GetTriangleIndicesString()
    {
      var ret = new StringBuilder();
      var count = 0;
      foreach (var item in MeshGeometry.TriangleIndices)
      {
        if (count > 0)
          ret.Append(' ');
        if (count / 3 > 0)
          ret.Append(' ');

        ret.Append(item.ToString());
        count++;
      }
      return ret.ToString();
    }
  }
  public sealed class DPMeshGeometry3D : DependencyObject
  {
    public DPMeshGeometry3D() { }
    public DPMeshGeometry3D(List<Point3D> positions, List<Vector3D> normals, List<Int32> triangleIndices)
    {
      this.Positions = positions;
      this.Normals = normals;
      this.TriangleIndices = triangleIndices;
    }

    public List<Point3D> Positions { get; set; }
    public List<Vector3D> Normals { get; set; }
    public List<Int32> TriangleIndices { get; set; }

    public Rect3D GetBounds()
    {
      if (Positions == null || Positions.Count <= 0)
        return Rect3D.Empty;

      var reference = this.Positions;

      var minPt = new Point3D(reference.Select(p => p.X).Min(), reference.Select(p => p.Y).Min(), reference.Select(p => p.Z).Min());
      var maxPt = new Point3D(reference.Select(p => p.X).Max(), reference.Select(p => p.Y).Max(), reference.Select(p => p.Z).Max());

      var vector3D = maxPt - minPt;
      return new Rect3D(minPt, new Size3D(vector3D.X, vector3D.Y, vector3D.Z));
    }

    public MeshGeometry3D CreateMeshGeometry3D()
    {
      try
      {
        if (!this.CheckAccess())
        {
          var ret = default(MeshGeometry3D);
          this.Dispatcher.Invoke(new Action(delegate { ret = CreateMeshGeometry3D(); }));
          return ret;
        }
        {
          if (Positions == null || Normals == null || TriangleIndices == null)
            return null;
          if (!Positions.Any() || !Normals.Any() || !TriangleIndices.Any())
            return null;

          var positions = new Point3DCollection();
          this.Positions.ForEach(positions.Add);

          var normals = new Vector3DCollection();
          this.Normals.ForEach(normals.Add);

          var triangleIndices = new Int32Collection();
          this.TriangleIndices.ForEach(triangleIndices.Add);

          var ret = new MeshGeometry3D
          {
            Positions = (positions),
            Normals = (normals),
            TriangleIndices = (triangleIndices)
          };
          return ret;
        }
      }
      catch (Exception)
      {
        return null;
      }
    }
  }
  public class MeshRefactor
  {
    public MeshRefactor(List<Int32> triangleIndices, List<Point3D> positions, List<Vector3D> normals, double unitSize)
    {
      MeshGeometry = new DPMeshGeometry3D()
      {
        TriangleIndices = triangleIndices,
        Positions = positions,
        Normals = normals
      };

      UnitSize = unitSize;
    }
    public MeshRefactor(List<Int32> triangleIndices, List<Point3D> positions, List<Vector3D> normals, UInt32 resolution)
    {
      MeshGeometry = new DPMeshGeometry3D()
      {
        TriangleIndices = triangleIndices,
        Positions = positions,
        Normals = normals
      };

      var bounds = MeshGeometry.GetBounds();

      var max = Math.Max(bounds.SizeX, bounds.SizeY);
      UnitSize = Math.Max(max, bounds.SizeZ) / resolution;
    }

    public DPMeshGeometry3D MeshGeometry { get; private set; }
    public double UnitSize { get; private set; }

    public static DPMeshGeometry3D GetSimplifiedMeshGeometry3D(DPMeshGeometry3D dPMeshGeometry3D, double unitSize)
    {
      var meshRefactor = new MeshRefactor(dPMeshGeometry3D.TriangleIndices, dPMeshGeometry3D.Positions,
        dPMeshGeometry3D.Normals, unitSize);
      return meshRefactor.CreateGeometry3D();
    }
    public static MeshGeometry3D GetSimplifiedMeshGeometry3D(MeshGeometry3D meshGeometry3D, double unitSize)
    {
      var meshRefactor = new MeshRefactor(meshGeometry3D.TriangleIndices.ToList(), meshGeometry3D.Positions.ToList(),
        meshGeometry3D.Normals.ToList(), unitSize);
      var meshGeometry = meshRefactor.CreateGeometry3D();
      if (meshGeometry == null)
        return null;
      return meshGeometry.CreateMeshGeometry3D();
    }
    private DPMeshGeometry3D CreateGeometry3D()
    {
      try
      {
        if (MeshGeometry == null)
          return null;

        var positionInfos = MeshGeometry.Positions.Select((p, index) => new { Position = p, Index = index, Offset = GetOffset(p) }).ToList();
        var normals = MeshGeometry.Normals.ToArray();
        var triangleIndices = MeshGeometry.TriangleIndices.ToArray();

        // merge the positions in the same cell.
        var groupedPositionInfos = positionInfos.GroupBy(p => p.Offset).ToList();

        positionInfos.Clear();

        foreach (var item in groupedPositionInfos)
        {
          var pointsInTheCell = item.Select(p => new { PositionInfo = p, RefCount = triangleIndices.Count(t => t == p.Index) }).OrderBy(p => p.RefCount).ToList();

          var firstPoint = pointsInTheCell.First().PositionInfo;
          var pointIndexInTheCell = firstPoint.Index;

          //var centerPoint = new Point3D(pointsInTheCell.Select(p => p.Position.X).Average(), pointsInTheCell.Select(p => p.Position.Y).Average(), pointsInTheCell.Select(p => p.Position.Z).Average());
          positionInfos.Add(new { Position = firstPoint.Position, Index = pointIndexInTheCell, Offset = firstPoint.Offset });

          if (pointsInTheCell.Count() <= 1)
            continue;

          for (var i = 1; i < pointsInTheCell.Count; i++)
          {
            var pointInTheCell = pointsInTheCell[i];

            //positionInfos.Remove(pointInTheCell);

            for (var n = 0; n < triangleIndices.Length; n++)
            {
              if (triangleIndices[n] == pointInTheCell.PositionInfo.Index)
              {
                triangleIndices[n] = pointIndexInTheCell;
              }
            }
          }
        }

        var newPositions = new List<Point3D>();
        var newNormals = new List<Vector3D>();
        var newTriangleIndices = new List<Int32>();

        positionInfos.ForEach(p =>
        {
          newPositions.Add(p.Position);
          newNormals.Add(normals[p.Index]);
        });

        var newPositionInfos = positionInfos.Select((p, newIndex) => new { PositionInfo = p, NewIndex = newIndex, OldIndex = p.Index }).ToList();

        var triangleCount = triangleIndices.Length / 3;
        for (var i = 0; i < triangleCount; i++)
        {
          var baseIndex = 3 * i;
          if (triangleIndices[baseIndex] == triangleIndices[baseIndex + 1] || triangleIndices[baseIndex] == triangleIndices[baseIndex + 2] ||
              triangleIndices[baseIndex + 1] == triangleIndices[baseIndex + 2])
          {
            continue;
          }

          newTriangleIndices.Add(newPositionInfos.Where(p => p.OldIndex == triangleIndices[baseIndex]).Select(p => p.NewIndex).FirstOrDefault());
          newTriangleIndices.Add(newPositionInfos.Where(p => p.OldIndex == triangleIndices[baseIndex + 1]).Select(p => p.NewIndex).FirstOrDefault());
          newTriangleIndices.Add(newPositionInfos.Where(p => p.OldIndex == triangleIndices[baseIndex + 2]).Select(p => p.NewIndex).FirstOrDefault());
        }

        return new DPMeshGeometry3D(newPositions, newNormals, newTriangleIndices);
      }
      catch (Exception)
      {
        return null;
      }
    }
    private Int32Size3D GetOffset(Point3D position)
    {
      var ret = new Int32Size3D((int)(position.X / UnitSize), (int)(position.Y / UnitSize), (int)(position.Z / UnitSize));
      return ret;
    }
  }
  public struct Int32Size3D
  {
    private int _x;
    private int _y;
    private int _z;

    public Int32 X
    {
      get { return _x; }
      set { _x = value; }
    }

    public Int32 Y
    {
      get { return _y; }
      set { _y = value; }
    }

    public Int32 Z
    {
      get { return _z; }
      set { _z = value; }
    }

    public Int32Size3D(Int32 x, Int32 y, Int32 z)
    {
      this._x = x;
      this._y = y;
      this._z = z;
    }

    public static bool operator ==(Int32Size3D size1, Int32Size3D size2)
    {
      if (size1.X == size2.X && size1.Y == size2.Y)
        return size1.Z == size2.Z;
      return false;
    }

    public static bool operator !=(Int32Size3D size1, Int32Size3D size2)
    {
      return !(size1 == size2);
    }

    public static bool Equals(Int32Size3D size1, Int32Size3D size2)
    {
      if (size1.X == size2.X && size1.Y == size2.Y)
        return size1.Z == size2.Z;
      return false;
    }

    public override bool Equals(object o)
    {
      if (o == null || !(o is Int32Size3D))
        return false;
      return Int32Size3D.Equals(this, (Int32Size3D)o);
    }

    public bool Equals(Int32Size3D value)
    {
      return Int32Size3D.Equals(this, value);
    }

    public override int GetHashCode()
    {
      return this.X.GetHashCode() ^ this.Y.GetHashCode() ^ this.Z.GetHashCode();
    }

    public override string ToString()
    {
      return string.Format("{0},{1},{2}", this.X, this.Y, this.Z);
    }
  }
  public class StepWork : Work
  {
    public StepWork(string workName, object reference)
      : base(workName)
    {
      //Debug.Assert(StepWork.IsInThisWork(reference) == false);

      this.Reference = reference;
    }
    public static bool IsInThisWork(object reference)
    {
      return StepWork.GetWork(reference) != null;
    }
    public static StepWork GetWork(object reference)
    {
      List<StepWork> initializeWorks;
      //lock (WorkManager.Current)
      {
        initializeWorks = WorkManager.Current.Works.OfType<StepWork>().ToList();
      }
      return (initializeWorks.FirstOrDefault(p => object.ReferenceEquals(p.Reference, reference)));
    }

    public object Reference { get; private set; }

    private long _maxSteps = 100;
    public long MaxSteps
    {
      get { return _maxSteps; }
      set
      {
        if (_maxSteps != value)
        {
          _maxSteps = value;
          this.SendPropertyChanged("MaxSteps");
          this.SendPropertyChanged("Description");
        }
      }
    }
    private long _currentStep = 0;
    public long CurrentStep
    {
      get { return _currentStep; }
      set
      {
        if (_currentStep != value)
        {
          _currentStep = value;
          this.SendPropertyChanged("CurrentStep");
          this.SendPropertyChanged("Description");
        }
      }
    }

    public override string Description
    {
      get { return string.Format("{0}...{1}/{2}({3}%)", this.Name, this.CurrentStep, this.MaxSteps, (this.CurrentStep * 100.0 / this.MaxSteps).ToString()); }
    }
  }
  public abstract class Work : IDisposable, INotifyPropertyChanged
  {
    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler PropertyChanged;

    public void SendPropertyChanged(String propertyName)
    {
      PropertyChangedEventHandler propertychanged = null;
      lock (this)
      {
        propertychanged = PropertyChanged;
      }
      if (propertychanged != null)
      {
        propertychanged(this, new PropertyChangedEventArgs(propertyName));
      }
    }

    public void SendPropertyChanged<TProperty>(Expression<Func<TProperty>> projection)
    {
      var memberExpression = (MemberExpression)projection.Body;
      this.SendPropertyChanged(memberExpression.Member.Name);
    }

    public string GetPropertyName<TProperty>(Expression<Func<TProperty>> projection)
    {
      var memberExpression = (MemberExpression)projection.Body;
      return (memberExpression.Member.Name);
    }

    #endregion

    protected Work(string name)
    {
      this.Name = name;
      this.BeginTime = DateTime.Now;

      WorkManager.Current.AddWork(this);

    }

    public void Dispose()
    {
      WorkManager.Current.RemoveWork(this);
    }

    private string _name = string.Empty;
    public string Name
    {
      get { return _name; }
      private set
      {
        if (_name != value)
        {
          _name = value;
          this.SendPropertyChanged("Name");
        }
      }
    }

    public DateTime BeginTime { get; private set; }

    public abstract string Description { get; }
  }
  public class WorkManager : INotifyPropertyChanged
  {
    #region INotifyPropertyChanged Members

    public event PropertyChangedEventHandler PropertyChanged;

    public void SendPropertyChanged(String propertyName)
    {
      if ((this.PropertyChanged != null))
      {
        this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
      }
    }

    #endregion

    private static WorkManager _current = null;
    public static WorkManager Current
    {
      get
      {
        if (_current == null)
        {
          BindingFlags bf = BindingFlags.Instance | BindingFlags.Public | BindingFlags.CreateInstance | BindingFlags.NonPublic;
          _current = (WorkManager)Activator.CreateInstance(typeof(WorkManager), bf, null, null, null);
        }
        return _current;
      }
    }

    private Work _currentWork = null;
    public Work CurrentWork
    {
      get { return _currentWork; }
      private set
      {
        if (_currentWork != value)
        {
          _currentWork = value;
          this.SendPropertyChanged("CurrentWork");
          this.SendPropertyChanged("CurrentWorkIsNull");
        }
      }
    }
    public bool CurrentWorkIsNull
    {
      get { return this.CurrentWork == null; }
    }

    readonly ObservableCollection<Work> _works = new ObservableCollection<Work>();
    public ObservableCollection<Work> Works { get { return _works; } }

    public void AddWork(Work work)
    {
      this.Works.Add(work);
      lock (this)
      {
        this.CurrentWork = work;
      }
    }

    public void RemoveWork(Work work)
    {
      this.Works.Remove(work);
      var nextWork = this.Works.LastOrDefault();
      lock (this)
      {
        this.CurrentWork = nextWork;
      }
    }
  }
}

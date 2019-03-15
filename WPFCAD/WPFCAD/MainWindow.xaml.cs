using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Xml;
using Microsoft.Win32;
using WPFCAD.Helper;

namespace WPFCAD
{
  public partial class MainWindow : Window
  {
    public MainWindow()
    {
      InitializeComponent();
    }
  }
  public class MainWindowViewModel : NotifyPropertyChanged
  {
    public MainWindowViewModel()
    {
      this.OpenCadCommand = new RelayCommand(OpenCad);
    }
    private void OpenCad(object parameter)
    {
      var viewport = parameter as Viewport3D;
      if (viewport == null)
        return;

      //var openFileDlg = new OpenFileDialog();
      //if (openFileDlg.ShowDialog() ?? false)
      {
        //this.HandleCad(openFileDlg.FileName, viewport);
        this.HandleCad(@"D:\Database\HexBlock2.iges", viewport);
      }
    }
    ModelVisual3D _modelVisual3D = new ModelVisual3D();
    String xamlFileName;
    private void HandleCad(String cadFilePath, Viewport3D viewPort)
    {
      xamlFileName = Environment.GetEnvironmentVariable("TEMP") + System.IO.Path.GetFileNameWithoutExtension(cadFilePath) + ".xamlsolid";
      var resultCode = DllImport.IgesToSolidXaml(cadFilePath, xamlFileName);
      if (IsConvertCadSuccessful(resultCode) && VerifyFileFormat(xamlFileName))
      {
        this.CadFilePath = cadFilePath;
        if (viewPort.Children.Contains(_modelVisual3D))
        {
          viewPort.Children.Remove(_modelVisual3D);
          _modelVisual3D.Content = null;
        }
        _model3D = null;
        _modelVisual3D.Content = Model3D;
        viewPort.Children.Add(_modelVisual3D);

        /////////////////////////////////////////////////////////////////////////////////////
        var _axisAngleRotation3D = new AxisAngleRotation3D(new Vector3D(-1, 0, 0), -15.0);
        var RotateTransform3D = new RotateTransform3D(_axisAngleRotation3D);
        var _scaleTransform3D = new ScaleTransform3D(100, 100, 100);
        var _translateTransform3D = new TranslateTransform3D(100, 0, 0);
        Transform3DGroup _transform = new Transform3DGroup();
        _transform.Children.Add(RotateTransform3D);
        _transform.Children.Add(_scaleTransform3D);
        _transform.Children.Add(_translateTransform3D);
        viewPort.Camera.Transform = _transform;
      }
      else
      {
        if (System.IO.File.Exists(xamlFileName))
          System.IO.File.Delete(xamlFileName);
      }
    }
    public static bool VerifyFileFormat(string convertedCadFilePath)
    {
      bool isValid = true;
      var xmlTextReader = new System.Xml.XmlTextReader(convertedCadFilePath);
      xmlTextReader.MoveToContent();
      if (!String.Equals(xmlTextReader.LocalName, "Model3DGroup", StringComparison.CurrentCultureIgnoreCase))
      {
        isValid = false;
        throw new Exception("Failed...");
      }
      xmlTextReader.Close();

      return isValid;
    }
    public static bool IsConvertCadSuccessful(int resultCode)
    {
      if (resultCode == 1 || resultCode == 2)
        return false;
      else if (resultCode == 3)
      {
        //String strCmdLineErr = DPPStrings.Properties.Resources.strCmdLineErr;
        //ExceptionHandler.ThrowException(strCmdLineErr);
        return false;
      }
      else if (resultCode == 4)
      {
        //String strErr = DPPStrings.Properties.Resources.strCadFileNotSupportErr;
        //ExceptionHandler.ThrowException(strErr);
        return false;
      }
      return true;
    }

    public RelayCommand OpenCadCommand { get; private set; }
    private Model3D _model3D;
    public Model3D Model3D
    {
      get
      {
        if (_model3D == null)
        {
          var gmGroup = GetDPGeometryModel3DGroup();
          if (gmGroup != null)
            _model3D = gmGroup.GetModel3D();
        }
        return _model3D;
      }
    }
    public DPGeometryModel3DGroup GetDPGeometryModel3DGroup(UInt32 resolution = 200)
    {
      try
      {
        var fileNameWithResolution = string.Format("{0}\\{1}_R{2}{3}", Path.GetDirectoryName(xamlFileName),
          Path.GetFileNameWithoutExtension(xamlFileName),
          resolution, Path.GetExtension(xamlFileName));

        if (File.Exists(fileNameWithResolution))
          return LoadModel3DGroup(fileNameWithResolution);


        var dpGeometryModel3DGroup = LoadModel3DGroup(xamlFileName);
        if (dpGeometryModel3DGroup == null)
          return null;

        if (dpGeometryModel3DGroup.GeometryModel3DList.Count <= 5)
          return dpGeometryModel3DGroup;

        dpGeometryModel3DGroup = dpGeometryModel3DGroup.GetSimplifiedGeometryModel3DGroup(resolution);
        if (dpGeometryModel3DGroup == null)
          return null;

        dpGeometryModel3DGroup.SaveToFile(fileNameWithResolution);
        return dpGeometryModel3DGroup;
      }
      catch (Exception ex)
      {
        return null;
      }
    }

    #region Load Model3DGroup from XamlFile
    public DPGeometryModel3DGroup LoadModel3DGroup(string xamlFile)
    {
      try
      {
        using (var fileStream = new FileStream(xamlFile, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
          using (var currentWork = new StepWork("Load 3D Mode", fileStream) { MaxSteps = fileStream.Length })
          {
            using (var reader = new XmlTextReader(fileStream))
            {
              if (!MoveToNextNode(reader, XmlNodeType.Element, "Model3DGroup"))
                return null;

              var ret = new DPGeometryModel3DGroup();

              if (!reader.IsEmptyElement)
              {
                while (true)
                {
                  var geometryModel3D = LoadGeometryModel3D(reader);
                  if (geometryModel3D == null)
                    break;

                  ret.GeometryModel3DList.Add(geometryModel3D);

                  currentWork.CurrentStep = fileStream.Position;
                }
              }

              reader.Close();

              return ret;
            }
          }
        }
      }
      catch (Exception ex)
      {
        return null;
      }
    }

    private DPGeometryModel3D LoadGeometryModel3D(XmlTextReader reader)
    {
      try
      {
        if (!MoveToNextNode(reader, XmlNodeType.Element, "GeometryModel3D"))
          return null;

        var ret = new DPGeometryModel3D();

        if (!reader.IsEmptyElement)
        {
          while (MoveToNextElement(reader))
          {
            if (reader.NodeType == XmlNodeType.EndElement)
              break;

            if (!reader.IsEmptyElement)
            {
              if (reader.Name == "GeometryModel3D.Geometry")
              {
                ret.MeshGeometry = LoadMeshGeometry3D(reader);
              }
              else if (reader.Name == "GeometryModel3D.Material")
              {
                ret.MaterialColor = LoadMaterialColor(reader);
              }
              else if (reader.Name == "GeometryModel3D.BackMaterial")
              {
                ret.BackMaterialColor = LoadMaterialColor(reader);
              }

              MoveToNextNode(reader, XmlNodeType.EndElement);
            }
          }
        }
        return ret;
      }
      catch (Exception ex)
      {
        return null;
      }
    }
    private DPMeshGeometry3D LoadMeshGeometry3D(XmlTextReader reader)
    {
      try
      {
        if (!MoveToNextNode(reader, XmlNodeType.Element, "MeshGeometry3D"))
          return null;

        List<Int32> triangleIndices = null;
        List<Point3D> positions = null;
        List<Vector3D> normals = null;

        if (reader.MoveToAttribute("TriangleIndices"))
          triangleIndices = LoadInt32Collection(reader.Value);
        if (reader.MoveToAttribute("Positions"))
          positions = LoadPoint3DCollection(reader.Value);
        if (reader.MoveToAttribute("Normals"))
          normals = LoadVector3DCollection(reader.Value);


        reader.MoveToElement();

        if (!reader.IsEmptyElement)
          reader.Read();

        var ret = new DPMeshGeometry3D();
        ret.Positions = positions;
        ret.Normals = normals;
        ret.TriangleIndices = triangleIndices;
        return ret;
      }
      catch (Exception ex)
      {
        return null;
      }
    }

    private static Color LoadMaterialColor(XmlTextReader reader)
    {
      try
      {
        if (!MoveToNextNode(reader, XmlNodeType.Element, "DiffuseMaterial"))
          return Colors.Transparent;

        var ret = Colors.Transparent;

        if (!reader.IsEmptyElement)
        {
          if (MoveToNextNode(reader, XmlNodeType.Element))
          {
            if (reader.Name == "DiffuseMaterial.Brush")
            {
              if (!reader.IsEmptyElement)
              {
                ret = LoadBrushColor(reader);
                MoveToNextNode(reader, XmlNodeType.EndElement);
              }
            }
          }

          MoveToNextNode(reader, XmlNodeType.EndElement);
        }

        return ret;
      }
      catch (Exception ex)
      {
        return Colors.Transparent;
      }
    }
    private static Color LoadBrushColor(XmlTextReader reader)
    {
      try
      {
        if (!MoveToNextNode(reader, XmlNodeType.Element))
          return Colors.Transparent;
        if (reader.Name == "SolidColorBrush")
        {
          var ret = Colors.Transparent;
          var colorObject = ColorConverter.ConvertFromString(reader.GetAttribute("Color"));
          if (colorObject != null)
            ret = (Color)colorObject;

          if (!reader.IsEmptyElement)
            MoveToNextNode(reader, XmlNodeType.EndElement);

          return ret;
        }
        return Colors.Transparent;
      }
      catch (Exception ex)
      {
        return Colors.Transparent;
      }
    }

    private static bool MoveToNextNode(XmlTextReader reader, XmlNodeType nodeType)
    {
      while (reader.Read())
      {
        if (reader.NodeType == nodeType)
          return true;
      }
      return false;
    }
    private static bool MoveToNextNode(XmlTextReader reader, XmlNodeType nodeType, string nodeName)
    {
      while (reader.Read())
      {
        if (reader.NodeType == nodeType && reader.Name == nodeName)
          return true;
      }
      return false;
    }
    private static bool MoveToNextElement(XmlTextReader reader)
    {
      while (reader.Read())
      {
        if (reader.NodeType == XmlNodeType.Element || reader.NodeType == XmlNodeType.EndElement)
          return true;
      }
      return false;

    }
    private List<Point3D> LoadPoint3DCollection(string text)
    {
      try
      {
        var ret = new List<Point3D>();
        var prevPos = 0;

        var point3D = new double[3];
        var pointValueIndex = 0;

        for (var i = 0; i < text.Length; i++)
        {
          if (text[i] == ' ')
          {
            if (i > prevPos)
            {
              var temp = ToDouble(text.Substring(prevPos, i - prevPos));
              if (temp.HasValue)
              {
                point3D[pointValueIndex++] = temp.Value;
                if (pointValueIndex >= 3)
                {
                  pointValueIndex = 0;
                  ret.Add(new Point3D(point3D[0], point3D[1], point3D[2]));
                }
              }
            }
            prevPos = i + 1;
          }
        }
        if (text.Length > prevPos)
        {
          var temp = ToDouble(text.Substring(prevPos, text.Length - prevPos));
          if (temp.HasValue)
          {
            point3D[pointValueIndex++] = temp.Value;
            if (pointValueIndex >= 3)
            {
              pointValueIndex = 0;
              ret.Add(new Point3D(point3D[0], point3D[1], point3D[2]));
            }
          }
        }
        return ret;
      }
      catch (Exception ex)
      {
        return null;
      }
    }
    private List<Vector3D> LoadVector3DCollection(string text)
    {
      try
      {
        var ret = new List<Vector3D>();
        var prevPos = 0;

        var point3D = new double[3];
        var pointValueIndex = 0;

        for (var i = 0; i < text.Length; i++)
        {
          if (text[i] == ' ')
          {
            if (i > prevPos)
            {
              var temp = ToDouble(text.Substring(prevPos, i - prevPos));
              if (temp.HasValue)
              {
                point3D[pointValueIndex++] = temp.Value;
                if (pointValueIndex >= 3)
                {
                  pointValueIndex = 0;
                  ret.Add(new Vector3D(point3D[0], point3D[1], point3D[2]));
                }
              }
            }
            prevPos = i + 1;
          }
        }
        if (text.Length > prevPos)
        {
          var temp = ToDouble(text.Substring(prevPos, text.Length - prevPos));
          if (temp.HasValue)
          {
            point3D[pointValueIndex++] = temp.Value;
            if (pointValueIndex >= 3)
            {
              pointValueIndex = 0;
              ret.Add(new Vector3D(point3D[0], point3D[1], point3D[2]));
            }
          }
        }
        return ret;
      }
      catch (Exception)
      {
        return null;
      }
    }
    private List<Int32> LoadInt32Collection(string text)
    {
      try
      {
        var ret = new List<Int32>();
        var prevPos = 0;

        for (var i = 0; i < text.Length; i++)
        {
          if (text[i] == ' ')
          {
            if (i > prevPos)
            {
              ret.Add(int.Parse(text.Substring(prevPos, i - prevPos)));
            }
            prevPos = i + 1;
          }
        }
        if (text.Length > prevPos)
        {
          ret.Add(int.Parse(text.Substring(prevPos, text.Length - prevPos)));
        }
        return ret;
      }
      catch (Exception ex)
      {
        return null;
      }
    }
    public static Nullable<Double> ToDouble(string str)
    {
      Double ret = 0.0;
      if (Double.TryParse(str, NumberStyles.Any, CultureInfo.CreateSpecificCulture("en"), out ret))
        return ret;
      return null;
    }
    #endregion

    private String _cadFilePath;
    public String CadFilePath
    {
      get { return _cadFilePath; }
      set
      {
        if (_cadFilePath != value)
        {
          _cadFilePath = value;
          this.RaisePropertyChanged(nameof(CadFilePath));
        }
      }
    }
  }
}

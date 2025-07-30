using Autodesk.Revit.DB;
using SharedRevit.Geometry.Implicit_Surfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace SharedRevit.Geometry
{
    public class MeshGeometryData
    {
        public SimpleMesh mesh { get; set; }
        public IShape BoundingSurface { get; set; }
        public ElementId SourceElementId { get; set; }
        public GeometryRole Role { get; set; } // A or B
    }
    public enum GeometryRole { A, B }
}

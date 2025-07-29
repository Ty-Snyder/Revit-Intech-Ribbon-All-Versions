using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;

namespace SharedRevit.Geometry
{
    internal static class RevitMeshToSimpleMesh
    {
        public static SimpleMesh Convert(Mesh mesh)
        {
            SimpleMesh simpleMesh = new SimpleMesh();
            foreach (XYZ vertex in mesh.Vertices)
            {
                simpleMesh.Vertices.Add(new Vector3((float)vertex.X, (float)vertex.Y, (float)vertex.Z));
            }
            return simpleMesh;
        }
    }

    internal struct SimpleMesh
    {
        public List<Vector3> Vertices;

        public SimpleMesh()
        {
            Vertices = new List<Vector3>();
        }
    }
}

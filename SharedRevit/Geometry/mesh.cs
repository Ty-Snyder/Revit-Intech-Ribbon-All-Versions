using Autodesk.Revit.DB;
using Autodesk.Revit.DB.DirectContext3D;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SharedRevit.Geometry
{
    internal static class RevitToSimpleMesh
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

        public static SimpleMesh Convert(Solid solid)
        {
            SimpleMesh simpleMesh = new SimpleMesh();
            foreach ( Face face in solid.Faces)
            {
                Mesh mesh = face.Triangulate(0.5);
                foreach (XYZ vertex in mesh.Vertices)
                {
                    simpleMesh.Vertices.Add(new Vector3((float)vertex.X, (float)vertex.Y, (float)vertex.Z));
                }
            }
            return simpleMesh;
        }

        public static SimpleMesh Convert(Element elem)
        {
            SimpleMesh simpleMesh = new SimpleMesh();
            Transform transform = Transform.Identity;
            Options options = new Options
            {
                ComputeReferences = true,
                IncludeNonVisibleObjects = true,
                DetailLevel = ViewDetailLevel.Fine
            };
            if (elem is FabricationPart fabPart)
            {
                transform = transform.Multiply(fabPart.GetTransform().Inverse);
            }
            GeometryElement geomElement = elem.get_Geometry(options);
            foreach (GeometryObject obj in geomElement)
            {
                ProcessGeometryObject(obj, transform, simpleMesh);
            }
            return simpleMesh;
        }


        public static SimpleMesh Convert(Element elem, Transform transform)
        {
            SimpleMesh simpleMesh = new SimpleMesh();

            Options options = new Options
            {
                ComputeReferences = true,
                IncludeNonVisibleObjects = true,
                DetailLevel = ViewDetailLevel.Fine
            };

            GeometryElement geomElement = elem.get_Geometry(options);
            foreach (GeometryObject obj in geomElement)
            {
                ProcessGeometryObject(obj, transform, simpleMesh);
            }

            return simpleMesh;
        }

        private static void ProcessGeometryObject(GeometryObject obj, Transform transform, SimpleMesh mesh)
        {
            switch (obj)
            {
                case Solid solid:
                    Solid transformedSolid = SolidUtils.CreateTransformed(solid, transform);
                    foreach (Edge edge in transformedSolid.Edges)
                    {
                        IList<XYZ> points = edge.Tessellate();
                        for (int i = 0; i < points.Count - 1; i++)
                        {
                            mesh.AddLine(points[i], points[i + 1]);
                        }
                    }
                    break;

                case Autodesk.Revit.DB.Mesh rawMesh:
                    Autodesk.Revit.DB.Mesh transformedMesh = rawMesh.get_Transformed(transform);
                    for (int i = 0; i < transformedMesh.NumTriangles; i++)
                    {
                        MeshTriangle tri = transformedMesh.get_Triangle(i);
                        mesh.AddLine(tri.get_Vertex(0), tri.get_Vertex(1));
                        mesh.AddLine(tri.get_Vertex(1), tri.get_Vertex(2));
                        mesh.AddLine(tri.get_Vertex(2), tri.get_Vertex(0));
                    }
                    break;

                case GeometryInstance instance:
                    Transform instanceTransform = instance.Transform.Multiply(transform);
                    foreach (GeometryObject instObj in instance.GetInstanceGeometry())
                    {
                        ProcessGeometryObject(instObj, instanceTransform, mesh);
                    }
                    break;
            }
        }


    }

    public struct SimpleLine
    {
        public int StartIndex;
        public int EndIndex;

        public SimpleLine(int start, int end)
        {
            StartIndex = start;
            EndIndex = end;
        }
    }


    public struct SimpleMesh
    {
        public List<Vector3> Vertices;
        public List<SimpleLine> Lines;

        public SimpleMesh()
        {
            Vertices = new List<Vector3>();
            Lines = new List<SimpleLine>();
        }

        public void AddLine(XYZ p1, XYZ p2)
        {
            int index1 = Vertices.Count;
            Vertices.Add(new Vector3((float)p1.X, (float)p1.Y, (float)p1.Z));

            int index2 = Vertices.Count;
            Vertices.Add(new Vector3((float)p2.X, (float)p2.Y, (float)p2.Z));

            Lines.Add(new SimpleLine(index1, index2));
        }
    }


}

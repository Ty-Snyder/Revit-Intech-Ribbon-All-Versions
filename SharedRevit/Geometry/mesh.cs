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

            Options options = new Options
            {
                ComputeReferences = true,
                IncludeNonVisibleObjects = true,
                DetailLevel = ViewDetailLevel.Fine
            };

            GeometryElement geomElement = elem.get_Geometry(options);
            foreach (GeometryObject obj in geomElement)
            {
                ProcessGeometryObject(obj, Transform.Identity, simpleMesh);
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
                    foreach (Face face in transformedSolid.Faces)
                    {
                        Mesh faceMesh = face.Triangulate(0.5);
                        foreach (XYZ vertex in faceMesh.Vertices)
                        {
                            mesh.Vertices.Add(new Vector3((float)vertex.X, (float)vertex.Y, (float)vertex.Z));
                        }
                    }
                    break;

                case Autodesk.Revit.DB.Mesh rawMesh:
                    Autodesk.Revit.DB.Mesh transformedMesh = rawMesh.get_Transformed(transform);
                    foreach (XYZ vertex in transformedMesh.Vertices)
                    {
                        mesh.Vertices.Add(new Vector3((float)vertex.X, (float)vertex.Y, (float)vertex.Z));
                    }
                    break;

                case GeometryInstance instance:
                    Transform instanceTransform = transform.Multiply(instance.Transform);
                    foreach (GeometryObject instObj in instance.GetInstanceGeometry())
                    {
                        ProcessGeometryObject(instObj, instanceTransform, mesh);
                    }
                    break;
            }
        }
    }

    public struct SimpleMesh
    {
        public HashSet<Vector3> Vertices;

        public SimpleMesh()
        {
            Vertices = new HashSet<Vector3>();
        }
    }
}

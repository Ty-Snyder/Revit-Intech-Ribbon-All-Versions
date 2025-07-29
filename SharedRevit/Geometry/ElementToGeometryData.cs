using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using System.Collections.Generic;
using System.Linq;

namespace SharedRevit.Geometry
{
    internal class ElementToGeometryData
    {

        public static List<MeshGeometryData> ConvertMEPToGeometryData(List<Reference> references, Document doc)
        {
            var geometryDataList = new List<MeshGeometryData>();

            foreach (Reference reference in references)
            {
                Element element = doc.GetElement(reference);
                if (element == null)
                    continue;

                bool isValidMEP =
                    element is Pipe ||
                    element is Duct ||
                    element is MEPCurve ||
                    element is FabricationPart;

                if (!isValidMEP)
                    continue;

                Options options = new Options
                {
                    ComputeReferences = true,
                    IncludeNonVisibleObjects = true,
                    DetailLevel = ViewDetailLevel.Fine
                };

                GeometryElement geomElement = element.get_Geometry(options);
                if (geomElement == null)
                    continue;

                List<Autodesk.Revit.DB.Mesh> meshes = new List<Autodesk.Revit.DB.Mesh>();

                foreach (GeometryObject obj in geomElement)
                {
                    ExtractMeshes(obj, meshes);
                }

                foreach (var mesh in meshes)
                {
                    if (mesh == null || mesh.NumTriangles == 0)
                        continue;

                    var meshData = ConvertRevitMeshToMRMesh(mesh);
                    if (meshData != null)
                    {
                        geometryDataList.Add(new MeshGeometryData
                        {
                            mesh = meshData,
                            BoundingBox = element.get_BoundingBox(null),
                            SourceElementId = element.Id,
                            Role = GeometryRole.A
                        });
                    }
                }
            }

            return geometryDataList;
        }

        private static void ExtractMeshes(GeometryObject obj, List<Autodesk.Revit.DB.Mesh> meshes)
        {
            if (obj is Autodesk.Revit.DB.Mesh mesh)
            {
                meshes.Add(mesh);
            }
            else if (obj is Solid solid)
            {
                foreach (Face face in solid.Faces)
                {
                    Autodesk.Revit.DB.Mesh faceMesh = face.Triangulate();
                    if (faceMesh != null && faceMesh.NumTriangles > 0)
                        meshes.Add(faceMesh);
                }
            }
            else if (obj is GeometryInstance instance)
            {
                foreach (GeometryObject instObj in instance.GetInstanceGeometry())
                {
                    ExtractMeshes(instObj, meshes);
                }
            }
        }

        private class XYZComparer : IEqualityComparer<XYZ>
        {
            public bool Equals(XYZ a, XYZ b) => a.IsAlmostEqualTo(b);
            public int GetHashCode(XYZ obj) => obj.GetHashCode();
        }
    }
}

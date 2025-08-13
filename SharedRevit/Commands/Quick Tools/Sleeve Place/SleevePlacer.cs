using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.DirectContext3D;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using SharedCore;
using SharedCore.SaveFile;
using SharedRevit.Geometry;
using SharedRevit.Geometry.Collision;
using SharedRevit.Geometry.Shapes;
using SharedRevit.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SharedRevit.Commands
{

    [Transaction(TransactionMode.Manual)]
    public class SleevePlace : IExternalCommand
    {
        
        RevitUtilsDefault RevitUtils = RevitUtilService.Get();
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Autodesk.Revit.UI.UIApplication app = commandData.Application;
            UIDocument uidoc = app.ActiveUIDocument;
            Document doc = uidoc.Document;
            RevitUtils.init(doc);

            List<Reference> references = null;
            try
            {

                // Step 1: Get currently selected elements
                ICollection<ElementId> selectedIds = uidoc.Selection.GetElementIds();

                // Step 2: Convert selected ElementIds to References
                List<Reference> preselectedRefs = selectedIds
                    .Select(id => new Reference(doc.GetElement(id)))
                    .ToList();

                // Step 3: Use PickObjects with preselection
                references = uidoc.Selection.PickObjects(
                    ObjectType.Element,
                    new DuctPipeSelectionFilter(),
                    "Select ducts or pipes.",
                    preselectedRefs
                ).ToList();

            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }

            // Filter out vertical pipes after selection

            references = references
             .Where(r =>
             {
                 Element elem = doc.GetElement(r);
                 if (elem is MEPCurve mepCurve)
                 {
                     ConnectorSet connectors = mepCurve.ConnectorManager.Connectors;
                     foreach (Connector connector in connectors)
                     {
                         XYZ dir = connector.CoordinateSystem.BasisZ.Normalize();
                         double vert = Math.Abs(Math.Abs(dir.Z) - 1);
                         if (vert >= 1e-4)
                         {
                             return true; // Not vertical
                         }
                     }
                 }
                 if (elem is FabricationPart fab)
                 {
                     return true;
                 }

                 return false;
             })
             .ToList();

            List<MeshGeometryData> mepGeometry = ElementToGeometryData.ConvertMEPToGeometryData(references, doc);
            if (mepGeometry.Count == 0)
            {
                return Result.Failed;
            }

            List<RevitLinkInstance> linked = RevitUtils.GetLinkedModels();
            string basePath = Path.Combine(App.BasePath, "Settings.txt");
            SaveFileManager saveFileManager = new SaveFileManager(basePath);
            SaveFileSection section = saveFileManager.GetSectionsByName("Sleeve Place", "linked Model");
            if (section.Rows.Count() == 0 && section.Rows[0].Count() == 0)
            {
                return Result.Failed;
            }

            RevitLinkInstance structuralModel = linked.FirstOrDefault(l => l.Name == section.Rows[0][0]);
            List<Wall> walls = GetWallsFromLinkedModel(structuralModel);
            List<MeshGeometryData> wallGeometry = ConvertTransformedWallsToGeometryData(structuralModel, walls);
            if (wallGeometry.Count == 0)
            {
                return Result.Failed;
            }

            var engine = new Engine.CollisionEngine();
            List<CollisionResult> res = new List<CollisionResult>();
            engine.Run(mepGeometry, wallGeometry, 15, 3, result =>
            {
                res.Add(result);
            });

            using (Transaction tx = new Transaction(doc, "Place Sleeves At Collisions"))
            {
                tx.Start();
                foreach (CollisionResult result in res)
                {
                    placeSleeveAtCollision(result, doc);
                }
                tx.Commit();
            }

            return Result.Succeeded;
        }

        public class DuctPipeSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem)
            {
                return elem is Duct || elem is FabricationPart || (elem is FamilyInstance fi && fi.MEPModel != null) ||
                       (elem is FabricationPart) || elem is Pipe;
            }

            public bool AllowReference(Reference r, XYZ p)
            {
                return true;
            }
        }

        public List<Wall> GetWallsFromLinkedModel(RevitLinkInstance structuralModel)
        {
            Document linkedDoc = structuralModel.GetLinkDocument();
            if (linkedDoc == null)
            {
                Autodesk.Revit.UI.TaskDialog.Show("Error", "Linked document is not loaded.");
                return new List<Wall>();
            }

            FilteredElementCollector wallCollector = new FilteredElementCollector(linkedDoc);
            List<Wall> walls = wallCollector
            .OfClass(typeof(Wall))
            .Cast<Wall>()
            .ToList();

            return walls;
        }


        public List<MeshGeometryData> ConvertTransformedWallsToGeometryData(RevitLinkInstance linkInstance, List<Wall> walls)
        {
            var geometryDataList = new List<MeshGeometryData>();
            Transform linkTransform = linkInstance.GetTransform();

            foreach (Wall wall in walls)
            {
#if NET48
                int value = wall.Id.IntegerValue;
#endif
               SimpleMesh meshData = RevitToSimpleMesh.Convert(wall, linkTransform);

                if (meshData.Vertices.Count != 0)
                {
                    geometryDataList.Add(new MeshGeometryData
                    {
                        mesh = meshData,
                        BoundingSurface = BoundingShape.SimpleMeshToIShape(meshData),
                        SourceElementId = wall.Id,
                        Role = GeometryRole.B
                    }); ;
                }
            }
            return geometryDataList;
        }
        private class XYZComparer : IEqualityComparer<XYZ>
        {
            public bool Equals(XYZ a, XYZ b) => a.IsAlmostEqualTo(b);
            public int GetHashCode(XYZ obj) => obj.GetHashCode();
        }


        string[] activeSettings = null;
        public void placeSleeveAtCollision(CollisionResult collision, Document doc)
        {
            SimpleMesh intersection = collision.Intersection;

            // Get linked model
            List<RevitLinkInstance> linked = RevitUtils.GetLinkedModels();
            string basePath = Path.Combine(App.BasePath, "Settings.txt");
            SaveFileManager saveFileManager = new SaveFileManager(basePath);
            SaveFileSection section = saveFileManager.GetSectionsByName("Sleeve Place", "linked Model");
            SaveFileSection overrideSection = saveFileManager.GetSectionsByName("Sleeve Place", "Forced Rect");
            RevitLinkInstance structuralModel = linked.FirstOrDefault(l => l.Name == section.Rows[0][0]);
            bool forceRec = overrideSection.Rows[0][0] == "True";

            if (structuralModel == null)
                return;

            Document linkedDoc = structuralModel.GetLinkDocument();
            if (linkedDoc == null)
                return;

            Element wallElement = linkedDoc.GetElement(collision.B.SourceElementId);
            if (!(wallElement is Wall wall))
                return;

            List<Geometry.Shapes.Face> faces = collision.Faces;
            if(faces.Count == 0)
            {
                return;
            }
            Autodesk.Revit.DB.XYZ revitNormal = new Autodesk.Revit.DB.XYZ(
                faces[0].Normal.X,
                faces[0].Normal.Y,
                faces[0].Normal.Z
            );

            GetBoundingMetrics(intersection, revitNormal, out double difHeight, out double difWidth, out double difLength, out XYZ center, out bool isRound);

            // Determine if round or rectangular
            Element pipeElement = doc.GetElement(collision.A.SourceElementId);
            isRound = isRound && !forceRec;
            double insulationThickness = GetInsulationThickness(doc, pipeElement);


            // Get family and symbol names from settings
            SaveFileSection sec = null;
            if (isRound)
            {
                sec = saveFileManager.GetSectionsByName("Sleeve Place", "Round Sleeve");
            }
            else
            {
                sec = saveFileManager.GetSectionsByName("Sleeve Place", "Rect Sleeve");
            }
            activeSettings = sec.lookUp(0, "True").FirstOrDefault() ?? sec.Rows[0];

            string familyName = activeSettings[2];

            string symbolName = faces.Count switch
            {
                0 => string.Empty,
                1 => activeSettings[3],
                _ => activeSettings[4]
            };
            if (string.IsNullOrEmpty(symbolName))
            {
                return;
            }

            // Find the correct FamilySymbol
            FamilySymbol sleeveSymbol = new FilteredElementCollector(doc)
             .OfClass(typeof(FamilySymbol))
             .OfType<FamilySymbol>()
             .FirstOrDefault(fs => fs.Family.Name == familyName && fs.Name == symbolName);

            if (sleeveSymbol == null)
            {
                Autodesk.Revit.UI.TaskDialog.Show("Error", $"Sleeve family '{familyName}' with type '{symbolName}' not found.");
                return;
            }

            if (!sleeveSymbol.IsActive)
            {
                sleeveSymbol.Activate();
            }

            // Find nearest level in host document
            Level level = GetElementLevel(pipeElement, doc);
            if (level == null)
                return;

            if (SleeveExistsAt(center, doc, sleeveSymbol.Family.Name, 0.2))
                return;

            FamilyInstance sleeve = doc.Create.NewFamilyInstance(center, sleeveSymbol, level, StructuralType.NonStructural);


            // Step 1: Rotate sleeve 90° around X-axis to set height/width orientation
            LocationPoint location = sleeve.Location as LocationPoint;
            XYZ origin = location?.Point ?? XYZ.Zero;

            double ninetyDegrees = Math.PI / 2;
            Line axis1 = Line.CreateUnbound(origin, XYZ.BasisX);
            ElementTransformUtils.RotateElement(doc, sleeve.Id, axis1, ninetyDegrees);

            Transform sleeveTransform = (sleeve as FamilyInstance)?.GetTransform();
            XYZ sleeveX = sleeveTransform?.BasisX ?? XYZ.BasisX;

            XYZ wallDirection = revitNormal;
            XYZ sleeveOrigin = sleeveTransform?.Origin ?? XYZ.Zero;

            // Rotate wallDirection by 90° counter-clockwise to get the desired facing direction
            XYZ targetDirection = new XYZ(-wallDirection.Y, wallDirection.X, wallDirection.Z);

            // Compute signed angle on XY plane
            double angle = sleeveX.AngleOnPlaneTo(targetDirection, XYZ.BasisZ);

            // Apply rotation if significant
            if (Math.Abs(angle) > 1e-6)
            {
                Line axis = Line.CreateUnbound(sleeveOrigin, XYZ.BasisZ);
                ElementTransformUtils.RotateElement(doc, sleeve.Id, axis, angle);
            }


            // Calculate tolerances and rounding values
            double heightTolerance, widthTolerance, lengthTolerance, lengthRound;
            double height, width;

            // Calculate raw dimensions
            double rawHeight, rawWidth;

            if (isRound)
            {
                double roundTolerance = double.Parse(activeSettings[8]) / 12;
                double roundIncrement = double.Parse(activeSettings[10]);

                lengthTolerance = double.Parse(activeSettings[7]) / 12;
                heightTolerance = roundTolerance;
                widthTolerance = roundTolerance;
                lengthRound = double.Parse(activeSettings[9]);

                rawHeight = difHeight + 2 * insulationThickness + heightTolerance;
                rawWidth = difWidth + 2 * insulationThickness + widthTolerance;

                height = RoundUpToNearestIncrement(rawHeight, roundIncrement);
                width = RoundUpToNearestIncrement(rawWidth, roundIncrement);
            }
            else
            {
                heightTolerance = double.Parse(activeSettings[9]) / 12;
                lengthTolerance = double.Parse(activeSettings[8]) / 12;
                widthTolerance = double.Parse(activeSettings[10]) / 12;
                lengthRound = double.Parse(activeSettings[11]);

                rawHeight = difHeight + 2 * insulationThickness + heightTolerance;
                rawWidth = difWidth + 2 * insulationThickness + widthTolerance;

                height = RoundUpToNearestIncrement(rawHeight, double.Parse(activeSettings[13]));
                width = RoundUpToNearestIncrement(rawWidth, double.Parse(activeSettings[12]));
            }

            Parameter lengthParam = sleeve.LookupParameter(activeSettings[5]);
            if (lengthParam != null && !lengthParam.IsReadOnly)
            {
                double thickness = RoundUpToNearestIncrement(difLength + lengthTolerance, lengthRound);
                lengthParam.Set(thickness);
            }

            Parameter pointDescription = sleeve.LookupParameter("Point_Description");
            Parameter PointNum0 = sleeve.LookupParameter("GTP_PointNumber_0");
            Parameter PointNum1 = sleeve.LookupParameter("GTP_PointNumber_1");
            Parameter service = pipeElement.LookupParameter("System Abbreviation");
            Parameter fullService = pipeElement.LookupParameter("Fabrication Service");
            Parameter sleeveService = sleeve.LookupParameter("_IMC - SYSTEM ABBREVIATION");
            String sizeString = string.Empty;
            if (isRound)
            {
                sizeString = (Math.Max(height, width) * 12).ToString()+ "\"";
            }
            else
            {
                sizeString = $"{width * 12}\" x {height * 12}\"";
            }
            if (pointDescription != null && !string.IsNullOrEmpty(sizeString))
            {
                pointDescription.Set($"{sizeString} - Opening");
            }

            if (service != null && PointNum0 != null && !string.IsNullOrEmpty(sizeString))
            {
                PointNum0.Set(sizeString + " " + service.AsValueString());
                if (PointNum1 != null)
                {
                    PointNum1.Set(sizeString + " " + service.AsValueString());
                }
            }
            else if (pipeElement is FabricationPart fab && fullService != null) 
            {
                Dictionary<string, string> airTypeMap = new Dictionary<string, string>
                {
                    { "Supply Air", "SA" },
                    { "Return Air", "RA" },
                    { "Transfer Air", "TA" },
                    { "Outside Air", "OA" },
                    { "Exhaust Air", "EA" }
                };
                foreach (string s in airTypeMap.Keys)
                {
                    if (fullService.AsValueString().Contains(s))
                    {
                        string abriviation = airTypeMap[s];
                        PointNum0.Set(sizeString + " " + abriviation);
                        if (PointNum1 != null)
                        {
                            PointNum1.Set(sizeString + " " + abriviation);
                        }
                    }
                }
            }
            if (sleeveService != null && service != null)
            {
                sleeveService.Set(service.AsValueString());
            }
            // Set sleeve dimensions
            SetSleeveDimensions(sleeve, isRound, width, height);
            MoveFamilyInstanceTo(sleeve, center);

        }
        private Level GetElementLevel(Element element, Document doc)
        {
            Parameter levelParam = element.get_Parameter(BuiltInParameter.RBS_START_LEVEL_PARAM)
            ?? element.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM);

            if (levelParam != null && levelParam.HasValue)
            {
                ElementId levelId = levelParam.AsElementId();
                return doc.GetElement(levelId) as Level;
            }

            if(element is FabricationPart fab)
            {
                ElementId id = fab.LevelId;
                return doc.GetElement(id) as Level;
            }

            return null;
        }
        public void MoveFamilyInstanceTo(FamilyInstance instance, XYZ targetPoint)
        {
            LocationPoint location = instance.Location as LocationPoint;
            if (location == null)
                throw new InvalidOperationException("FamilyInstance does not have a LocationPoint.");

            XYZ currentPoint = location.Point;
            XYZ translation = targetPoint - currentPoint;

            if (!translation.IsZeroLength())
            {
                ElementTransformUtils.MoveElement(instance.Document, instance.Id, translation);
            }
        }


        private double GetInsulationThickness(Document doc, Element pipe)
        {
            if (pipe == null)
                return 0;

            Parameter thicknessParam = pipe.LookupParameter("Insulation Thickness");
            if (thicknessParam != null)
            {
                if (thicknessParam != null && thicknessParam.HasValue)
                    return thicknessParam.AsDouble(); // in feet
            }

            return 0;
        }

        private double RoundUpToNearestIncrement(double valueInFeet, double incrementInInches)
        {
            if (incrementInInches != 0)
            {
                double incrementInFeet = incrementInInches / 12.0;
                double tolerance = 0.00032; // Arbitrarily small value in feet

                double remainder = valueInFeet % incrementInFeet;

                if (remainder < tolerance || Math.Abs(remainder - incrementInFeet) < tolerance)
                {
                    // Already close enough to an increment — return the rounded value
                    return Math.Round(valueInFeet / incrementInFeet) * incrementInFeet;
                }

                // Otherwise, round up
                return Math.Ceiling(valueInFeet / incrementInFeet) * incrementInFeet;
            }
            return valueInFeet;
        }

        private bool SleeveExistsAt(XYZ center, Document doc, string familyName, double tolerance = 0.4)
        {
            // Create an Outline (not BoundingBoxXYZ)
            XYZ min = new XYZ(center.X - tolerance, center.Y - tolerance, center.Z - tolerance);
            XYZ max = new XYZ(center.X + tolerance, center.Y + tolerance, center.Z + tolerance);
            Outline outline = new Outline(min, max);

            BoundingBoxIntersectsFilter bboxFilter = new BoundingBoxIntersectsFilter(outline);

            var collector = new FilteredElementCollector(doc)
            .OfClass(typeof(FamilyInstance))
            .WherePasses(bboxFilter)
            .Cast<FamilyInstance>()
            .Where(fi => fi.Symbol.Family.Name.Equals(familyName, StringComparison.OrdinalIgnoreCase));

            foreach (var sleeve in collector)
            {
                if (sleeve.Location is LocationPoint locPoint)
                {
                    if (locPoint.Point.DistanceTo(center) < tolerance)
                        return true;
                }
            }
            return false;
        }

        private void GetBoundingMetrics(
            SimpleMesh mesh,
            XYZ wallNormal,
            out double zExtent,
            out double sectionExtent,
            out double normalExtent,
            out XYZ center,
            out bool round)
        {
            double minZ = double.MaxValue;
            double maxZ = double.MinValue;

            double minSection = double.MaxValue;
            double maxSection = double.MinValue;

            double minNormal = double.MaxValue;
            double maxNormal = double.MinValue;

            double minX = double.MaxValue, maxX = double.MinValue;
            double minY = double.MaxValue, maxY = double.MinValue;

            XYZ reference = XYZ.BasisZ;
            if (wallNormal.IsAlmostEqualTo(XYZ.BasisZ))
                reference = XYZ.BasisX;

            XYZ sectionDirection = wallNormal.CrossProduct(reference).Normalize();

            foreach (Vector3 vertex in mesh.Vertices)
            {
                double x = vertex.X;
                double y = vertex.Y;
                double z = vertex.Z;

                double sectionCoord = x * sectionDirection.X +
                                      y * sectionDirection.Y +
                                      z * sectionDirection.Z;

                double normalCoord = x * wallNormal.X +
                                     y * wallNormal.Y +
                                     z * wallNormal.Z;

                minZ = Math.Min(minZ, z);
                maxZ = Math.Max(maxZ, z);

                minX = Math.Min(minX, x);
                maxX = Math.Max(maxX, x);

                minY = Math.Min(minY, y);
                maxY = Math.Max(maxY, y);

                minSection = Math.Min(minSection, sectionCoord);
                maxSection = Math.Max(maxSection, sectionCoord);

                minNormal = Math.Min(minNormal, normalCoord);
                maxNormal = Math.Max(maxNormal, normalCoord);
            }

            zExtent = maxZ - minZ;
            sectionExtent = maxSection - minSection;
            normalExtent = maxNormal - minNormal;

            center = new XYZ(
                (minX + maxX) * 0.5,
                (minY + maxY) * 0.5,
                (minZ + maxZ) * 0.5
            );

            double maxRadSq = 0;

            foreach (Vector3 v in mesh.Vertices)
            {
                // Vector from center to vertex
                double dx = v.X - center.X;
                double dy = v.Y - center.Y;
                double dz = v.Z - center.Z;

                Vector3 vec = new Vector3((float)dx, (float)dy, (float)dz);

                // Project vec onto the plane perpendicular to wallNormal
                Vector3 wallNormalVec = new Vector3((float)wallNormal.X, (float)wallNormal.Y, (float)wallNormal.Z);
                float dot = Vector3.Dot(vec, wallNormalVec);
                Vector3 projection = vec - wallNormalVec * dot;

                double radSq = projection.LengthSquared();
                maxRadSq = Math.Max(maxRadSq, radSq);
            }

            double radius = Math.Sqrt(maxRadSq);
            double circularArea = Math.PI * radius * radius;
            double projectedArea = sectionExtent * zExtent;

            round = circularArea < projectedArea;
        }

        private void SetSleeveDimensions(FamilyInstance sleeve, bool isRound, double width, double height)
        {
            if (isRound)
            {
                Parameter diameterParam = sleeve.LookupParameter(activeSettings[6]);
                if (diameterParam != null && !diameterParam.IsReadOnly)
                    diameterParam.Set(Math.Max(width, height));
            }
            else
            {
                Parameter widthParam = sleeve.LookupParameter(activeSettings[6]);
                Parameter heightParam = sleeve.LookupParameter(activeSettings[7]);

                if (widthParam != null && !widthParam.IsReadOnly)
                    widthParam.Set(width);

                if (heightParam != null && !heightParam.IsReadOnly)
                    heightParam.Set(height);
            }
        }

        private XYZ GetSleeveAxis(FamilyInstance sleeve)
        {
            return XYZ.BasisZ; // fallback
        }

    }
}

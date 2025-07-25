using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using SharedRevit.Geometry;
using SharedRevit.Geometry.Collision;
using SharedRevit.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using SharedCore;
using SharedCore.SaveFile;

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
                 return false;
             })
             .ToList();




            List<GeometryData> mepGeometry = ElementToGeometryData.ConvertMEPToGeometryData(references, doc);
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
            List<GeometryData> wallGeometry = ConvertTransformedWallsToGeometryData(structuralModel, walls);
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

        public List<GeometryData> ConvertTransformedWallsToGeometryData(RevitLinkInstance linkInstance, List<Wall> walls)
        {
            List<GeometryData> geometryDataList = new List<GeometryData>();

            // Get the transform from the linked model to the host model
            Transform linkTransform = linkInstance.GetTransform();

            foreach (Wall wall in walls)
            {
                GeometryElement geomElement = wall.get_Geometry(new Options
                {
                    ComputeReferences = true,
                    IncludeNonVisibleObjects = false,
                    DetailLevel = ViewDetailLevel.Fine
                });

                if (geomElement == null) continue;

                foreach (GeometryObject geomObj in geomElement)
                {
                    Solid solid = geomObj as Solid;
                    if (solid != null && solid.Volume > 0)
                    {
                        // Apply the transform to the solid
                        Solid transformedSolid = SolidUtils.CreateTransformed(solid, linkTransform);

                        // Transform the bounding box
                        BoundingBoxXYZ originalBox = wall.get_BoundingBox(null);
                        BoundingBoxXYZ transformedBox = null;

                        if (originalBox != null)
                        {
                            transformedBox = new BoundingBoxXYZ
                            {
                                Min = linkTransform.OfPoint(originalBox.Min),
                                Max = linkTransform.OfPoint(originalBox.Max)
                            };
                        }

                        geometryDataList.Add(new GeometryData
                        {
                            Solid = transformedSolid,
                            BoundingBox = transformedBox,
                            SourceElementId = wall.Id,
                            Role = GeometryRole.B
                        });
                    }
                }
            }

            return geometryDataList;
        }

        string[] activeSettings = null;
        public void placeSleeveAtCollision(CollisionResult collision, Document doc)
        {
            Solid intersection = collision.Intersection;
            if (intersection == null || intersection.Faces.Size == 0)
                return;

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
            XYZ wallNormal = GetWallNormalFromElement(wall);
            if (wallNormal == null)
                return;

            GetBoundingMetrics(intersection, wallNormal, out double difHeight, out double difWidth, out double difLength, out XYZ center);

            // Determine if round or rectangular
            Element pipeElement = doc.GetElement(collision.A.SourceElementId);
            bool isRound = IsRound(pipeElement) && !forceRec;
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
            string symbolName = activeSettings[3];

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

            if (SleeveExistsAt(center, doc, sleeveSymbol.Family.Name))
                return;

            FamilyInstance sleeve = doc.Create.NewFamilyInstance(center, sleeveSymbol, level, StructuralType.NonStructural);


            // Step 1: Rotate sleeve 90° around X-axis to set height/width orientation
            LocationPoint location = sleeve.Location as LocationPoint;
            XYZ origin = location?.Point ?? XYZ.Zero;

            double ninetyDegrees = Math.PI / 2;
            Line axis1 = Line.CreateUnbound(origin, XYZ.BasisX);
            ElementTransformUtils.RotateElement(doc, sleeve.Id, axis1, ninetyDegrees);


            // Step 2: Rotate around Z-axis to align with wall normal
            Transform sleeveTransform = (sleeve as FamilyInstance)?.GetTransform();
            XYZ sleeveX = sleeveTransform?.BasisX ?? XYZ.BasisX;

            // Wall normal is already in XY plane
            XYZ wallDirection = wallNormal.Normalize();

            // Adjust angle by ±90° (π/2) to account for rotated frame
            double angle = sleeveX.AngleTo(wallDirection) - (Math.PI / 2);

            // Optional: Clamp angle to [-π, π] if needed
            if (angle > Math.PI) angle -= 2 * Math.PI;
            if (angle < -Math.PI) angle += 2 * Math.PI;

            if (Math.Abs(angle) > 1e-6)
            {
                Line axis2 = Line.CreateUnbound(origin, XYZ.BasisZ);
                ElementTransformUtils.RotateElement(doc, sleeve.Id, axis2, angle);
            }


            // Calculate tolerances and rounding values
            double heightTolerance, widthTolerance, lengthTolerance, lengthRound;
            double height, width;

            // Calculate raw dimensions
            double rawHeight, rawWidth;

            if (isRound)
            {
                double roundTolerance = double.Parse(activeSettings[7]) / 12;
                double roundIncrement = double.Parse(activeSettings[9]);

                heightTolerance = roundTolerance;
                widthTolerance = roundTolerance;
                lengthTolerance = double.Parse(activeSettings[6]) / 12;
                lengthRound = double.Parse(activeSettings[8]);

                rawHeight = difHeight + 2 * insulationThickness + heightTolerance;
                rawWidth = difWidth + 2 * insulationThickness + widthTolerance;

                height = RoundUpToNearestIncrement(rawHeight, roundIncrement);
                width = RoundUpToNearestIncrement(rawWidth, roundIncrement);
            }
            else
            {
                heightTolerance = double.Parse(activeSettings[8]) / 12;
                widthTolerance = double.Parse(activeSettings[9]) / 12;
                lengthTolerance = double.Parse(activeSettings[7]) / 12;
                lengthRound = double.Parse(activeSettings[10]);

                rawHeight = difHeight + 2 * insulationThickness + heightTolerance;
                rawWidth = difWidth + 2 * insulationThickness + widthTolerance;

                height = RoundUpToNearestIncrement(rawHeight, double.Parse(activeSettings[12]));
                width = RoundUpToNearestIncrement(rawWidth, double.Parse(activeSettings[11]));
            }

            Parameter lengthParam = sleeve.LookupParameter(activeSettings[4]);
            if (lengthParam != null && !lengthParam.IsReadOnly)
            {
                double thickness = RoundUpToNearestIncrement(difLength + lengthTolerance, lengthRound);
                lengthParam.Set(thickness);
            }

            Parameter pointDescription = sleeve.LookupParameter("Point_Description");
            Parameter PointNum0 = sleeve.LookupParameter("GTP_PointNumber_0");
            Parameter PointNum1 = sleeve.LookupParameter("GTP_PointNumber_1");
            Parameter service = pipeElement.LookupParameter("System Abbreviation");
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

            ElementId pipeId = pipe.Id;

            var insulation = new FilteredElementCollector(doc)
            .OfClass(typeof(PipeInsulation))
            .Cast<PipeInsulation>()
            .FirstOrDefault(ins => ins.HostElementId == pipeId);

            if (insulation != null)
            {

                Parameter thicknessParam = insulation.LookupParameter("Insulation Thickness");
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
                double tolerance = 1e-6; // Arbitrarily small value in feet (~0.000012 inches)

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


        private XYZ GetWallNormalFromElement(Wall wall)
        {
            LocationCurve locCurve = wall.Location as LocationCurve;
            if (locCurve != null)
            {
                Curve curve = locCurve.Curve;
                XYZ wallDirection = (curve.GetEndPoint(1) - curve.GetEndPoint(0)).Normalize();
                return wallDirection.CrossProduct(XYZ.BasisZ).Normalize(); // outward normal
            }
            return null;
        }

        private bool SleeveExistsAt(XYZ center, Document doc, string familyName, double tolerance = 0.1)
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

        private bool IsRound(Element element)
        {
            if (element is Duct duct)
            {
                ConnectorSet connectors = duct.ConnectorManager.Connectors;
                foreach (Connector connector in connectors)
                {
                    if (connector.Shape == ConnectorProfileType.Round)
                        return true;
                    if (connector.Shape == ConnectorProfileType.Rectangular)
                        return false;
                }
            }

            if (element is Pipe)
                return true;

            return false;
        }

        private void GetBoundingMetrics(
         Solid solid,
         XYZ wallNormal,
         out double zExtent,
         out double sectionExtent,
         out double normalExtent,
         out XYZ centroid)
        {
            double minZ = double.MaxValue;
            double maxZ = double.MinValue;

            double minSection = double.MaxValue;
            double maxSection = double.MinValue;

            double minNormal = double.MaxValue;
            double maxNormal = double.MinValue;

            XYZ reference = XYZ.BasisZ;
            if (wallNormal.IsAlmostEqualTo(XYZ.BasisZ))
                reference = XYZ.BasisX;

            XYZ sectionDirection = wallNormal.CrossProduct(reference).Normalize();

            // For centroid calculation
            XYZ sum = XYZ.Zero;
            int count = 0;

            foreach (Face face in solid.Faces)
            {
                Mesh mesh = face.Triangulate();
                foreach (XYZ vertex in mesh.Vertices)
                {
                    double z = vertex.Z;
                    double sectionCoord = vertex.DotProduct(sectionDirection);
                    double normalCoord = vertex.DotProduct(wallNormal);

                    if (z < minZ) minZ = z;
                    if (z > maxZ) maxZ = z;

                    if (sectionCoord < minSection) minSection = sectionCoord;
                    if (sectionCoord > maxSection) maxSection = sectionCoord;

                    if (normalCoord < minNormal) minNormal = normalCoord;
                    if (normalCoord > maxNormal) maxNormal = normalCoord;

                    sum += vertex;
                    count++;
                }
            }

            zExtent = maxZ - minZ;
            sectionExtent = maxSection - minSection;
            normalExtent = maxNormal - minNormal;

            centroid = count > 0 ? sum.Divide(count) : XYZ.Zero;
        }

        private void SetSleeveDimensions(FamilyInstance sleeve, bool isRound, double width, double height)
        {
            if (isRound)
            {
                Parameter diameterParam = sleeve.LookupParameter(activeSettings[5]);
                if (diameterParam != null && !diameterParam.IsReadOnly)
                    diameterParam.Set(Math.Max(width, height));
            }
            else
            {
                Parameter widthParam = sleeve.LookupParameter(activeSettings[5]);
                Parameter heightParam = sleeve.LookupParameter(activeSettings[6]);

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

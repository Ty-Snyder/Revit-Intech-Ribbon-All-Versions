using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Intech;
using SharedCore;
using SharedCore.SaveFile;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
namespace SharedRevit.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class PlaceSleeveAtConnectorCommand : IExternalCommand
    {
        string[] activeSettings = null;
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {

                Reference pickedRef = uidoc.Selection.PickObject(ObjectType.Element, new MEPElementSelectionFilter(), "Select a pipe or fitting");
                Element selectedElement = doc.GetElement(pickedRef);
                XYZ clickPoint = pickedRef.GlobalPoint;

                if (selectedElement == null)
                    return Result.Failed;

                Connector connector = GetClosestConnector(selectedElement, clickPoint);
                if (connector == null)
                {
                    TaskDialog.Show("Error", "No connector found on selected element.");
                    return Result.Failed;
                }

                XYZ origin = connector.Origin;
                bool isRound = IsRound(selectedElement);
                double insulationThickness = GetInsulationThickness(doc, selectedElement);

                // Load settings
                string basePath = Path.Combine(App.BasePath, "Settings.txt");
                SaveFileManager saveFileManager = new SaveFileManager(basePath);
                SaveFileSection sec = isRound
                    ? saveFileManager.GetSectionsByName("Sleeve Place", "Round Sleeve")
                    : saveFileManager.GetSectionsByName("Sleeve Place", "Rectangular Sleeve");

                activeSettings = sec.lookUp(0, "True").FirstOrDefault() ?? sec.Rows[0];
                string familyName = activeSettings[2];
                string symbolName = activeSettings[3];

                FamilySymbol sleeveSymbol = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .OfType<FamilySymbol>()
                    .FirstOrDefault(fs => fs.Family.Name == familyName && fs.Name == symbolName);

                if (sleeveSymbol == null)
                {
                    TaskDialog.Show("Error", $"Sleeve family '{familyName}' with type '{symbolName}' not found.");
                    return Result.Failed;
                }

                if (!sleeveSymbol.IsActive)
                {
                    using (Transaction t = new Transaction(doc, "Activate Sleeve Symbol"))
                    {
                        t.Start();
                        sleeveSymbol.Activate();
                        t.Commit();
                    }
                }

                Level level = GetElementLevel(selectedElement, doc);
                if (level == null)
                    return Result.Failed;

                using (Transaction tx = new Transaction(doc, "Place Sleeve at Connector"))
                {
                    tx.Start();

                    FamilyInstance sleeve = doc.Create.NewFamilyInstance(origin, sleeveSymbol, level, StructuralType.NonStructural);

                    MoveFamilyInstanceTo(sleeve, connector.Origin);

                    AlignSleeveToConnector(sleeve, connector, doc);

                    // Set arbitrary depth
                    double defaultLength = 0.5; // 6 inches
                    Parameter depthParam = sleeve.LookupParameter(activeSettings[4]);
                    if (depthParam != null && !depthParam.IsReadOnly)
                    {
                        depthParam.Set(defaultLength);
                    }

                    // Get pipe diameter
                    double diameter = GetNominalDiameter(selectedElement);
                    double tolerance = double.Parse(activeSettings[7]) / 12;
                    double increment = double.Parse(activeSettings[9]);

                    double sleeveSize = RoundUpToNearestIncrement(diameter + 2 * insulationThickness + tolerance, increment);
                    Parameter pointDescription = sleeve.LookupParameter("Point_Description");
                    Parameter PointNum0 = sleeve.LookupParameter("GTP_PointNumber_0");
                    Parameter PointNum1 = sleeve.LookupParameter("GTP_PointNumber_1");
                    Parameter pipeService = selectedElement.LookupParameter("System Abbreviation");
                    Double pipeSize = sleeveSize;
                    if (pointDescription != null && pipeSize != null)
                    {
                        pointDescription.Set($"{pipeSize * 12}\" - Opening");
                        if (pipeService != null && PointNum0 != null)
                        {
                            PointNum0.Set(pipeSize * 12 + " " + pipeService.AsValueString());
                            if (PointNum1 != null)
                            {
                                PointNum1.Set(pipeSize * 12 + " " + pipeService.AsValueString());
                            }
                        }
                    }
                    SetSleeveDimensions(sleeve, isRound, sleeveSize, sleeveSize);
                    tx.Commit();
                }
                Execute(commandData, ref message, elements);
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
        public void MoveFamilyInstanceTo(FamilyInstance instance, XYZ targetPoint)
        {
            LocationPoint location = instance.Location as LocationPoint;
            if (location == null)
                throw new System.InvalidOperationException("FamilyInstance does not have a LocationPoint.");

            XYZ currentPoint = location.Point;
            XYZ translation = targetPoint - currentPoint;

            if (!translation.IsZeroLength())
            {
                ElementTransformUtils.MoveElement(instance.Document, instance.Id, translation);
            }
        }

        private void AlignSleeveToConnector(FamilyInstance sleeve, Connector targetConnector, Document doc)
        {
            if (targetConnector == null || targetConnector.CoordinateSystem == null)
                return;

            XYZ targetZ = targetConnector.CoordinateSystem.BasisZ.Normalize();
            XYZ sleeveZ = GetSleeveAxis(sleeve);
            targetZ = new XYZ(targetZ.X , targetZ.Y, 0).Normalize();
            double angle = sleeveZ.AngleTo(targetZ) + Math.PI;
            if (angle < 1e-6)
                return;

            XYZ rotationAxis = sleeveZ.CrossProduct(targetZ).Normalize();
            if (rotationAxis.IsZeroLength())
                return;

            Line axis = Line.CreateUnbound(sleeve.GetTransform().Origin, rotationAxis);

            ElementTransformUtils.RotateElement(doc, sleeve.Id, axis, angle);
        }

        private XYZ GetSleeveAxis(FamilyInstance sleeve)
        {
            if (sleeve.MEPModel != null)
            {
                var connectors = sleeve.MEPModel.ConnectorManager.Connectors
                .Cast<Connector>()
                .Where(c => c.ConnectorType == ConnectorType.Physical)
                .OrderBy(c => c.Origin.Z)
                .ToList();

                if (connectors.Count >= 2)
                {
                    return (connectors[1].Origin - connectors[0].Origin).Normalize();
                }
            }

            return XYZ.BasisZ;
        }


        public class MEPElementSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem)
            {
                // Disallow insulation
                if (elem is PipeInsulation)
                    return false;

                // Allow pipes, ducts, fittings, and family instances with MEP connectors
                return elem is MEPCurve ||
                       (elem is FamilyInstance fi && fi.MEPModel != null);
            }

            public bool AllowReference(Reference reference, XYZ position) => false;
        }

        private Connector GetClosestConnector(Element element, XYZ point)
        {
            ConnectorSet connectors = null;

            if (element is MEPCurve mEP)
                connectors = mEP.ConnectorManager.Connectors;
            else if (element is FamilyInstance fi && fi.MEPModel != null)
                connectors = fi.MEPModel.ConnectorManager.Connectors;

            if (connectors == null || connectors.Size == 0)
                return null;
            double closest = double.MaxValue;
            Connector connect = null;
            foreach ( Connector con in connectors)
            {
                double cur = con.Origin.DistanceTo(point);
                if (cur < closest)
                {
                    closest = cur;
                    connect = con;
                }
            }
            return connect;
        }

        private double GetNominalDiameter(Element element)
        {
            if (element is Pipe pipe)
                return element.LookupParameter("Outside Diameter").AsDouble(); // in feet

            return 0;
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
                    return thicknessParam.AsDouble();
            }

            return 0;
        }

        private bool IsRound(Element element)
        {
            Parameter shapeParam = element.LookupParameter("Section Shape");
            if (shapeParam != null && shapeParam.HasValue)
            {
                string shape = shapeParam.AsValueString();
                return shape.Equals("Round", StringComparison.OrdinalIgnoreCase);
            }

            return true; // default to round
        }

        private double RoundUpToNearestIncrement(double value, double increment)
        {
            double round = increment / 12;
            return Math.Ceiling(value / round) * round;
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
                Parameter widthParam = sleeve.LookupParameter("Sleeve_Width");
                Parameter heightParam = sleeve.LookupParameter("Sleeve_Length");

                if (widthParam != null && !widthParam.IsReadOnly)
                    widthParam.Set(width);

                if (heightParam != null && !heightParam.IsReadOnly)
                    heightParam.Set(height);
            }
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

    }

}

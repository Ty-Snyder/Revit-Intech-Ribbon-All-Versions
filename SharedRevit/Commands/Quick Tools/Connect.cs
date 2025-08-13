using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using OfficeOpenXml.FormulaParsing.Excel.Functions.Finance.FinancialDayCount;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace SharedRevit.Commands
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    [Autodesk.Revit.Attributes.Regeneration(Autodesk.Revit.Attributes.RegenerationOption.Manual)]
    public class ConnectElementsCommand : IExternalCommand
    {

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            View activeView = uidoc.ActiveView;

            try
            {
                // First click: select element to move
                Reference ref1 = uidoc.Selection.PickObject(ObjectType.Element, "Select first element (will move)");
                Element elem1 = doc.GetElement(ref1);
                XYZ click1 = ref1.GlobalPoint;

                // Second click: select target element
                Reference ref2 = uidoc.Selection.PickObject(ObjectType.Element, "Select second element (stationary)");
                Element elem2 = doc.GetElement(ref2);
                XYZ click2 = ref2.GlobalPoint;

                Connector fromConnector = GetClosestConnector(activeView, elem1, click1);
                Connector toConnector = GetClosestConnector(activeView, elem2, click2);

                if (fromConnector == null || toConnector == null)
                {
                    message = "Could not find connectors on one or both elements.";
                    return Result.Failed;
                }

                using (Transaction tx = new Transaction(doc, "Align Connectors"))
                {
                    tx.Start();

                    bool isPipeOrDuct = elem1 is Pipe || elem1 is Duct;

                    if (isPipeOrDuct)
                    {
                        // Align and rotate for pipe or generic duct
                        AlignConnectorToConnector(fromConnector, toConnector, doc);
                    }
                    else
                    {
                        // Move only for everything else (e.g., FabricationPart, equipment, fittings)
                        XYZ moveVector = toConnector.Origin - fromConnector.Origin;
                        ElementTransformUtils.MoveElement(doc, elem1.Id, moveVector);
                    }

                    if (fromConnector.Domain == toConnector.Domain &&
                            fromConnector.ConnectorType == toConnector.ConnectorType)
                    {
                        fromConnector.ConnectTo(toConnector);
                    }

                    tx.Commit();
                }

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

        public static void AlignConnectorToConnector(Connector fromConnector, Connector toConnector, Document doc)
        {
            Element element = fromConnector.Owner;

            XYZ fromOrigin = fromConnector.Origin;
            XYZ fromZ = fromConnector.CoordinateSystem.BasisZ;
            XYZ toOrigin = toConnector.Origin;
            XYZ toZ = toConnector.CoordinateSystem.BasisZ;

            XYZ vectorToTarget = toOrigin - fromOrigin;
            double projectionLength = vectorToTarget.DotProduct(fromZ);
            XYZ projectedPoint = fromOrigin + projectionLength * fromZ;

            XYZ translation = toOrigin - projectedPoint;

            if (!translation.IsZeroLength())
            {
                ElementTransformUtils.MoveElement(doc, element.Id, translation);
            }

            XYZ rotationAxis = fromZ.CrossProduct(-toZ);
            double angle = fromZ.AngleTo(-toZ);

            if (!rotationAxis.IsZeroLength() && angle > 1e-6)
            {
                rotationAxis = rotationAxis.Normalize();
                Line rotationLine = Line.CreateUnbound(toOrigin, rotationAxis);
                ElementTransformUtils.RotateElement(doc, element.Id, rotationLine, angle);
            }

            // Adjust curve for Pipe or Duct
            if (element.Location is LocationCurve locationCurve && locationCurve.Curve is Line line)
            {
                XYZ start = line.GetEndPoint(0);
                XYZ end = line.GetEndPoint(1);

                double distToStart = start.DistanceTo(toOrigin);
                double distToEnd = end.DistanceTo(toOrigin);

                Line newLine = distToStart < distToEnd
                    ? Line.CreateBound(toOrigin, end)
                    : Line.CreateBound(start, toOrigin);

                locationCurve.Curve = newLine;
            }
        }



        private Connector GetClosestConnector(View view, Element element, XYZ pickedPoint)
        {
            ConnectorSet connectors = GetConnectors(element);
            if (connectors == null) return null;

            XYZ viewDirection = GetViewDirection(view);
            if (viewDirection == null) return null;

            // Project picked point onto view plane
            XYZ projectedPicked = ProjectOntoPlane(pickedPoint, viewDirection);

            Connector closest = null;
            double minDist = double.MaxValue;

            foreach (Connector conn in connectors)
            {
                XYZ projectedConn = ProjectOntoPlane(conn.Origin, viewDirection);
                double dist = (projectedConn - projectedPicked).GetLength();
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = conn;
                }
            }

            return closest;
        }

        private XYZ GetViewDirection(View view)
        {
            if (view is View3D v3d && !v3d.IsPerspective)
                return v3d.ViewDirection.Normalize();
            else if (view is ViewSection || view is ViewPlan)
                return XYZ.BasisZ; // Simplified assumption for 2D views
            return null;
        }

        private XYZ ProjectOntoPlane(XYZ point, XYZ normal)
        {
            // Remove the component along the normal
            double distance = point.DotProduct(normal);
            return point - normal * distance;
        }

        private ConnectorSet GetConnectors(Element element)
        {
            ConnectorSet connectors = null;
            if (element is MEPCurve mEP)
                connectors = mEP.ConnectorManager?.Connectors;
            else if (element is FamilyInstance fi)
                connectors = fi.MEPModel?.ConnectorManager?.Connectors;
            else if (element is FabricationPart fab)
                connectors = fab.ConnectorManager?.Connectors;
            return connectors;
        }
    }
}

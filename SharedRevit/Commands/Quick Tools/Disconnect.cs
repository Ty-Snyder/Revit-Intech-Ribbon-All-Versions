using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Fabrication;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;

namespace SharedRevit.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class Disconnect : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            View view = uidoc.ActiveView;

            try
            {
                Reference pickedRef = uidoc.Selection.PickObject(ObjectType.Element, "Select an element to disconnect");
                Element element = doc.GetElement(pickedRef);
                XYZ pickedPoint = pickedRef.GlobalPoint;

                DisconnectClosestConnector2D(doc, view, element, pickedPoint);


                DisconnectClosestConnector2D(doc, view, element, pickedPoint);
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
            Execute(commandData, ref message, elements);
            return Result.Succeeded;
        }

        private void DisconnectClosestConnector2D(Document doc, View view, Element element, XYZ pickedPoint)
        {
            Connector closest = GetClosestConnector2D(view, element, pickedPoint);
            if (closest == null) return;

            using (Transaction tx = new Transaction(doc, "Disconnect Closest Connector"))
            {
                tx.Start();

                List<Connector> connected = new List<Connector>();
                foreach (Connector refConn in closest.AllRefs)
                {
                    if (refConn.IsConnected && refConn.Owner.Id != element.Id)
                    {
                        connected.Add(refConn);
                    }
                }

                foreach (Connector conn in connected)
                {
                    closest.DisconnectFrom(conn);
                }

                tx.Commit();
            }
        }

        private Connector GetClosestConnector2D(View view, Element element, XYZ pickedPoint)
        {
            ConnectorSet connectors = GetConnectors(element);
            if (connectors == null) return null;

            XYZ viewDirection = GetViewDirection(view);
            if (viewDirection == null) return null;

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
            else if (view is ViewPlan || view is ViewSection)
                return XYZ.BasisZ; // Simplified assumption
            return null;
        }

        private XYZ ProjectOntoPlane(XYZ point, XYZ normal)
        {
            double distance = point.DotProduct(normal);
            return point - normal * distance;
        }

        private ConnectorSet GetConnectors(Element element)
        {
            if (element is MEPCurve mEP)
                return mEP.ConnectorManager?.Connectors;
            else if (element is FamilyInstance fi)
                return fi.MEPModel?.ConnectorManager?.Connectors;
            else if (element is FabricationPart fab)
                return fab.ConnectorManager?.Connectors;
            return null;
        }
    }
}

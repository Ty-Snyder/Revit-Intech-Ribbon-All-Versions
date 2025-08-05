using Autodesk.Revit.DB;
using SharedRevit.Geometry.Implicit_Surfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SharedRevit.Geometry.Collision
{
    public class Engine
    {

        public class CollisionEngine
        {
            public delegate void CollisionHandler(CollisionResult result);


            public void Run(
                IEnumerable<MeshGeometryData> groupA,
                IEnumerable<MeshGeometryData> groupB,
                double cellSize,
                double overlap,
                CollisionHandler onCollision)
            {
                var partitions = new ConcurrentDictionary<(int x, int y, int z), Partition>();
                var processedPairs = new ConcurrentDictionary<(ElementId, ElementId), byte>();

                // Collision phase
                Parallel.ForEach(groupA, a =>
                {
                    foreach (var b in groupB)
                    {
                        Box shapeA = (Box)a.BoundingSurface;
                        Box shapeB = (Box)b.BoundingSurface;
                        BoundingBox3D intersection = BoundingBox3D.Intersect(shapeA.GetBoundingBox(), shapeB.GetBoundingBox());
                        if (!intersection.IsEmpty)
                        {
                            var idA = a.SourceElementId;
                            var idB = b.SourceElementId;
                            if (idA > idB)
                                (idA, idB) = (idB, idA);
                            if (!processedPairs.TryAdd((idA, idB), 0))
                                continue;

                            Box intersectBox = ShapeUtils.IntersectBoxes(shapeB, shapeA);

                            SimpleMesh intersectionMesh = intersectBox.ClipMeshToBox(a.mesh);

                            if (intersectionMesh.Vertices.Count < 3)
                                continue;
                            onCollision(new CollisionResult
                            {
                                A = a,
                                B = b,
                                Intersection = intersectionMesh
                            });
                        }
                    }
                });
            }

            public (int x, int y, int z) GetCellIndex(Vector3 point, double cellSize)
            {
                return (
                    (int)Math.Floor(point.X / cellSize),
                    (int)Math.Floor(point.Y / cellSize),
                    (int)Math.Floor(point.Z / cellSize)
                );
            }
        }
    }
}

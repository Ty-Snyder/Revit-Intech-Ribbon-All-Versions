using Autodesk.Revit.DB;
using SharedRevit.Geometry.Shapes;
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

                // Helper to get cell coordinates from Vector3
                (int x, int y, int z) GetCellCoords(Vector3 point) =>
                    ((int)(point.X / cellSize), (int)(point.Y / cellSize), (int)(point.Z / cellSize));

                // Assign groupB elements to spatial partitions
                foreach (var b in groupB)
                {
                    var bounds = b.BoundingSurface.GetBoundingBox(); // returns custom BoundingBox3D
                    var min = GetCellCoords(bounds.Min);
                    var max = GetCellCoords(bounds.Max);

                    for (int x = min.x; x <= max.x; x++)
                        for (int y = min.y; y <= max.y; y++)
                            for (int z = min.z; z <= max.z; z++)
                            {
                                var key = (x, y, z);
                                var partition = partitions.GetOrAdd(key, _ => new Partition());
                                lock (partition)
                                {
                                    partition.GroupB.Add(b);
                                }
                            }
                }

                // Collision phase (unchanged input loop)
                Parallel.ForEach(groupA, a =>
                {
                    var bounds = a.BoundingSurface.GetBoundingBox(); // returns custom BoundingBox3D
                    var min = GetCellCoords(bounds.Min);
                    var max = GetCellCoords(bounds.Max);

                    HashSet<MeshGeometryData> candidates = new();

                    for (int x = min.x; x <= max.x; x++)
                        for (int y = min.y; y <= max.y; y++)
                            for (int z = min.z; z <= max.z; z++)
                            {
                                var key = (x, y, z);
                                if (partitions.TryGetValue(key, out var partition))
                                {
                                    lock (partition)
                                    {
                                        foreach (var b in partition.GroupB)
                                            candidates.Add(b);
                                    }
                                }
                            }

                    foreach (var b in candidates)
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
                            (SimpleMesh intersectionMesh, List<SharedRevit.Geometry.Shapes.Face> clippedFaces) = intersectBox.ClipMeshToBox(a.mesh);

                            if (intersectionMesh.Vertices.Count < 3)
                                continue;

                            onCollision(new CollisionResult
                            {
                                A = a,
                                B = b,
                                Intersection = intersectionMesh,
                                Faces = clippedFaces
                            });
                        }
                    }
                });
            }
        }
    }
}

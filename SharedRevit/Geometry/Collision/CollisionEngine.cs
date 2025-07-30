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

                // Partitioning phase
                Parallel.ForEach(groupA.Concat(groupB), record =>
                {
                    var inflated = record.BoundingSurface.GetBoundingBox();
                    var min = GetCellIndex(inflated.Min, cellSize);
                    var max = GetCellIndex(inflated.Max, cellSize);

                    for (int x = min.x; x <= max.x; x++)
                        for (int y = min.y; y <= max.y; y++)
                            for (int z = min.z; z <= max.z; z++)
                            {
                                var key = (x, y, z);
                                var partition = partitions.GetOrAdd(key, _ => new Partition());
                                lock (partition)
                                    partition.Add(record);
                            }
                });


                List<Partition> containingPartition = new List<Partition>();
                foreach (Partition part in partitions.Values)
                {
                    if (!part.isEmpty())
                    {
                        containingPartition.Add(part);
                    }
                }

                // Collision phase
                Parallel.ForEach(containingPartition, partition =>
                {

                    foreach (var a in partition.GroupA)
                    {
                        foreach (var b in partition.GroupB)
                        {
                            IShape shapeA =  a.BoundingSurface;
                            IShape shapeB = b.BoundingSurface;
                            IShape intersection = new IntersectionShape(shapeA, shapeB);
                            if (!intersection.GetBoundingBox().IsEmpty)
                            {
                                var idA = a.SourceElementId;
                                var idB = b.SourceElementId;
                                if (idA > idB)
                                    (idA, idB) = (idB, idA);
                                if (!processedPairs.TryAdd((idA, idB), 0))
                                    continue;
                                onCollision(new CollisionResult
                                {
                                    A = a,
                                    B = b,
                                    Intersection = intersection,
                                });
                            }
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

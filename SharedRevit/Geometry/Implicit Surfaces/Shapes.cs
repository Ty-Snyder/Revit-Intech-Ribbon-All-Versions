using MathNet.Numerics.LinearAlgebra;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Net.NetworkInformation;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SharedRevit.Geometry.Shapes
{
    public interface IShape
    {

        double SignedDistance(Vector3 point);

        // Optional: Bounding box for acceleration structures
        BoundingBox3D GetBoundingBox();

    }

    public struct Box : IShape
    {
        public Vector3 Size { get; }
        public Matrix4x4 Transform { get; }

        public Box(Vector3 size, Matrix4x4 transform)
        {
            Size = size;
            Transform = transform;
            BoundingBox3D box = this.GetBoundingBox();
        }

        public double SignedDistance(Vector3 point)
        {
            // Transform point into box's local space
            Matrix4x4.Invert(Transform, out Matrix4x4 inverse);
            Vector3 localPoint = Vector3.Transform(point, inverse);

            // Box is centered at origin in local space
            Vector3 halfSize = Size * 0.5f;
            Vector3 q = Vector3.Abs(localPoint) - halfSize;
            float outsideDistance = Vector3.Max(q, Vector3.Zero).Length();
            float insideDistance = Math.Min(Math.Max(q.X, Math.Max(q.Y, q.Z)), 0.0f);
            return outsideDistance + insideDistance;
        }


        public (SimpleMesh ClippedMesh, List<Face> UsedFaces) ClipMeshToBox(SimpleMesh inputMesh)
        {
            SimpleMesh outputMesh = new SimpleMesh();
            Dictionary<int, int> vertexMap = new Dictionary<int, int>();
            HashSet<Face> usedFaces = new HashSet<Face>();

            List<Face> boxFaces = this.GetFaces();

            foreach (var line in inputMesh.Lines)
            {
                Vector3 start = inputMesh.Vertices[line.StartIndex];
                Vector3 end = inputMesh.Vertices[line.EndIndex];

                float dStart = (float)this.SignedDistance(start);
                float dEnd = (float)this.SignedDistance(end);

                // Fast rejection: both points outside
                if (dStart > 0 && dEnd > 0)
                {
                    List<Vector3> clipped = new List<Vector3>();

                    foreach (var face in boxFaces)
                    {
                        if (TryIntersectFace(start, end, face, out Vector3 point))
                        {
                            clipped.Add(point);
                            usedFaces.Add(face);
                            if (clipped.Count == 2) break;
                        }
                    }

                    if (clipped.Count == 2)
                    {
                        int i1 = outputMesh.Vertices.Count;
                        outputMesh.Vertices.Add(clipped[0]);

                        int i2 = outputMesh.Vertices.Count;
                        outputMesh.Vertices.Add(clipped[1]);

                        outputMesh.Lines.Add(new SimpleLine(i1, i2));
                    }

                    continue;
                }

                // Fully inside
                if (dStart <= 0 && dEnd <= 0)
                {
                    int newStart = GetOrAddVertex(outputMesh, vertexMap, line.StartIndex, start);
                    int newEnd = GetOrAddVertex(outputMesh, vertexMap, line.EndIndex, end);
                    outputMesh.Lines.Add(new SimpleLine(newStart, newEnd));
                    continue;
                }

                // One point inside, one outside — clip
                List<Vector3> clippedPoints = new List<Vector3>();

                foreach (var face in boxFaces)
                {
                    if (TryIntersectFace(start, end, face, out Vector3 point))
                    {
                        clippedPoints.Add(point);
                        usedFaces.Add(face);
                        if (clippedPoints.Count == 2) break;
                    }
                }

                if (clippedPoints.Count == 1)
                {
                    int insideIndex = dStart <= 0
                        ? GetOrAddVertex(outputMesh, vertexMap, line.StartIndex, start)
                        : GetOrAddVertex(outputMesh, vertexMap, line.EndIndex, end);

                    int clippedIndex = outputMesh.Vertices.Count;
                    outputMesh.Vertices.Add(clippedPoints[0]);

                    outputMesh.Lines.Add(new SimpleLine(insideIndex, clippedIndex));
                }
                else if (clippedPoints.Count == 2)
                {
                    int i1 = outputMesh.Vertices.Count;
                    outputMesh.Vertices.Add(clippedPoints[0]);

                    int i2 = outputMesh.Vertices.Count;
                    outputMesh.Vertices.Add(clippedPoints[1]);

                    outputMesh.Lines.Add(new SimpleLine(i1, i2));
                }
            }

            return (outputMesh, usedFaces.ToList());
        }

        private bool TryIntersectFace(Vector3 start, Vector3 end, Face face, out Vector3 intersection)
        {
            Vector3 direction = end - start;
            float denom = Vector3.Dot(face.Normal, direction);

            if (Math.Abs(denom) < 1e-6f)
            {
                intersection = default;
                return false; // Parallel
            }

            float t = -(Vector3.Dot(face.Normal, start) + face.D) / denom;

            if (t < 0 || t > 1)
            {
                intersection = default;
                return false; // Outside segment
            }

            intersection = start + t * direction;
            return face.IsPointInside(intersection);
        }

        private static int GetOrAddVertex(SimpleMesh mesh, Dictionary<int, int> map, int originalIndex, Vector3 vertex)
        {
            if (map.TryGetValue(originalIndex, out int newIndex))
                return newIndex;

            newIndex = mesh.Vertices.Count;
            mesh.Vertices.Add(vertex);
            map[originalIndex] = newIndex;
            return newIndex;
        }

        public override string ToString()
        {
            string sizeStr = $"Size: ({Size.X:0.###}, {Size.Y:0.###}, {Size.Z:0.###})";
            string translationStr = $"Translation Matrix: [{Transform.M11:0.###}, {Transform.M12:0.###}, {Transform.M13:0.###}, {Transform.M14:0.###}] " +
                                    $"[{Transform.M21:0.###}, {Transform.M22:0.###}, {Transform.M23:0.###}, {Transform.M24:0.###}] " +
                                    $"[{Transform.M31:0.###}, {Transform.M32:0.###}, {Transform.M33:0.###}, {Transform.M34:0.###}] " +
                                    $"[{Transform.M41:0.###}, {Transform.M42:0.###}, {Transform.M43:0.###}, {Transform.M44:0.###}]";

            return $"{sizeStr} - {translationStr}";
        }

        public BoundingBox3D GetBoundingBox()
        {
            // Compute 8 corners of the box in local space
            Vector3 halfSize = Size * 0.5f;
            Vector3[] corners = new Vector3[8];
            int i = 0;
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                        corners[i++] = Vector3.Transform(new Vector3(x, y, z) * halfSize, Transform);

            // Compute bounding box in world space
            Vector3 min = corners[0];
            Vector3 max = corners[0];
            foreach (var c in corners)
            {
                min = Vector3.Min(min, c);
                max = Vector3.Max(max, c);
            }

            return new BoundingBox3D(min, max);
        }

        public List<(Vector3 Start, Vector3 End)> GetEdges()
        {
            Vector3 halfSize = Size * 0.5f;

            Vector3[] localCorners = new Vector3[]
            {
                new Vector3(-halfSize.X, -halfSize.Y, -halfSize.Z),
                new Vector3( halfSize.X, -halfSize.Y, -halfSize.Z),
                new Vector3( halfSize.X,  halfSize.Y, -halfSize.Z),
                new Vector3(-halfSize.X,  halfSize.Y, -halfSize.Z),
                new Vector3(-halfSize.X, -halfSize.Y,  halfSize.Z),
                new Vector3( halfSize.X, -halfSize.Y,  halfSize.Z),
                new Vector3( halfSize.X,  halfSize.Y,  halfSize.Z),
                new Vector3(-halfSize.X,  halfSize.Y,  halfSize.Z),
            };

            int[,] edgeIndices = new int[,]
            {
                {0,1},{1,2},{2,3},{3,0},
                {4,5},{5,6},{6,7},{7,4},
                {0,4},{1,5},{2,6},{3,7}
            };

            List<(Vector3 Start, Vector3 End)> edges = new List<(Vector3, Vector3)>();

            for (int i = 0; i < edgeIndices.GetLength(0); i++)
            {
                Vector3 start = Vector3.Transform(localCorners[edgeIndices[i, 0]], Transform);
                Vector3 end = Vector3.Transform(localCorners[edgeIndices[i, 1]], Transform);
                edges.Add((start, end));
            }

            return edges;
        }


        public List<Face> GetFaces()
        {
            Vector3 halfSize = Size * 0.5f;
            Vector3 boxCenter = Vector3.Transform(Vector3.Zero, Transform); // World-space center of the box

            var faceDefs = new (Vector3 normal, Vector3 axisU, Vector3 axisV, Vector2 halfExtents)[]
            {
                ( Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new Vector2(halfSize.Y, halfSize.Z)),
                (-Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new Vector2(halfSize.Y, halfSize.Z)),
                ( Vector3.UnitY, Vector3.UnitX, Vector3.UnitZ, new Vector2(halfSize.X, halfSize.Z)),
                (-Vector3.UnitY, Vector3.UnitX, Vector3.UnitZ, new Vector2(halfSize.X, halfSize.Z)),
                ( Vector3.UnitZ, Vector3.UnitX, Vector3.UnitY, new Vector2(halfSize.X, halfSize.Y)),
                (-Vector3.UnitZ, Vector3.UnitX, Vector3.UnitY, new Vector2(halfSize.X, halfSize.Y)),
            };

            var faces = new List<Face>();

            foreach (var (localNormal, axisU, axisV, halfExtents) in faceDefs)
            {
                Vector3 localCenter = localNormal * halfSize;
                Vector3 worldCenter = Vector3.Transform(localCenter, Transform);
                Vector3 worldNormal = Vector3.TransformNormal(localNormal, Transform);
                Vector3 worldAxisU = Vector3.TransformNormal(axisU, Transform);
                Vector3 worldAxisV = Vector3.TransformNormal(axisV, Transform);

                // Ensure normal points away from box center
                Vector3 toFace = Vector3.Normalize(worldCenter - boxCenter);
                if (Vector3.Dot(worldNormal, toFace) < 0)
                {
                    worldNormal = -worldNormal;
                }

                faces.Add(new Face(worldNormal, worldCenter, worldAxisU, worldAxisV, halfExtents));
            }

            return faces;
        }


        public bool ContainsPoint(Vector3 point)
        {
            Matrix4x4.Invert(Transform, out Matrix4x4 inverse);
            Vector3 local = Vector3.Transform(point, inverse);
            Vector3 half = Size * 0.5f;
            return Math.Abs(local.X) <= half.X &&
                   Math.Abs(local.Y) <= half.Y &&
                   Math.Abs(local.Z) <= half.Z;
        }

        public List<Vector3> GetCorners()
        {
            Vector3 half = Size * 0.5f;
            Vector3[] local = new Vector3[]
            {
                new Vector3(-half.X, -half.Y, -half.Z),
                new Vector3( half.X, -half.Y, -half.Z),
                new Vector3( half.X,  half.Y, -half.Z),
                new Vector3(-half.X,  half.Y, -half.Z),
                new Vector3(-half.X, -half.Y,  half.Z),
                new Vector3( half.X, -half.Y,  half.Z),
                new Vector3( half.X,  half.Y,  half.Z),
                new Vector3(-half.X,  half.Y,  half.Z),
            };
            var self = this; // capture 'this' safely
            var localPoints = local.Select(p => Vector3.Transform(p, self.Transform)).ToList();

            return localPoints;
        }

    }

    public struct Face
    {
        public Vector3 Normal { get; }
        public float D { get; }
        public Vector3 Center { get; }
        public Vector3 AxisU { get; }
        public Vector3 AxisV { get; }
        public Vector2 HalfExtents { get; }

        public Face(Vector3 normal, Vector3 pointOnPlane, Vector3 axisU, Vector3 axisV, Vector2 halfExtents)
        {
            Normal = Vector3.Normalize(normal);
            D = -Vector3.Dot(Normal, pointOnPlane);
            Center = pointOnPlane;
            AxisU = Vector3.Normalize(axisU);
            AxisV = Vector3.Normalize(axisV);
            HalfExtents = halfExtents;
        }

        public bool IsPointInside(Vector3 point)
        {
            Vector3 local = point - Center;
            float u = Vector3.Dot(local, AxisU);
            float v = Vector3.Dot(local, AxisV);
            return Math.Abs(u) <= HalfExtents.X && Math.Abs(v) <= HalfExtents.Y;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is Face)) return false;
            Face other = (Face)obj;

            return Normal.Equals(other.Normal)
                && D.Equals(other.D)
                && AxisU.Equals(other.AxisU)
                && AxisV.Equals(other.AxisV)
                && HalfExtents.Equals(other.HalfExtents);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + Normal.GetHashCode();
                hash = hash * 23 + D.GetHashCode();
                hash = hash * 23 + AxisU.GetHashCode();
                hash = hash * 23 + AxisV.GetHashCode();
                hash = hash * 23 + HalfExtents.GetHashCode();
                return hash;
            }
        }
    }




    public class ShapeUtils
    {


        public static Box IntersectBoxes(Box a, Box b)
        {
            var points = new List<Vector3>();

            // 1. Intersect edges of A with faces of B
            foreach (var edge in a.GetEdges())
            {
                foreach (var face in b.GetFaces())
                {

                    if (IntersectSegmentWithFace(edge.Start, edge.End, face, out Vector3 p))
                    {
                        Console.WriteLine($"Intersection at: {p}");
                        points.Add(p);
                    }

                }
            }

            // 2. Intersect edges of B with faces of A
            foreach (var edge in b.GetEdges())
            {
                foreach (var face in a.GetFaces())
                {

                    if (IntersectSegmentWithFace(edge.Start, edge.End, face, out Vector3 p))
                    {
                        Console.WriteLine($"Intersection at: {p}");
                        points.Add(p);
                    }

                }
            }

            // 3. Include corners of A inside B
            foreach (var corner in a.GetCorners())
            {
                if (b.ContainsPoint(corner))
                    points.Add(corner);
            }

            // 4. Include corners of B inside A
            foreach (var corner in b.GetCorners())
            {
                if (a.ContainsPoint(corner))
                    points.Add(corner);
            }

            if (points.Count == 0)
                return default; // No intersection

            // 5. Transform points into A's local space
            Matrix4x4.Invert(a.Transform, out Matrix4x4 aInv);
            var localPoints = points.Select(p => Vector3.Transform(p, aInv)).ToList();

            // 6. Fit a new box in A's local space
            Vector3 min = localPoints[0], max = localPoints[0];
            foreach (var p in localPoints)
            {
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }

            Vector3 newSize = max - min;
            Vector3 centerLocal = (min + max) * 0.5f;
            Vector3 centerWorld = Vector3.Transform(centerLocal, a.Transform);

            // 7. Return new box in A's orientation
            Matrix4x4 newTransform = a.Transform;
            newTransform.Translation = centerWorld;

            return new Box(newSize, newTransform);
        }
        public static bool IntersectSegmentWithFace(Vector3 a, Vector3 b, Face face, out Vector3 intersection)
        {
            Vector3 ab = b - a;
            float denom = Vector3.Dot(face.Normal, ab);

            if (Math.Abs(denom) < 1e-6f)
            {
                intersection = default;
                return false;
            }

            float t = -(Vector3.Dot(face.Normal, a) + face.D) / denom;

            if (t < 0 || t > 1)
            {
                intersection = default;
                return false;
            }

            intersection = a + t * ab;

            return face.IsPointInside(intersection);
        }


        public static List<List<Vector3>> GetBoxFaces(Box box)
        {
            Vector3 half = box.Size * 0.5f;

            // Local corners
            Vector3[] corners = new Vector3[]
            {
                new Vector3(-half.X, -half.Y, -half.Z),
                new Vector3( half.X, -half.Y, -half.Z),
                new Vector3( half.X,  half.Y, -half.Z),
                new Vector3(-half.X,  half.Y, -half.Z),
                new Vector3(-half.X, -half.Y,  half.Z),
                new Vector3( half.X, -half.Y,  half.Z),
                new Vector3( half.X,  half.Y,  half.Z),
                new Vector3(-half.X,  half.Y,  half.Z)
            };

            // Transform to world space
            for (int i = 0; i < corners.Length; i++)
                corners[i] = Vector3.Transform(corners[i], box.Transform);

            // Define faces using corner indices
            int[][] faceIndices = new int[][]
            {
                new[] {0, 1, 2, 3}, // Bottom
                new[] {4, 5, 6, 7}, // Top
                new[] {0, 1, 5, 4}, // Front
                new[] {2, 3, 7, 6}, // Back
                new[] {1, 2, 6, 5}, // Right
                new[] {0, 3, 7, 4}, // Left
            };

            return faceIndices.Select(face => face.Select(i => corners[i]).ToList()).ToList();
        }

        public static List<Vector3> ClipPolygonAgainstAABB(List<Vector3> polygon, Vector3 min, Vector3 max)
        {
            List<Vector3> output = new List<Vector3>(polygon);

            // Clip against each axis
            foreach (var axis in new[] { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ })
            {
                output = ClipAgainstPlane(output, axis, Vector3.Dot(axis, min), true);
                output = ClipAgainstPlane(output, axis, Vector3.Dot(axis, max), false);
            }

            return output;
        }

        private static List<Vector3> ClipAgainstPlane(List<Vector3> poly, Vector3 normal, float d, bool keepInside)
        {
            List<Vector3> result = new List<Vector3>();
            if (poly.Count == 0) return result;

            Vector3 prev = poly[1];
            bool prevInside = Vector3.Dot(normal, prev) <= d == keepInside;

            foreach (var curr in poly)
            {
                bool currInside = Vector3.Dot(normal, curr) <= d == keepInside;

                if (currInside)
                {
                    if (!prevInside)
                    {
                        Vector3 dir = curr - prev;
                        float t = (d - Vector3.Dot(normal, prev)) / Vector3.Dot(normal, dir);
                        result.Add(prev + t * dir);
                    }
                    result.Add(curr);
                }
                else if (prevInside)
                {
                    Vector3 dir = curr - prev;
                    float t = (d - Vector3.Dot(normal, prev)) / Vector3.Dot(normal, dir);
                    result.Add(prev + t * dir);
                }

                prev = curr;
                prevInside = currInside;
            }

            return result;
        }

        public static IShape Union(IShape a, IShape b)
        {
            return new UnionShape(a, b);
        }
        public static IShape Intersection(IShape a, IShape b)
        {
            return new IntersectionShape(a, b);
        }
        public static IShape Subtraction(IShape a, IShape b)
        {
            return new SubtractionShape(a, b);
        }

        public static Matrix4x4 CreateTransformFromOriginAndDirection(Vector3 origin, Vector3 direction, Vector3 upHint)
        {
            Vector3 forward = Vector3.Normalize(direction);

            // If direction is zero, fallback to identity
            if (forward.LengthSquared() < 1e-6f)
                return Matrix4x4.Identity;

            // If upHint is parallel or nearly parallel to forward, choose a new upHint
            if (Math.Abs(Vector3.Dot(forward, Vector3.Normalize(upHint))) > 0.999f)
                upHint = Math.Abs(forward.Y) < 0.999f ? Vector3.UnitY : Vector3.UnitX;

            Vector3 right = Vector3.Normalize(Vector3.Cross(upHint, forward));
            Vector3 up = Vector3.Cross(forward, right);

            return new Matrix4x4(
                right.X, up.X, forward.X, 0,
                right.Y, up.Y, forward.Y, 0,
                right.Z, up.Z, forward.Z, 0,
                origin.X, origin.Y, origin.Z, 1
            );
        }

        public static float Clamp(float value, float min, float max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

    }



    public class UnionShape : IShape
    {
        private readonly IShape a, b;

        public UnionShape(IShape a, IShape b)
        {
            this.a = a;
            this.b = b;
        }

        public double SignedDistance(Vector3 point)
        {
            return Math.Min(a.SignedDistance(point), b.SignedDistance(point));
        }

        public BoundingBox3D GetBoundingBox()
        {
            return BoundingBox3D.Union(a.GetBoundingBox(), b.GetBoundingBox());
        }
    }

    public class IntersectionShape : IShape
    {
        private readonly IShape a, b;

        public IntersectionShape(IShape a, IShape b)
        {
            this.a = a;
            this.b = b;
        }

        public double SignedDistance(Vector3 point)
        {
            return Math.Max(a.SignedDistance(point), b.SignedDistance(point));
        }

        public BoundingBox3D GetBoundingBox()
        {
            return BoundingBox3D.Intersect(a.GetBoundingBox(), b.GetBoundingBox());
        }
    }
    public class SubtractionShape : IShape
    {
        private readonly IShape a, b;

        public SubtractionShape(IShape a, IShape b)
        {
            this.a = a;
            this.b = b;
        }

        public double SignedDistance(Vector3 point)
        {
            return Math.Max(a.SignedDistance(point), -b.SignedDistance(point));
        }

        public BoundingBox3D GetBoundingBox()
        {
            return a.GetBoundingBox(); // Conservative
        }
    }

    public struct BoundingBox3D
    {
        public bool IsEmpty => Min == Max;

        public Vector3 Min { get; }
        public Vector3 Max { get; }

        public BoundingBox3D(Vector3 min, Vector3 max)
        {
            Min = min;
            Max = max;
        }

        public static BoundingBox3D Union(BoundingBox3D a, BoundingBox3D b)
        {
            return new BoundingBox3D(
                Vector3.Min(a.Min, b.Min),
                Vector3.Max(a.Max, b.Max)
            );
        }

        public static BoundingBox3D Intersect(BoundingBox3D a, BoundingBox3D b)
        {
            Vector3 min = Vector3.Max(a.Min, b.Min);
            Vector3 max = Vector3.Min(a.Max, b.Max);

            if (min.X > max.X || min.Y > max.Y || min.Z > max.Z)
                return new BoundingBox3D(Vector3.Zero, Vector3.Zero);

            return new BoundingBox3D(min, max);
        }

        public static List<BoundingBox3D> Subtract(BoundingBox3D a, BoundingBox3D b)
        {
            var i = Intersect(a, b);
            if (i.Size == Vector3.Zero)
                return new List<BoundingBox3D> { a }; // No overlap

            var boxes = new List<BoundingBox3D>();

            // Left slab
            if (a.Min.X < i.Min.X)
                boxes.Add(new BoundingBox3D(
                    new Vector3(a.Min.X, a.Min.Y, a.Min.Z),
                    new Vector3(i.Min.X, a.Max.Y, a.Max.Z)));

            // Right slab
            if (i.Max.X < a.Max.X)
                boxes.Add(new BoundingBox3D(
                    new Vector3(i.Max.X, a.Min.Y, a.Min.Z),
                    new Vector3(a.Max.X, a.Max.Y, a.Max.Z)));

            // Bottom slab
            if (a.Min.Y < i.Min.Y)
                boxes.Add(new BoundingBox3D(
                    new Vector3(i.Min.X, a.Min.Y, a.Min.Z),
                    new Vector3(i.Max.X, i.Min.Y, a.Max.Z)));

            // Top slab
            if (i.Max.Y < a.Max.Y)
                boxes.Add(new BoundingBox3D(
                    new Vector3(i.Min.X, i.Max.Y, a.Min.Z),
                    new Vector3(i.Max.X, a.Max.Y, a.Max.Z)));

            // Front slab
            if (a.Min.Z < i.Min.Z)
                boxes.Add(new BoundingBox3D(
                    new Vector3(i.Min.X, i.Min.Y, a.Min.Z),
                    new Vector3(i.Max.X, i.Max.Y, i.Min.Z)));

            // Back slab
            if (i.Max.Z < a.Max.Z)
                boxes.Add(new BoundingBox3D(
                    new Vector3(i.Min.X, i.Min.Y, i.Max.Z),
                    new Vector3(i.Max.X, i.Max.Y, a.Max.Z)));

            return boxes;
        }

        public Vector3 Size => Max - Min;
        public Vector3 Center => (Min + Max) * 0.5f;
    }
}

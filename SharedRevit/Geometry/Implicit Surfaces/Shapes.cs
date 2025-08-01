
using Autodesk.Revit.DB;
using MathNet.Numerics.LinearAlgebra;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Net.NetworkInformation;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SharedRevit.Geometry.Implicit_Surfaces
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

        public SimpleMesh IntersectLines(SimpleMesh mesh)
        {
            SimpleMesh result = new SimpleMesh();

            foreach (var line in mesh.Lines)
            {
                Vector3 p0 = mesh.Vertices[line.StartIndex];
                Vector3 p1 = mesh.Vertices[line.EndIndex];

                if (LineIntersectsBox(p0, p1, out Vector3 intersection1, out Vector3? intersection2))
                {
                    result.Vertices.Add(intersection1);
                    if (intersection2.HasValue)
                        result.Vertices.Add(intersection2.Value);
                }
            }

            return result;
        }

        private static float GetComponent(Vector3 v, int index)
        {
            return index switch
            {
                0 => v.X,
                1 => v.Y,
                2 => v.Z,
                _ => throw new ArgumentOutOfRangeException(nameof(index))
            };
        }

        private bool LineIntersectsBox(Vector3 p0, Vector3 p1, out Vector3 intersection1, out Vector3? intersection2)
        {
            // Transform line into box local space
            Matrix4x4.Invert(this.Transform, out Matrix4x4 inverse);
            Vector3 l0 = Vector3.Transform(p0, inverse);
            Vector3 l1 = Vector3.Transform(p1, inverse);

            Vector3 dir = l1 - l0;
            Vector3 min = -Size * 0.5f;
            Vector3 max = Size * 0.5f;

            float tmin = 0, tmax = 1;

            for (int i = 0; i < 3; i++)
            {
                float start = GetComponent(l0, i);
                float delta = GetComponent(dir, i);
                float minVal = GetComponent(min, i);
                float maxVal = GetComponent(max, i);

                if (Math.Abs(delta) < 1e-6f)
                {
                    if (start < minVal || start > maxVal)
                    {
                        intersection1 = default;
                        intersection2 = null;
                        return false;
                    }
                }
                else
                {
                    float t1 = (minVal - start) / delta;
                    float t2 = (maxVal - start) / delta;

                    if (t1 > t2) (t1, t2) = (t2, t1);

                    tmin = Math.Max(tmin, t1);
                    tmax = Math.Min(tmax, t2);

                    if (tmin > tmax)
                    {
                        intersection1 = default;
                        intersection2 = null;
                        return false;
                    }
                }
            }

            intersection1 = Vector3.Transform(l0 + tmin * dir, this.Transform);
            intersection2 = tmax > tmin ? Vector3.Transform(l0 + tmax * dir, this.Transform) : (Vector3?)null;
            return true;
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
    }

    public class ShapeUtils
    {

        public static Box? Intersection (Box a, Box b, int resolution = 3)
        {

            List<Vector3> points = new();

            // Sample points from both boxes
            points.AddRange(SampleBoxPoints(a, resolution));
            points.AddRange(SampleBoxPoints(b, resolution));

            // Keep only points inside both boxes
            var inside = points.Where(p => a.SignedDistance(p) <= 0 && b.SignedDistance(p) <= 0).ToList();

            if (inside.Count < 3)
                return null;

            // Build a SimpleMesh from the intersection points
            SimpleMesh mesh = new SimpleMesh();
            mesh.Vertices.AddRange(inside);

            // Use your existing PCA-based box fitting
            return (Box)BoundingShape.SimpleMeshToIShape(mesh);
        }

        private static IEnumerable<Vector3> SampleBoxPoints(Box box, int resolution)
        {
            List<Vector3> points = new();
            Vector3 half = box.Size * 0.5f;

            for (int xi = -resolution; xi <= resolution; xi++)
                for (int yi = -resolution; yi <= resolution; yi++)
                    for (int zi = -resolution; zi <= resolution; zi++)
                    {
                        Vector3 local = new Vector3(
                            xi / (float)resolution * half.X,
                            yi / (float)resolution * half.Y,
                            zi / (float)resolution * half.Z
                        );

                        Vector3 world = Vector3.Transform(local, box.Transform);
                        points.Add(world);
                    }

            return points;
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

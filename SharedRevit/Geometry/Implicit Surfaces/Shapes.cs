
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Net.NetworkInformation;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using MathNet.Numerics.LinearAlgebra;

namespace SharedRevit.Geometry.Implicit_Surfaces
{
    public interface IShape
    {
        Vector3 Gradient(Vector3 point);

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

        public Vector3 Gradient(Vector3 point)
        {
            Matrix4x4.Invert(Transform, out Matrix4x4 inverse);
            Vector3 localPoint = Vector3.Transform(point, inverse);
            Vector3 halfSize = Size * 0.5f;
            Vector3 localGradient = Vector3.Normalize(localPoint);

            // Transform gradient back to world space
            Vector3 worldGradient = Vector3.TransformNormal(localGradient, Transform);
            return Vector3.Normalize(worldGradient);
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

    public struct Capsile : IShape
    {
        public Vector3 A; // Center of one hemisphere
        public Vector3 B; // Center of the other hemisphere
        public double Radius;

        public Capsile(Vector3 a, Vector3 b, double radius)
        {
            A = a;
            B = b;
            Radius = radius;
        }

        public double SignedDistance(Vector3 point)
        {
            Vector3 pa = point - A;
            Vector3 ba = B - A;
            float h = (float)ShapeUtils.Clamp(Vector3.Dot(pa, ba) / Vector3.Dot(ba, ba), 0, 1);
            Vector3 closest = A + h * ba;
            return (point - closest).Length() - Radius;
        }

        public Vector3 Gradient(Vector3 point)
        {
            Vector3 pa = point - A;
            Vector3 ba = B - A;
            float h = (float)ShapeUtils.Clamp(Vector3.Dot(pa, ba) / Vector3.Dot(ba, ba), 0, 1);
            Vector3 closest = A + h * ba;
            Vector3 grad = Vector3.Normalize(point - closest);
            return grad;
        }

        public BoundingBox3D GetBoundingBox()
        {
            Vector3 min = Vector3.Min(A, B) - new Vector3((float)Radius);
            Vector3 max = Vector3.Max(A, B) + new Vector3((float)Radius);
            return new BoundingBox3D(min, max);
        }
    }

    public struct Cylinder : IShape
    {
        public Vector3 A; // Base center
        public Vector3 B; // Top center
        public float Radius;

        public Cylinder(Vector3 a, Vector3 b, float radius)
        {
            A = a;
            B = b;
            Radius = radius;
        }

        public double SignedDistance(Vector3 point)
{
    Vector3 pa = point - A;
    Vector3 ba = B - A;
    float baLength = ba.Length();
    Vector3 baNorm = ba / baLength;

    float h = Vector3.Dot(pa, baNorm);
    Vector3 radial = pa - baNorm * h;

    float clampedH = ShapeUtils.Clamp(h, 0, baLength);
    Vector3 closest = A + baNorm * clampedH;
    float radialDist = (point - closest).Length();

    if (h < 0)
        return Math.Sqrt(radial.LengthSquared() + h * h) - Radius;
    else if (h > baLength)
        return Math.Sqrt(radial.LengthSquared() + (h - baLength) * (h - baLength)) - Radius;
    else
        return radial.Length() - Radius;
}


        public Vector3 Gradient(Vector3 point)
        {
            // Approximate gradient using central differences
            float eps = 1e-4f;
            float dx = (float)SignedDistance(point + new Vector3(eps, 0, 0)) - (float)SignedDistance(point - new Vector3(eps, 0, 0));
            float dy = (float)SignedDistance(point + new Vector3(0, eps, 0)) - (float)SignedDistance(point - new Vector3(0, eps, 0));
            float dz = (float)SignedDistance(point + new Vector3(0, 0, eps)) - (float)SignedDistance(point - new Vector3(0, 0, eps));
            return Vector3.Normalize(new Vector3(dx, dy, dz));
        }


        public BoundingBox3D GetBoundingBox()
        {
            Vector3 axis = Vector3.Normalize(B - A);
            Vector3 up = Math.Abs(Vector3.Dot(axis, Vector3.UnitY)) > 0.99f ? Vector3.UnitX : Vector3.UnitY;
            Vector3 right = Vector3.Normalize(Vector3.Cross(axis, up));
            Vector3 forward = Vector3.Cross(right, axis);

            Vector3[] offsets = new[]
            {
                right * Radius,
                -right * Radius,
                forward * Radius,
                -forward * Radius
            };

            Vector3 min = Vector3.Min(A, B);
            Vector3 max = Vector3.Max(A, B);

            foreach (var offset in offsets)
            {
                min = Vector3.Min(min, A + offset);
                min = Vector3.Min(min, B + offset);
                max = Vector3.Max(max, A + offset);
                max = Vector3.Max(max, B + offset);
            }

            return new BoundingBox3D(min, max);
        }

    }


    public class ShapeUtils
    {
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

        public Vector3 Gradient(Vector3 point)
        {
            return a.SignedDistance(point) < b.SignedDistance(point)
                ? a.Gradient(point)
                : b.Gradient(point);
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

        public Vector3 Gradient(Vector3 point)
        {
            return a.SignedDistance(point) > b.SignedDistance(point)
                ? a.Gradient(point)
                : b.Gradient(point);
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

        public Vector3 Gradient(Vector3 point)
        {
            return a.SignedDistance(point) > -b.SignedDistance(point)
                ? a.Gradient(point)
                : -b.Gradient(point);
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

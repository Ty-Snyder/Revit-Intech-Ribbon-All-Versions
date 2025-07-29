
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SharedRevit.Geometry.Implicit_Surfaces
{
    public interface IShape
    {
        Vector3 Gradient(Vector3 point);

        double SignedDistance(Vector3 point);

        // Optional: Bounding box for acceleration structures
        BoundingBox3D GetBoundingBox();

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

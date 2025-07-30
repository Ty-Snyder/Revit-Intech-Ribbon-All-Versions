using Autodesk.Revit.DB;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Complex;
using SharedRevit.Geometry.Implicit_Surfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace SharedRevit.Geometry
{
    internal class BoundingShape
    {
        public static IShape SimpleMeshToIShape(SimpleMesh mesh)
        {
            var (axes, translation) = ComputePrincipalAxes(mesh);
            return ShapeOptimize(axes, translation, mesh);
        }

        public static (Vector3[] Axes, Vector3 Translation) ComputePrincipalAxes(SimpleMesh mesh)
        {
            if (mesh.Vertices == null || mesh.Vertices.Count < 3)
                throw new ArgumentException("Mesh must contain at least 3 vertices.");

            // Convert List<Vector3> to matrix rows
            var rows = mesh.Vertices.Select(v => new double[] { v.X, v.Y, v.Z });
            var data = Matrix<double>.Build.DenseOfRows(mesh.Vertices.Count, 3, rows);

            // Compute mean (centroid)
            var mean = data.ColumnSums() / data.RowCount;
            var translation = new Vector3((float)mean[0], (float)mean[1], (float)mean[2]);

            // Center the data
            for (int i = 0; i < data.RowCount; i++)
            {
                var centeredRow = data.Row(i) - mean;
                data.SetRow(i, centeredRow);
            }

            // Covariance matrix
            var covariance = data.TransposeThisAndMultiply(data) / (data.RowCount - 1);

            // Eigen decomposition
            var evd = covariance.Evd(Symmetricity.Symmetric);

            // Sort eigenvectors by eigenvalues descending
            var eigenPairs = evd.EigenValues
                .Select((val, idx) => new { Value = val.Real, Vector = evd.EigenVectors.Column(idx) })
                .OrderByDescending(e => e.Value)
                .ToArray();

            // Convert to Vector3
            var axes = eigenPairs.Select(e => new Vector3(
                (float)e.Vector[0],
                (float)e.Vector[1],
                (float)e.Vector[2]
            )).ToArray();

            return (axes, translation);
        }

        public static IShape ShapeOptimize(Vector3[]  axes, Vector3 translation, SimpleMesh mesh)
        {
            Matrix4x4 rotation = new Matrix4x4(
                    axes[0].X, axes[1].X, axes[2].X, 0,
                    axes[0].Y, axes[1].Y, axes[2].Y, 0,
                    axes[0].Z, axes[1].Z, axes[2].Z, 0,
                    0, 0, 0, 1
                );
            Matrix4x4 translationMatrix = Matrix4x4.CreateTranslation(-translation);

            Matrix4x4 transform = translationMatrix * rotation;

            Vector3 min = new Vector3(float.MaxValue);
            Vector3 max = new Vector3(float.MinValue);

            Matrix4x4 inverseTransform;
            Matrix4x4.Invert(transform, out inverseTransform);

            float capsuleMinX = float.MaxValue;
            float capsuleMaxX = float.MinValue;
            float maxRadiusSq = 0f;


            foreach (Vector3 vert in mesh.Vertices)
            {
                // Transform vertex to PCA space
                Vector3 v = Vector3.Transform(vert, transform);

                // Update bounding box
                min = Vector3.Min(min, v);
                max = Vector3.Max(max, v);

                // Track X extents (along principal axis)
                if (v.X < capsuleMinX) capsuleMinX = v.X;
                if (v.X > capsuleMaxX) capsuleMaxX = v.X;

                // Track max radius in Y-Z plane
                float radiusSq = v.Y * v.Y + v.Z * v.Z;
                if (radiusSq > maxRadiusSq) maxRadiusSq = radiusSq;


            }

            // Bounding box volume
            Vector3 boxSize = max - min;
            float boxVolume = boxSize.X * boxSize.Y * boxSize.Z;

            //float capsuleHeight = capsuleMaxX - capsuleMinX;
            //float capsuleRadius = (float)Math.Sqrt(maxRadiusSq);
            //float capCylinderHeight = Math.Max(0f, capsuleHeight - 2f * capsuleRadius);
            //float capsuleVolume = (float)Math.PI * capsuleRadius * capsuleRadius * capCylinderHeight + // cylinder
            //    (4f / 3f) * (float)Math.PI * (float)Math.Pow(capsuleRadius, 3);         // 2 hemispheres

            float cylinderRadius = (float)Math.Sqrt(maxRadiusSq);
            float height = boxSize.X;
            float cylinderVolume = (float)Math.PI * cylinderRadius * cylinderRadius * height;

            float minVolume = Math.Min(boxVolume, cylinderVolume); //Math.Min(cylinderVolume, capsuleVolume));
            switch (minVolume)
            {
                case float v when v == boxVolume:
                    // Box is the best fit
                    return new Box(boxSize, transform);
                case float v when v == cylinderVolume:
                    // Cylinder is the best fit
                    Vector3 cylocalA = new Vector3(min.X, 0, 0);
                    Vector3 cylocalB = new Vector3(max.X, 0, 0);
                    Vector3 cyworldA = Vector3.Transform(cylocalA, inverseTransform);
                    Vector3 cyworldB = Vector3.Transform(cylocalB, inverseTransform);
                    return new Cylinder(cyworldA,cyworldB,cylinderRadius);
                //case float v when v == capsuleVolume:
                //    // Capsule is the best fit
                //    Vector3 localA = new Vector3(capsuleMinX, 0, 0);
                //    Vector3 localB = new Vector3(capsuleMaxX, 0, 0);
                //    Vector3 worldA = Vector3.Transform(localA, inverseTransform);
                //    Vector3 worldB = Vector3.Transform(localB, inverseTransform);
                //    return new Capsile(worldA, worldB, capsuleRadius);
            }

            return null;
        }

    }
}

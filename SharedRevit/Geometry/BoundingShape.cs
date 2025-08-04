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

            var axes = eigenPairs.Select(e =>
            {
                var v = e.Vector;
                var norm = Math.Sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2]);
                return new Vector3(
                    (float)(v[0] / norm),
                    (float)(v[1] / norm),
                    (float)(v[2] / norm)
                );
            }).ToArray();

            return (axes, translation);
        }

        public static IShape ShapeOptimize(Vector3[]  axes, Vector3 translation, SimpleMesh mesh)
        {
            Matrix4x4 rotation = new Matrix4x4(
                axes[0].X, axes[0].Y, axes[0].Z, 0,
                axes[1].X, axes[1].Y, axes[1].Z, 0,
                axes[2].X, axes[2].Y, axes[2].Z, 0,
                0, 0, 0, 1
            );

            Matrix4x4 translationMatrix = Matrix4x4.CreateTranslation(translation); // move to origin
            Matrix4x4 transform = rotation * translationMatrix;

            Vector3 min = new Vector3(float.MaxValue);
            Vector3 max = new Vector3(float.MinValue);

            Matrix4x4 inverseTransform;
            Matrix4x4.Invert(transform, out inverseTransform);


            foreach (Vector3 vert in mesh.Vertices)
            {
                // Transform vertex to PCA space
                Vector3 v = Vector3.Transform(vert, inverseTransform);

                // Update bounding box
                min = Vector3.Min(min, v);
                max = Vector3.Max(max, v);
            }
            
            Vector3 boxSize = max - min;
            Vector3 boxCenterLocal = (min + max) * 0.5f; // center in PCA space
            Vector3 boxCenterWorld = Vector3.Transform(boxCenterLocal, transform);
            Vector3 offset = translation - boxCenterWorld;
            Matrix4x4 correctedTranslation = Matrix4x4.CreateTranslation(translation - offset);
            Matrix4x4 correctedTransform = rotation * correctedTranslation;

            return new Box(boxSize, correctedTransform);
        }

    }
}

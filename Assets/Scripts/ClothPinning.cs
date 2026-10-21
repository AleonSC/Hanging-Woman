using System.Collections.Generic;
using UnityEngine;

namespace HangingWoman
{
    /// <summary>
    /// Sets how far each cloth vertex may stray from its skinned position, using values stored
    /// in the mesh's second UV channel (x = max distance in metres). 0 pins a vertex to the
    /// body (a waistband, a hair root); larger values let it hang and swing.
    ///
    /// Unity's cloth merges vertices that share a position and doesn't promise to keep mesh
    /// order, so each cloth particle takes the value of the nearest mesh vertex.
    /// </summary>
    [RequireComponent(typeof(Cloth))]
    public class ClothPinning : MonoBehaviour
    {
        void Start()
        {
            Apply(GetComponent<Cloth>(), GetComponent<SkinnedMeshRenderer>());
        }

        /// <returns>False if neither particles nor constraint slots were available yet.</returns>
        public static bool Apply(Cloth cloth, SkinnedMeshRenderer smr)
        {
            if (cloth == null || smr == null || smr.sharedMesh == null) return false;
            var mesh = smr.sharedMesh;
            var pins = new List<Vector2>();
            mesh.GetUVs(1, pins);
            var meshVerts = mesh.vertices;
            if (pins.Count != meshVerts.Length) return false;

            var particles = cloth.vertices;
            var coefficients = cloth.coefficients;
            if (coefficients == null || coefficients.Length == 0) return false;

            if (particles == null || particles.Length == 0)
            {
                // No particles yet: assume one constraint per distinct mesh position, in mesh order.
                var firstIndex = new Dictionary<Vector3, int>();
                var order = new List<int>();
                for (int j = 0; j < meshVerts.Length; j++)
                {
                    if (firstIndex.ContainsKey(meshVerts[j])) continue;
                    firstIndex[meshVerts[j]] = j;
                    order.Add(j);
                }
                if (order.Count != coefficients.Length) return false;
                for (int i = 0; i < coefficients.Length; i++)
                    coefficients[i].maxDistance = pins[order[i]].x;
                cloth.coefficients = coefficients;
                return true;
            }
            int count = Mathf.Min(particles.Length, coefficients.Length);

            // Particles should be in the renderer's local space; fall back to world if they don't line up.
            var local = MapToMesh(particles, count, meshVerts, null, out float localError);
            if (localError > 0.02f)
            {
                var world = MapToMesh(particles, count, meshVerts, cloth.transform, out float worldError);
                if (worldError < localError) local = world;
            }

            for (int i = 0; i < count; i++)
                coefficients[i].maxDistance = pins[local[i]].x;
            cloth.coefficients = coefficients;
            return true;
        }

        static int[] MapToMesh(Vector3[] particles, int count, Vector3[] meshVerts, Transform fromWorld, out float meanError)
        {
            var map = new int[count];
            float error = 0f;
            for (int i = 0; i < count; i++)
            {
                Vector3 p = fromWorld != null ? fromWorld.InverseTransformPoint(particles[i]) : particles[i];
                int best = 0;
                float bestDistance = float.MaxValue;
                for (int j = 0; j < meshVerts.Length; j++)
                {
                    float d = (meshVerts[j] - p).sqrMagnitude;
                    if (d < bestDistance)
                    {
                        bestDistance = d;
                        best = j;
                    }
                }
                map[i] = best;
                error += Mathf.Sqrt(bestDistance);
            }
            meanError = count > 0 ? error / count : 0f;
            return map;
        }
    }
}

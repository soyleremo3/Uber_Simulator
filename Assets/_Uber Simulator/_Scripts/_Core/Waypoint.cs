using System.Collections.Generic;
using UnityEngine;

namespace DeliverySim
{
    /// <summary>
    /// One node of the road graph used by RouteManager for the GPS line.
    /// Place these along roads and link neighbors in the Inspector.
    /// Links are treated as bidirectional by RouteManager.
    /// </summary>
    public class Waypoint : MonoBehaviour
    {
        [Tooltip("Directly reachable neighbor waypoints. Links are bidirectional.")]
        public List<Waypoint> neighbors = new List<Waypoint>();

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 0.8f);

            if (neighbors == null)
            {
                return;
            }

            Gizmos.color = Color.yellow;
            foreach (Waypoint n in neighbors)
            {
                if (n != null)
                {
                    Gizmos.DrawLine(transform.position, n.transform.position);
                }
            }
        }
    }
}

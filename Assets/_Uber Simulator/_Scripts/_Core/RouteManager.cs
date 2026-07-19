using System.Collections.Generic;
using UnityEngine;

namespace DeliverySim
{
    /// <summary>
    /// Draws a GPS-style route line from the player vehicle to the current order
    /// target. If Waypoint nodes exist in the scene, the route follows the road
    /// graph (BFS shortest hop path); otherwise it falls back to a straight line.
    /// OrderManager calls SetDestination/ClearDestination.
    /// </summary>
    public class RouteManager : MonoBehaviour
    {
        public static RouteManager Instance { get; private set; }

        [Header("References")]
        [Tooltip("Route start (player vehicle). Auto-found via VehicleController if empty.")]
        [SerializeField] private Transform routeStart;

        [Header("Line")]
        [SerializeField] private float lineWidth = 0.6f;
        [SerializeField] private float lineHeightOffset = 0.3f;
        [SerializeField] private Color lineColor = new Color(0.1f, 0.6f, 1f, 0.9f);
        [Tooltip("Seconds between route recomputes (recomputing every frame is wasteful).")]
        [SerializeField] private float updateInterval = 0.25f;

        private LineRenderer line;
        private Waypoint[] waypoints = new Waypoint[0];
        private Vector3? destination;
        private float updateTimer;

        public Vector3? CurrentDestination => destination;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            CreateLine();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Start()
        {
            waypoints = FindObjectsByType<Waypoint>(FindObjectsSortMode.None);

            if (routeStart == null)
            {
                VehicleController vehicle = FindFirstObjectByType<VehicleController>();
                if (vehicle != null)
                {
                    routeStart = vehicle.transform;
                }
            }
        }

        private void Update()
        {
            if (destination == null || routeStart == null)
            {
                return;
            }

            updateTimer -= Time.deltaTime;
            if (updateTimer <= 0f)
            {
                updateTimer = updateInterval;
                RebuildLine();
            }
        }

        public void SetDestination(Vector3 worldPosition)
        {
            destination = worldPosition;
            updateTimer = 0f;
            if (line != null)
            {
                line.enabled = true;
            }
        }

        public void ClearDestination()
        {
            destination = null;
            if (line != null)
            {
                line.positionCount = 0;
                line.enabled = false;
            }
        }

        /// <summary>Straight-line distance from the vehicle to the destination (for HUD).</summary>
        public float GetDistanceToDestination()
        {
            if (destination == null || routeStart == null)
            {
                return -1f;
            }

            return Vector3.Distance(routeStart.position, destination.Value);
        }

        private void CreateLine()
        {
            GameObject lineObject = new GameObject("RouteLine");
            lineObject.transform.SetParent(transform, false);
            line = lineObject.AddComponent<LineRenderer>();
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startWidth = lineWidth;
            line.endWidth = lineWidth;
            line.startColor = lineColor;
            line.endColor = lineColor;
            line.positionCount = 0;
            line.enabled = false;
        }

        private void RebuildLine()
        {
            if (line == null || destination == null || routeStart == null)
            {
                return;
            }

            List<Vector3> points = BuildPath(routeStart.position, destination.Value);
            line.positionCount = points.Count;
            for (int i = 0; i < points.Count; i++)
            {
                line.SetPosition(i, points[i] + Vector3.up * lineHeightOffset);
            }
        }

        private List<Vector3> BuildPath(Vector3 from, Vector3 to)
        {
            var result = new List<Vector3> { from };

            if (waypoints != null && waypoints.Length >= 2)
            {
                Waypoint startNode = FindNearest(from);
                Waypoint endNode = FindNearest(to);

                if (startNode != null && endNode != null && startNode != endNode)
                {
                    List<Waypoint> graphPath = FindGraphPath(startNode, endNode);
                    if (graphPath != null)
                    {
                        foreach (Waypoint w in graphPath)
                        {
                            result.Add(w.transform.position);
                        }
                    }
                }
            }

            result.Add(to);
            return result;
        }

        private Waypoint FindNearest(Vector3 position)
        {
            Waypoint nearest = null;
            float bestSqr = float.MaxValue;

            foreach (Waypoint w in waypoints)
            {
                if (w == null)
                {
                    continue;
                }

                float sqr = (w.transform.position - position).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    nearest = w;
                }
            }

            return nearest;
        }

        /// <summary>BFS over the waypoint graph (links treated as bidirectional). Returns null when no path exists.</summary>
        private List<Waypoint> FindGraphPath(Waypoint start, Waypoint end)
        {
            var cameFrom = new Dictionary<Waypoint, Waypoint> { { start, null } };
            var queue = new Queue<Waypoint>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                Waypoint current = queue.Dequeue();
                if (current == end)
                {
                    break;
                }

                foreach (Waypoint next in EnumerateNeighbors(current))
                {
                    if (next != null && !cameFrom.ContainsKey(next))
                    {
                        cameFrom[next] = current;
                        queue.Enqueue(next);
                    }
                }
            }

            if (!cameFrom.ContainsKey(end))
            {
                return null;
            }

            var path = new List<Waypoint>();
            for (Waypoint node = end; node != null; node = cameFrom[node])
            {
                path.Add(node);
            }

            path.Reverse();
            return path;
        }

        private IEnumerable<Waypoint> EnumerateNeighbors(Waypoint node)
        {
            if (node.neighbors != null)
            {
                foreach (Waypoint n in node.neighbors)
                {
                    yield return n;
                }
            }

            // Reverse links: nodes that list "node" as a neighbor are reachable too.
            foreach (Waypoint other in waypoints)
            {
                if (other != null && other != node && other.neighbors != null && other.neighbors.Contains(node))
                {
                    yield return other;
                }
            }
        }
    }
}

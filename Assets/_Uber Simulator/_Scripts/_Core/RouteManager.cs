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
        [Tooltip("How far above the detected ground surface the line floats. Keep small so it reads as a road decal, not a floating beam.")]
        [SerializeField] private float lineHeightOffset = 0.05f;
        [SerializeField] private Color lineColor = new Color(0.1f, 0.6f, 1f, 0.9f);
        [Tooltip("Seconds between route recomputes (recomputing every frame is wasteful).")]
        [SerializeField] private float updateInterval = 0.25f;
        [Tooltip("How many texture tiles flow toward the destination per second (0 = static line).")]
        [SerializeField] private float scrollSpeed = 1.2f;

        [Header("Ground Snapping")]
        [Tooltip("Raycasts each point down onto the ground so the line hugs road/terrain height instead of floating at a flat Y (Forza-style route decal).")]
        [SerializeField] private bool snapToGround = true;
        [SerializeField] private LayerMask groundLayerMask = ~0;
        [SerializeField] private float raycastStartHeight = 20f;
        [SerializeField] private float raycastMaxDistance = 100f;
        [Tooltip("Used when the raycast finds no ground (missing/wrong-layer collider) — without this the line falls back to the source point's raw Y (e.g. the car's chassis pivot) and visibly floats.")]
        [SerializeField] private float fallbackGroundY = 0f;

        private LineRenderer line;
        private Material lineMaterial;
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

            if (lineMaterial != null && scrollSpeed != 0f)
            {
                // Tiles scroll from the player toward the destination for a GPS "flow" cue.
                Vector2 offset = lineMaterial.mainTextureOffset;
                offset.x -= scrollSpeed * Time.deltaTime;
                lineMaterial.mainTextureOffset = offset;
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

            lineMaterial = new Material(Shader.Find("Sprites/Default"));
            lineMaterial.mainTexture = CreateChevronTexture();
            line.material = lineMaterial;

            // Local (not the default View/billboard) alignment makes the ribbon
            // lie flat against the ground plane instead of always facing the
            // camera like a floating wall — this is what makes it read as a
            // road decal, Forza-style, rather than a beam hovering in the air.
            line.alignment = LineAlignment.TransformZ;
            line.textureMode = LineTextureMode.Tile;
            line.numCapVertices = 4;
            line.numCornerVertices = 4;
            line.startWidth = lineWidth;
            line.endWidth = lineWidth;
            line.startColor = lineColor;
            line.endColor = lineColor;
            line.positionCount = 0;
            line.enabled = false;
        }

        /// <summary>Repeating ">" chevrons along U so the route reads as directional flow, not a static bar.</summary>
        private static Texture2D CreateChevronTexture()
        {
            const int width = 32;
            const int height = 16;
            const float chevronFraction = 0.6f;
            const float strokeWidth = height * 0.14f;

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };

            float chevronWidth = width * chevronFraction;
            float midY = height * 0.5f;

            for (int x = 0; x < width; x++)
            {
                bool inChevron = x < chevronWidth;
                float progress = inChevron ? x / chevronWidth : 0f;
                float topArmY = Mathf.Lerp(0f, midY, progress);
                float bottomArmY = Mathf.Lerp(height, midY, progress);

                for (int y = 0; y < height; y++)
                {
                    bool onArm = inChevron &&
                        (Mathf.Abs(y - topArmY) < strokeWidth || Mathf.Abs(y - bottomArmY) < strokeWidth);
                    texture.SetPixel(x, y, onArm ? Color.white : new Color(1f, 1f, 1f, 0f));
                }
            }

            texture.Apply();
            return texture;
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
                line.SetPosition(i, GroundSnap(points[i]));
            }
        }

        private static readonly RaycastHit[] groundHitBuffer = new RaycastHit[16];

        /// <summary>Raycasts a point down onto the ground so the line hugs road/terrain height instead of floating at the source point's raw Y.</summary>
        private Vector3 GroundSnap(Vector3 point)
        {
            if (snapToGround)
            {
                Vector3 rayOrigin = point + Vector3.up * raycastStartHeight;
                float maxDistance = raycastStartHeight + raycastMaxDistance;

                // Ignore triggers so pickup/delivery marker colliders don't get hit instead of the road.
                // Use RaycastAll (sorted by distance) instead of the first hit: pickup/delivery/station
                // kiosks sit exactly at destination points and have a SOLID collider (on purpose, so the
                // car can't drive through), which otherwise blocks this ray and snaps the line to the
                // kiosk roof instead of the road beneath it — reads as the line floating/jumping.
                int hitCount = Physics.RaycastNonAlloc(rayOrigin, Vector3.down, groundHitBuffer,
                    maxDistance, groundLayerMask, QueryTriggerInteraction.Ignore);

                System.Array.Sort(groundHitBuffer, 0, hitCount,
                    Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance)));

                bool snapped = false;
                for (int i = 0; i < hitCount; i++)
                {
                    Collider hitCollider = groundHitBuffer[i].collider;
                    if (hitCollider.GetComponentInParent<InteractionPoint>() != null ||
                        hitCollider.GetComponentInParent<FuelStation>() != null ||
                        hitCollider.GetComponentInParent<RepairStation>() != null)
                    {
                        continue; // Kiosk/Visual stand collider — keep looking for the real ground.
                    }

                    point.y = groundHitBuffer[i].point.y;
                    snapped = true;
                    break;
                }

                if (!snapped)
                {
                    // No usable ground collider under this point — fall back to a known ground
                    // height instead of leaving the point's raw Y (which for the vehicle end is
                    // its chassis pivot, well above the road).
                    point.y = fallbackGroundY;
                }
            }

            return point + Vector3.up * lineHeightOffset;
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

using System.Collections.Generic;
using UnityEngine;

namespace DeliverySim
{
    /// <summary>
    /// Draws a GPS-style route decal from the player vehicle to the current order
    /// target. If Waypoint nodes exist in the scene, the route follows the road
    /// graph (BFS shortest hop path); otherwise it falls back to a straight line.
    /// OrderManager calls SetDestination/ClearDestination.
    ///
    /// Rendered as a real flat ribbon MESH lying on the ground (not a LineRenderer)
    /// — a LineRenderer's "TransformZ" alignment only lies flat if the object's
    /// local Z axis points straight up, which it never did here, so the ribbon was
    /// actually standing on edge like a fence (that's the "duruyor havada dik"
    /// bug). Building real ground-plane geometry avoids that class of problem
    /// entirely and reads correctly from every camera angle.
    /// </summary>
    public class RouteManager : MonoBehaviour
    {
        public static RouteManager Instance { get; private set; }

        [Header("References")]
        [Tooltip("Route start (player vehicle). Auto-found via VehicleController if empty.")]
        [SerializeField] private Transform routeStart;

        [Header("Line")]
        [SerializeField] private float lineWidth = 0.6f;
        [Tooltip("How far above the detected ground surface the decal floats. Keep small so it reads as a road decal, not a floating beam.")]
        [SerializeField] private float lineHeightOffset = 0.06f;
        [SerializeField] private Color lineColor = new Color(0f, 0f, 0f, 0.95f);
        [Tooltip("How many texture tiles flow toward the destination per second (0 = static line).")]
        [SerializeField] private float scrollSpeed = 1.2f;
        [Tooltip("World-space length (meters) of one arrow tile along the route.")]
        [SerializeField] private float tileLength = 3f;

        [Header("Ground Snapping")]
        [Tooltip("Raycasts each point down onto the ground so the decal hugs road/terrain height instead of floating at a flat Y.")]
        [SerializeField] private bool snapToGround = true;
        [SerializeField] private LayerMask groundLayerMask = ~0;
        [SerializeField] private float raycastStartHeight = 20f;
        [SerializeField] private float raycastMaxDistance = 100f;
        [Tooltip("Used when the raycast finds no ground (missing/wrong-layer collider) — without this the decal falls back to the source point's raw Y (e.g. the car's chassis pivot) and visibly floats.")]
        [SerializeField] private float fallbackGroundY = 0f;

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh mesh;
        private Material routeMaterial;

        private Waypoint[] waypoints = new Waypoint[0];
        private Vector3? destination;

        // Reused every frame to avoid per-frame allocations.
        private readonly List<Vector3> pathPoints = new List<Vector3>(32);
        private readonly List<Vector3> snappedPoints = new List<Vector3>(32);
        private readonly List<Vector3> meshVertices = new List<Vector3>(64);
        private readonly List<Vector2> meshUvs = new List<Vector2>(64);
        private readonly List<int> meshTriangles = new List<int>(192);

        public Vector3? CurrentDestination => destination;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            CreateRouteMesh();
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
                if (meshRenderer != null && meshRenderer.enabled)
                {
                    meshRenderer.enabled = false;
                }

                return;
            }

            if (meshRenderer != null && !meshRenderer.enabled)
            {
                meshRenderer.enabled = true;
            }

            // Rebuilt every frame: the path is short (a handful of waypoint hops),
            // so this is cheap, and it's what guarantees the ribbon never lags
            // behind the moving car or the ground it hugs — that staleness was
            // the actual source of the "kasıyor" / "bazen gözükmüyor" complaints.
            RebuildMesh();

            if (routeMaterial != null && scrollSpeed != 0f)
            {
                // Tiles scroll from the player toward the destination for a GPS "flow" cue.
                Vector2 offset = routeMaterial.mainTextureOffset;
                offset.x -= scrollSpeed * Time.deltaTime;
                routeMaterial.mainTextureOffset = offset;
            }
        }

        public void SetDestination(Vector3 worldPosition)
        {
            destination = worldPosition;
        }

        public void ClearDestination()
        {
            destination = null;
            if (mesh != null)
            {
                mesh.Clear();
            }

            if (meshRenderer != null)
            {
                meshRenderer.enabled = false;
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

        private void CreateRouteMesh()
        {
            GameObject lineObject = new GameObject("RouteLine");
            lineObject.transform.SetParent(transform, false);

            meshFilter = lineObject.AddComponent<MeshFilter>();
            meshRenderer = lineObject.AddComponent<MeshRenderer>();

            mesh = new Mesh { name = "RouteRibbon" };
            mesh.MarkDynamic();
            meshFilter.mesh = mesh;

            // Sprites/Default: unlit, alpha-blended, double-sided (Cull Off) and
            // doesn't write depth, all out of the box — no extra shader keywords to
            // wire up by hand (unlike URP Lit/Unlit, whose transparency requires
            // setting Surface Type + blend keywords in code or it silently renders
            // opaque, turning the chevron cutout into a solid black block).
            routeMaterial = new Material(Shader.Find("Sprites/Default"));
            routeMaterial.mainTexture = CreateChevronTexture();
            routeMaterial.color = lineColor;

            meshRenderer.sharedMaterial = routeMaterial;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            meshRenderer.enabled = false;
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

        private void RebuildMesh()
        {
            if (mesh == null || destination == null || routeStart == null)
            {
                return;
            }

            pathPoints.Clear();
            BuildPath(routeStart.position, destination.Value, pathPoints);

            snappedPoints.Clear();
            for (int i = 0; i < pathPoints.Count; i++)
            {
                snappedPoints.Add(GroundSnap(pathPoints[i]));
            }

            // Drop consecutive duplicate/near-duplicate points (degenerate segments
            // would otherwise produce a zero-length direction and flip the chevron
            // orientation at that spot — the "bazıları geri gösteriyor" symptom).
            for (int i = snappedPoints.Count - 2; i >= 0; i--)
            {
                if ((snappedPoints[i + 1] - snappedPoints[i]).sqrMagnitude < 0.0001f)
                {
                    snappedPoints.RemoveAt(i + 1);
                }
            }

            meshVertices.Clear();
            meshUvs.Clear();
            meshTriangles.Clear();

            if (snappedPoints.Count < 2)
            {
                mesh.Clear();
                return;
            }

            float halfWidth = lineWidth * 0.5f;
            float cumulativeDistance = 0f;

            for (int i = 0; i < snappedPoints.Count; i++)
            {
                Vector3 point = snappedPoints[i];

                // Direction always points from this point toward the destination side,
                // so the chevrons flow consistently start -> end along the whole path.
                Vector3 direction = i < snappedPoints.Count - 1
                    ? (snappedPoints[i + 1] - point)
                    : (point - snappedPoints[i - 1]);
                direction.y = 0f;
                direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;

                // Cross with world-up (not the segment's own "up") so the ribbon's
                // width is always horizontal — this is what keeps it flat on the
                // ground instead of standing up like a wall.
                Vector3 right = Vector3.Cross(Vector3.up, direction).normalized * halfWidth;

                meshVertices.Add(point - right);
                meshVertices.Add(point + right);

                float u = cumulativeDistance / Mathf.Max(0.01f, tileLength);
                meshUvs.Add(new Vector2(u, 0f));
                meshUvs.Add(new Vector2(u, 1f));

                if (i < snappedPoints.Count - 1)
                {
                    cumulativeDistance += Vector3.Distance(point, snappedPoints[i + 1]);
                }
            }

            for (int i = 0; i < snappedPoints.Count - 1; i++)
            {
                int a = i * 2;
                int b = i * 2 + 1;
                int c = (i + 1) * 2;
                int d = (i + 1) * 2 + 1;

                meshTriangles.Add(a);
                meshTriangles.Add(c);
                meshTriangles.Add(b);

                meshTriangles.Add(b);
                meshTriangles.Add(c);
                meshTriangles.Add(d);
            }

            mesh.Clear();
            mesh.SetVertices(meshVertices);
            mesh.SetUVs(0, meshUvs);
            mesh.SetTriangles(meshTriangles, 0);
            mesh.RecalculateBounds();
        }

        private static readonly RaycastHit[] groundHitBuffer = new RaycastHit[16];

        /// <summary>Raycasts a point down onto the ground so the decal hugs road/terrain height instead of floating at the source point's raw Y.</summary>
        private Vector3 GroundSnap(Vector3 point)
        {
            if (snapToGround)
            {
                Vector3 rayOrigin = point + Vector3.up * raycastStartHeight;
                float maxDistance = raycastStartHeight + raycastMaxDistance;

                // Ignore triggers so pickup/delivery marker colliders don't get hit instead of the road.
                // Use every hit (sorted by distance) instead of just the first: pickup/delivery/station
                // kiosks sit exactly at destination points and have a SOLID collider (on purpose, so the
                // car can't drive through), which otherwise blocks this ray and snaps the decal to the
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

                    if (routeStart != null && hitCollider.transform.IsChildOf(routeStart))
                    {
                        // The ray from directly above the vehicle hits the car's OWN body/roof
                        // collider first — without this, the line's start point snaps to
                        // windshield height instead of the road under the car.
                        continue;
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

        private void BuildPath(Vector3 from, Vector3 to, List<Vector3> result)
        {
            result.Add(from);

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

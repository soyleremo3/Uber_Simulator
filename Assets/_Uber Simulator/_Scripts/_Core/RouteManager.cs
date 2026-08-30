using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace DeliverySim
{
    /// <summary>
    /// Draws a GPS-style route decal from the player vehicle to the current order
    /// target. Path source, in order: the baked NavMesh (routes around buildings —
    /// primary), then the scene Waypoint graph (Dijkstra, real-distance shortest
    /// path), then a straight line. OrderManager calls SetDestination/ClearDestination.
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
        /// <summary>Upcoming maneuver, derived from the angle between consecutive route segments — consumed by the HUD turn indicator.</summary>
        public enum TurnDirection { None, Straight, Left, Right }

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

        [Header("Pathfinding")]
        [Tooltip("Route around obstacles using the baked NavMesh. Falls back to the Waypoint graph, then a straight line, if no NavMesh path is found.")]
        [SerializeField] private bool useNavMesh = true;
        [Tooltip("How far from a point to search for the NavMesh when snapping the vehicle / destination onto it.")]
        [SerializeField] private float navMeshSampleRadius = 8f;
        [Tooltip("Başlangıç waypoint'i seçilirken 'araç->wp + wp->hedef' toplamı en küçük olan seçilir (aracın arkasındaki waypoint'e geri gitmeyi önler). Aday araç ileri yönündeyse skoruna bu kadar (m) indirim uygulanır.")]
        [SerializeField] private float startNodeForwardBonus = 18f;

        [Header("Turn Indicator")]
        [Tooltip("Sıradaki dönüş araçtan bu mesafeden (m) uzaktaysa HUD 'DÜZ GİT' der; yaklaşınca sola/sağa döner.")]
        [SerializeField] private float turnLookaheadDistance = 55f;
        [Tooltip("Araca bu mesafeden (m) yakın rota köşeleri yok sayılır — aracın hemen dibindeki açı titreşimini eler.")]
        [SerializeField] private float turnMinDistance = 5f;
        [Tooltip("Ardışık iki yol parçası arasındaki açı bu dereceyi (|deg|) geçerse dönüş sayılır; altı yumuşak viraj = düz.")]
        [SerializeField] private float turnAngleThresholdDegrees = 28f;

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

        // Symmetric adjacency, built once from Waypoint.neighbors. The Inspector links
        // are declared one-directional but documented as bidirectional, so we mirror
        // every link here. Doing it once kills the old per-query O(n^2) reverse scan
        // in EnumerateNeighbors (which, called once per Dijkstra expansion, made the
        // whole path search O(n^3) every frame — a real frame hitch on 89 nodes) and
        // its double-yield of already-listed neighbours.
        private Dictionary<Waypoint, List<Waypoint>> adjacency;

        private Vector3? destination;
        private TurnDirection nextTurn = TurnDirection.None;

        // Reused every frame to avoid per-frame allocations. Constructed lazily —
        // NavMeshPath can't be built from a field initializer / constructor.
        private NavMeshPath navMeshPath;

        // Reused every frame to avoid per-frame allocations.
        private readonly List<Vector3> pathPoints = new List<Vector3>(32);
        private readonly List<Vector3> snappedPoints = new List<Vector3>(32);
        private readonly List<Vector3> meshVertices = new List<Vector3>(64);
        private readonly List<Vector2> meshUvs = new List<Vector2>(64);
        private readonly List<int> meshTriangles = new List<int>(192);

        // Dijkstra working sets — reused (Clear() keeps capacity) so the every-frame
        // rebuild doesn't allocate three collections per frame.
        private readonly Dictionary<Waypoint, float> graphDist = new Dictionary<Waypoint, float>(128);
        private readonly Dictionary<Waypoint, Waypoint> graphCameFrom = new Dictionary<Waypoint, Waypoint>(128);
        private readonly HashSet<Waypoint> graphVisited = new HashSet<Waypoint>();
        private readonly List<Waypoint> graphPathScratch = new List<Waypoint>(32);

        // Last Y a ground raycast actually resolved this rebuild — used as the snap
        // fallback instead of the flat fallbackGroundY, so a point that misses the
        // narrow "Road" mask (over a sidewalk/plaza/driveway collider on another
        // layer) follows the road height it had a moment ago instead of dropping to
        // y=0 and punching through building bases.
        private float lastGroundY;

        // Hoisted so GroundSnap doesn't allocate a fresh comparer delegate for every
        // path point, every frame.
        private static readonly IComparer<RaycastHit> groundHitDistanceComparer =
            Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance));

        public Vector3? CurrentDestination => destination;

        /// <summary>Upcoming turn along the currently rendered route — legible-guidance HUD cue (no route = None).</summary>
        public TurnDirection NextTurn => nextTurn;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            // Self-heal: scenes/inspector values created before the dedicated "Road"
            // layer existed are still serialized as "everything" (~0). Once that layer
            // exists in the project, narrow to it automatically so GroundSnap can no
            // longer hit building/prop colliders — that was the "route ribbon snaps
            // onto rooftops instead of the street" bug.
            if (groundLayerMask.value == ~0)
            {
                int roadLayer = LayerMask.NameToLayer("Road");
                if (roadLayer >= 0)
                {
                    groundLayerMask = 1 << roadLayer;
                }
            }

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
            BuildAdjacency();

            if (routeStart == null)
            {
                VehicleController vehicle = FindFirstObjectByType<VehicleController>();
                if (vehicle != null)
                {
                    routeStart = vehicle.transform;
                }
            }
        }

        /// <summary>
        /// Builds the symmetric neighbour map from every Waypoint.neighbors list.
        /// A link A->B also becomes B->A here, matching Waypoint's documented
        /// "links are bidirectional" contract even when only one side was wired up
        /// in the Inspector. Nulls and self-links are dropped; duplicates collapsed.
        /// </summary>
        private void BuildAdjacency()
        {
            adjacency = new Dictionary<Waypoint, List<Waypoint>>(waypoints.Length);

            foreach (Waypoint w in waypoints)
            {
                if (w == null)
                {
                    continue;
                }

                if (!adjacency.ContainsKey(w))
                {
                    adjacency[w] = new List<Waypoint>(4);
                }
            }

            foreach (Waypoint w in waypoints)
            {
                if (w == null || w.neighbors == null)
                {
                    continue;
                }

                foreach (Waypoint n in w.neighbors)
                {
                    if (n == null || n == w)
                    {
                        continue;
                    }

                    LinkAdjacency(w, n);
                    LinkAdjacency(n, w);
                }
            }
        }

        private void LinkAdjacency(Waypoint from, Waypoint to)
        {
            if (!adjacency.TryGetValue(from, out List<Waypoint> list))
            {
                list = new List<Waypoint>(4);
                adjacency[from] = list;
            }

            if (!list.Contains(to))
            {
                list.Add(to);
            }
        }

        private void Update()
        {
            if (routeStart == null)
            {
                // Vehicle may have been spawned after this manager's Start() (async
                // load / pooled player) — keep trying so the route isn't dead forever.
                VehicleController vehicle = FindFirstObjectByType<VehicleController>();
                if (vehicle != null)
                {
                    routeStart = vehicle.transform;
                }
            }

            if (destination == null || routeStart == null)
            {
                SetRouteVisible(false);
                return;
            }

            // Rebuilt every frame: the path is short (a handful of waypoint hops),
            // so this is cheap, and it's what guarantees the ribbon never lags
            // behind the moving car or the ground it hugs — that staleness was
            // the actual source of the "kasıyor" complaint. RebuildMesh() is now the
            // single place that decides visibility (via SetRouteVisible), so the
            // renderer's enabled flag and the mesh content can never desync again —
            // that desync was the "bazen gözükmüyor" flicker bug.
            RebuildMesh();

            // Diagnostic: press F9 while a route is shown to dump the exact path to
            // the console (car pose, destination, every ribbon point + the turn cue).
            // Use it to report a "route sends me the wrong way" spot precisely.
            if (Input.GetKeyDown(KeyCode.F9))
            {
                DumpRouteDiagnostic();
            }

            if (routeMaterial != null && scrollSpeed != 0f && meshRenderer != null && meshRenderer.enabled)
            {
                // Tiles scroll from the player toward the destination for a GPS "flow" cue.
                Vector2 offset = routeMaterial.mainTextureOffset;
                offset.x -= scrollSpeed * Time.deltaTime;
                routeMaterial.mainTextureOffset = offset;
            }
        }

        /// <summary>F9 diagnostic — logs the full current route so a bad spot can be reported exactly.</summary>
        private void DumpRouteDiagnostic()
        {
            var sb = new System.Text.StringBuilder();
            Vector3 carPos = routeStart != null ? routeStart.position : Vector3.zero;
            Vector3 carFwd = routeStart != null ? routeStart.forward : Vector3.forward;
            sb.AppendLine("[RouteDiag] ---- press F9 route dump ----");
            sb.AppendLine($"[RouteDiag] car pos {carPos:F1}  fwd {carFwd:F2}  dest {(destination.HasValue ? destination.Value.ToString("F1") : "none")}  useNavMesh={useNavMesh}  NextTurn={nextTurn}");
            sb.AppendLine($"[RouteDiag] ribbon points ({snappedPoints.Count}):");
            for (int i = 0; i < snappedPoints.Count; i++)
            {
                Vector3 p = snappedPoints[i];
                Vector3 fromCar = p - carPos;
                fromCar.y = 0f;
                Vector3 seg = i > 0 ? (snappedPoints[i] - snappedPoints[i - 1]) : Vector3.zero;
                seg.y = 0f;
                sb.AppendLine($"[RouteDiag]   p{i} ({p.x:F0},{p.z:F0})  {fromCar.magnitude:F0}m from car  seg {(i > 0 ? seg.magnitude.ToString("F0") + "m" : "-")}");
            }

            Debug.Log(sb.ToString());
        }

        public void SetDestination(Vector3 worldPosition)
        {
            destination = worldPosition;
        }

        public void ClearDestination()
        {
            destination = null;
            SetRouteVisible(false);
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

        /// <summary>
        /// Single source of truth for route visibility. Every place that used to
        /// toggle meshRenderer.enabled and mesh.Clear() independently (Update,
        /// ClearDestination, RebuildMesh's degenerate-path branch) now goes through
        /// here, so those two states can no longer desync — that desync was the
        /// "sometimes shows, sometimes doesn't" flicker bug.
        /// </summary>
        private void SetRouteVisible(bool visible)
        {
            if (meshRenderer != null)
            {
                meshRenderer.enabled = visible;
            }

            if (!visible)
            {
                if (mesh != null)
                {
                    mesh.Clear();
                }

                nextTurn = TurnDirection.None;
            }
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
                SetRouteVisible(false);
                return;
            }

            pathPoints.Clear();
            BuildPath(routeStart.position, destination.Value, pathPoints);

            // Seed the ground-snap fallback with the configured flat height; the
            // first point that resolves a real road hit replaces it for the rest.
            lastGroundY = fallbackGroundY;

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
                SetRouteVisible(false);
                return;
            }

            UpdateNextTurn(snappedPoints);

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

            SetRouteVisible(true);
        }

        /// <summary>
        /// Derives the NEXT IMMINENT maneuver by walking the route polyline from the
        /// vehicle, accumulating travelled distance, and reporting the first vertex
        /// whose heading change (incoming vs outgoing segment) exceeds the turn
        /// threshold AND falls inside the lookahead window. Anything farther reads as
        /// "go straight" until the driver approaches it.
        ///
        /// The old version scanned the WHOLE path from index 0 and returned the first
        /// bend anywhere on it — so it announced turns hundreds of metres away, was
        /// tripped by gentle road curves (20° threshold), and flipped on the noisy
        /// near-zero-length first segment right under the car. That is the "her şeyi
        /// yanlış gösteriyor" report.
        /// </summary>
        private void UpdateNextTurn(List<Vector3> points)
        {
            if (points.Count < 2 || routeStart == null)
            {
                nextTurn = TurnDirection.None;
                return;
            }

            if (points.Count < 3)
            {
                nextTurn = TurnDirection.Straight;
                return;
            }

            nextTurn = TurnDirection.Straight;

            Vector3 vehiclePos = routeStart.position;
            Vector3 vehicleFwd = routeStart.forward;
            vehicleFwd.y = 0f;
            bool haveFwd = vehicleFwd.sqrMagnitude > 0.001f;
            if (haveFwd)
            {
                vehicleFwd.Normalize();
            }

            float travelled = 0f;

            for (int i = 1; i < points.Count - 1; i++)
            {
                Vector3 incoming = points[i] - points[i - 1];
                Vector3 outgoing = points[i + 1] - points[i];
                incoming.y = 0f;
                outgoing.y = 0f;

                travelled += incoming.magnitude;
                if (travelled > turnLookaheadDistance)
                {
                    break; // Next real turn is past the window — keep "Straight".
                }

                Vector3 flatToVertex = points[i] - vehiclePos;
                flatToVertex.y = 0f;

                // Never announce a maneuver at a vertex that's BEHIND the vehicle: if
                // a stray backward corner slipped past BuildPath's trim, the ~180°
                // heading change there would otherwise fire a bogus "SOLA/SAĞA DÖN".
                if (haveFwd && flatToVertex.sqrMagnitude > 0.01f &&
                    Vector3.Dot(flatToVertex.normalized, vehicleFwd) < -0.25f)
                {
                    continue;
                }

                if (flatToVertex.magnitude < turnMinDistance ||
                    incoming.sqrMagnitude < 0.25f || outgoing.sqrMagnitude < 0.25f)
                {
                    continue; // Vertex on top of the car / degenerate segment noise.
                }

                float angle = Vector3.SignedAngle(incoming, outgoing, Vector3.up);
                if (Mathf.Abs(angle) >= turnAngleThresholdDegrees)
                {
                    // Unity Y-up SignedAngle: forward->right is +90, so positive = right.
                    nextTurn = angle > 0f ? TurnDirection.Right : TurnDirection.Left;
                    return;
                }
            }
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

                System.Array.Sort(groundHitBuffer, 0, hitCount, groundHitDistanceComparer);

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
                    lastGroundY = point.y; // remember for points that miss the narrow Road mask
                    snapped = true;
                    break;
                }

                if (!snapped)
                {
                    // No usable ground collider under this point — it's over a
                    // sidewalk/plaza/driveway collider on some other layer, or the
                    // waypoint sits just off the road mesh. Follow the last road
                    // height we actually resolved rather than slamming to
                    // fallbackGroundY (usually 0), which drops the ribbon through
                    // building bases on any map that isn't built at y=0.
                    point.y = lastGroundY;
                }
            }

            return point + Vector3.up * lineHeightOffset;
        }

        private void BuildPath(Vector3 from, Vector3 to, List<Vector3> result)
        {
            result.Add(from);

            if (useNavMesh && TryAppendNavMeshPath(from, to, result))
            {
                result.Add(to);
                return;
            }

            // No NavMesh path — route along the Waypoint road graph. Everything below
            // only inserts intermediate corners; result stays bracketed by the real
            // from/to, and RebuildMesh's dedup pass drops any that coincide.
            if (waypoints == null || waypoints.Length < 2)
            {
                result.Add(to);
                return;
            }

            float straightDist = Vector3.Distance(from, to);

            Vector3 forward = routeStart != null ? routeStart.forward : (to - from);
            Waypoint startNode = FindStartNode(from, forward, to);
            Waypoint endNode = FindEndNode(to, startNode != null ? startNode.transform.position : from);
            Waypoint nearestStart = FindNearest(from);
            Waypoint nearestEnd = FindNearest(to);

            // start == end node: the vehicle and a nearby target snap to the SAME
            // waypoint. The old code added nothing here, leaving result = [from, to] —
            // a straight diagonal that, for a short target, slices through whatever
            // building sits between them (the reported "through the house"). Route
            // from -> sharedNode -> to instead, but only when that node genuinely
            // lies on the way; otherwise it just bolts on a sideways dog-leg that
            // reads as the ribbon curling off to one side.
            if (startNode != null && startNode == endNode)
            {
                Vector3 nodePos = startNode.transform.position;
                float viaLen = Vector3.Distance(from, nodePos) + Vector3.Distance(nodePos, to);
                if (viaLen <= straightDist * 1.6f + 8f)
                {
                    result.Add(nodePos);
                }

                result.Add(to);
                return;
            }

            // Try the forward/target-biased pair first. If either biased node sits on
            // its own disconnected graph island, retry with the plain-nearest nodes
            // before giving up — a slightly off entry node still beats a straight line
            // through a wall.
            List<Waypoint> graphPath = null;
            float graphLen = 0f;
            if (TryGraphPath(startNode, endNode, ref graphPath, ref graphLen) ||
                TryGraphPath(nearestStart, endNode, ref graphPath, ref graphLen) ||
                TryGraphPath(startNode, nearestEnd, ref graphPath, ref graphLen) ||
                TryGraphPath(nearestStart, nearestEnd, ref graphPath, ref graphLen))
            {
                // Reject an absurd detour: a 300 m loop for a 40 m target means the
                // two graph endpoints simply aren't linked on the near side (a
                // missing cross-link in the scene's Waypoint graph — a data fix, not
                // a code one). A straight line at least points the right way. The
                // factor is generous so a genuinely winding city route still passes.
                if (graphLen <= straightDist * 3f + 50f)
                {
                    AppendTrimmedGraphPath(from, to, graphPath, result);
                }
            }

            result.Add(to);
        }

        /// <summary>
        /// Runs one Dijkstra attempt. Returns false (leaving <paramref name="path"/>
        /// untouched) for a null/degenerate pair or when the nodes aren't connected.
        /// The path buffer is reused by FindGraphPath, so a caller must consume it
        /// before the next attempt — the short-circuiting || chain in BuildPath does.
        /// </summary>
        private bool TryGraphPath(Waypoint start, Waypoint end, ref List<Waypoint> path, ref float length)
        {
            if (start == null || end == null || start == end)
            {
                return false;
            }

            List<Waypoint> found = FindGraphPath(start, end, out float len);
            if (found == null || found.Count < 2)
            {
                return false;
            }

            path = found;
            length = len;
            return true;
        }

        /// <summary>
        /// Appends the graph path's node positions to <paramref name="result"/>,
        /// first trimming a run of leading nodes that sit behind the vehicle (so the
        /// ribbon never starts with a backward hook) and a trailing node that
        /// overshoots the target (so it never ends on a hairpin). At least one node
        /// always survives.
        /// </summary>
        private void AppendTrimmedGraphPath(Vector3 from, Vector3 to, List<Waypoint> graphPath, List<Vector3> result)
        {
            int first = 0;
            int last = graphPath.Count - 1;

            // "Forward" reference: the vehicle's facing when we have a meaningful one,
            // else straight at the target.
            Vector3 refDir = Vector3.zero;
            if (routeStart != null)
            {
                refDir = routeStart.forward;
                refDir.y = 0f;
            }

            if (refDir.sqrMagnitude < 0.001f)
            {
                refDir = to - from;
                refDir.y = 0f;
            }

            bool haveRef = refDir.sqrMagnitude > 0.001f;
            if (haveRef)
            {
                refDir.Normalize();

                // Drop a RUN of leading nodes behind the vehicle (old code dropped at
                // most one, and only when node[1] was already ahead — a two-node
                // backward hook survived and both curled the ribbon and tripped a
                // bogus HUD turn).
                while (first < last)
                {
                    Vector3 d = graphPath[first].transform.position - from;
                    d.y = 0f;
                    if (d.sqrMagnitude > 0.01f && Vector3.Dot(d.normalized, refDir) < -0.25f)
                    {
                        first++;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            // Drop a trailing node that overshoots the target: if, on arriving at the
            // last node, reaching the target means doubling back opposite to the way
            // you came in, that node is past the target and only adds a hairpin.
            if (last - first >= 1)
            {
                Vector3 arrive = graphPath[last].transform.position - graphPath[last - 1].transform.position;
                Vector3 onward = to - graphPath[last].transform.position;
                arrive.y = 0f;
                onward.y = 0f;
                if (arrive.sqrMagnitude > 0.01f && onward.sqrMagnitude > 0.01f &&
                    Vector3.Dot(arrive.normalized, onward.normalized) < -0.25f)
                {
                    last--;
                }
            }

            for (int i = first; i <= last; i++)
            {
                result.Add(graphPath[i].transform.position);
            }
        }

        /// <summary>
        /// Picks the graph entry node. NOT just the geometrically nearest waypoint —
        /// that can sit BEHIND the vehicle, so the route then tells the player to turn
        /// around and drive back to it before heading to the target ("geriden git"
        /// bug). Among the few nearest candidates, choose the one minimising
        /// dist(vehicle,wp) + dist(wp,target), with a bonus for waypoints in the
        /// vehicle's forward direction. Falls back to plain nearest.
        /// </summary>
        private Waypoint FindStartNode(Vector3 from, Vector3 forward, Vector3 to)
        {
            if (waypoints == null || waypoints.Length == 0)
            {
                return null;
            }

            forward.y = 0f;
            bool haveForward = forward.sqrMagnitude > 0.0001f;
            if (haveForward)
            {
                forward.Normalize();
            }

            // Distance of the closest waypoint — candidates must be within a small
            // multiple of it so we don't jump to a far "on the way" node.
            float nearestSqr = float.MaxValue;
            foreach (Waypoint w in waypoints)
            {
                if (w == null)
                {
                    continue;
                }

                float sqr = (w.transform.position - from).sqrMagnitude;
                if (sqr < nearestSqr)
                {
                    nearestSqr = sqr;
                }
            }

            float maxStartDist = Mathf.Sqrt(nearestSqr) * 3f + 20f;

            Waypoint best = null;
            float bestScore = float.MaxValue;

            foreach (Waypoint w in waypoints)
            {
                if (w == null)
                {
                    continue;
                }

                Vector3 wp = w.transform.position;
                float dFrom = Vector3.Distance(from, wp);
                if (dFrom > maxStartDist)
                {
                    continue;
                }

                float score = dFrom + Vector3.Distance(wp, to);

                if (haveForward)
                {
                    Vector3 dir = wp - from;
                    dir.y = 0f;
                    if (dir.sqrMagnitude > 0.01f && Vector3.Dot(dir.normalized, forward) > 0.25f)
                    {
                        score -= startNodeForwardBonus;
                    }
                }

                if (score < bestScore)
                {
                    bestScore = score;
                    best = w;
                }
            }

            return best != null ? best : FindNearest(from);
        }

        /// <summary>
        /// Picks the graph EXIT node near the target. NOT just the geometrically
        /// nearest waypoint to the target — that one can sit on the far side of it
        /// (across the street, behind a wall), so the final graph hop and the last
        /// leg to the target double back on themselves ("geriye kıvrılıyor"). Among
        /// waypoints close to the target, choose the one minimising
        /// dist(wp,target) + dist(wp,entryPos): a wrong-side node is farther from the
        /// entry point, so this favours the near-side approach. Falls back to plain
        /// nearest.
        /// </summary>
        private Waypoint FindEndNode(Vector3 to, Vector3 entryPos)
        {
            if (waypoints == null || waypoints.Length == 0)
            {
                return null;
            }

            float nearestSqr = float.MaxValue;
            foreach (Waypoint w in waypoints)
            {
                if (w == null)
                {
                    continue;
                }

                float sqr = (w.transform.position - to).sqrMagnitude;
                if (sqr < nearestSqr)
                {
                    nearestSqr = sqr;
                }
            }

            // Tight band: the exit node may be a little farther from the target than
            // the closest waypoint (to get to the correct side of the street) but not
            // so much farther that the final endNode->target leg becomes a long line
            // through whatever's between them.
            float maxEndDist = Mathf.Sqrt(nearestSqr) * 1.5f + 12f;

            Waypoint best = null;
            float bestScore = float.MaxValue;

            foreach (Waypoint w in waypoints)
            {
                if (w == null)
                {
                    continue;
                }

                Vector3 wp = w.transform.position;
                float dTo = Vector3.Distance(wp, to);
                if (dTo > maxEndDist)
                {
                    continue;
                }

                float score = dTo + Vector3.Distance(wp, entryPos);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = w;
                }
            }

            return best != null ? best : FindNearest(to);
        }

        /// <summary>
        /// Appends the interior corners of a NavMesh path between <paramref name="from"/>
        /// and <paramref name="to"/> to <paramref name="result"/>. The NavMesh is baked
        /// with buildings/fences as obstacles, so this path can never cut through a wall
        /// — that was the "route ribbon goes through the house" bug. Returns false (so the
        /// caller can fall back to the Waypoint graph) when either end is off-mesh or no
        /// path exists.
        /// </summary>
        private bool TryAppendNavMeshPath(Vector3 from, Vector3 to, List<Vector3> result)
        {
            navMeshPath ??= new NavMeshPath();

            if (!NavMesh.SamplePosition(from, out NavMeshHit fromHit, navMeshSampleRadius, NavMesh.AllAreas) ||
                !NavMesh.SamplePosition(to, out NavMeshHit toHit, navMeshSampleRadius, NavMesh.AllAreas))
            {
                return false;
            }

            if (!NavMesh.CalculatePath(fromHit.position, toHit.position, NavMesh.AllAreas, navMeshPath) ||
                navMeshPath.status == NavMeshPathStatus.PathInvalid ||
                navMeshPath.corners.Length < 2)
            {
                return false;
            }

            // Skip corner 0 (== fromHit) and the last (== toHit); BuildPath brackets the
            // list with the real from/to, and the RebuildMesh dedup pass drops the rest.
            Vector3[] corners = navMeshPath.corners;
            for (int i = 1; i < corners.Length - 1; i++)
            {
                result.Add(corners[i]);
            }

            return true;
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

        /// <summary>
        /// Dijkstra over the waypoint graph (links treated as bidirectional), edge cost
        /// = real world distance between linked nodes. Returns null when no path exists
        /// (disconnected graph / start on its own island). <paramref name="pathLength"/>
        /// receives the real travelled distance start->end (0 when no path).
        ///
        /// Was plain BFS (minimizes hop COUNT) — on an uneven graph that could surface a
        /// route with fewer hops but a longer real distance than the alternative; this
        /// guarantees the shortest actual route. Node counts here are small (tens, not
        /// thousands), so a linear "pick closest unvisited" scan per step is plenty fast
        /// without needing a binary-heap priority queue. Working sets are reused fields
        /// (Clear() keeps capacity) so the every-frame rebuild doesn't allocate.
        ///
        /// The returned list is a REUSED buffer — consume it before calling this again.
        /// </summary>
        private List<Waypoint> FindGraphPath(Waypoint start, Waypoint end, out float pathLength)
        {
            pathLength = 0f;

            if (start == null || end == null)
            {
                return null;
            }

            graphDist.Clear();
            graphCameFrom.Clear();
            graphVisited.Clear();
            graphPathScratch.Clear();

            graphDist[start] = 0f;
            graphCameFrom[start] = null;

            while (true)
            {
                Waypoint current = null;
                float bestDist = float.MaxValue;
                foreach (KeyValuePair<Waypoint, float> candidate in graphDist)
                {
                    if (!graphVisited.Contains(candidate.Key) && candidate.Value < bestDist)
                    {
                        bestDist = candidate.Value;
                        current = candidate.Key;
                    }
                }

                if (current == null || current == end)
                {
                    break;
                }

                graphVisited.Add(current);

                foreach (Waypoint next in EnumerateNeighbors(current))
                {
                    if (next == null || graphVisited.Contains(next))
                    {
                        continue;
                    }

                    float candidateDist = graphDist[current] + Vector3.Distance(
                        current.transform.position, next.transform.position);

                    if (!graphDist.TryGetValue(next, out float existingDist) || candidateDist < existingDist)
                    {
                        graphDist[next] = candidateDist;
                        graphCameFrom[next] = current;
                    }
                }
            }

            if (!graphCameFrom.ContainsKey(end))
            {
                return null;
            }

            // cameFrom can't hold a cycle with non-negative edge costs, but cap the
            // walk anyway so a corrupt map can never spin here.
            int guard = waypoints.Length + 2;
            for (Waypoint node = end; node != null && guard-- > 0; node = graphCameFrom[node])
            {
                graphPathScratch.Add(node);
            }

            graphPathScratch.Reverse();
            pathLength = graphDist.TryGetValue(end, out float d) ? d : 0f;
            return graphPathScratch;
        }

        private static readonly List<Waypoint> emptyNeighbors = new List<Waypoint>(0);

        /// <summary>
        /// Directly reachable neighbours of <paramref name="node"/>, from the
        /// pre-built symmetric adjacency map (both directions of every Inspector
        /// link, deduped, no nulls). O(1) lookup — the old version rescanned all
        /// waypoints and did a List.Contains per node on every call.
        /// </summary>
        private List<Waypoint> EnumerateNeighbors(Waypoint node)
        {
            if (adjacency == null)
            {
                BuildAdjacency();
            }

            return node != null && adjacency.TryGetValue(node, out List<Waypoint> list)
                ? list
                : emptyNeighbors;
        }
    }
}

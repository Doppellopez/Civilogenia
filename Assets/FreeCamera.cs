using UnityEngine;

/// <summary>
/// Cámara libre con teclado:
///   Cursores        arriba / abajo: hacia el polo norte / sur; derecha / izquierda: hacia el este / oeste
///   . y ,           hacia el este / oeste
///   + y -           acercar / alejar
/// La cámara siempre mira al centro de la Tierra, con el norte hacia arriba.
///   Mayús           moverse más rápido
/// Se añade sola a la cámara principal al pulsar Play, se aleja para encuadrar la Tierra
/// y la velocidad crece con la altura sobre la superficie.
/// </summary>
public class FreeCamera : MonoBehaviour
{
    [Header("Movimiento")]
    [Tooltip("Velocidad mínima; lejos del planeta la cámara va más rápido")]
    public float moveSpeed = 8f;
    [Tooltip("Velocidad = altura sobre la superficie x este factor")]
    public float speedPerAltitude = 0.6f;
    public float fastMultiplier = 4f;

    [Header("Giro")]
    public float turnSpeed = 60f;

    [Header("Encuadre inicial")]
    public bool frameEarthOnStart = true;
    [Tooltip("Distancia inicial en radios del planeta")]
    public float startDistanceInRadii = 3.2f;

    float yaw;
    float pitch;
    EarthTopography earth;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AttachToMainCamera()
    {
        Camera cam = Camera.main;

        if (cam != null && cam.GetComponent<FreeCamera>() == null)
            cam.gameObject.AddComponent<FreeCamera>();
    }

    void Start()
    {
        earth = FindFirstObjectByType<EarthTopography>();

        if (earth != null && frameEarthOnStart)
            FrameEarth();

        Vector3 euler = transform.eulerAngles;
        yaw = euler.y;
        pitch = euler.x > 180f ? euler.x - 360f : euler.x;
    }

    // Coloca la cámara delante del planeta mirando a su centro y ajusta los planos de recorte
    void FrameEarth()
    {
        float r = earth.FinalRadius;
        Vector3 center = earth.transform.position;

        transform.position = center + new Vector3(0f, r * 0.35f, -r * startDistanceInRadii);
        transform.LookAt(center);

        Camera cam = GetComponent<Camera>();
        if (cam != null)
        {
            cam.nearClipPlane = Mathf.Max(0.3f, r * 0.002f);
            cam.farClipPlane = SolarSystemBackdrop.FarClipFor(r); // hasta el sistema solar exterior
        }
    }

    // ---------------------------------------------------------------- Seguimiento de un punto

    [Header("Seguimiento (doble clic)")]
    [Tooltip("Tiempo máximo entre los dos clics")]
    public float doubleClickTime = 0.3f;

    bool following;
    Vector3 followLocal;        // punto fijado, en espacio local de la Tierra
    Quaternion lastEarthRotation;
    float lastClickTime = -10f;
    Vector2 lastClickPos;

    void HandleDoubleClick()
    {
        if (earth == null || !Input.GetMouseButtonDown(0)) return;

        Vector2 pos = Input.mousePosition;
        bool isDouble = Time.unscaledTime - lastClickTime <= doubleClickTime && (pos - lastClickPos).sqrMagnitude < 25f * 25f;
        lastClickTime = isDouble ? -10f : Time.unscaledTime;
        lastClickPos = pos;
        if (!isDouble) return;

        Camera cam = GetComponent<Camera>();
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(pos);
        Transform e = earth.transform;

        // Intersección del rayo con la esfera de la Tierra
        Vector3 oc = ray.origin - e.position;
        float r = earth.FinalRadius;
        float b = Vector3.Dot(oc, ray.direction);
        float disc = b * b - (oc.sqrMagnitude - r * r);

        if (disc < 0f)
        {
            StopFollowing(); // doble clic fuera del planeta: suelta el punto
            return;
        }

        Vector3 hit = ray.origin + ray.direction * (-b - Mathf.Sqrt(disc));
        Vector3 dir = e.InverseTransformDirection((hit - e.position).normalized);

        followLocal = earth.LocalSurfacePoint(dir);
        lastEarthRotation = e.rotation;
        following = true;
        ViewedCity = null;

        // Doble clic sobre una ciudad: vista de la ciudad (cámara cerca, mirándola desde el sur)
        City city = CityAt(dir);
        if (city != null) EnterCityView(city);
    }

    /// <summary>Ciudad cuya vista está activa (null si no hay); la interfaz muestra sus recursos por casilla.</summary>
    public static City ViewedCity { get; private set; }

    // Ciudad bajo la dirección dada (espacio local de la Tierra), si hay alguna
    City CityAt(Vector3 dir)
    {
        CivGame game = CivGame.Instance;
        if (game == null || !game.ready) return null;

        City best = null;
        float bestAngle = game.grid.angularSpacing * 0.9f;

        foreach (City c in game.allCities)
        {
            if (!c.alive) continue;
            float a = Mathf.Acos(Mathf.Clamp(Vector3.Dot(dir, game.grid.dir[c.cell]), -1f, 1f));
            if (a < bestAngle) { bestAngle = a; best = c; }
        }

        return best;
    }

    // Coloca la cámara cerca de la ciudad, algo elevada y al sur, mirándola con el norte arriba
    void EnterCityView(City city)
    {
        CivGame game = CivGame.Instance;
        Transform e = earth.transform;

        followLocal = game.grid.pos[city.cell];
        ViewedCity = city;

        Vector3 normal = e.TransformDirection(game.grid.dir[city.cell]);
        Vector3 north = Vector3.ProjectOnPlane(e.TransformDirection(Vector3.up), normal).normalized;
        float distance = game.grid.spacing * 3.5f;

        transform.position = e.TransformPoint(followLocal) + normal * distance * 0.75f - north * distance * 0.65f;
        lastEarthRotation = e.rotation;
    }

    void StopFollowing()
    {
        ViewedCity = null;
        if (!following) return;
        following = false;

        Vector3 euler = transform.eulerAngles;
        yaw = euler.y;
        pitch = euler.x > 180f ? euler.x - 360f : euler.x;
    }

    // Tras mover la Tierra: la cámara gira con ella (conserva el encuadre) y mira siempre al punto
    void LateUpdate()
    {
        if (!following || earth == null) return;

        Transform e = earth.transform;

        Quaternion delta = e.rotation * Quaternion.Inverse(lastEarthRotation);
        lastEarthRotation = e.rotation;
        transform.position = e.position + delta * (transform.position - e.position);

        Vector3 target = e.TransformPoint(followLocal);
        Vector3 toTarget = target - transform.position;
        if (toTarget.sqrMagnitude > 1e-4f)
            // El norte de la Tierra (su eje local Y) siempre hacia arriba en la pantalla
            transform.rotation = Quaternion.LookRotation(toTarget, e.TransformDirection(Vector3.up));
    }

    [Header("Zoom (rueda del ratón)")]
    [Tooltip("Fracción de la altura sobre la superficie que se avanza por cada muesca de la rueda")]
    public float zoomPerNotch = 0.2f;
    [Tooltip("Altura mínima sobre la superficie, en unidades")]
    public float minAltitude = 0.05f;

    // La rueda acerca (adelante) o aleja (atrás) la cámara a lo largo del rayo que pasa por el cursor,
    // así el zoom se dirige a lo que se ve bajo el ratón. El paso crece con la altura, como la velocidad.
    void HandleZoom()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Approximately(scroll, 0f)) return;

        Camera cam = GetComponent<Camera>();
        if (cam == null) return;

        Vector3 dir = cam.ScreenPointToRay(Input.mousePosition).direction;
        float step = Mathf.Max(minAltitude, Altitude()) * zoomPerNotch * scroll;

        if (earth != null && step > 0f)
        {
            // No atravesar la superficie: se limita el avance a la altura mínima
            float minDist = earth.FinalRadius + minAltitude;
            Vector3 next = transform.position + dir * step;
            Vector3 fromCenter = next - earth.transform.position;
            if (fromCenter.magnitude < minDist)
            {
                next = earth.transform.position + fromCenter.normalized * minDist;
            }
            transform.position = next;
            return;
        }

        transform.position += dir * step;
    }

    void Update()
    {
        HandleDoubleClick();
        HandleZoom();

        if (following)
        {
            // Retroceso suelta el punto (Espacio ya pausa la partida); los cursores siguen moviendo la cámara
            if (Input.GetKeyDown(KeyCode.Backspace)) StopFollowing();
            else { MoveWithArrows(); return; }
        }

        MoveWithArrows();
        LookAtEarthCenter();
    }

    // La cámara siempre mira al centro de la Tierra, con su norte hacia arriba en la pantalla
    void LookAtEarthCenter()
    {
        if (earth == null) return;

        Transform e = earth.transform;
        Vector3 toCenter = e.position - transform.position;
        if (toCenter.sqrMagnitude < 1e-6f) return;

        Vector3 north = e.TransformDirection(Vector3.up);
        if (Vector3.Cross(toCenter, north).sqrMagnitude < 1e-6f * toCenter.sqrMagnitude) return; // justo sobre un polo

        transform.rotation = Quaternion.LookRotation(toCenter, north);
    }

    // Cursores: arriba / abajo llevan la cámara hacia el polo norte / sur; derecha / izquierda hacia el este / oeste.
    // También , y . (oeste / este); + y - acercan y alejan.
    void MoveWithArrows()
    {
        if (earth == null) return;

        float speed = Mathf.Max(moveSpeed, Altitude() * speedPerAltitude);
        if (Input.GetKey(KeyCode.LeftShift)) speed *= fastMultiplier;

        float poleDir = 0f;
        if (Input.GetKey(KeyCode.UpArrow)) poleDir += 1f;
        if (Input.GetKey(KeyCode.DownArrow)) poleDir -= 1f;

        float eastDir = 0f;
        if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.Period)) eastDir += 1f;
        if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.Comma)) eastDir -= 1f;

        float zoomDir = 0f;
        if (Input.GetKey(KeyCode.Plus) || Input.GetKey(KeyCode.KeypadPlus) || Input.GetKey(KeyCode.Equals)) zoomDir += 1f;
        if (Input.GetKey(KeyCode.Minus) || Input.GetKey(KeyCode.KeypadMinus) || Input.GetKey(KeyCode.Slash)) zoomDir -= 1f;

        Transform e = earth.transform;
        Vector3 radial = transform.position - e.position;
        float distance = radial.magnitude;
        if (distance < 1e-4f) return;

        Vector3 north = e.TransformDirection(Vector3.up);
        float step = speed * Time.deltaTime / distance * Mathf.Rad2Deg; // grados de órbita por fotograma

        // Hacia el polo: giro alrededor del eje (radial x norte); este: giro alrededor del eje norte
        Vector3 poleAxis = Vector3.Cross(radial / distance, north);
        Quaternion rot = Quaternion.identity;
        if (poleDir != 0f && poleAxis.sqrMagnitude > 1e-6f)
            rot = Quaternion.AngleAxis(poleDir * step, poleAxis.normalized);
        if (eastDir != 0f)
            rot = Quaternion.AngleAxis(-eastDir * step, north) * rot;

        radial = rot * radial;

        // Zoom con + y -, sin atravesar la superficie
        if (zoomDir != 0f)
        {
            float minDist = earth.FinalRadius + minAltitude;
            float newDist = Mathf.Max(minDist, radial.magnitude - zoomDir * speed * Time.deltaTime);
            radial = radial.normalized * newDist;
        }

        transform.position = e.position + radial;
    }

    float Altitude()
    {
        if (earth == null) return 0f;

        float distance = Vector3.Distance(transform.position, earth.transform.position);
        return Mathf.Max(0f, distance - earth.FinalRadius);
    }
}

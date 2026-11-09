using UnityEngine;
using UnityEngine.EventSystems;

/// Kamera isometric: zoom scroll wheel, pan drag tombol tengah mouse.
/// Rotasi tetap fixed (45 yaw, 30 pitch). Pasang di Main Camera.
[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    public float minZoom = 3f;
    public float maxZoom = 10f;
    public float zoomStep = 1f;
    public float zoomSmooth = 12f;
    public float distance = 30f;

    Camera cam;
    float targetSize;
    Vector3 focus;
    Vector3 lastMouse;

    void Start()
    {
        cam = GetComponent<Camera>();
        targetSize = Mathf.Clamp(cam.orthographicSize, minZoom, maxZoom);
        // titik di lantai yang saat ini dilihat kamera
        focus = transform.position + transform.forward * distance;
        focus.y = 0f;
    }

    void LateUpdate()
    {
        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        // Zoom
        float scroll = Input.mouseScrollDelta.y;
        if (!overUI && Mathf.Abs(scroll) > 0.01f)
            targetSize = Mathf.Clamp(targetSize - scroll * zoomStep, minZoom, maxZoom);
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetSize, 1f - Mathf.Exp(-zoomSmooth * Time.unscaledDeltaTime));

        // Pan (tombol tengah)
        if (Input.GetMouseButtonDown(2)) lastMouse = Input.mousePosition;
        if (Input.GetMouseButton(2) && !overUI)
        {
            Vector3 d = Input.mousePosition - lastMouse;
            lastMouse = Input.mousePosition;

            float perPixel = cam.orthographicSize * 2f / Screen.height;
            float pitch = transform.eulerAngles.x * Mathf.Deg2Rad;
            Vector3 right = transform.right;
            Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

            focus -= (right * d.x + fwd * (d.y / Mathf.Sin(pitch))) * perPixel;
            float half = GameConfig.GridSize / 2f;
            focus.x = Mathf.Clamp(focus.x, 0f, GameConfig.GridSize);
            focus.z = Mathf.Clamp(focus.z, 0f, GameConfig.GridSize);
        }

        transform.position = focus - transform.forward * distance;
    }
}

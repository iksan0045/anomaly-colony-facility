using UnityEngine;

public class UnitController : MonoBehaviour
{
    public UnitData data;
    public CellBase cell;
    [HideInInspector] public UnitVisual visual;

    Vector3 target;
    float idleTimer;

    public bool Arrived { get; private set; } = true;
    System.Collections.Generic.List<Vector3> path;
    int pathIdx;

    public void Bind(UnitData d, Transform home)
    {
        data = d;
        d.controller = this;
        name = "Unit_" + d.id;
        transform.SetParent(home, false);

        visual = GetComponent<UnitVisual>();
        if (visual == null) visual = gameObject.AddComponent<UnitVisual>();
        visual.Setup(d);

        transform.position = RandomPoint();
        target = transform.position;
    }

    public void AttachTo(CellBase c)
    {
        cell = c;
        gameObject.SetActive(true);

        path = CellBase.InstantAssign ? null : GridManager.Instance.FindPath(transform.position, c);
        Debug.Log(name + " path=" + (path == null ? "null" : path.Count.ToString()) + " instant=" + CellBase.InstantAssign);   // hapus setelah beres
        if (path == null || path.Count == 0) { Arrive(); return; }

        Arrived = false;
        pathIdx = 0;
        transform.SetParent(GameManager.Instance.UnitRoot, true);
    }

    public void Detach()
    {
        path = null;
        Arrived = true;
        cell = null;
        visual.SetClip(null);
        transform.SetParent(GameManager.Instance.UnitRoot, true);
        var p = transform.position;
        transform.position = new Vector3(p.x, 0f, p.z);
        gameObject.SetActive(true);
    }
    void Arrive()
    {
        path = null;
        Arrived = true;
        transform.SetParent(cell.transform, false);   // yang dulu dilakukan AttachTo
        visual.SetWalking(false);
        cell.LayoutUnits();
    }

    void WalkStep()
    {
        Vector3 target = path[pathIdx];
        Vector3 to = target - transform.position;
        to.y = 0f;
        float step = data.moveSpeed * GameConfig.AssignWalkBoost * Time.deltaTime;

        if (to.magnitude <= step)
        {
            transform.position = new Vector3(target.x, 0f, target.z);
            if (++pathIdx >= path.Count) Arrive();
            return;
        }

        Vector3 dir = to.normalized;
        transform.position += dir * step;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 10f * Time.deltaTime);
        visual.SetWalking(true);
    }

    void Update()
    {
        if (cell != null) return;
        if (!Arrived) { WalkStep(); return; }
        if (idleTimer > 0f) { idleTimer -= Time.deltaTime; visual.SetWalking(false); return; }

        Vector3 dir = target - transform.position;
        if (dir.sqrMagnitude < 0.01f)
        {
            idleTimer = Random.Range(0.5f, 2.5f);
            target = RandomPoint();
            visual.SetWalking(false);
            return;
        }

        visual.SetWalking(true);
        transform.position = Vector3.MoveTowards(transform.position, target, data.moveSpeed * Time.deltaTime);
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)   // hadap ke arah jalan
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 10f * Time.deltaTime);
    }

    static Vector3 RandomPoint()
    {
        int n = GameConfig.GridSize;
        for (int i = 0; i < 10; i++)
        {
            var p = new Vector3(Random.Range(0.5f, n - 0.5f), 0f, Random.Range(0.5f, n - 0.5f));
            if (GridManager.Instance == null || GridManager.Instance.IsFree(p)) return p;
        }
        return new Vector3(0.5f, 0f, 0.5f);
    }
}
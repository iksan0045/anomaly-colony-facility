using UnityEngine;

public class UnitController : MonoBehaviour
{
    public UnitData data;
    public CellBase cell;
    [HideInInspector] public UnitVisual visual;

    Vector3 target;
    float idleTimer;

    public void Bind(UnitData d, Transform home)
    {
        data = d;
        d.controller = this;
        name = "Unit_" + d.id;
        transform.SetParent(home, false);

        visual = GetComponent<UnitVisual>();
        if (visual == null) visual = gameObject.AddComponent<UnitVisual>();
        visual.Setup(d.archetype, d.variant, d.scale);

        transform.position = RandomPoint();
        target = transform.position;
    }

    public void AttachTo(CellBase c)
    {
        cell = c;
        transform.SetParent(c.transform, false);
        visual.SetWalking(false);
    }

    public void Detach()
    {
        cell = null;
        transform.SetParent(GameManager.Instance.UnitRoot, true);
        var p = transform.position; p.y = 0f; transform.position = p;
        target = p;
        idleTimer = 0f;
    }

    void Update()
    {
        if (cell != null) return;

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
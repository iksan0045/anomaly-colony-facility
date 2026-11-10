using UnityEngine;

/// Mengurus tampilan 3D unit + animasi. Dipasang di root prefab Unit_*.
///
/// Animator Controller yang dipakai model sebaiknya punya:
///   - Parameter bool    "IsWalking"  (Idle -> Walk saat true, kembali saat false)
///   - Parameter trigger (opsional, nanti) "Produce", "Breed", dst. -> PlayTrigger("Produce")
/// Tanpa model/Animator: bola grey-box memantul kecil saat berjalan.
public class UnitVisual : MonoBehaviour
{
    static readonly int WalkHash = Animator.StringToHash("IsWalking");

    Transform fallbackBody;
    GameObject model;
    ClipPlayer clipPlayer;
    Animator anim;
    bool hasWalkParam;
    bool walking;
    float bobTime;
    [SerializeField] bool debugWalking; 

    public void Setup(UnitData d)
    {
        var a = d.archetype;
        var s = d.special;

        fallbackBody = transform.Find("Body");
        transform.localScale = Vector3.one * d.scale;

        GameObject prefab = (s != null && s.model != null) ? s.model
                        : (a != null ? a.GetModel(d.variant) : null);
        if (prefab == null) return;


        model = Instantiate(prefab, transform);
        model.name = "Model";
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;

        // Collider model tidak boleh menghalangi klik ke cell
        foreach (var c in model.GetComponentsInChildren<Collider>()) Destroy(c);
        if (fallbackBody != null) fallbackBody.gameObject.SetActive(false);

        anim = model.GetComponentInChildren<Animator>();
        if (anim == null) return;
        var ctrl = (s != null && s.animatorOverride != null) ? s.animatorOverride
             : (a != null ? a.animatorOverride : null);
        if (ctrl != null && anim.runtimeAnimatorController == null)
            anim.runtimeAnimatorController = ctrl;

        clipPlayer = anim.gameObject.AddComponent<ClipPlayer>();
        clipPlayer.Setup(anim);
        hasWalkParam = HasParam(WalkHash, AnimatorControllerParameterType.Bool);
        hasWalkParam = HasParam(WalkHash, AnimatorControllerParameterType.Bool);
        if (!hasWalkParam)
        Debug.LogWarning(name + ": controller tidak punya parameter bool 'IsWalking'", this);
        if (hasWalkParam && clipPlayer == null) anim.SetBool(WalkHash, false);
    }

    void LateUpdate()
    {
        if (model == null) return;
        model.transform.localPosition = Vector3.zero;   // LateUpdate berjalan setelah animasi dievaluasi
        model.transform.localRotation = Quaternion.identity;
    }

    public void SetWalking(bool value)
    {
        if (walking == value) return;
        walking = value;
        debugWalking = value;
        if (!hasWalkParam) return;

        if (clipPlayer != null && clipPlayer.HasController)
            clipPlayer.SetBool(WalkHash, value);   // yang benar-benar menggerakkan animasi
        anim.SetBool(WalkHash, value);             // cermin saja, supaya tab Parameters ikut berubah
    }

    public void PlayTrigger(string triggerName)
    {
        if (anim == null || anim.runtimeAnimatorController == null) return;
        int hash = Animator.StringToHash(triggerName);
        if (!HasParam(hash, AnimatorControllerParameterType.Trigger)) return;
        if (clipPlayer != null && clipPlayer.HasController) clipPlayer.SetTrigger(hash);
        else anim.SetTrigger(hash);
    }

    

    public void SetClip(AnimationClip clip)
    {
        if (clipPlayer != null) clipPlayer.Play(clip);
    }

    void Update()
    {
        // Fallback grey-box: memantul saat berjalan
        if (model != null || fallbackBody == null) return;
        bobTime = walking ? bobTime + Time.deltaTime * 10f : 0f;
        var p = fallbackBody.localPosition;
        p.y = 0.15f + (walking ? Mathf.Abs(Mathf.Sin(bobTime)) * 0.08f : 0f);
        fallbackBody.localPosition = p;
    }

    bool HasParam(int hash, AnimatorControllerParameterType type)
    {
        if (anim == null || anim.runtimeAnimatorController == null) return false;
        foreach (var p in anim.parameters)
            if (p.nameHash == hash && p.type == type) return true;
        return false;
    }
}

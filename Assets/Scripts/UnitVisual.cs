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
    Animator anim;
    bool hasWalkParam;
    bool walking;
    float bobTime;

    public void Setup(UnitArchetype archetype, int variant, float scale)
    {
        fallbackBody = transform.Find("Body");
        transform.localScale = Vector3.one * scale;

        GameObject prefab = archetype != null ? archetype.GetModel(variant) : null;
        if (prefab == null) return; // tetap pakai bola grey-box

        model = Instantiate(prefab, transform);
        model.name = "Model";
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;

        // Collider model tidak boleh menghalangi klik ke cell
        foreach (var c in model.GetComponentsInChildren<Collider>()) Destroy(c);
        if (fallbackBody != null) fallbackBody.gameObject.SetActive(false);

        anim = model.GetComponentInChildren<Animator>();
        if (anim == null) return;
        if (archetype.animatorOverride != null && anim.runtimeAnimatorController == null)
            anim.runtimeAnimatorController = archetype.animatorOverride;
        hasWalkParam = HasParam(WalkHash, AnimatorControllerParameterType.Bool);
        anim.SetBool(WalkHash, false);
    }

    public void SetWalking(bool value)
    {
        if (walking == value) return;
        walking = value;
        if (hasWalkParam) anim.SetBool(WalkHash, value);
    }

    /// Hook animasi khusus nanti: unit.controller.visual.PlayTrigger("Produce");
    public void PlayTrigger(string triggerName)
    {
        if (anim == null || anim.runtimeAnimatorController == null) return;
        int hash = Animator.StringToHash(triggerName);
        if (HasParam(hash, AnimatorControllerParameterType.Trigger)) anim.SetTrigger(hash);
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

using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// Dipasang di GameObject Animator model unit. Memutar clip (loop) di atas Animator Controller:
/// controller tetap jalan (idle/walk), clip di-blend masuk/keluar saat Play(clip) / Play(null).
/// Karena graph ini memegang output Animator, parameter controller harus diatur lewat SetBool/SetTrigger di sini.
/// Clip tidak boleh bertanda Legacy dan harus memakai rig yang sama dengan model.
[DisallowMultipleComponent]
public class ClipPlayer : MonoBehaviour
{
    public float blendSpeed = 6f;

    PlayableGraph graph;
    AnimationMixerPlayable mixer;
    AnimatorControllerPlayable ctrl;
    AnimationClipPlayable clipPlayable;
    AnimationClip wanted, loaded;
    bool hasCtrl, ready;
    float time, weight;
    

    public bool HasController { get { return hasCtrl; } }

    public void Setup(Animator animator)
    {
        if (animator == null) return;
        animator.applyRootMotion = false;

        graph = PlayableGraph.Create("ClipPlayer_" + name);
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        var output = AnimationPlayableOutput.Create(graph, "Anim", animator);

        mixer = AnimationMixerPlayable.Create(graph, 2);
        output.SetSourcePlayable(mixer);

        var rc = animator.runtimeAnimatorController;
        if (rc != null)
        {
            ctrl = AnimatorControllerPlayable.Create(graph, rc);
            graph.Connect(ctrl, 0, mixer, 0);
            hasCtrl = true;
        }

        mixer.SetInputWeight(0, hasCtrl ? 1f : 0f);
        mixer.SetInputWeight(1, 0f);
        graph.Play();
        ready = true;
    }

    /// null = berhenti (kembali ke controller). Aman dipanggil tiap frame dengan clip yang sama.
    public void Play(AnimationClip clip) { wanted = clip; }

    public void SetBool(int hash, bool value) { if (hasCtrl) ctrl.SetBool(hash, value); }
    public void SetTrigger(int hash) { if (hasCtrl) ctrl.SetTrigger(hash); }

    void Update()
    {
        if (!ready) return;

        // Ganti clip hanya setelah yang lama selesai memudar
        if (wanted != loaded && (loaded == null || weight <= 0.001f || !hasCtrl))
            Load(wanted);

        float target = (loaded != null && wanted == loaded) ? 1f : 0f;
        weight = Mathf.MoveTowards(weight, target, blendSpeed * Time.deltaTime);

        if (loaded != null)
        {
            time += Time.deltaTime;                       // waktu manual: loop pasti
            if (loaded.length > 0f) time %= loaded.length;
            clipPlayable.SetTime(time);
        }

        mixer.SetInputWeight(0, hasCtrl ? 1f - weight : 0f);
        mixer.SetInputWeight(1, weight);
    }

    

    void Load(AnimationClip clip)
    {
        if (loaded != null)
        {
            graph.Disconnect(mixer, 1);
            graph.DestroyPlayable(clipPlayable);
        }
        loaded = clip;
        time = 0f;
        if (clip == null) return;

        clipPlayable = AnimationClipPlayable.Create(graph, clip);
        clipPlayable.SetSpeed(0.0);
        graph.Connect(clipPlayable, 0, mixer, 1);
    }

    void OnDestroy()
    {
        if (graph.IsValid()) graph.Destroy();
    }
}

using UnityEngine;

/// Unit spesial: identitas, model, dan stat tetap sendiri. Satu asset per unit spesial.
/// Tipe tetap Producer/Fighter/Breeder, jadi aturan cell tidak berubah.
/// Daftarkan ke UnitDatabase.specials (Assets/Data/UnitDatabase.asset).
[CreateAssetMenu(menuName = "Facility/Special Unit", fileName = "Special_New")]
public class SpecialUnit : ScriptableObject
{
    public string displayName = "Special";
    [Header("Profil")]
    public Sprite portrait;
    [Tooltip("Producer/Fighter/Breeder. Leader tidak dipakai di sini.")]
    public UnitType type = UnitType.Fighter;
    [Tooltip("true = hanya boleh ada satu di fasilitas.")]
    public bool unique = true;

    [Header("Tampilan")]
    [Tooltip("Kosong = bola grey-box.")]
    public GameObject model;
    [Tooltip("Opsional. Dipakai jika Animator di model belum punya controller. Kosong = pakai milik archetype tipenya.")]
    public RuntimeAnimatorController animatorOverride;
    [Tooltip("Opsional. Kosong = pakai breedClip archetype tipenya.")]
    public AnimationClip breedClip;
    [Tooltip("Clip yang dimainkan LEADER saat breeding dengan unit bertipe ini. Clip untuk unit ini sendiri tetap di Breed Clip.")]
    public AnimationClip leaderBreedClip;
    public bool leaderFacesMe = false;
    public float scale = 1f;
    [Tooltip("Lama breeding (tick). 0 = ikut archetype tipenya.")]
    [Min(0)] public int breedTicks = 0;

    [Header("Stat tetap")]
    [Range(1, 10)] public int power = 8;
    [Range(1, 10)] public int fertility = 5;
    public float moveSpeed = 1.2f;
}

# Anomaly Containment Facility — MVP (Unity 3D)

## Struktur Project

```
Assets/
└── Scripts/
    ├── Core/
    │   ├── GameBootstrap.cs   # auto-build kamera, cahaya, manager, UI saat Play
    │   ├── GameManager.cs     # tick, resource, unit, raid, unlock, log
    │   └── GameConfig.cs      # semua angka balancing + warna
    ├── Data/
    │   ├── Enums.cs           # CellType, UnitType
    │   └── UnitData.cs        # data anomali (power, mood, cell)
    ├── Grid/
    │   ├── GridManager.cs     # grid 8x8, mode place, seleksi, rotasi
    │   └── Cell.cs            # cell di grid: assign, produksi, breeding, visual unit
    └── UI/
        └── UIController.cs    # HUD, palette, panel detail, RAID, log (UGUI via kode)
```

File yang diberikan datar (tanpa folder). Buat folder di atas di Project window
lalu pindahkan scriptnya. Unity tidak peduli lokasi folder script.

## Setup

1. Unity LTS (2022.3 atau Unity 6), template **3D (Built-in)**.
2. Copy semua `.cs` ke `Assets/Scripts/`.
3. *Project Settings > Player > Active Input Handling* = **Input Manager (Old)** atau **Both**
   (script memakai `Input.GetMouseButtonDown`).
4. Buka scene kosong, tekan **Play**. Tidak ada objek yang perlu dibuat manual.

## Kontrol (mouse only)

| Aksi | Cara |
|---|---|
| Mode place | klik cell di panel kiri |
| Taruh cell | klik kiri di grid (hijau = valid, merah = tidak) |
| Rotasi 1x2 | klik kanan saat mode place |
| Batal place | klik tombol cell lagi (atau Esc) |
| Lihat detail | klik cell di grid |
| Assign unit | pilih cell, klik "Assign: ..." di panel kanan |
| Keluarkan unit | klik "Keluarkan: ..." di panel kanan |
| Raid | tombol RAID bawah tengah |

## Aturan yang diimplementasi

- Tick 1 detik. Resource max 50, +25 per STORAGE. Max unit 12.
- DETENTION: mood +5/tick. Unit di luar DETENTION: mood -2/tick.
- PRODUCTION: PRODUCER +1 Energy/tick, FIGHTER +1 Material/tick.
- BREEDING: 2 unit, 10 tick, spawn 1 unit (tipe acak, power = rata-rata orang tua ± 2).
- Ration: tiap unit makan 1 Material/tick.
- RAID: total power semua FIGHTER vs `3 + raidCount*2`. Menang = capture 1 unit acak, kalah = 1 fighter power -2. Cooldown 15 tick.

## Asumsi yang saya tambahkan (spec belum menentukan, ubah di GameConfig/Cell)

- **Biaya build** dalam Energy: Detention 5, Production 10, Breeding 20, Storage 10, Control 15. Start: 30 Energy, 20 Material. (Set `EnergyCost` ke 0 untuk menonaktifkan.)
- **Unit awal**: 1 Producer (P4), 1 Fighter (P6), 1 Breeder (P5).
- **Cell terbuka awal**: Detention, Production, Control. CONTROL ke-1 membuka Storage, ke-2 membuka Breeding, ke-3 dan seterusnya +2 max unit.
- **Kapasitas**: Detention 1, Production 2 (karena 1x2), Breeding 2. Production menolak Breeder; Breeding hanya menerima Breeder.
- **Mood 0**: unit di PRODUCTION berhenti produksi. **Material habis**: mood tambahan -2/tick.
- Stat `fertility` Breeder belum dipakai (spec hanya mendefinisikan `power` di data unit).
- Cell dibuat dari primitive Cube lewat kode, bukan prefab. Untuk prefab terpisah per tipe, ganti `GameObject.CreatePrimitive` di `Cell.Init`.

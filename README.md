# 👻 Misteri KKN: Desa Terkutuk

> **Realistic Indonesian Village Horror Game** — built with Unity

[![Unity](https://img.shields.io/badge/Engine-Unity-black?logo=unity)](https://unity.com/)
[![C#](https://img.shields.io/badge/Language-C%23-239120?logo=csharp)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![Platform](https://img.shields.io/badge/Platform-PC-blue)](https://unity.com/)
[![Genre](https://img.shields.io/badge/Genre-Horror-darkred)]()

---

## 📖 Tentang Game

**Misteri KKN: Desa Terkutuk** adalah game horror realistis bergaya first-person yang terinspirasi dari mitos dan budaya pedesaan Indonesia. Pemain berperan sebagai mahasiswa KKN yang terjebak di sebuah desa terkutuk, menghadapi entitas supranatural yang memiliki kecerdasan dan perilaku adaptif.

Game ini mengedepankan:
- **Atmosfer horror yang autentik** — setting desa Indonesia dengan elemen budaya lokal
- **Ghost AI adaptif** — hantu dengan perilaku dinamis yang belajar dari tindakan pemain
- **Mekanik psikologis** — sistem sanity yang memengaruhi persepsi dan kemampuan pemain
- **Narasi bercabang** — pilihan pemain memengaruhi jalannya cerita

---

## 🗂️ Struktur Scene

| Scene | Keterangan |
|-------|-----------|
| `MainMenu` | Layar utama, pemilihan chapter |
| `Intro` | Cutscene pembuka, pengenalan cerita |
| `Chapter1` | Gameplay utama — Desa Terkutuk |
| `Gameplay` | Scene runtime / session aktif |
| `Ending` | Resolusi cerita berdasarkan pilihan pemain |

Scene dikelola oleh `KKN_SceneManager` (singleton `DontDestroyOnLoad`) dan diinisialisasi melalui `KKN_Bootstrap`.

---

## 🧠 Sistem & Arsitektur

### Namespace

```
KKN.Game.Core       — sistem inti (SaveSystem, Bootstrap, SceneManager)
KKN.Game.Systems    — gameplay systems (Sanity, Flashlight, Inventory, Objective)
KKN.Game.Enemy      — Ghost AI dan entitas musuh
KKN.Game.Puzzle     — puzzle mechanics (GeneratorPuzzle, dll.)
KKN.Game.UI         — seluruh UI runtime
```

### Core Systems

| Script | Namespace | Fungsi |
|--------|-----------|--------|
| `KKN_SceneManager.cs` | `KKN.Game.Core` | Manajemen scene (singleton, DontDestroyOnLoad) |
| `KKN_Bootstrap.cs` | `KKN.Game.Core` | Inisialisasi runtime, injeksi dependency |
| `SaveSystem.cs` | `KKN.Game.Core` | Penyimpanan & pemuatan data (partial class) |
| `ObjectiveManager.cs` | `KKN.Game.Systems` | Sistem misi dan tujuan pemain |
| `SanitySystem.cs` | `KKN.Game.Systems` | Sistem kewarasan — memengaruhi gameplay & visual |
| `FlashlightSystem.cs` | `KKN.Game.Systems` | Manajemen senter (baterai, flicker, dll.) |
| `InventorySystem.cs` | `KKN.Game.Systems` | Inventory 5-tab: SEMUA / KUNCI / BAHAN / DOKUMEN / LAINNYA |
| `PlayerMovement.cs` | `KKN.Game.Systems` | Kontrol karakter pemain |
| `GeneratorPuzzle.cs` | `KKN.Game.Puzzle` | Puzzle generator listrik |

---

## 👁️ Ghost AI System

AI hantu dibangun menggunakan arsitektur **state machine** yang kompleks dengan perilaku adaptif.

### State Machine

```
IDLE ──► PATROL ──► INVESTIGATE
  │           │           │
  ▼           ▼           ▼
OBSERVE     LURK        CHASE
  │           │           │
  ▼           ▼           ▼
WATCH       PEEK      ATTACK / MANIFEST
```

| State | Deskripsi |
|-------|-----------|
| `Idle` | Diam, menunggu stimulus |
| `Patrol` | Berkeliling area berdasarkan waypoint |
| `Investigate` | Menyelidiki suara / jejak pemain |
| `Observe` | Mengamati dari jarak jauh tanpa mendekati |
| `Lurk` | Mengikuti pemain secara tersembunyi |
| `Watch` | Mengintai dari posisi statis |
| `Peek` | Mengintip dari balik objek / pintu |
| `Chase` | Mengejar pemain secara agresif |
| `Manifest` | Manifestasi fisik — jumpscare / serangan |

### Personality

| Enum | Gaya Berburu |
|------|-------------|
| `EnemyPersonality.Stalker` | Sabar, mengikuti, menunggu momen |
| `EnemyPersonality.Trickster` | Acak, menipu, muncul tak terduga |

### Komponen AI Pendukung

| Script | Fungsi |
|--------|--------|
| `GhostAI.cs` | File utama AI (partial class, modular) |
| `GhostZoneData.cs` | Data zona & area preferensi hantu |
| `GhostInterestPoint.cs` | Titik ketertarikan hantu di scene |
| `ParanormalEventManager.cs` | Pemicu event paranormal & atmosfer |

### Fitur AI Lanjutan

- **Frustration & Adaptive Difficulty** — hantu semakin agresif jika pemain terus menghindarinya
- **Memory System** — hantu mengingat lokasi terakhir pemain
- **Landmark-Aware Navigation** — navigasi berdasarkan `GhostZoneData` & `GhostInterestPoint`
- **Tension-Based Manifestation** — hantu memanifestasi berdasarkan akumulasi ketegangan

---

## 🎨 UI System

### Canvas & Layer

| Canvas | Isi |
|--------|-----|
| **HUD Canvas** | Sanity vignette, crosshair, indikator status |
| **Inventory Canvas** | 5 tab item: SEMUA / KUNCI / BAHAN / DOKUMEN / LAINNYA |
| **Settings Canvas** | Volume, sensitivitas, grafis — tersimpan di `PlayerPrefs` |
| **Document Zoom Canvas** | Viewer dokumen / surat dalam game |
| **Jumpscare Canvas** | Overlay jumpscare dengan animasi |

### Runtime UI Scripts

| Script | Namespace | Fungsi |
|--------|-----------|--------|
| `PlayerHUD.cs` | `KKN.Game.UI` | HUD utama pemain |
| `TabInventoryUI.cs` | `KKN.Game.UI` | Inventory tab-based |
| `SettingsMenu.cs` | `KKN.Game.UI` | Menu pengaturan |
| `DocumentZoomViewer.cs` | `KKN.Game.UI` | Tampilan dokumen fullscreen |
| `JumpscareManager.cs` | `KKN.Game.UI` | Manajemen event jumpscare |
| `InventorySlotUI.cs` | `KKN.Game.UI` | Slot item di inventory |
| `DocumentSlotUI.cs` | `KKN.Game.UI` | Slot dokumen di inventory |

### Design Language
- **Palet warna:** Gelap (`#0A0A0A`) + Merah tua (`#8B0000`) + Emas (`#C8A45A`)
- **Builder tool:** `KKN_UICanvas_Builder.cs` (Editor Window, ~1635 baris)
- **Custom Inspector:** `KKN_UICanvas_Editor.cs` dengan Auto-Assign via reflection

---

## 📁 Struktur Folder (Rekomendasi)

```
Assets/
├── Scripts/
│   ├── Core/               # KKN.Game.Core
│   │   ├── KKN_SceneManager.cs
│   │   ├── KKN_Bootstrap.cs
│   │   └── SaveSystem.cs (partial)
│   ├── Systems/            # KKN.Game.Systems
│   │   ├── SanitySystem.cs
│   │   ├── FlashlightSystem.cs
│   │   ├── InventorySystem.cs
│   │   ├── ObjectiveManager.cs
│   │   └── PlayerMovement.cs
│   ├── Enemy/              # KKN.Game.Enemy
│   │   ├── GhostAI.cs
│   │   ├── GhostZoneData.cs
│   │   ├── GhostInterestPoint.cs
│   │   └── ParanormalEventManager.cs
│   ├── Puzzle/             # KKN.Game.Puzzle
│   │   └── GeneratorPuzzle.cs
│   └── UI/                 # KKN.Game.UI
│       ├── PlayerHUD.cs
│       ├── TabInventoryUI.cs
│       ├── SettingsMenu.cs
│       ├── DocumentZoomViewer.cs
│       ├── JumpscareManager.cs
│       ├── InventorySlotUI.cs
│       └── DocumentSlotUI.cs
├── Editor/
│   ├── KKN_UICanvas_Builder.cs
│   └── KKN_UICanvas_Editor.cs
├── Scenes/
│   ├── MainMenu.unity
│   ├── Intro.unity
│   ├── Gameplay.unity
│   ├── Chapter1.unity
│   └── Ending.unity
├── Prefabs/
├── Art/
│   ├── UI/
│   └── Environment/
└── Audio/
```

---

## 🔧 Konvensi & Konstanta

```csharp
// Tag pemain
GameConstants.TAG_PLAYER  // = "Player"

// Namespace penuh
KKN.Game.Core
KKN.Game.Systems
KKN.Game.Puzzle
KKN.Game.Enemy
KKN.Game.UI
```

---

## 🚀 Setup & Cara Menjalankan

### Persyaratan
- **Unity** 2022.3 LTS atau lebih baru
- **.NET Standard 2.1**
- Platform target: **PC (Windows/Mac/Linux)**

### Langkah Setup

```bash
# 1. Clone repository
git clone https://github.com/zaaammmmm/misteri-kkn.git

# 2. Buka dengan Unity Hub
#    File > Open Project > pilih folder hasil clone

# 3. Buka scene Bootstrap terlebih dahulu
#    Assets/Scenes/MainMenu.unity
```

> ⚠️ **Penting:** Selalu buka scene melalui `MainMenu` atau gunakan `KKN_Bootstrap` untuk memastikan semua singleton terinisialisasi dengan benar sebelum gameplay dimulai.

---

## 🗺️ Roadmap

- [x] Scene management system (DontDestroyOnLoad)
- [x] SaveSystem (partial class architecture)
- [x] GhostAI state machine dasar
- [x] UI Canvas system (HUD, Inventory, Settings, Document, Jumpscare)
- [x] Sanity & Flashlight system
- [x] Ghost personality (Stalker / Trickster)
- [x] Advanced AI states (Observe, Lurk, Watch, Peek)
- [x] Frustration & adaptive difficulty system
- [x] Landmark-aware navigation (GhostZoneData / GhostInterestPoint)
- [x] Tension-based manifestation system
- [x] ParanormalEventManager
- [ ] Chapter 2 & seterusnya
- [ ] Audio sistem terintegrasi (musik adaptif, FMOD)
- [ ] Build & packaging final

---

## 👤 Developer

| Nama | Peran |
|------|-------|
| **Zammm** | Game Developer (Unity / C#) |

---

## 📄 Lisensi

Proyek ini bersifat **privat**. Semua hak cipta dimiliki oleh developer.

---

<p align="center">
  <i>"Desa itu diam... tapi tidak tidur."</i>
</p>

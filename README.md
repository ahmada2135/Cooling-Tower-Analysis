# Cooling Tower Simulation

Aplikasi simulasi **Cooling Tower** berbasis Windows Forms dengan analisis multi-domain (Time, Frequency, Laplace, Z-Domain) dan visualisasi 3D.

## 📋 Fitur Utama

### 1. **Parameter Input**
Konfigurasi 5 sensor cooling tower:
- **Temperature** (SHT85) - 400 Hz
- **Humidity** (SHT85) - 400 Hz  
- **Airflow** (Testo 440) - 1.25 Hz
- **Vibration** (PCB 352C33) - 10 kHz
- **Dissolved Oxygen** (Apera DO850) - 0.033 Hz

### 2. **Visualisasi Real-Time**
- **Time Domain**: Grafik sinyal sensor real-time dengan multi-rate sampling
- **Frequency Domain**: Analisis spektrum FFT untuk setiap sensor
- **S-Domain (Laplace)**: Transfer function & pole plot untuk analisis stabilitas
- **Z-Domain**: Pole-zero map diskrit dengan unit circle

### 3. **Physics Models**
Menampilkan rumus fisika yang digunakan:
- Rumus input sensor (SHT85, Testo 440, PCB, Apera)
- Rumus simulasi program (model matematis sistem)

### 4. **3D Model Visualization**
Visualisasi model 3D cooling tower menggunakan WebView2 dan model-viewer.

## 🛠️ Teknologi yang Digunakan

- **C# .NET Framework 4.8** - Windows Forms Application
- **OxyPlot** - Plotting library untuk grafik
- **MathNet.Numerics** - FFT dan analisis matematika
- **WebView2** - Render model 3D (.glb)
- **Multi-rate Sampling** - Sampling berbeda untuk tiap sensor

## 📦 Dependencies

```xml
- MathNet.Numerics 5.0.0
- OxyPlot.Core 2.1.2
- OxyPlot.WindowsForms 2.1.2
- Microsoft.Web.WebView2 1.0.3595.46
```

## 🚀 Cara Menjalankan

1. **Clone repository**
   ```bash
   git clone <repository-url>
   cd CoolingTowerSimulation
   ```

2. **Buka di Visual Studio**
   - Buka file `CoolingTowerSimulation.sln`
   - Restore NuGet packages (otomatis)

3. **Build & Run**
   - Tekan `F5` atau klik **Start**
   - Pastikan WebView2 Runtime terinstall

4. **Operasional**
   - Atur parameter sensor di tab "Parameter Input"
   - Klik **▶ Start Realtime Simulation**
   - Ubah parameter saat simulasi berjalan untuk melihat efek real-time
   - Klik **🔄 Update FFT/Laplace/Z Analysis** untuk update analisis statis

## 📊 Struktur Tab

1. **Parameter Input** - Konfigurasi sensor & kontrol simulasi
2. **Physics Models** - Rumus sensor & simulasi
3. **3D Model Visualization** - Visualisasi cooling tower 3D
4. **Time Domain (REALTIME)** - Grafik sinyal real-time
5. **Frequency Domain (FFT)** - Spektrum frekuensi
6. **S-Domain (Laplace)** - Analisis transfer function
7. **Z-Domain (Discrete)** - Pole-zero map diskrit

## 🔬 Model Fisika

### Multi-Rate Sampling Strategy
| Sensor | Sampling Rate | Time Sampling (Ts) |
|--------|--------------|-------------------|
| Temperature | 400 Hz | 2.5 ms |
| Humidity | 400 Hz | 2.5 ms |
| Airflow | 1.25 Hz | 0.8 s |
| Vibration | 10 kHz | 0.1 ms |
| Dissolved Oxygen | 0.033 Hz | 30 s |

### Transfer Function (S-Domain)
```
G_sys(s) = K_sys / (τ_sys·s + 1)
```
- Stabilitas: Pole di Left Half Plane (LHP)
- Feedback: Closed-loop dengan gain reduction

### Discrete System (Z-Domain)
```
z = e^(sT)
Stability: |z| < 1
```

## 📁 File Penting

- `Form1.cs` - Main application logic
- `Program.cs` - Entry point
- `cooling_tower.glb` - Model 3D (opsional)
- `input_formulas.png` - Gambar rumus input sensor
- `simulation_formulas.png` - Gambar rumus simulasi

## ⚙️ Konfigurasi

### Time Constants (τ)
- Temperature: 0.5 s
- Humidity: 0.67 s
- Airflow: 0.2 s
- Vibration: 0.02 s
- Dissolved Oxygen: 1.25 s

### System Parameters
- Effective Gain (K_eff): 1.0
- Feedback Gain (K_fb): 0.5
- Simulation Window: 10 seconds
- Update Rate: 50 ms

## 🐛 Troubleshooting

**WebView2 tidak muncul?**
- Install [WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/)

**Model 3D tidak tampil?**
- Pastikan file `cooling_tower.glb` ada di folder output (`bin/Debug/` atau `bin/Release/`)

**NuGet restore gagal?**
- Klik kanan solution → **Restore NuGet Packages**
- Atau jalankan: `nuget restore CoolingTowerSimulation.sln`

## 📝 License

Copyright © 2025. Project untuk simulasi dan analisis sistem cooling tower.

## 👨‍💻 Author

Cooling Tower Simulation - Multi-Domain Analysis System

---

**Note**: Parameter dapat diubah real-time saat simulasi berjalan untuk melihat efek dinamis pada semua domain analisis.

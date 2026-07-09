# HNX OS Optimizer ⚡

![HNX OS Optimizer](background.png)

HNX OS Optimizer, Windows 10 ve 11 işletim sistemlerini oyun ve genel masaüstü performansı için maksimum düzeyde optimize eden, modern, animasyonlu ve stabil C# WPF uygulamasıdır. Proje, tamamen yerel Windows API'leri, PowerShell komutları ve Kayıt Defteri (Registry) yöntemleri kullanılarak sıfırdan geliştirilmiştir.

Uygulamanın en önemli özelliği, yapılan tüm optimizasyon işlemlerinin yedeklenip istendiği takdirde **Geri Alma Merkezi** üzerinden güvenle geri yüklenebilmesidir.

---

## 🎨 Tasarım ve Arayüz Özellikleri
- **Koyu Tema & Modern Akrilik Arayüz:** Gözü yormayan renk paleti (`Blue-Violet` ve `Neon Pink` tonları).
- **Program Klasöründen Okunan Arka Plan:** Programın çalıştığı dizindeki `background.png` dosyası otomatik olarak yüklenip şık bir akrilik filtre ile arka plan olarak kullanılır.
- **Yumuşak Geçişler ve Animasyonlar:** Sayfa geçişleri sırasında yumuşak fade/slide animasyonları, butonlarda hover tepkimeleri ve işlem sırasında dalgalı efekt yapan modern animasyonlu ProgressBar.
- **Sol Kenar Menüsü:** Simgeli ve başlık açıklamalı modern navigasyon sistemi.

---

## 🛠️ Temel Özellikler

### 1. Performans
* **Güç Planı Seçici:** Güç Tasarrufu, Dengeli, Yüksek Performans ve Nihai Performans planları arasında anında geçiş (Eğer Nihai Performans planı mevcut değilse sistemde otomatik olarak oluşturulur).
* **Gereksiz Hizmetleri Kapatma (Toggle):** SysMain (Superfetch), DiagTrack (Müşteri Deneyimi Teşhisi), dmwappushservice (WAP İtme), WSearch (Arama İndeksleme), Xbox Live Hizmetleri, Print Spooler (Yazıcı kullanmayanlar için), BITS ve Windows Update servislerini devre dışı bırakabilme.
* **Görsel Efektleri Kapatma:** Pencere animasyonlarını, pencere şeffaflıklarını, Aero Peek ve Aero Shake özelliklerini kapatarak işlemci ve RAM üzerindeki yükü sıfırlama.
* **Windows Game Mode & GPU Önceliği:** Windows Oyun Modu'nu aktifleştirme ve foreground (ön plan) uygulamaları için GPU önceliğini yükseltme (`Win32PrioritySeparation=38`).

### 2. Gizlilik
* **Telemetri Seviyesi Kaydırıcısı (Slider):** Teşhis verilerini 0 (Sadece Güvenlik) ile 3 (Tam Veri Gönderimi) arasında ayarlayabilme.
* **Telemetri / İzleme Kapatıcılar:** Cortana sesli asistanı, Windows 11 Copilot entegrasyonu, OneDrive eşitlemesi, Edge ve Office arka plan telemetrileri, Reklam Kimliği takibi, Konum Hizmetleri ve Windows Geri Bildirim Sıklığı anketlerini devre dışı bırakabilme.

### 3. Ağ & İnternet
* **DNS Değiştirici:** Hazır listeden Google, Cloudflare, Quad9, OpenDNS, AdGuard DNS adreslerini tek tıkla tanımlayabilme veya özel DNS IP'leri girebilme.
* **TCP Optimizasyonları:** Nagle's Algorithm (TCP No Delay) kapatarak oyunlarda gecikmeyi (ping) düşürme, TCP Window boyutunu otomatik olarak optimize etme ve Windows'un rezerve ettiği %20 QoS limitini kaldırma.
* **DNS Önbelleği Temizleme:** Bağlantı hatalarını düzeltmek için DNS resolver önbelleğini sıfırlayan hızlı buton.
* **Hosts Dosyası Düzenleyici:** Entegre metin kutusu aracılığıyla hosts dosyasını görüntüleme, doğrudan düzenleyip kaydetme veya yedekten geri dönme.
* **Ping & Shodan.io Sorgulama:** IP/Host adreslerine ping atma veya Shodan InternetDB API üzerinden açık port ve zafiyet kontrolü yapabilme.

### 4. Temizlik
* **Sistem Çöpü Temizleyici:** Temp klasörleri, Prefetch (Ön yükleme) dizini, Windows Update indirme önbelleği ve Geri Dönüşüm Kutusu'nu toplu veya seçmeli temizleme.
* **Metro (UWP) Uygulama Kaldırıcı:** Cortana, OneDrive, Xbox, Skype, Hava Durumu, Mail gibi arka planda RAM tüketen yerleşik Windows uygulamalarını listeleme ve toplu olarak sistemden kaldırma.
* **Başlangıç Yöneticisi:** Windows açılışında otomatik çalışan kayıt defteri ve başlangıç klasöründeki programları listeleme ve kaldırma.

### 5. Ek Araçlar
* **Kayıt Defteri Düzeltici:** Bozuk dosya uzantı ilişkilendirmelerini ve geçersiz Explorer MRU (geçmiş) kayıtlarını tarayıp düzeltme.
* **Sağ Tık Menü Düzenleyici:** Masaüstüne "Komut Penceresi Aç", "Not Defteri ile Aç", "PowerShell Aç" kısayollarını ekleme veya kaldırma.
* **PATH Değişkenleri Editörü:** Sistem PATH yollarını listeleme, silme ve yenilerini ekleme.
* **Donanım İnceleme:** WMI gerektirmeden hızlıca işlemci (CPU), ekran kartı (GPU), RAM, depolama diskleri ve Windows mimari detaylarını okuma.
* **Dosya Kilidi Çözücü:** Windows **Restart Manager API** kullanarak silinemeyen/kilitli bir dosyayı hangi arka plan programının kullandığını (PID ve isimle) bulma ve kilidi kaldırarak işlemi sonlandırma.

### 6. Geri Alma Merkezi (Rollback Center)
* Yapılan tüm optimizasyon adımları tarih, saat, işlem adı ve durum bilgisiyle burada loglanır.
* **Yedekten Tümünü Geri Yükle** seçeneği ile yapılan sistem ayarları eski haline getirilir.
* **Sistem Geri Yükleme Noktası:** Her kritik işlemden önce otomatik olarak oluşturulur (Geri Alma Merkezi'nden manuel olarak da tetiklenebilir).
* **Dosya & Uygulama Uyarısı:** Silinen geçici dosyaların ve kaldırılan metro uygulamalarının yedekten geri getirilemeyeceği konusunda kullanıcıyı açıkça uyarır.
* **Yedekler Dizini:** `C:\HNX_Backup\` (Registry, DNS, Servis başlangıç durumları ve Hosts dosyası burada JSON ve .bak formatında saklanır).
* **Sistem Günlüğü (Log):** Tüm işlemlerin detayı ve oluşan hatalar anlık olarak `C:\HNX_Log.txt` dosyasına yazılır.

---

## 💻 Teknik Gereksinimler & Kurulum
- **Platform:** Windows 10 / 11 (x64)
- **Framework:** .NET 8.0 veya üzeri (.NET 10.0 test edilmiştir)
- **Yetki:** Sistem seviyesinde değişiklik yapabilmesi için uygulamanın **Yönetici Olarak (Administrator)** çalıştırılması zorunludur. Projede gömülü olan `app.manifest` bu yükseltmeyi otomatik olarak talep eder.

### Derleme (Build)
Proje klasörüne komut satırından (CMD / PowerShell) girerek aşağıdaki komutla derleyebilirsiniz:
```bash
dotnet build -c Release
```
Derleme bittiğinde, çalıştırılabilir EXE dosyası ve `background.png` kaynağı aşağıdaki dizine aktarılacaktır:
`bin\Release\net10.0-windows\HNXOSOptimizer.exe`

---

## 👤 Geliştirici ve İletişim
- **GitHub:** [Henox77](https://github.com/Henox77)
- **Instagram:** [@efeyylw](https://instagram.com/efeyylw)

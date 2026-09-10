# HaftalıkYazılım

Yazılım üzerine haftalık teknik yazıların yayımlandığı sade bir web sitesi.
Her yazı bir **PDF** olarak yüklenir; okuyucu sitede görüntüler (yakınlaştırma,
sayfa geçişi, okuma ilerleme çubuğu) ve dilerse indirir.

- **Backend:** ASP.NET Core Web API (C#, **.NET 10**) + EF Core + **PostgreSQL**
- **Frontend:** saf HTML + CSS + JavaScript (ES modülleri). Derleme adımı YOK,
  framework YOK. Tek gömülü kütüphane: PDF görüntüleme için **PDF.js**.
- **Kimlik doğrulama:** çerez (cookie) tabanlı. Roller: `Author`, `Admin`.
  Okumak için hesap gerekmez.
- **Dış NuGet paketleri (az):** `Npgsql.EntityFrameworkCore.PostgreSQL`,
  `Microsoft.EntityFrameworkCore.Design` (tasarım zamanı), `Microsoft.AspNetCore.OpenApi`.
  (Profil fotoğrafı tarayıcıda küçültülür → sunucuda görsel kütüphanesi yok.)

---

## 1. Gereksinimler

- .NET SDK **10.x**
- **PostgreSQL 14+** (yerelde geliştirme için de gerekir)
- Modern bir tarayıcı

### ⚠️ ".NET workload" hatası

Bu makinedeki .NET "workload set" kaydı bozuktu; düz bir ASP.NET Core projesi bile
derlenmiyordu. Kök dizindeki [`Directory.Build.props`](Directory.Build.props)
`MSBuildEnableWorkloadResolver=false` ile bunu kapatıyor (proje workload
kullanmıyor → güvenli). Kalıcı düzeltme: `sudo dotnet workload repair`.

---

## 2. Yerelde çalıştırma

### 2.1 PostgreSQL veritabanını hazırla (bir kez)

```bash
# PostgreSQL çalışıyor olmalı. Sonra:
psql -d postgres -c "CREATE ROLE haftalikyazilim LOGIN PASSWORD 'haftalikyazilim_dev';"
psql -d postgres -c "CREATE DATABASE haftalikyazilim OWNER haftalikyazilim;"
```

Bağlantı dizesi `appsettings.json → ConnectionStrings:Default` içinde bu değerlerle
hazır. Farklıysa oradan değiştir (ya da `ConnectionStrings__Default` ortam değişkeni ver).

### 2.2 Şemayı oluştur

```bash
dotnet tool restore
dotnet ef database update --project src/HaftalikYazilim.Web
```

`appsettings.json → Database:SeedOnStartup` zaten `true`; uygulama ilk açılışta
bekleyen migration'ları uygular ve **hiç kullanıcı yoksa** bir admin oluşturur:

| Alan | Varsayılan |
|---|---|
| E-posta | `admin@haftalikyazilim.local` |
| Şifre | `DegistirBeniAdmin!2026` — **gerçek ortamda değiştir** |

### 2.3 Çalıştır

```bash
dotnet run --project src/HaftalikYazilim.Web
```

→ **http://localhost:5099**  ·  Giriş: `/giris.html`  ·  VS Code'da **F5** de çalışır.

---

## 3. Proje yapısı

```
src/HaftalikYazilim.Web/
├─ Program.cs              # her şeyin bağlandığı yer (bölüm bölüm yorumlu)
├─ appsettings.json        # yapılandırma
├─ Migrations/             # EF Core migration'ları (PostgreSQL)
│
├─ Domain/                 # EF varlıkları + enum'lar (User, Article)
├─ Data/                   # AppDbContext (tablo tanımları, trigram indeksleri), DbSeeder
├─ Contracts/              # API'nin aldığı/verdiği DTO'lar
├─ Services/               # İŞ MANTIĞI (ArticleService, ProfileService, *Storage, PasswordGenerator ...)
├─ Controllers/            # ince HTTP katmanı (Auth, Articles, Profile, Authors, Admin, Site)
├─ Infrastructure/         # istisnalar, global hata yakalayıcı, rol sabitleri
├─ Options/                # appsettings bölümlerinin C# karşılıkları
├─ App_Data/               # (git'e girmez) yüklenen PDF'ler + profil fotoğrafları
│
└─ wwwroot/                # ---- FRONTEND ----
   ├─ index / yazilar / oku / giris / sifre-sifirla        (okuyucu)
   ├─ profilim / yazi-duzenle / profil-duzenle / profil    (yazar)
   ├─ yonetim.html                                         (admin)
   ├─ assets/css/theme.css   # SADECE renk/font/boşluk/hareket değişkenleri → burayı düzenle
   ├─ assets/css/site.css    # yerleşim + bileşen stilleri
   ├─ assets/js/
   │  ├─ config · api · session · dom · icons · avatar · motion · popover · pagination
   │  ├─ nav.js               # paylaşılan üst menü (tek kaynak)
   │  ├─ article-card.js      # yazı kartı        article-table.js  # yazı tablosu
   │  ├─ pdf-viewer.js        # PDF.js sarmalayıcı
   │  └─ pages/*.js           # her HTML sayfasının kendi scripti
   └─ lib/pdfjs/              # PDF.js (gömülü, CDN'siz)
```

### Backend akışı

```
HTTP → Controller ([Authorize], model bağlama)
        → Service (kural + karar: kim neyi görebilir/değiştirebilir)
            → AppDbContext (EF Core → PostgreSQL)
            → IPdfStorage / IImageStorage (disk)
     ← DTO (Contracts/)  — Domain nesnesi ASLA doğrudan dönmez
```

Hatalar servis katmanında `AppValidationException` / `AppForbiddenException` /
`AppNotFoundException` fırlatılarak bildirilir; `GlobalExceptionHandler` bunları
`400 / 403 / 404` + ProblemDetails JSON'a çevirir.

---

## 4. Yapılandırma (`appsettings.json`)

| Anahtar | Ne işe yarar |
|---|---|
| `ConnectionStrings:Default` | PostgreSQL bağlantı dizesi |
| `Database:SeedOnStartup` | Açılışta migration uygula + ilk admini oluştur |
| `Storage:Root` / `Storage:AvatarRoot` | PDF / profil fotoğrafı klasörleri (uygulama köküne göre) |
| `Storage:MaxUploadBytes` | En büyük PDF (byte). Varsayılan **50 MB** |
| `Storage:MaxAvatarBytes` | En büyük profil fotoğrafı (byte). Varsayılan 3 MB |
| `AdminSeed:*` | İlk admin hesabı |
| `PasswordReset:CooldownDays` | "Şifremi unuttum" — bir kullanıcı kaç günde 1 kez isteyebilir (7) |
| `PasswordReset:NewPasswordLength` | Üretilen yeni şifrenin uzunluğu (14) |
| `Site:ContactEmail` / `Site:ContactPhone` | Anasayfa iletişim kutusu + "hesap yok" mesajı. Boşsa gizlenir |

Gerçek ortamda gizli değerleri `appsettings.json`'a yazma; ortam değişkeni ver:
`ConnectionStrings__Default=...`, `AdminSeed__Password=...` gibi.

---

## 5. Özellikler / davranış

- **Yazılar sayfası:** sayfada en fazla 20 kart, altta **sayfa numaraları**. Tek
  arama kutusu **başlıkta ve yazar adında** arar (PostgreSQL `pg_trgm` GIN
  indeksleriyle → büyük tabloda bile tarama yapmaz, en az 2 karakter gerekir).
- **Yazı kartı:** başlık + (varsa) 2 satır özet. Alt satır: `avatar + yazar · tarih`
  solda, `Yeni + PDF indir` sağda. Son **7 günde** yayımlanan yazılarda "Yeni" rozeti.
- **ⓘ baloncuğu:** üstüne gel / dokun / klavye ile aç. Özet (yoksa "özet
  bırakılmamış"), sonra **yazar bloğu** (foto + ad + kısa tanıtım — profile tıklanır),
  sonra boyut / görüntülenme / indirme.
- **Profil:** kullanıcı `Profilim` menüsünden görünen adını, **kısa tanıtım** (bio)
  ve **uzun "hakkında"** metnini düzenler (karakter sınırı yok). Profil fotoğrafı
  tarayıcıda 512px kareye küçültülüp JPEG olarak gönderilir; **yakınlaştır / yatay /
  dikey** kaydırıcılarıyla dairedeki görünüm ayarlanır (CSS ile, yeniden yükleme yok).
- **Yazılarım:** yönetim ekranındakiyle aynı tablo (yalnız "Yazar" sütunu yok) +
  satır sonunda çöp kutusu + sağ altta "+" düğmesi.
- **Yazı formu:** özet **isteğe bağlı** (alt sınır yok, üst sınır 500). PDF 50 MB'a kadar.
- **Şifremi unuttum** (`/sifre-sifirla.html`): e-posta YOK. Hesap varsa 14 haneli
  yeni şifre üretilir ve **ekranda gösterilir**; hesap yoksa "yönetici açar" denir.
  Bir kullanıcı 7 günde 1 kez isteyebilir.
  > Güvenlik notu: yeni şifre ekranda gösterildiği için, bir yazarın e-postasını
  > bilen biri onun şifresini sıfırlayabilir. Tek yazarlı sitede kabul edilebilir;
  > yazar sayısı artınca e-posta tabanlı sıfırlamaya geçmek yerinde olur.

---

## 6. API uçları

| Metot | Yol | Kim |
|---|---|---|
| POST | `/api/auth/login` · `/logout` | herkes |
| GET | `/api/auth/me` | herkes (yoksa 204) |
| POST | `/api/auth/forgot-password` | herkes (rate-limit) |
| GET | `/api/articles?search=&authorId=&page=&pageSize=` | herkes (yayında) |
| GET | `/api/articles/mine` | giriş |
| GET | `/api/articles/{id}` · `/file` · `/download` | herkes\* |
| POST/PUT/DELETE | `/api/articles` · `/{id}` | Author/Admin (+ sahiplik) |
| POST | `/api/articles/{id}/publish` · `/unpublish` | sahip/Admin |
| GET | `/api/profile/me` · PUT `/me` | giriş |
| POST | `/api/profile/me/avatar` · DELETE `/me/avatar` | giriş |
| GET | `/api/profile/{id}` · `/{id}/avatar` | herkes |
| GET | `/api/admin/articles?includeDeleted=` · `/api/admin/users` | Admin |
| GET | `/api/authors` · `/api/site/info` | herkes |

Geliştirmede OpenAPI: `http://localhost:5099/openapi/v1.json` ·
elle deneme: `src/HaftalikYazilim.Web/HaftalikYazilim.Web.http`.

---

## 7. Sunucuya kurulum

**AlmaLinux 8 / RHEL 8 için tek komut** (Mac'ten, proje kökünde):

```bash
src/HaftalikYazilim.Web/deploy/deploy.sh          # varsayılan: root@89.252.152.15
```

Bu: self-contained (linux-x64) yayınlar → dosyaları sunucuya kopyalar →
`server-setup.sh`'i çalıştırır (PostgreSQL 16, nginx, certbot, systemd servisi,
`haftalik` kullanıcısı, kalıcı `/var/lib/haftalikyazilim/`, SELinux, firewall,
fontlar). Ayrıntı + elle adımlar: **[deploy/DEPLOY.md](src/HaftalikYazilim.Web/deploy/DEPLOY.md)**.

Kilit noktalar (başka bir ortama kuruyorsan):

- **Runtime:** self-contained yayınla → sunucuda .NET gerekmez. Framework-dependent
  istersen `aspnetcore-runtime-10.0` (proje **.NET 10**).
- **PostgreSQL 14+** (AlmaLinux varsayılanı PG10 çok eski → PGDG'den 16). Bağlantı
  dizesi `ConnectionStrings__Default` env ile. Migration'lar `pg_trgm` eklentisini
  kurar (PG13+ ve DB sahibi olmak yeterli — "trusted" eklenti).
- **Ters vekil:** `Program.cs` `UseForwardedHeaders` ile `X-Forwarded-Proto`'yu
  okur; nginx `proxy_set_header X-Forwarded-Proto $scheme` göndermeli, yoksa
  HTTPS yönlendirme döngüsü olur. nginx'te `client_max_body_size 60m`.
- **Kalıcı diskte olmalı** (deploy'lar arası korunur): yüklenen PDF/avatar
  (`Storage__Root`, `Storage__AvatarRoot`), oturum anahtarları (`DataProtection__KeyDir`),
  link kartı önbelleği (`Og__CacheDir`). Hepsi env ile publish klasörü dışına.
- **Font:** link önizleme kartları için sunucuda bir `.ttf` gerekir
  (`dnf install dejavu-sans-fonts` veya `Og__FontPath`). `libicu` de kur.
- **Gizli değerler** env ile: `AdminSeed__Password`, DB şifresi. Çerez Production'da
  otomatik `Secure=Always`.

İleride cache / kuyruk / analitik gerekirse: okuma metotları `ArticleService`'te
tek yerde (cache seam'i), sayaçlar (`ViewCount`/`DownloadCount`) `Article`'da hazır,
`src/` çoklu projeye hazır (ayrı bir `Worker` eklenebilir).

---

## 8. Güvenlik notları

- Gerçek ortamda **admin şifresini** ve **DB şifresini** değiştir (ortam değişkeni).
- Yüklenen PDF: uzantı + içerik türü + `%PDF-` imzası denetlenir, `wwwroot` DIŞINDA
  saklanır, controller üzerinden servis edilir.
- Yüklenen fotoğraf: uzantı + içerik türü + magic-byte denetlenir, küçük boyut sınırı
  uygulanır, sunucuda decode EDİLMEZ (yeniden boyutlandırma tarayıcıda yapılır).
- "Şifremi unuttum" davranışının notu için bkz. **5. bölüm**.

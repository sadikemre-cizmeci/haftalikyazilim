# Sunucuya dağıtım — HaftalıkYazılım

Hedef: **AlmaLinux 8 / RHEL 8** sunucu (ör. `89.252.152.15`), tek makinede
PostgreSQL + uygulama + nginx + Let's Encrypt.

## Tek komut

Mac'te, proje kökünde:

```bash
src/HaftalikYazilim.Web/deploy/deploy.sh
```

(Başka sunucu için: `deploy.sh root@1.2.3.4`)

Bu komut sırayla:

1. Projeyi **self-contained linux-x64** yayınlar (`./publish`) — sunucuya .NET kurmaya gerek yok.
2. `publish/` ve `deploy/` klasörlerini sunucuya kopyalar (`tar` over `ssh`).
3. Sunucuda `server-setup.sh`'i çalıştırır.

## İlk kurulumda terminalde sorulacaklar

| Nerede | Ne |
|---|---|
| SSH | sunucunun **root şifresi** (birkaç kez sorabilir — SSH anahtarı kurarsan bir daha sormaz) |
| server-setup | **veritabanı şifresi** (yeni belirle) |
| server-setup | **admin şifresi** (site yöneticisi — yeni belirle) |
| server-setup | **admin e-posta** |
| certbot | e-posta + Let's Encrypt şartlarını kabul (A) + yönlendirme seçimi |

Bu değerler `/etc/haftalikyazilim.env` (chmod 600) dosyasına yazılır. Sonraki
dağıtımlarda **tekrar sorulmaz.**

## server-setup.sh ne kurar / yapar

- `epel-release`, `nginx`, `certbot`, `dejavu-sans-fonts`, `fontconfig`, `libicu`,
  `firewalld`, `policycoreutils-python-utils`, `rsync`
- **PostgreSQL 16** (PGDG deposu) + `initdb` + servis
- `haftalikyazilim` DB rolü + `haftalikyazilim` veritabanı + `pg_hba.conf` (127.0.0.1 → scram-sha-256)
- `haftalik` sistem kullanıcısı (uygulama **root ile çalışmaz**)
- Kalıcı veri klasörleri: `/var/lib/haftalikyazilim/{uploads,avatars,keys,og-cache,fonts}`
  → yüklenen PDF'ler, oturum anahtarları, link kartları burada; **redeploy bunları silmez**
- Uygulama dosyaları: `/var/www/haftalikyazilim`
- Kart fontu: DejaVuSans → `/var/lib/haftalikyazilim/fonts/card.ttf`
- systemd servisi: `haftalikyazilim.service`
- nginx ters vekil: `/etc/nginx/conf.d/haftalikyazilim.conf` (`client_max_body_size 60m`)
- SELinux: `httpd_can_network_connect 1`
- firewall: http + https
- İlk açılışta uygulama migration'ları uygular + admin hesabını oluşturur
  (`Database:SeedOnStartup=true`)
- **Let's Encrypt** sertifikası (DNS sunucuya işaret ediyorsa)

## DNS (Güzel Hosting paneli)

Sertifikadan **önce** hazır olmalı:

| Host | Tür | Değer |
|---|---|---|
| `@` | A | `89.252.152.15` |
| `www` | CNAME | `haftalikyazilim.com` |

Ayrıca alan adının **nameserver'ları** Güzel'in DNS yönetimi NS'lerine işaret etmeli
(panel bunu uyarıyor). Kontrol: sunucuda `getent hosts haftalikyazilim.com` → IP dönmeli.
DNS hazır değilken `deploy.sh` çalışır, sadece sertifika adımını atlar; sonra:

```bash
ssh root@89.252.152.15 'certbot --nginx -d haftalikyazilim.com -d www.haftalikyazilim.com'
```

## Sonraki dağıtımlar (kod güncellemesi)

Aynı komut:

```bash
src/HaftalikYazilim.Web/deploy/deploy.sh
```

Servisi durdurur → dosyaları değiştirir (`App_Data` ve `/var/lib/...` dokunulmaz) →
migration varsa uygular → servisi başlatır.

## İşe yarar komutlar (sunucuda)

```bash
systemctl status haftalikyazilim
journalctl -u haftalikyazilim -f          # canlı log
systemctl restart haftalikyazilim
sudo -u postgres psql haftalikyazilim     # veritabanı
certbot renew --dry-run                    # sertifika yenileme testi
```

## Güvenlik — kurulumdan sonra

1. `/root/.ssh/authorized_keys`'e kendi SSH anahtarını ekle.
2. `/etc/ssh/sshd_config`: `PasswordAuthentication no`, `PermitRootLogin prohibit-password` →
   `systemctl restart sshd`.
3. `/etc/haftalikyazilim.env` içindeki `AdminSeed__Password` sadece İLK açılışta admin
   oluşturmak için kullanılır. İlk girişten sonra profilinden şifreni değiştir.

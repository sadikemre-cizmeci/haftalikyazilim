#!/usr/bin/env bash
# ============================================================================
# HaftalikYazilim — sunucu kurulum / güncelleme betiği (AlmaLinux 8 / RHEL 8)
# ----------------------------------------------------------------------------
# SUNUCUDA root olarak çalışır. Tekrar tekrar çalıştırmak GÜVENLİDİR (idempotent):
#   • ilk çalıştırma  → her şeyi kurar, DB/şifre sorar, sertifika alır
#   • sonraki çalıştırmalar (redeploy) → sadece uygulamayı yeniden başlatır
#
# Beklenen dizin düzeni (deploy.sh bunları koyar):
#   /opt/haftalikyazilim-deploy/app/     → 'dotnet publish' çıktısı (self-contained linux-x64)
#   /opt/haftalikyazilim-deploy/deploy/  → bu betik + haftalikyazilim.service + nginx conf
# ============================================================================
set -euo pipefail

DOMAIN="haftalikyazilim.com"
ALT_DOMAIN="www.haftalikyazilim.com"
APP_DIR="/var/www/haftalikyazilim"
DATA_DIR="/var/lib/haftalikyazilim"
ENV_FILE="/etc/haftalikyazilim.env"
SRC_APP="/opt/haftalikyazilim-deploy/app"
SRC_DEPLOY="/opt/haftalikyazilim-deploy/deploy"
PG_BIN="/usr/pgsql-16/bin"
PG_DATA="/var/lib/pgsql/16/data"

say()  { printf '\n\033[1;36m==> %s\033[0m\n' "$*"; }
warn() { printf '\033[1;33m!! %s\033[0m\n' "$*"; }

[ "$(id -u)" -eq 0 ] || { echo "root olarak çalıştır (sudo)."; exit 1; }
[ -f "$SRC_APP/HaftalikYazilim.Web.dll" ] || { echo "Yayın çıktısı yok: $SRC_APP  (önce deploy.sh)"; exit 1; }

# ---------------------------------------------------------------------------
say "0/12  Apache (httpd) çalışıyorsa durdur — 80 portu nginx'e lazım"
if systemctl is-enabled --quiet httpd 2>/dev/null || systemctl is-active --quiet httpd 2>/dev/null; then
  systemctl disable --now httpd
  echo "  httpd durduruldu ve devre dışı bırakıldı."
fi

# ---------------------------------------------------------------------------
say "1/12  Temel paketler"
dnf install -y epel-release || true
dnf install -y nginx certbot python3-certbot-nginx \
               dejavu-sans-fonts fontconfig libicu \
               firewalld policycoreutils-python-utils rsync

# ---------------------------------------------------------------------------
say "2/12  PostgreSQL 16 (PGDG deposu)"
dnf install -y "https://download.postgresql.org/pub/repos/yum/reporpms/EL-8-x86_64/pgdg-redhat-repo-latest.noarch.rpm" || true
dnf -qy module disable postgresql || true
dnf install -y postgresql16-server postgresql16-contrib

if [ ! -f "$PG_DATA/PG_VERSION" ]; then
  say "     initdb"
  "$PG_BIN/postgresql-16-setup" initdb
fi
systemctl enable --now postgresql-16

# ---------------------------------------------------------------------------
say "3/12  Uygulama kullanıcısı + dizinler"
id haftalik &>/dev/null || useradd -r -s /sbin/nologin -d "$APP_DIR" haftalik
mkdir -p "$APP_DIR"
mkdir -p "$DATA_DIR"/{uploads,avatars,keys,og-cache,fonts}
chown -R haftalik:haftalik "$DATA_DIR"

# ---------------------------------------------------------------------------
say "4/12  Sırlar / ortam dosyası ($ENV_FILE)"
if [ ! -f "$ENV_FILE" ]; then
  echo "İlk kurulum — birkaç değer gerekiyor:"
  read -rsp "  Veritabanı şifresi (yeni belirle): " DB_PW; echo
  read -rsp "  Admin (site yöneticisi) şifresi (yeni belirle): " ADMIN_PW; echo
  read -rp  "  Admin e-posta: " ADMIN_EMAIL
  umask 077
  cat > "$ENV_FILE" <<EOF
# HaftalikYazilim — üretim ortam değişkenleri. chmod 600. Git'e KOYMA.
ConnectionStrings__Default=Host=127.0.0.1;Port=5432;Database=haftalikyazilim;Username=haftalikyazilim;Password=${DB_PW}
Database__SeedOnStartup=true
AdminSeed__Email=${ADMIN_EMAIL}
AdminSeed__Username=admin
AdminSeed__DisplayName=Site Yöneticisi
AdminSeed__Password=${ADMIN_PW}
Site__BaseUrl=https://${DOMAIN}
Site__ContactEmail=${ADMIN_EMAIL}
EOF
  chmod 600 "$ENV_FILE"
else
  echo "  Zaten var — dokunulmuyor."
fi
# DB adımı için şifreyi env dosyasından oku (bağlantı dizesi satırından)
DB_PW="$(grep '^ConnectionStrings__Default=' "$ENV_FILE" | sed 's/.*;Password=//')"
[ -n "$DB_PW" ] || { echo "Env dosyasından DB şifresi okunamadı."; exit 1; }

# ---------------------------------------------------------------------------
say "5/12  Veritabanı rolü + veritabanı"
# Şifreyi psql değişkeni (:'pw') ile geçir → tırnak/özel karakter sorunu olmaz.
sudo -u postgres psql -v ON_ERROR_STOP=1 -v pw="$DB_PW" <<'SQL'
DO $$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='haftalikyazilim') THEN
    CREATE ROLE haftalikyazilim LOGIN;
  END IF;
END $$;
ALTER ROLE haftalikyazilim WITH LOGIN PASSWORD :'pw';
SQL
if ! sudo -u postgres psql -tAc "SELECT 1 FROM pg_database WHERE datname='haftalikyazilim'" | grep -q 1; then
  sudo -u postgres createdb -O haftalikyazilim haftalikyazilim
fi

# ---------------------------------------------------------------------------
say "6/12  pg_hba.conf — 127.0.0.1 için şifreli erişim"
if ! grep -qF "haftalikyazilim   haftalikyazilim   127.0.0.1/32" "$PG_DATA/pg_hba.conf"; then
  # Bizim kuralı dosyanın EN BAŞINA koy (pg_hba yukarıdan aşağı, ilk eşleşen kazanır).
  {
    echo "host    haftalikyazilim   haftalikyazilim   127.0.0.1/32   scram-sha-256"
    echo "host    haftalikyazilim   haftalikyazilim   ::1/128        scram-sha-256"
    cat "$PG_DATA/pg_hba.conf"
  } > "$PG_DATA/pg_hba.conf.new"
  cat "$PG_DATA/pg_hba.conf.new" > "$PG_DATA/pg_hba.conf"   # inode/izinleri koru
  rm -f "$PG_DATA/pg_hba.conf.new"
  systemctl reload postgresql-16
fi

# ---------------------------------------------------------------------------
say "7/12  Uygulama dosyalarını yerine koy"
systemctl stop haftalikyazilim 2>/dev/null || true
rsync -a --delete \
  --exclude 'appsettings.Development.json' \
  "$SRC_APP"/ "$APP_DIR"/
chmod +x "$APP_DIR/HaftalikYazilim.Web" || true
chown -R haftalik:haftalik "$APP_DIR"

# ---------------------------------------------------------------------------
say "8/12  Kart fontu"
if [ ! -f "$DATA_DIR/fonts/card.ttf" ]; then
  TTF="$(find /usr/share/fonts -iname 'DejaVuSans.ttf' -print -quit || true)"
  if [ -n "$TTF" ]; then
    cp "$TTF" "$DATA_DIR/fonts/card.ttf"
    chown haftalik:haftalik "$DATA_DIR/fonts/card.ttf"
  else
    warn "DejaVuSans.ttf bulunamadı — link kartı metni kutu çıkabilir. Bir .ttf'i $DATA_DIR/fonts/card.ttf'e koy."
  fi
fi

# ---------------------------------------------------------------------------
say "9/12  systemd servisi"
cp "$SRC_DEPLOY/haftalikyazilim.service" /etc/systemd/system/haftalikyazilim.service
systemctl daemon-reload
systemctl enable haftalikyazilim

# ---------------------------------------------------------------------------
say "10/12  Nginx + SELinux + firewall"
# Temel config'i her dağıtımda tazele (client_max_body_size, gzip, proxy ayarları).
# Bu, certbot'un eklediği 443 bloğunu siler AMA adım 12 onu hemen geri ekler.
cp "$SRC_DEPLOY/nginx-haftalikyazilim.conf" /etc/nginx/conf.d/haftalikyazilim.conf
# AlmaLinux'ta gelen varsayılan "welcome" sayfasını kapat (Apache/nginx test sayfası).
[ -f /etc/nginx/conf.d/welcome.conf ] && mv /etc/nginx/conf.d/welcome.conf /etc/nginx/conf.d/welcome.conf.disabled || true
nginx -t
# SELinux enforcing/permissive ise nginx'in Kestrel'e (localhost:5000) bağlanmasına izin ver.
# SELinux 'disabled' ise bu komut hata verir ve zaten gerekmez → atla.
if command -v selinuxenabled >/dev/null 2>&1 && selinuxenabled; then
  setsebool -P httpd_can_network_connect 1 || warn "setsebool başarısız (SELinux) — nginx 502 verirse elle bak."
else
  echo "  SELinux kapalı — setsebool atlandı."
fi
systemctl enable --now firewalld 2>/dev/null || warn "firewalld başlatılamadı (sağlayıcı harici güvenlik duvarı olabilir) — atlanıyor."
firewall-cmd --permanent --add-service=http --add-service=https >/dev/null 2>&1 || true
firewall-cmd --reload >/dev/null 2>&1 || true
systemctl enable nginx
if ! systemctl restart nginx; then
  warn "nginx başlamadı. 80 portunu tutan:"
  ss -tlnp | grep ':80 ' || true
  journalctl -u nginx -n 20 --no-pager
  exit 1
fi

# ---------------------------------------------------------------------------
say "11/12  Uygulamayı başlat"
systemctl restart haftalikyazilim
sleep 5
if systemctl is-active --quiet haftalikyazilim; then
  echo "  haftalikyazilim: active (running)"
else
  warn "Uygulama başlamadı. Son loglar:"
  journalctl -u haftalikyazilim -n 40 --no-pager
  exit 1
fi
curl -fsS -o /dev/null http://127.0.0.1:5000/ && echo "  http://127.0.0.1:5000 yanıt veriyor." || warn "Uygulama porta yanıt vermiyor."

# ---------------------------------------------------------------------------
say "12/12  HTTPS sertifikası (Let's Encrypt)"
if [ -d "/etc/letsencrypt/live/$DOMAIN" ]; then
  # Sertifika var. Ama nginx config'i her dağıtımda YENİDEN kopyalandığı için
  # (adım 10) 443 bloğu + yönlendirme silinmiş olur → certbot'a onları tekrar
  # ekletmemiz gerekir. --reinstall: sertifika geçerli olsa bile config'i uygula.
  echo "  Sertifika var — nginx'e 443 + yönlendirme yeniden uygulanıyor…"
  certbot install --nginx --cert-name "$DOMAIN" --redirect -n \
    || certbot --nginx -d "$DOMAIN" -d "$ALT_DOMAIN" --redirect --reinstall -n \
    || warn "certbot yeniden uygulanamadı — elle:  certbot --nginx -d $DOMAIN -d $ALT_DOMAIN --redirect"
  nginx -t && systemctl reload nginx
else
  DNS_IP="$(getent hosts "$DOMAIN" | awk '{print $1; exit}')"
  SRV_IP="$(curl -fsS https://api.ipify.org || echo '?')"
  if [ "$DNS_IP" != "$SRV_IP" ]; then
    warn "DNS ($DOMAIN → ${DNS_IP:-yok}) sunucu IP'sine ($SRV_IP) işaret etmiyor."
    warn "DNS yayılınca şunu çalıştır:  certbot --nginx -d $DOMAIN -d $ALT_DOMAIN"
  else
    certbot --nginx -d "$DOMAIN" -d "$ALT_DOMAIN" --redirect \
      || warn "certbot başarısız — DNS yayılınca 'certbot --nginx -d $DOMAIN -d $ALT_DOMAIN' ile tekrar dene."
  fi
fi

say "BİTTİ"
echo "Site:            https://$DOMAIN  (sertifika alındıysa)"
echo "Uygulama logu:   journalctl -u haftalikyazilim -f"
echo "Yeniden dağıtım: deploy.sh'i tekrar çalıştır (Mac'ten)."

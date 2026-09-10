#!/usr/bin/env bash
# ============================================================================
# HaftalikYazilim — Mac'ten sunucuya dağıtım
# ----------------------------------------------------------------------------
# Ne yapar:
#   1) Projeyi self-contained (linux-x64) yayınlar     → ./publish
#   2) publish/ + deploy/ klasörlerini sunucuya kopyalar (tar over ssh)
#   3) Sunucuda server-setup.sh'i çalıştırır (kurulum + başlatma + sertifika)
#
# Kullanım (proje kökünden ya da bu klasörden):
#   ./deploy.sh                 # varsayılan sunucu: root@89.252.152.15
#   ./deploy.sh root@1.2.3.4    # başka sunucu
#
# ŞİFRE: Bu betik tek bir SSH bağlantısı açıp tüm adımlarda onu kullanır
# (ControlMaster). Yani şifre EN FAZLA BİR KEZ sorulur. Hiç sorulmaması için
# bir kez SSH anahtarı kur:   ssh-copy-id root@89.252.152.15
# ============================================================================
set -euo pipefail

TARGET="${1:-root@89.252.152.15}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJ="$(cd "$HERE/.." && pwd)"                 # .../src/HaftalikYazilim.Web
REMOTE_BASE="/opt/haftalikyazilim-deploy"

# --- Tek SSH bağlantısını paylaş (şifre en fazla 1 kez) --------------------
CTL="$(mktemp -u "${TMPDIR:-/tmp}/hy-ssh-XXXXXX")"
SSH=(ssh -o ControlMaster=auto -o ControlPath="$CTL" -o ControlPersist=300 -o ConnectTimeout=15)
cleanup() { "${SSH[@]}" -O exit "$TARGET" 2>/dev/null || true; }
trap cleanup EXIT

echo "==> Sunucu: $TARGET"
echo "==> Proje : $PROJ"

# 1) Yayınla ---------------------------------------------------------------
echo "==> dotnet publish (self-contained linux-x64)…"
rm -rf "$PROJ/publish"
dotnet publish "$PROJ/HaftalikYazilim.Web.csproj" \
  -c Release -r linux-x64 --self-contained true \
  -o "$PROJ/publish" --nologo -v q

[ -f "$PROJ/publish/HaftalikYazilim.Web.dll" ] || { echo "Yayın çıktısı oluşmadı."; exit 1; }

# 2) Bağlan (şifre burada BİR kez sorulur) + kopyala ---------------------
echo "==> Sunucuya bağlanılıyor… (şifre yalnızca bir kez sorulur; SSH anahtarı kurulursa hiç sorulmaz)"
"${SSH[@]}" "$TARGET" "rm -rf $REMOTE_BASE && mkdir -p $REMOTE_BASE/app $REMOTE_BASE/deploy"

# macOS'un bsdtar'ı Apple özniteliklerini (com.apple.provenance) ve ._ sidecar
# dosyalarını da paketler; sunucudaki GNU tar bunları tanımaz ve HER dosya için
# uyarı basar. Bu iki bayrak (macOS 11+ / bsdtar 3.5+ standart) onları hiç koymaz.
TAR=(tar --no-xattrs --no-mac-metadata)
export COPYFILE_DISABLE=1

echo "==> Uygulama dosyaları kopyalanıyor…"
"${TAR[@]}" -czf - -C "$PROJ/publish" . | "${SSH[@]}" "$TARGET" "tar xzf - -C $REMOTE_BASE/app"
echo "==> Dağıtım betikleri kopyalanıyor…"
"${TAR[@]}" -czf - -C "$HERE" . | "${SSH[@]}" "$TARGET" "tar xzf - -C $REMOTE_BASE/deploy"

# 3) Kur / güncelle -----------------------------------------------------
echo "==> Sunucu kurulumu çalışıyor…"
"${SSH[@]}" -t "$TARGET" "chmod +x $REMOTE_BASE/deploy/server-setup.sh && $REMOTE_BASE/deploy/server-setup.sh"

echo "==> Bitti."

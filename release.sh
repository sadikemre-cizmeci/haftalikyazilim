#!/usr/bin/env bash
# ============================================================================
# release.sh — main'deki GÜNCEL kodu sunucuya gönderir.
# ----------------------------------------------------------------------------
# Akış:  branch'te çalış → GitHub'da main'e merge et → BU komutu çalıştır.
#
#   ./release.sh                → main'e geç, GitHub'dan çek, onay sor, gönder
#   ./release.sh -y             → onay sormadan gönder
#   ./release.sh -y root@1.2.3.4 → başka sunucuya
#
# Ön koşul (bir kez): GitHub uzak deposu tanımlı olmalı —
#   git remote add origin git@github.com:KULLANICI/haftalikyazilim.git
# ============================================================================
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"

AUTO=0
if [ "${1:-}" = "-y" ]; then AUTO=1; shift; fi

# 1) Çalışma alanı temiz mi? (kaydedilmemiş değişiklikle dağıtım yapma)
if ! git diff --quiet || ! git diff --cached --quiet; then
  echo "✗ Kaydedilmemiş değişiklik var. Önce commit'le veya 'git stash' yap:"
  git status --short
  exit 1
fi

# 2) origin tanımlı mı?
if ! git remote get-url origin >/dev/null 2>&1; then
  echo "✗ 'origin' uzak deposu yok. Bir kez:"
  echo "    git remote add origin git@github.com:KULLANICI/haftalikyazilim.git"
  exit 1
fi

# 3) main'e geç ve GitHub'dan güncelle (yalnızca ileri sarma)
git checkout main
git pull --ff-only origin main

echo
echo "Gönderilecek sürüm:"
git log -1 --pretty='  %h  %s  (%an, %ar)'
echo

# 4) Onay
if [ "$AUTO" -ne 1 ]; then
  read -rp "Sunucuya gönderilsin mi? [e/H] " ans
  case "$ans" in e|E) ;; *) echo "İptal edildi."; exit 0 ;; esac
fi

# 5) Derle + kopyala + yeniden başlat (deploy.sh idempotent; şifre sormaz)
exec src/HaftalikYazilim.Web/deploy/deploy.sh "$@"

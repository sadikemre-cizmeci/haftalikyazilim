// ============================================================================
// pages/sifre-sifirla.js — ŞİFREMİ UNUTTUM
// ----------------------------------------------------------------------------
// E-posta gönderME yok. Kullanıcı e-postasını girer:
//   • hesap varsa → sunucu 14 haneli yeni şifre üretir, yanıtta döner, ekranda gösterilir
//   • hesap yoksa → 404 (mesaj: yönetici hesap açar)
//   • 7 gün içinde ikinci kez → 429 (bekleme mesajı)
// ============================================================================

import { renderNav } from "../nav.js";
import { api, ApiError } from "../api.js";
import { qs, setYear, toast } from "../dom.js";
import { t } from "../i18n.js";

async function init() {
  renderNav(null);
  setYear();

  const form = qs("#reset-form");
  const errorBox = qs("#reset-error");
  const submitBtn = qs("#reset-submit");

  form.addEventListener("submit", async (e) => {
    e.preventDefault();
    errorBox.hidden = true;

    const identifier = qs("#reset-id").value.trim();
    if (!identifier) {
      showError(errorBox, t("sifre.needId"));
      return;
    }

    submitBtn.disabled = true;
    submitBtn.textContent = t("sifre.submitting");

    try {
      const res = await api.post("/api/auth/forgot-password", { identifier });
      showResult(res);
    } catch (err) {
      const msg = err instanceof ApiError ? err.message : t("sifre.failed");
      showError(errorBox, msg);
      submitBtn.disabled = false;
      submitBtn.textContent = t("sifre.submit");
    }
  });

  qs("#copy-password").addEventListener("click", async () => {
    try {
      await navigator.clipboard.writeText(qs("#new-password").textContent);
      toast(t("sifre.copied"), "success");
    } catch {
      toast(t("sifre.copyManual"), "error");
    }
  });
}

function showResult(res) {
  qs("#reset-form").hidden = true;
  qs("#reset-message").textContent = res.message || "";
  qs("#new-password").textContent = res.newPassword || "";
  qs("#reset-result").hidden = false;
}

function showError(box, message) {
  box.textContent = message;
  box.hidden = false;
}

init();

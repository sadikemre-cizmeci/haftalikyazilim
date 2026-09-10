// ============================================================================
// pages/kayit.js — ÜYE KAYDI
// ----------------------------------------------------------------------------
// Başarılıysa sunucu otomatik giriş yapar; kullanıcıyı Yazılarım'a yollarız.
// ============================================================================

import { renderNav } from "../nav.js";
import { ApiError } from "../api.js";
import { qs, setYear } from "../dom.js";
import { getCurrentUser, register } from "../session.js";
import { t } from "../i18n.js";

async function init() {
  renderNav(null);
  setYear();

  // Zaten girişliyse anasayfaya.
  if (await getCurrentUser()) {
    window.location.href = "yazilar.html";
    return;
  }

  const form = qs("#register-form");
  const errorBox = qs("#register-error");
  const submitBtn = qs("#register-submit");

  // Kullanıcı adı alanını yazarken sadeleştir (küçük harf + izinli karakter).
  const usernameInput = qs("#reg-username");
  usernameInput.addEventListener("input", () => {
    const clean = usernameInput.value.toLowerCase().replace(/[^a-z0-9._]/g, "");
    if (clean !== usernameInput.value) usernameInput.value = clean;
  });

  form.addEventListener("submit", async (e) => {
    e.preventDefault();
    errorBox.hidden = true;

    const payload = {
      displayName: qs("#reg-display").value.trim(),
      username: usernameInput.value.trim(),
      email: qs("#reg-email").value.trim(),
      password: qs("#reg-password").value,
    };

    if (payload.displayName.length < 2) return showError(errorBox, t("kayit.errDisplay"));
    if (payload.username.length < 3) return showError(errorBox, t("kayit.errUsername"));
    if (!payload.email.includes("@")) return showError(errorBox, t("kayit.errEmail"));
    if (payload.password.length < 8) return showError(errorBox, t("kayit.errPw"));

    submitBtn.disabled = true;
    submitBtn.textContent = t("kayit.submitting");

    try {
      await register(payload);
      window.location.href = "profilim.html";
    } catch (err) {
      showError(errorBox, err instanceof ApiError ? err.message : t("kayit.failed"));
      submitBtn.disabled = false;
      submitBtn.textContent = t("kayit.submit");
    }
  });
}

function showError(box, message) {
  box.textContent = message;
  box.hidden = false;
}

init();

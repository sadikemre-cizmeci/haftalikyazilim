// ============================================================================
// pages/giris.js — GİRİŞ sayfası
// ----------------------------------------------------------------------------
// Adres:  giris.html   (ya da  giris.html?next=/profilim.html  — giriş sonrası dönüş)
// ============================================================================

import { renderNav } from "../nav.js";
import { ApiError } from "../api.js";
import { qs, setYear, queryParam } from "../dom.js";
import { getCurrentUser, login } from "../session.js";
import { t } from "../i18n.js";

async function init() {
  renderNav(null);
  setYear();

  const form = qs("#login-form");
  const errorBox = qs("#login-error");
  const submitBtn = qs("#login-submit");

  // Zaten giriş yapılmışsa doğrudan hedefe git.
  const existing = await getCurrentUser();
  if (existing) {
    goNext();
    return;
  }

  form.addEventListener("submit", async (e) => {
    e.preventDefault();
    errorBox.hidden = true;

    const identifier = qs("#login-id").value.trim();
    const password = qs("#login-password").value;

    if (!identifier || !password) {
      showError(errorBox, t("giris.needBoth"));
      return;
    }

    submitBtn.disabled = true;
    submitBtn.textContent = t("giris.submitting");

    try {
      await login(identifier, password);
      goNext();
    } catch (err) {
      const msg =
        err instanceof ApiError && err.status === 429
          ? t("giris.tooMany")
          : err instanceof ApiError
            ? err.message
            : t("giris.failed");
      showError(errorBox, msg);
      submitBtn.disabled = false;
      submitBtn.textContent = t("giris.submit");
    }
  });
}

/** Giriş sonrası nereye gidilecek? ?next= varsa oraya, yoksa "Yazılarım". */
function goNext() {
  const next = queryParam("next");
  // Güvenlik: yalnızca site içi göreli yollara izin ver (açık yönlendirme engeli).
  const safe = next && next.startsWith("/") && !next.startsWith("//") ? next : "profilim.html";
  window.location.href = safe;
}

function showError(box, message) {
  box.textContent = message;
  box.hidden = false;
}

init();

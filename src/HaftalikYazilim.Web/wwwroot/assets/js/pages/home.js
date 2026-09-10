// ============================================================================
// pages/home.js — ANASAYFA
// ----------------------------------------------------------------------------
// Metin doğrudan index.html'de. Burada: üst menü, yıl, iletişim satırı.
// İletişim bilgisi appsettings.json → "Site" bölümünden gelir; boşsa gizli.
// ============================================================================

import { renderNav } from "../nav.js";
import { api } from "../api.js";
import { qs, setYear } from "../dom.js";
import { getCurrentUser } from "../session.js";

async function init() {
  renderNav("home");
  setYear();

  // "Hesap aç" düğmesi sadece giriş YAPMAYANlara.
  const user = await getCurrentUser();
  const signup = qs("#signup-btn");
  if (signup && !user) signup.hidden = false;

  await loadContact();
}

async function loadContact() {
  const line = qs("#contact-line");
  if (!line) return;

  let info;
  try {
    info = await api.get("/api/site/info");
  } catch {
    line.hidden = true;
    return;
  }

  if (!info.contactEmail && !info.contactPhone) {
    line.hidden = true;
    return;
  }

  if (info.contactEmail) {
    const a = qs("#contact-email");
    a.textContent = info.contactEmail;
    a.href = `mailto:${info.contactEmail}`;
  }
  if (info.contactPhone) {
    const wrap = qs("#contact-phone-wrap");
    const a = qs("#contact-phone");
    a.textContent = info.contactPhone;
    a.href = `tel:${info.contactPhone.replace(/[^\d+]/g, "")}`;
    wrap.hidden = false;
  }

  line.hidden = false;
}

init();

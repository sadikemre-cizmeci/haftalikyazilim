// ============================================================================
// pdf-viewer.js — sitedeki PDF görüntüleyici
// ----------------------------------------------------------------------------
// Mozilla'nın PDF.js kütüphanesini kullanır. Kütüphane projeye DAHİL edilmiştir
// (wwwroot/lib/pdfjs/), yani CDN'e / internete bağımlı DEĞİLDİR.
//
// Özellikler:
//   • Sayfa sayfa render (tek <canvas>)
//   • Yakınlaştır / uzaklaştır / "genişliğe sığdır"
//   • Önceki / sonraki sayfa, sayfa numarasına git
//   • Klavye:  ← →  PageUp PageDown  Home End   ve   +  -
//   • Yüksek çözünürlüklü ekranlarda net görüntü (devicePixelRatio)
//
// Kullanım (bkz. pages/oku.js):
//   import { PdfViewer } from "../pdf-viewer.js";
//   const viewer = new PdfViewer(containerEl, { url: "/api/articles/5/file" });
//   await viewer.load();
//   // sayfadan çıkarken:  viewer.destroy();
// ============================================================================

import * as pdfjsLib from "../../lib/pdfjs/pdf.min.mjs";
import { el, render } from "./dom.js";
import { ICONS } from "./icons.js";
import { t } from "./i18n.js";

// PDF.js "worker" dosyasını göster. Ağır çözümleme işini ayrı bir iş parçacığında
// yapar; bu satır olmadan büyük PDF'lerde arayüz donar.
pdfjsLib.GlobalWorkerOptions.workerSrc = new URL(
  "../../lib/pdfjs/pdf.worker.min.mjs",
  import.meta.url,
).href;

export class PdfViewer {
  /**
   * @param {HTMLElement} container  Görüntüleyicinin yerleşeceği kap.
   * @param {{ url: string, downloadUrl?: string|null, onProgress?: Function,
   *          toolbarStart?: HTMLElement, toolbarExtra?: HTMLElement,
   *          onExit?: Function, showDownload?: boolean }} opts
   *        url          → PDF'i GÖRÜNTÜLEME adresi (API ucu, .../file)
   *        downloadUrl  → "İndir" düğmesinin adresi (null → indir düğmesi yok)
   *        onProgress   → sayfa değiştikçe çağrılır (okuma ilerleme çubuğu için)
   *        toolbarStart → araç çubuğunun EN BAŞINA konacak öğe (ör. yazar künyesi)
   *        toolbarExtra → sağ bölüme (indir'den önce) konacak öğe (ör. beğen/kaydet/paylaş)
   *        onExit       → verilirse araç çubuğunun EN SAĞINA "Çık" düğmesi; tıklanınca çağrılır
   *        showDownload → "İndir" düğmesini göster (varsayılan true; downloadUrl null ise yok sayılır)
   */
  constructor(container, {
    url, downloadUrl, onProgress, toolbarStart, toolbarExtra, onExit, showDownload = true,
  } = {}) {
    this.container = container;
    this.url = url;
    this.downloadUrl = downloadUrl === null
      ? null
      : (downloadUrl || url.replace(/\/file(\?|$)/, "/download$1"));
    this.showDownload = showDownload && this.downloadUrl !== null;
    this.onProgress = typeof onProgress === "function" ? onProgress : null;
    this.onExit = typeof onExit === "function" ? onExit : null;
    this.toolbarStart = toolbarStart || null;
    this.toolbarExtra = toolbarExtra || null;

    this.pdf = null;
    this.pageCount = 0;
    this.pageNum = 1;

    this.scale = 1.0;
    this.minScale = 0.25;
    this.maxScale = 5.0;
    // Elle yakınlaştırma adımı — küçük tutulur ki hassas ayar yapılabilsin.
    this.zoomStep = 1.1;
    // Açılışta "genişliğe sığdır" ölçeğinin üst sınırı. Küçük → sayfa daha
    // makul boyutta ve ortada başlar (kullanıcı isterse + ile büyütür).
    this.fitMaxScale = 1.15;

    // Yükleniyor göstergesi (yalnızca ilk render için; bkz. _syncToolbar).
    this.rendering = false;
    // Sürmekte olan pdf.js render görevi — aynı <canvas>'a çakışan ikinci bir
    // render() çağrısı gelirse (hızlı zoom/sayfa tıklamaları) önce bunu iptal
    // ederiz; pdf.js aynı canvas'ta üst üste render'a izin vermiyor.
    this._renderTask = null;

    // Ekran çözünürlüğü çarpanı (retina vb.). Net görüntü için.
    this.outputScale = window.devicePixelRatio || 1;

    // "Genişliğe sığdır" en son hangi genişliğe göre yapıldı? (gereksiz tekrar için)
    this._lastFitWidth = 0;
    // Kullanıcı elle zoom yaptıysa artık otomatik sığdırma yapma.
    this._userZoomed = false;

    // Olay dinleyicilerini sökebilmek için referanslarını sakla.
    this._onKeyDown = this._onKeyDown.bind(this);

    // Sahne genişliği değişince (pencere yeniden boyutlandı, panel görünür oldu,
    // düzen yeni oturdu) yeniden sığdır. clientWidth 0 → gerçek değere geçişi de yakalar.
    this._resizeObserver = new ResizeObserver(
      this._debounce(() => {
        const w = this._stageEl?.clientWidth || 0;
        if (w > 50 && w !== this._lastFitWidth && !this._userZoomed) {
          this._fitToWidth();
        }
      }, 150),
    );
  }

  // ------------------------------------------------------------------
  // Yaşam döngüsü
  // ------------------------------------------------------------------

  /** PDF'i indirir, iskeleti kurar ve ilk sayfayı çizer. */
  async load() {
    this._buildSkeleton();
    this._setStatus(t("pdfv.loading"));

    try {
      this.pdf = await pdfjsLib.getDocument({ url: this.url }).promise;
    } catch (err) {
      this._setStatus(t("pdfv.loadFail"));
      console.error("PDF yüklenemedi:", err);
      return;
    }

    this.pageCount = this.pdf.numPages;
    this._pageTotalEl.textContent = String(this.pageCount);

    // İlk açılışta sayfayı görüntü alanının genişliğine sığdırmayı DENE.
    // Düzen henüz oturmadıysa (clientWidth 0) makul bir ölçekle çiz; asıl
    // sığdırmayı ResizeObserver birazdan yapar.
    await this._fitToWidth({ rerender: false });
    await this._renderCurrent();

    document.addEventListener("keydown", this._onKeyDown);
    this._resizeObserver.observe(this._stageEl);
  }

  /** Sayfadan ayrılırken çağır: dinleyicileri ve PDF kaynağını serbest bırakır. */
  destroy() {
    document.removeEventListener("keydown", this._onKeyDown);
    this._resizeObserver.disconnect();
    this._renderTask?.cancel();
    this.pdf?.destroy();
    this.pdf = null;
  }

  // ------------------------------------------------------------------
  // Gezinme
  // ------------------------------------------------------------------

  goToPage(num) {
    const target = Math.min(Math.max(1, Math.floor(num) || 1), this.pageCount);
    if (target === this.pageNum) return;
    this.pageNum = target;
    this._scrollToPageTop();
    this._renderPage(target);
  }

  prev() {
    this.goToPage(this.pageNum - 1);
  }

  next() {
    this.goToPage(this.pageNum + 1);
  }

  /**
   * Sayfa değişince görünüm HER ZAMAN yeni sayfanın EN ÜSTÜNDEN başlasın —
   * önceki sayfada nerede kalındığından (aşağıda okunuyor olsa bile) bağımsız.
   * Sahnenin üstünü, yapışkan araç çubuğunun HEMEN ALTINA getirecek kadar
   * kaydırırız — kaydıran kapsayıcı pencere olsun (normal okuma sayfası) ya da
   * üstteki önizleme katmanı olsun (bkz. pdf-overlay.js, `overflow-y: auto`),
   * ikisinde de çalışsın diye gerçek kaydıran atayı kendimiz buluruz.
   */
  _scrollToPageTop() {
    if (!this._stageEl || !this._toolbarEl) return;

    const bar = this._toolbarEl.getBoundingClientRect();
    const stage = this._stageEl.getBoundingClientRect();
    const delta = stage.top - (bar.bottom + 8); // + aşağı kaydır, - yukarı kaydır
    if (Math.abs(delta) < 1) return;

    const scroller = this._findScrollAncestor(this._stageEl);
    if (scroller === document.scrollingElement) {
      window.scroll({ top: window.scrollY + delta, left: window.scrollX, behavior: "instant" });
    } else {
      scroller.scrollTop += delta;
    }
  }

  /** _stageEl'in üstünde GERÇEKTEN dikey taşan en yakın atayı bulur; yoksa pencere. */
  _findScrollAncestor(el) {
    let node = el.parentElement;
    while (node && node !== document.body) {
      if (/(auto|scroll|overlay)/.test(getComputedStyle(node).overflowY) &&
          node.scrollHeight > node.clientHeight + 1) {
        return node;
      }
      node = node.parentElement;
    }
    return document.scrollingElement || document.documentElement;
  }

  // ------------------------------------------------------------------
  // Yakınlaştırma
  // ------------------------------------------------------------------

  setScale(next) {
    const clamped = Math.min(Math.max(next, this.minScale), this.maxScale);
    if (Math.abs(clamped - this.scale) < 0.001) return;
    this.scale = clamped;
    this._renderCurrent();
  }

  zoomIn() {
    this._userZoomed = true; // artık otomatik "genişliğe sığdır" devreye girmesin
    this.setScale(this.scale * this.zoomStep);
  }

  zoomOut() {
    this._userZoomed = true;
    this.setScale(this.scale / this.zoomStep);
  }

  /** Geçerli sayfayı görüntü alanının genişliğine sığacak ölçeğe getirir. */
  async _fitToWidth({ rerender = true, viaButton = false } = {}) {
    if (!this.pdf) return;

    // "Genişliğe sığdır" düğmesine basıldıysa otomatik sığdırmayı yeniden aç.
    if (viaButton) {
      this._userZoomed = false;
    }

    const width = this._stageEl.clientWidth;

    // Düzen henüz oturmadıysa (panel gizli, ilk kare) clientWidth 0 gelebilir.
    // Böyle bir durumda makul bir ölçek kullan; ResizeObserver gerçek genişlikte
    // bu metodu tekrar çağıracak.
    if (width < 50) {
      this.scale = 1.0;
      if (rerender) await this._renderCurrent();
      return;
    }

    this._lastFitWidth = width;

    const page = await this.pdf.getPage(this.pageNum);
    const pageWidthAtScale1 = page.getViewport({ scale: 1 }).width;

    // Sayfayı sahne genişliğinin ~%84'üne sığdır → kenarlarda boşluk kalsın,
    // sayfa ortada ve makul boyutta dursun (fitMaxScale üst sınır).
    const available = Math.min(width * 0.84, width - 64);
    const nextScale = Math.max(
      this.minScale,
      Math.min(available / pageWidthAtScale1, this.fitMaxScale),
    );

    this.scale = nextScale;
    if (rerender) {
      await this._renderCurrent();
    }
  }

  // ------------------------------------------------------------------
  // Render
  // ------------------------------------------------------------------

  _renderCurrent() {
    return this._renderPage(this.pageNum);
  }

  /**
   * Sayfayı çizer. Hızlı üst üste zoom/sayfa tıklamalarında ÇAKIŞMAYI önlemek
   * için: aynı <canvas>'ta hâlâ süren bir render varsa önce onu iptal eder,
   * sonra yenisini başlatır (pdf.js aynı canvas'ta eşzamanlı render() çağrısına
   * izin vermez — izin verilirse görüntü bozulur, ör. ters/küçük çizim).
   */
  async _renderPage(num) {
    if (!this.pdf) return;

    this._renderTask?.cancel();
    this._renderTask = null;

    this.rendering = true;
    this._syncToolbar();

    try {
      const page = await this.pdf.getPage(num);
      // Bekleme sırasında başka bir sayfaya geçildiyse bu çizimi atla.
      if (num !== this.pageNum) return;

      const viewport = page.getViewport({ scale: this.scale });

      const canvas = this._canvasEl;
      const ctx = canvas.getContext("2d");

      // Backing store'u ekran çözünürlüğüne göre büyüt, CSS boyutunu mantıksal tut.
      canvas.width = Math.floor(viewport.width * this.outputScale);
      canvas.height = Math.floor(viewport.height * this.outputScale);
      canvas.style.width = `${Math.floor(viewport.width)}px`;
      canvas.style.height = `${Math.floor(viewport.height)}px`;

      const transform =
        this.outputScale !== 1
          ? [this.outputScale, 0, 0, this.outputScale, 0, 0]
          : null;

      const task = page.render({ canvasContext: ctx, viewport, transform });
      this._renderTask = task;
      await task.promise;
      if (this._renderTask === task) this._renderTask = null;
    } catch (err) {
      // RenderingCancelledException → az önce biz iptal ettik; sessiz geç.
      if (err?.name !== "RenderingCancelledException") {
        console.error("Sayfa çizilemedi:", err);
      }
    } finally {
      this.rendering = false;
      this._syncToolbar();
    }
  }

  // ------------------------------------------------------------------
  // Arayüz (iskele + araç çubuğu)
  // ------------------------------------------------------------------

  _buildSkeleton() {
    // --- Sayfa gezinme: [‹] [2 / 8] [›] tek bir hap (pill) içinde ---
    this._prevBtn = pagerButton(ICONS.prev, t("pdfv.prevPage"), () => this.prev());
    this._nextBtn = pagerButton(ICONS.next, t("pdfv.nextPage"), () => this.next());

    this._pageInputEl = el("input", {
      class: "pdfv__pageinput",
      type: "text",
      inputmode: "numeric",
      value: "1",
      "aria-label": t("pdfv.pageNumber"),
    });
    const commitPage = () => this.goToPage(Number(this._pageInputEl.value.replace(/\D/g, "")));
    this._pageInputEl.addEventListener("change", commitPage);
    this._pageInputEl.addEventListener("keydown", (e) => {
      if (e.key === "Enter") { e.preventDefault(); commitPage(); this._pageInputEl.blur(); }
    });

    this._pageTotalEl = el("span", { class: "pdfv__pagetotal" }, ["?"]);

    const pager = el("div", { class: "pdfv__pager" }, [
      this._prevBtn,
      el("span", { class: "pdfv__pagelabel" }, [this._pageInputEl, el("span", { class: "pdfv__pageslash" }, ["/"]), this._pageTotalEl]),
      this._nextBtn,
    ]);

    // --- Yakınlaştırma: sadece  −  +  ---
    this._zoomOutBtn = pagerButton(ICONS.zoomOut, t("pdfv.zoomOut"), () => this.zoomOut());
    this._zoomInBtn = pagerButton(ICONS.zoomIn, t("pdfv.zoomIn"), () => this.zoomIn());
    const zoomGroup = el("div", { class: "pdfv__pager pdfv__pager--zoom" }, [this._zoomOutBtn, this._zoomInBtn]);

    // --- "Çık" — araç çubuğunun EN SOLU (geri/kaçış eylemi sola yazılıdır) ---
    const exitBtn = this.onExit
      ? el("button", {
          class: "btn btn--sm pdfv__exit", type: "button", onclick: () => this.onExit(),
        }, [el("span", { class: "btn__ico", html: ICONS.exit }), t("pdfv.exit")])
      : null;

    // --- Sağ bölüm: dış eylemler (beğen/kaydet/paylaş…) + indir ---
    const rightItems = [];
    if (this.toolbarExtra) rightItems.push(this.toolbarExtra);
    if (this.showDownload) {
      rightItems.push(el("a", { class: "btn btn--sm", href: this.downloadUrl },
        [el("span", { class: "btn__ico", html: ICONS.download }), t("pdfv.download")]));
    }

    const toolbar = el("div", { class: "pdfv__toolbar" }, [
      exitBtn,                                  // en sol: Çık
      this.toolbarStart,                        // sonra: yazar künyesi (varsa)
      pager,
      zoomGroup,
      el("div", { class: "pdfv__spacer" }),
      ...rightItems,
    ].filter(Boolean));
    this._toolbarEl = toolbar;   // _scrollToPageTop() konumunu buna göre hesaplar

    // Sahne (kaydırılabilir alan) + canvas
    this._canvasEl = el("canvas", { class: "pdfv__canvas" });
    this._statusEl = el("div", { class: "pdfv__loading" }, [""]);
    this._stageEl = el("div", { class: "pdfv__stage" }, [this._statusEl]);

    render(this.container, toolbar, this._stageEl);
    this.container.classList.add("pdfv");
  }

  _setStatus(text) {
    if (!this._statusEl) return;
    if (text) {
      this._statusEl.textContent = text;
      if (!this._statusEl.isConnected) this._stageEl.append(this._statusEl);
    } else {
      this._statusEl.remove();
      // Canvas'ı sahneye koy (bir kez).
      if (!this._canvasEl.isConnected) {
        this._stageEl.append(this._canvasEl);
      }
    }
  }

  _syncToolbar() {
    this._setStatus(this.rendering && !this._canvasEl.isConnected ? t("pdfv.loading") : "");
    if (document.activeElement !== this._pageInputEl) {
      this._pageInputEl.value = String(this.pageNum);
    }
    this._prevBtn.disabled = this.pageNum <= 1;
    this._nextBtn.disabled = this.pageNum >= this.pageCount;

    // Okuma ilerlemesi (oku.js üstteki ince çubuğu günceller).
    if (this.onProgress && this.pageCount > 0) {
      this.onProgress(this.pageNum, this.pageCount);
    }
  }

  // ------------------------------------------------------------------
  // Klavye
  // ------------------------------------------------------------------

  _onKeyDown(e) {
    // Sayfa numarası kutusuna yazarken klavye kısayollarını devre dışı bırak.
    if (e.target instanceof HTMLInputElement) return;

    switch (e.key) {
      case "ArrowRight":
      case "PageDown":
        e.preventDefault();
        this.next();
        break;
      case "ArrowLeft":
      case "PageUp":
        e.preventDefault();
        this.prev();
        break;
      case "Home":
        e.preventDefault();
        this.goToPage(1);
        break;
      case "End":
        e.preventDefault();
        this.goToPage(this.pageCount);
        break;
      case "+":
      case "=":
        e.preventDefault();
        this.zoomIn();
        break;
      case "-":
        e.preventDefault();
        this.zoomOut();
        break;
      default:
        break;
    }
  }

  // ------------------------------------------------------------------
  // Küçük yardımcı
  // ------------------------------------------------------------------

  _debounce(fn, ms) {
    let t;
    return (...args) => {
      clearTimeout(t);
      t = setTimeout(() => fn(...args), ms);
    };
  }
}

/** Araç çubuğu için ikon düğmesi üretir. */
function iconButton(svg, label, onClick) {
  return el(
    "button",
    { class: "icon-btn", type: "button", title: label, "aria-label": label, onclick: onClick },
    [el("span", { html: svg })],
  );
}

/** Hap (pager) içindeki ok düğmesi — kenarlıksız, hapın parçası gibi görünür. */
function pagerButton(svg, label, onClick) {
  return el(
    "button",
    { class: "pdfv__pagerbtn", type: "button", title: label, "aria-label": label, onclick: onClick },
    [el("span", { html: svg })],
  );
}

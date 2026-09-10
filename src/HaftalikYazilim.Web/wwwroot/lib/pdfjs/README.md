# PDF.js (projeye dahil edilmiş)

Bu klasör Mozilla'nın **PDF.js** kütüphanesini içerir. CDN'e / internete
bağımlılık olmasın diye dosyalar doğrudan projeye kopyalandı.

| Dosya | Ne işe yarar |
|---|---|
| `pdf.min.mjs` | Ana kütüphane (ES modülü) |
| `pdf.worker.min.mjs` | PDF çözümlemeyi ayrı iş parçacığında yapan "worker" |
| `LICENSE` | Apache License 2.0 |

**Sürüm:** `pdfjs-dist@4.10.38`

## Güncellemek için

```bash
V=4.10.38   # istediğin sürüm
curl -sSL -o pdf.min.mjs        "https://cdn.jsdelivr.net/npm/pdfjs-dist@${V}/build/pdf.min.mjs"
curl -sSL -o pdf.worker.min.mjs "https://cdn.jsdelivr.net/npm/pdfjs-dist@${V}/build/pdf.worker.min.mjs"
curl -sSL -o LICENSE            "https://cdn.jsdelivr.net/npm/pdfjs-dist@${V}/LICENSE"
```

Kütüphaneyi kullanan tek yer: `wwwroot/assets/js/pdf-viewer.js`

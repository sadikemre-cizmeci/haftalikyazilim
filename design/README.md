# Logo üreteci

`logo.svg` elle çizilmedi — `logo-gen.mjs` ile üretiliyor. Fırça darbesinin
kalınlığı, kıvrımı, topağı ve kuyruktaki renkler oradaki sayılarla ayarlanır.

```bash
node design/logo-gen.mjs src/HaftalikYazilim.Web/wwwroot/assets/img/logo.svg
```

Ayar noktaları (hepsi `logo-gen.mjs` içinde, yorumlu):

| Ne | Değişken |
|---|---|
| Dairenin yarıçapı | `R` |
| Nereden başlayıp nerede bitsin (açı) | `A0`, `A1` |
| Kuyruk nereden içeri kıvrılmaya başlasın | `CURL_START`, `CURL_END_R` |
| Fırçanın kalınlığı | `width()` içindeki `let w = 13.4` |
| Başlangıç topağının büyüklüğü | `width()` içindeki `Math.exp(...) * 10.5` ve `startBlob` |
| Kuyruğun ne zaman incelmeye başladığı | `width()` içindeki `const TAIL = 0.79` |
| Kuru fırça boşlukları | `skips` dizisi |
| Kuyruktaki canlı renkler | `colorBristles` dizisi |
| Kenar dokusunun sertliği | `feDisplacementMap` `scale` değerleri |

Önizleme sayfası: `wwwroot/logo.html` (logo kesinleşince silinebilir).

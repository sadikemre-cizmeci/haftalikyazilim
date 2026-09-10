namespace HaftalikYazilim.Web.Domain;

/// <summary>
/// Bir yazının yayın durumu.
///
/// - <see cref="Draft"/> (Taslak): Sadece yazının sahibi ve Admin görebilir.
///   Herkese açık "Yazılar" listesinde ÇIKMAZ.
/// - <see cref="Published"/> (Yayında): Herkes görebilir, PDF'i indirebilir.
///
/// Yeni yazı ilk oluşturulduğunda <see cref="Draft"/> olur. Yazar hazır olduğunda
/// "Yayınla" der ve durum <see cref="Published"/> olur.
///
/// Bu enum da veritabanına METİN olarak yazılır.
/// </summary>
public enum ArticleStatus
{
    Draft = 0,
    Published = 1
}

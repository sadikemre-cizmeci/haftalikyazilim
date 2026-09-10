namespace HaftalikYazilim.Web.Domain;

/// <summary>
/// Sistemdeki kullanıcı rolleri.
///
/// - <see cref="Author"/> (Yazar): Sadece KENDİ yazılarını oluşturur, düzenler,
///   yayınlar ve siler. Başkasının yazısına dokunamaz.
/// - <see cref="Admin"/> (Yönetici): Her şeyi görür. Herkesin yazısında ve
///   kullanıcı listesinde işlem yapabilir.
///
/// NOT: "Okuyucu" burada bir rol DEĞİLDİR. Siteyi okuyan / PDF indiren kişilerin
/// hesabı olmasına gerek yoktur; yazı listesi ve indirme uçları anonim erişime açıktır.
///
/// Bu enum veritabanına METİN olarak yazılır (bkz. <c>AppDbContext.OnModelCreating</c>).
/// Böylece veritabanına elle baktığında "Admin" / "Author" görürsün, "0" / "1" değil.
/// </summary>
public enum UserRole
{
    Author = 0,
    Admin = 1
}

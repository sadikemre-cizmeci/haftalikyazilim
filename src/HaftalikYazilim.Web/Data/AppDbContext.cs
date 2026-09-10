using HaftalikYazilim.Web.Domain;
using Microsoft.EntityFrameworkCore;

namespace HaftalikYazilim.Web.Data;

/// <summary>
/// EF Core veritabanı bağlamı. Uygulamanın veritabanıyla TEK temas noktası burasıdır.
/// Sağlayıcı: PostgreSQL (Npgsql) — bkz. Program.cs.
///
/// Migration almak / uygulamak:
///     dotnet ef migrations add &lt;Ad&gt; --project src/HaftalikYazilim.Web
///     dotnet ef database update       --project src/HaftalikYazilim.Web
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Article> Articles => Set<Article>();
    public DbSet<Follow> Follows => Set<Follow>();
    public DbSet<ArticleLike> ArticleLikes => Set<ArticleLike>();
    public DbSet<ArticleBookmark> ArticleBookmarks => Set<ArticleBookmark>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<ArticleEvent> ArticleEvents => Set<ArticleEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // pg_trgm: "içinde geçen" (substring) aramayı HIZLI yapan PostgreSQL eklentisi.
        // Aşağıdaki GIN indeksleri sayesinde  ILIKE '%kelime%'  sorguları büyük
        // tabloda bile tarama yapmadan çalışır → arama DB'yi yormaz.
        modelBuilder.HasPostgresExtension("pg_trgm");

        // ====================================================================
        // USERS
        // ====================================================================
        modelBuilder.Entity<User>(user =>
        {
            user.Property(u => u.Email).IsRequired().HasMaxLength(256);
            user.Property(u => u.DisplayName).IsRequired().HasMaxLength(120);
            user.Property(u => u.PasswordHash).IsRequired();

            // Rol enum'u veritabanına METİN olarak ("Admin" / "Author").
            user.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);

            // Profil metinleri: uzunluk sınırı YOK → PostgreSQL 'text'.
            user.Property(u => u.Bio);
            user.Property(u => u.About);
            user.Property(u => u.AvatarFileName).HasMaxLength(260);

            user.Property(u => u.Username).IsRequired().HasMaxLength(30);

            user.HasIndex(u => u.Email).IsUnique();
            user.HasIndex(u => u.Username).IsUnique();

            // Yazar adına göre arama için trigram indeksi.
            user.HasIndex(u => u.DisplayName)
                .HasMethod("gin")
                .HasOperators("gin_trgm_ops");
        });

        // ====================================================================
        // ARTICLES
        // ====================================================================
        modelBuilder.Entity<Article>(article =>
        {
            article.Property(a => a.Title).IsRequired().HasMaxLength(200);
            // Özet artık zorunlu değil; sadece üst sınır.
            article.Property(a => a.Summary).HasMaxLength(500);
            article.Property(a => a.Slug).IsRequired().HasMaxLength(220);
            article.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
            article.Property(a => a.StoredFileName).IsRequired().HasMaxLength(260);
            article.Property(a => a.OriginalFileName).IsRequired().HasMaxLength(260);

            article.HasOne(a => a.Author)
                   .WithMany(u => u.Articles)
                   .HasForeignKey(a => a.AuthorId)
                   .OnDelete(DeleteBehavior.Restrict);

            article.HasIndex(a => a.Status);
            article.HasIndex(a => a.AuthorId);
            article.HasIndex(a => a.Slug);

            // Başlık/özet içinde arama için trigram (GIN) indeksleri.
            article.HasIndex(a => a.Title)
                .HasMethod("gin")
                .HasOperators("gin_trgm_ops");
            article.HasIndex(a => a.Summary)
                .HasMethod("gin")
                .HasOperators("gin_trgm_ops");

            // Herkese açık liste sıralaması: (Status, PublishedAt DESC) — sayfalı
            // sorguların indeks kullanması için.
            article.HasIndex(a => new { a.Status, a.PublishedAtUtc });

            // GLOBAL QUERY FILTER: silinmiş yazılar hiçbir sorguda gelmez.
            // Silinmişleri görmek için .IgnoreQueryFilters() kullanılır (Admin ekranı).
            article.HasQueryFilter(a => !a.IsDeleted);
        });

        // ====================================================================
        // SOSYAL: takip / beğeni / yıldız / yorum
        // ====================================================================

        modelBuilder.Entity<Follow>(f =>
        {
            // Bileşik anahtar: bir kişi bir kişiyi en fazla bir kez takip eder.
            f.HasKey(x => new { x.FollowerId, x.FolloweeId });

            f.HasOne(x => x.Follower).WithMany()
                .HasForeignKey(x => x.FollowerId).OnDelete(DeleteBehavior.Cascade);
            f.HasOne(x => x.Followee).WithMany()
                .HasForeignKey(x => x.FolloweeId).OnDelete(DeleteBehavior.Cascade);

            f.HasIndex(x => x.FolloweeId); // "beni kimler takip ediyor" sorgusu için
        });

        modelBuilder.Entity<ArticleLike>(l =>
        {
            l.HasKey(x => new { x.ArticleId, x.UserId });
            l.HasOne(x => x.Article).WithMany()
                .HasForeignKey(x => x.ArticleId).OnDelete(DeleteBehavior.Cascade);
            l.HasOne(x => x.User).WithMany()
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            l.HasIndex(x => new { x.UserId, x.CreatedAtUtc }); // "beğendiklerim" listesi
        });

        modelBuilder.Entity<ArticleBookmark>(b =>
        {
            b.HasKey(x => new { x.ArticleId, x.UserId });
            b.HasOne(x => x.Article).WithMany()
                .HasForeignKey(x => x.ArticleId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.User).WithMany()
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.UserId, x.CreatedAtUtc }); // "kaydettiklerim" listesi
        });

        modelBuilder.Entity<Comment>(c =>
        {
            c.Property(x => x.Body).IsRequired().HasMaxLength(4000);
            c.HasOne(x => x.Article).WithMany()
                .HasForeignKey(x => x.ArticleId).OnDelete(DeleteBehavior.Cascade);
            c.HasOne(x => x.User).WithMany()
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            c.HasIndex(x => new { x.ArticleId, x.CreatedAtUtc });
            c.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<ArticleEvent>(e =>
        {
            e.Property(x => x.Kind).IsRequired().HasMaxLength(16);
            e.HasOne(x => x.Article).WithMany()
                .HasForeignKey(x => x.ArticleId).OnDelete(DeleteBehavior.Cascade);
            // Zaman filtreli gruplama için: (Kind, CreatedAtUtc)
            e.HasIndex(x => new { x.Kind, x.CreatedAtUtc });
        });
    }
}

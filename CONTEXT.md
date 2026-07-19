# OrderProcessing — Proje Karar Kaydı

> Bu dosya proje başlangıcında alınan tüm mimari ve teknik kararları belgelemek için oluşturulmuştur.

## 📋 Proje Özeti

**Amaç:** GitHub portföyü için, modern yazılım konseptlerini (async programming, event-driven architecture, Kafka) sergileyen bir sipariş işleme sistemi.

**Konsept:** Bir e-ticaret siparişi Web API üzerinden gönderilir → Kafka topic'ine yazılır → birden fazla consumer tarafından kademeli olarak işlenir.

---

## 🧱 Teknik Kararlar

| Karar | Seçim | Gerekçe |
|-------|-------|---------|
| **Programlama Dili** | C# .NET 10 | En güncel stabil sürüm, cihazda mevcut |
| **Kafka Dağıtımı** | Docker Compose + KRaft | Zookeeper'sız modern Kafka mimarisi |
| **Consumer Mimarisi** | Kademeli Pipeline (Seçenek B) | Çoklu topic, event-driven pipeline, daha profesyonel |
| **Proje Türü** | Web API + Kullanıcı Arayüzü | REST endpoint'lerle sipariş gönderimi + görsel takip |
| **Proje Yapısı** | Monorepo — Tek Solution | .NET Solution altında multiple projects |
| **Branch Stratejisi** | `develop` → PR → `main` | `develop` üzerinde geliştirme, PR ile `main`'e merge |
| **Sync → Async Dönüşümü** | Issue #1 → PR #2 | Önce sync çalışan sistem, sonra async'e geçiş (202 Accepted) |
| **ORM** | Entity Framework Core + SQLite | Hafif, dosya tabanlı, migration desteği |
| **API Dokümantasyonu** | Scalar | Swagger'a modern alternatif, .NET 10 ile native destek |
| **Consumer Projeleri** | Worker Service | Bağımsız çalıştırılabilir BackgroundService'ler |
| **Blazor Render Mode** | InteractiveServer | .NET 10'da her sayfaya `@rendermode InteractiveServer` zorunlu |
| **Blazor DbContext** | IServiceScopeFactory | Scoped DbContext disposed oluyor, her işlem için yeni scope |

---

## 🏗️ Consumer Pipeline Mimarisi (Seçenek B)

```
Web API (Producer) → Kafka Topic "raw-orders"
                         │
                         ▼
                  OrderProcessor (Consumer)
                  ├── Siparişi doğrula
                  ├── Stok kontrolü yap
                  ├── Toplam tutarı hesapla
                  └── "processed-orders" topic'ine yaz
                         │
            ┌────────────┼────────────┐
            ▼            ▼            ▼
     EmailConsumer  StockConsumer  LoggingConsumer
```

---

## 📦 Sipariş Veri Modeli (Detaylı)

`Order` entity'si şunları içerecek:
- **Id** (Guid)
- **CustomerId** (Guid)
- **CustomerName** (string)
- **CustomerEmail** (string)
- **Items** (List<OrderItem>)
- **TotalPrice** (decimal) — hesaplanan
- **Status** (enum: Pending → Processing → Processed → Failed)
- **CreatedAt** (DateTime)

`OrderItem` entity'si:
- **Id** (Guid)
- **ProductId** (Guid)
- **ProductName** (string)
- **UnitPrice** (decimal)
- **Quantity** (int)
- **SubTotal** (decimal) — UnitPrice * Quantity

---

## 📁 Proje Dizini Yapısı

```
Async Programming/
├── docker/
│   └── docker-compose.yml
├── OrderProcessing.sln
├── src/
│   ├── OrderProcessing.Core/             # Class Library — modeller, EF DbContext, config
│   ├── OrderProcessing.API/              # Web API (Producer) + Blazor Server UI
│   ├── OrderProcessing.OrderProcessor/   # Worker Service — ana işlemci consumer
│   ├── OrderProcessing.EmailConsumer/    # Worker Service — mail simülasyonu
│   ├── OrderProcessing.StockConsumer/    # Worker Service — stok simülasyonu
│   └── OrderProcessing.LoggingConsumer/  # Worker Service — loglama
├── .gitignore
└── README.md
```

---

## 🌿 Git Branch Stratejisi

- `main` — Production-ready kod
- `develop` — Geliştirme branch'i
- Issue'dan branch aç → PR ile `develop`'a merge
- `develop` belirli noktalarda `main`'e merge

---

## 🗺️ Geliştirme Aşamaları

| Aşama | Açıklama | Commit |
|-------|----------|--------|
| **Phase 1** | Proje scaffold, Docker altyapısı, Core katmanı | `init: project scaffold with core models...` |
| **Phase 2** | Web API + Sync işleme + Blazor Dashboard | `feat: add web api with sync order processing...` |
| **Phase 3** | Kafka entegrasyonu + Worker Service'ler | `feat: add kafka producer and worker service consumers` |
| **Phase 4** | **Issue #1 → PR #2:** Sync → Async dönüşümü | `refactor: convert order processing to fully async pipeline` |
| **Phase 4 (fix)** | Blazor InteractiveServer, DbContext lifecycle, paket güncelleme | `fix: Blazor Server interactive mode, DbContext lifecycle...` |
| **Phase 5** | README, dokümantasyon, final polish | `docs: add comprehensive readme with multi-terminal guidance` |
| **Phase 6** | Branch stratejisi düzeltmesi + v1.0.0 release | `main` oluşturuldu, develop main'e merge edildi, `v1.0.0` tag'i eklendi |

## 🔧 Çözülen Sorunlar (Karar Kaydı)

| Sorun | Çözüm |
|-------|-------|
| Kafka KRaft `0.0.0.0` hatası | `advertised.listeners` ve `listeners` ayrı ayrı yapılandırıldı |
| API port conflict | Eski process `lsof -ti:5001` ile temizleniyor |
| Log format exception (OrderPublisher) | `[{Topic}]` placeholder kaldırıldı |
| Blazor DbContext disposed | `IServiceScopeFactory` ile her işlemde yeni scope |
| Blazor butonlar çalışmıyor | Her sayfaya `@rendermode InteractiveServer` eklendi |
| NU1903 güvenlik warning'leri (eski) | ❌ `Directory.Build.props` ile global suppress (best practice ihlali) |
| NU1903 güvenlik warning'leri (düzeltildi) | ✅ Transitive dependency override ile çözüldü: `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 → 3.53.3, `Microsoft.OpenApi` 2.0.0 → 2.11.0, NuGet Audit `WarningsAsErrors` olarak aktif |

---

> **Son güncelleme:** 2026-07-19
>
> **🧹 Branch stratejisi düzeltmesi (portfolio):**
> - `main` branch'i başlangıçta hiç oluşturulmamıştı; tüm commit'ler doğrudan `develop`'a atılmıştı
> - Çözüm: `develop`'dan `main` branch'i oluşturuldu, GitHub'a push edildi
> - `v1.0.0` tag'i eklendi
> - GitHub repository ayarlarından default branch `develop` → `main` olarak değiştirilmeli
> **Katılımcı:** Serfiraz Abdullah Mumcu

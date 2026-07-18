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
| **Branch Stratejisi** | `main` + `develop` | `develop` üzerinde geliştirme, PR ile `main`'e merge |
| **Sync → Async Dönüşümü** | Issue + PR ile | Önce sync çalışan sistem, sonra async'e geçiş |
| **ORM** | Entity Framework Core + SQLite | Hafif, dosya tabanlı, migration desteği |
| **API Dokümantasyonu** | Scalar | Swagger'a modern alternatif, .NET 10 ile native destek |
| **Consumer Projeleri** | Worker Service | Bağımsız çalıştırılabilir BackgroundService'ler |

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
| **Phase 1** | Proje scaffold, Docker altyapısı, Core katmanı | `init: project setup and core layer` |
| **Phase 2** | Web API + Producer (sync sipariş gönderimi) | `feat: add web api with sync order creation` |
| **Phase 3** | Consumer'ların devreye alınması | `feat: add kafka consumers` |
| **Phase 4** | **Issue:** Sync → Async dönüşümü | PR ile çözülecek |
| **Phase 5** | UI / Dashboard | `feat: add order monitoring dashboard` |
| **Phase 6** | README, dokümantasyon, polish | `docs: add readme and documentation` |

---

> **Son güncelleme:** 2026-07-18
> **Katılımcı:** Serfiraz Abdullah Mumcu

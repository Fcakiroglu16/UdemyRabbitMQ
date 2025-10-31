#region

using RabbitMQ.Client;

#endregion

namespace Producer;

/// <summary>
///     RabbitMQ üzerinde kuyruk oluşturma ile ilgili tüm senaryoları içeren sınıf.
///     Her metod farklı bir kuyruk oluşturma senaryosunu göstermektedir.
/// </summary>
public class RabbitMQQueueScenarios
{
    private readonly IChannel _channel;

    public RabbitMQQueueScenarios(IChannel channel)
    {
        _channel = channel;
    }

    #region Priority Queue Senaryoları

    /// <summary>
    ///     Senaryo 13: Öncelikli kuyruk oluşturma
    ///     - x-max-priority: Maksimum öncelik değeri (genellikle 1-10 arası)
    ///     - Yüksek öncelikli mesajlar önce işlenir
    ///     Kullanım: Kritik mesajların öncelikli işlenmesi gerektiğinde
    ///     Not: Performans maliyeti olabilir, gerçekten gerekmedikçe kullanmayın
    /// </summary>
    public async Task CreatePriorityQueue()
    {
        var queueName = "priority-queue";
        var arguments = new Dictionary<string, object>
        {
            { "x-max-priority", 10 } // 0-10 arası öncelik
        };

        await _channel.QueueDeclareAsync(
            queueName,
            true,
            false,
            false,
            arguments
        );

        Console.WriteLine($"✓ Öncelikli kuyruk oluşturuldu: {queueName}");
        Console.WriteLine("  → 0-10 arası öncelik seviyesi desteklenir");
        Console.WriteLine("  → Yüksek öncelikli mesajlar önce işlenir");
    }

    #endregion

    #region Lazy Queue Senaryoları

    /// <summary>
    ///     Senaryo 14: Lazy Queue oluşturma
    ///     - x-queue-mode: "lazy" (Mesajlar disk'e yazılır, RAM tasarrufu)
    ///     Kullanım: Çok fazla mesaj biriken kuyruklar, bellek tasarrufu gerektiğinde
    ///     Artı: RAM kullanımı düşük
    ///     Eksi: Performans biraz düşer
    ///     // Modern RabbitMQ (Sürüm 3.12 ve Üzeri) default olarak lazy queue kullanır.
    /// </summary>
    public async Task CreateLazyQueue()
    {
        var queueName = "lazy-queue";
        var arguments = new Dictionary<string, object>
        {
            { "x-queue-mode", "lazy" }
        };

        await _channel.QueueDeclareAsync(
            queueName,
            true,
            false,
            false,
            arguments
        );

        Console.WriteLine($"✓ Lazy Queue oluşturuldu: {queueName}");
        Console.WriteLine("  → Mesajlar disk'te saklanır, RAM tasarrufu sağlar");
        Console.WriteLine("  → Milyonlarca mesaj için idealdir");
    }

    #endregion

    #region Single Active Consumer Senaryoları

    /// <summary>
    ///     Senaryo 15: Single Active Consumer kuyruk oluşturma
    ///     - x-single-active-consumer: true
    ///     - Aynı anda sadece bir consumer aktif olur
    ///     - Aktif consumer kapanırsa, otomatik olarak başka biri aktif olur
    ///     Kullanım: Mesajların sıralı işlenmesi gerektiğinde
    /// </summary>
    public async Task CreateSingleActiveConsumerQueue()
    {
        var queueName = "single-active-consumer-queue";
        var arguments = new Dictionary<string, object>
        {
            { "x-single-active-consumer", true }
        };

        await _channel.QueueDeclareAsync(
            queueName,
            true,
            false,
            false,
            arguments
        );

        Console.WriteLine($"✓ Single Active Consumer kuyruk oluşturuldu: {queueName}");
        Console.WriteLine("  → Aynı anda sadece bir consumer mesaj alabilir");
        Console.WriteLine("  → Failover desteği vardır");
    }

    #endregion

    #region Temel Kuyruk Senaryoları

    /// <summary>
    ///     Senaryo 1: En basit geçici kuyruk oluşturma
    ///     - Durable: false (RabbitMQ yeniden başlatıldığında kuyruk kaybolur)
    ///     - Exclusive: false (Başka bağlantılar tarafından erişilebilir)
    ///     - AutoDelete: false (Son consumer ayrıldığında kuyruk silinmez)
    ///     - Arguments: null (Ekstra parametre yok)
    ///     Kullanım: Test ve geliştirme ortamları için uygundur
    /// </summary>
    public async Task CreateBasicTemporaryQueue()
    {
        var queueName = "basic-temp-queue";


        await _channel.QueueDeclareAsync(
            queueName,
            false,
            false,
            false
        );

        Console.WriteLine($"✓ Basit geçici kuyruk oluşturuldu: {queueName}");
    }

    /// <summary>
    ///     Senaryo 2: Kalıcı (Durable) kuyruk oluşturma
    ///     - Durable: true (RabbitMQ yeniden başlatıldığında kuyruk korunur)
    ///     - Exclusive: false
    ///     - AutoDelete: false
    ///     Kullanım: Üretim ortamları, kritik mesajlar için önerilir
    ///     Not: Mesajların da kalıcı olması için mesaj özellikleri ile Persistent=true ayarlanmalıdır
    /// </summary>
    public async Task CreateDurableQueue()
    {
        var queueName = "durable-queue";
        await _channel.QueueDeclareAsync(
            queueName,
            true,
            false,
            false
        );

        Console.WriteLine($"✓ Kalıcı kuyruk oluşturuldu: {queueName}");
        Console.WriteLine("  → RabbitMQ yeniden başlatılsa bile bu kuyruk korunacak");
    }

    /// <summary>
    ///     Senaryo 3: Özel (Exclusive) kuyruk oluşturma
    ///     - Durable: false
    ///     - Exclusive: true (Sadece bu bağlantı tarafından kullanılabilir)
    ///     - AutoDelete: true (Bağlantı kapandığında otomatik silinir)
    ///     Kullanım: Özel iş süreçleri, tek bir consumer için özel kuyruklar
    ///     Not: Exclusive kuyruklar genellikle auto-delete ile birlikte kullanılır
    /// </summary>
    public async Task CreateExclusiveQueue()
    {
        var queueName = "exclusive-queue";

        await _channel.QueueDeclareAsync(
            queueName,
            false,
            true,
            true
        );

        Console.WriteLine($"✓ Özel kuyruk oluşturuldu: {queueName}");
        Console.WriteLine("  → Bu kuyruk sadece bu bağlantı tarafından kullanılabilir");
        Console.WriteLine("  → Bağlantı kapandığında otomatik silinecek");
    }

    /// <summary>
    ///     Senaryo 4: Otomatik silinen (AutoDelete) kuyruk oluşturma
    ///     - Durable: false
    ///     - Exclusive: false
    ///     - AutoDelete: true (Son consumer ayrıldığında kuyruk otomatik silinir)
    ///     Kullanım: Geçici iş yükleri, dinamik kuyruklar için uygundur
    /// </summary>
    public async Task CreateAutoDeleteQueue()
    {
        var queueName = "auto-delete-queue";

        await _channel.QueueDeclareAsync(
            queueName,
            false,
            false,
            true
        );

        Console.WriteLine($"✓ Otomatik silinen kuyruk oluşturuldu: {queueName}");
        Console.WriteLine("  → Tüm consumer'lar ayrıldığında bu kuyruk otomatik silinecek");
    }

    /// <summary>
    ///     Senaryo 5: Sunucu tarafından isim verilen kuyruk oluşturma
    ///     - Queue: string.Empty (RabbitMQ otomatik benzersiz isim verir)
    ///     - Genellikle exclusive ve auto-delete ile kullanılır
    ///     Kullanım: Temporary reply kuyrukları, RPC pattern, pub/sub senaryoları
    /// </summary>
    public async Task<string> CreateServerNamedQueue()
    {
        var queueDeclareOk = await _channel.QueueDeclareAsync(
            string.Empty,
            false,
            true,
            true
        );

        var queueName = queueDeclareOk.QueueName;

        Console.WriteLine($"✓ Sunucu tarafından isimlendirilmiş kuyruk: {queueName}");
        Console.WriteLine("  → RabbitMQ otomatik olarak benzersiz bir isim verdi");

        return queueName;
    }

    #endregion

    #region TTL (Time To Live) Senaryoları

    /// <summary>
    ///     Senaryo 6: Mesaj TTL'li kuyruk oluşturma
    ///     - Message TTL: Kuyruktaki mesajların maksimum yaşam süresi
    ///     - x-message-ttl: Milisaniye cinsinden süre
    ///     Kullanım: Geçici mesajlar, cache senaryoları, zaman aşımı gerektiren işler
    /// </summary>
    public async Task CreateQueueWithMessageTTL()
    {
        var queueName = "message-ttl-queue";
        var arguments = new Dictionary<string, object>
        {
            { "x-message-ttl", 60000 } // 60 saniye (60000 ms)
        };

        await _channel.QueueDeclareAsync(
            queueName,
            true,
            false,
            false,
            arguments
        );

        Console.WriteLine($"✓ Mesaj TTL'li kuyruk oluşturuldu: {queueName}");
        Console.WriteLine("  → Mesajlar 60 saniye sonra otomatik silinecek");
    }

    /// <summary>
    ///     Senaryo 7: Kuyruk TTL'li kuyruk oluşturma
    ///     - Queue TTL: Kuyruk belirli süre kullanılmazsa otomatik silinir
    ///     - x-expires: Milisaniye cinsinden süre
    ///     Kullanım: Dinamik kuyruklar, geçici iş süreçleri
    /// </summary>
    public async Task CreateQueueWithQueueTTL()
    {
        var queueName = "queue-ttl-queue";
        var arguments = new Dictionary<string, object>
        {
            { "x-expires", 300000 } // 5 dakika (300000 ms)
        };

        await _channel.QueueDeclareAsync(
            queueName,
            true,
            false,
            false,
            arguments
        );

        Console.WriteLine($"✓ Kuyruk TTL'li kuyruk oluşturuldu: {queueName}");
        Console.WriteLine("  → 5 dakika kullanılmazsa kuyruk otomatik silinecek");
    }

    #endregion

    #region Dead Letter Exchange (DLX) Senaryoları

    /// <summary>
    ///     Senaryo 8: Dead Letter Exchange (DLX) ile kuyruk oluşturma
    ///     - x-dead-letter-exchange: Reddedilen veya TTL aşan mesajların gideceği exchange
    ///     - x-dead-letter-routing-key: DLX'e gönderilirken kullanılacak routing key
    ///     Kullanım: Hata yönetimi, mesaj yeniden deneme mekanizmaları
    /// </summary>
    public async Task CreateQueueWithDeadLetterExchange()
    {
        var queueName = "main-queue-with-dlx";
        var deadLetterExchange = "dead-letter-exchange";
        var deadLetterRoutingKey = "dead-letter-key";

        // Önce DLX'i oluştur
        await _channel.ExchangeDeclareAsync(deadLetterExchange, ExchangeType.Direct, true);

        var arguments = new Dictionary<string, object>
        {
            { "x-dead-letter-exchange", deadLetterExchange },
            { "x-dead-letter-routing-key", deadLetterRoutingKey },
            { "x-message-ttl", 30000 } // 30 saniye sonra DLX'e gönderilir
        };

        await _channel.QueueDeclareAsync(
            queueName,
            true,
            false,
            false,
            arguments
        );

        Console.WriteLine($"✓ DLX'li kuyruk oluşturuldu: {queueName}");
        Console.WriteLine($"  → Reddedilen mesajlar '{deadLetterExchange}' exchange'ine gönderilecek");
    }

    /// <summary>
    ///     Senaryo 9: Dead Letter Queue (DLQ) sistemi oluşturma
    ///     - Ana kuyruk + DLX + Dead Letter Queue kombinasyonu
    ///     Kullanım: Hatalı mesajları yakalama ve analiz etme
    /// </summary>
    public async Task CreateDeadLetterQueueSystem()
    {
        var mainQueueName = "main-processing-queue";
        var dlxName = "dlx-exchange";
        var dlqName = "dead-letter-queue";
        var dlqRoutingKey = "failed-messages";

        // 1. DLX oluştur
        await _channel.ExchangeDeclareAsync(dlxName, ExchangeType.Direct, true);

        // 2. Dead Letter Queue oluştur
        await _channel.QueueDeclareAsync(
            dlqName,
            true,
            false,
            false
        );

        // 3. DLQ'yu DLX'e bağla
        await _channel.QueueBindAsync(dlqName, dlxName, dlqRoutingKey);

        // 4. Ana kuyruğu DLX ile oluştur
        var arguments = new Dictionary<string, object>
        {
            { "x-dead-letter-exchange", dlxName },
            { "x-dead-letter-routing-key", dlqRoutingKey }
        };

        await _channel.QueueDeclareAsync(
            mainQueueName,
            true,
            false,
            false,
            arguments
        );

        Console.WriteLine("✓ Dead Letter Queue sistemi oluşturuldu:");
        Console.WriteLine($"  → Ana Kuyruk: {mainQueueName}");
        Console.WriteLine($"  → DLX: {dlxName}");
        Console.WriteLine($"  → DLQ: {dlqName}");
        Console.WriteLine("  → Başarısız mesajlar DLQ'ya yönlendirilecek");
    }

    #endregion

    #region Max Length Senaryoları

    /// <summary>
    ///     Senaryo 10: Maksimum mesaj sayısı limiti olan kuyruk
    ///     - x-max-length: Kuyruktaki maksimum mesaj sayısı
    ///     - Limit aşıldığında en eski mesaj silinir
    ///     Kullanım: Bellek yönetimi, buffer kuyrukları
    /// </summary>
    public async Task CreateQueueWithMaxLength()
    {
        var queueName = "max-length-queue";
        var arguments = new Dictionary<string, object>
        {
            { "x-max-length", 1000 } // Maksimum 1000 mesaj
        };

        await _channel.QueueDeclareAsync(
            queueName,
            true,
            false,
            false,
            arguments
        );

        Console.WriteLine($"✓ Maksimum mesaj sayılı kuyruk oluşturuldu: {queueName}");
        Console.WriteLine("  → Maksimum 1000 mesaj tutulabilir");
        Console.WriteLine("  → Limit aşıldığında en eski mesaj silinir");
    }

    /// <summary>
    ///     Senaryo 11: Maksimum byte boyutu limiti olan kuyruk
    ///     - x-max-length-bytes: Kuyruğun maksimum byte boyutu
    ///     Kullanım: Bellek optimizasyonu, büyük mesajlar için kontrol
    /// </summary>
    public async Task CreateQueueWithMaxLengthBytes()
    {
        var queueName = "max-length-bytes-queue";
        var arguments = new Dictionary<string, object>
        {
            { "x-max-length-bytes", 10485760 } // 10 MB (10 * 1024 * 1024)
        };

        await _channel.QueueDeclareAsync(
            queueName,
            true,
            false,
            false,
            arguments
        );

        Console.WriteLine($"✓ Maksimum byte boyutlu kuyruk oluşturuldu: {queueName}");
        Console.WriteLine("  → Maksimum 10 MB mesaj tutulabilir");
    }

    /// <summary>
    ///     Senaryo 12: Overflow davranışı belirlenmiş kuyruk
    ///     - x-overflow: Limit aşıldığında ne yapılacağını belirler
    ///     • "drop-head": En eski mesaj silinir (varsayılan)
    ///     • "reject-publish": Yeni mesajlar reddedilir
    ///     • "reject-publish-dlx": Yeni mesajlar reddedilip DLX'e gönderilir
    /// </summary>
    public async Task CreateQueueWithOverflowBehavior()
    {
        var queueName = "overflow-reject-queue";
        var arguments = new Dictionary<string, object>
        {
            { "x-max-length", 100 },
            { "x-overflow", "reject-publish" } // Yeni mesajları reddet
        };

        await _channel.QueueDeclareAsync(
            queueName,
            true,
            false,
            false,
            arguments
        );

        Console.WriteLine($"✓ Overflow davranışlı kuyruk oluşturuldu: {queueName}");
        Console.WriteLine("  → Maksimum 100 mesaj");
        Console.WriteLine("  → Limit aşıldığında yeni mesajlar reddedilir");
    }

    #endregion

    #region Quorum Queue Senaryoları

    /// <summary>
    ///     Senaryo 16: Quorum Queue oluşturma (RabbitMQ 3.8+)
    ///     - x-queue-type: "quorum" (Yüksek erişilebilirlik ve veri güvenliği)
    ///     - Raft consensus algoritması kullanır
    ///     - Replica'lar arasında otomatik senkronizasyon
    ///     Kullanım: Kritik mesajlar, yüksek güvenilirlik gerektiren senaryolar
    ///     Not: RabbitMQ 3.8+ gerektirir
    /// </summary>
    public async Task CreateQuorumQueue()
    {
        var queueName = "quorum-queue";
        var arguments = new Dictionary<string, object>
        {
            { "x-queue-type", "quorum" }
        };

        await _channel.QueueDeclareAsync(
            queueName,
            true, // Quorum queues her zaman durable'dır
            false,
            false,
            arguments
        );

        Console.WriteLine($"✓ Quorum Queue oluşturuldu: {queueName}");
        Console.WriteLine("  → Yüksek erişilebilirlik ve veri güvenliği sağlar");
        Console.WriteLine("  → Cluster ortamları için önerilir");
    }

    /// <summary>
    ///     Senaryo 17: Delivery limit'li Quorum Queue
    ///     - x-delivery-limit: Bir mesajın kaç kez redeliver edilebileceği
    ///     - Limit aşıldığında mesaj DLX'e gönderilir
    ///     Kullanım: Quorum queue'larda poison message koruması
    /// </summary>
    public async Task CreateQuorumQueueWithDeliveryLimit()
    {
        var queueName = "quorum-queue-with-delivery-limit";
        var dlxName = "quorum-dlx";

        // DLX oluştur
        await _channel.ExchangeDeclareAsync(dlxName, ExchangeType.Fanout, true);

        var arguments = new Dictionary<string, object>
        {
            { "x-queue-type", "quorum" },
            { "x-delivery-limit", 3 }, // Maksimum 3 deneme
            { "x-dead-letter-exchange", dlxName }
        };

        await _channel.QueueDeclareAsync(
            queueName,
            true,
            false,
            false,
            arguments
        );

        Console.WriteLine($"✓ Delivery limit'li Quorum Queue oluşturuldu: {queueName}");
        Console.WriteLine("  → Maksimum 3 kez redeliver edilebilir");
        Console.WriteLine("  → 3 başarısız denemeden sonra DLX'e gönderilir");
    }

    #endregion

    #region Stream Queue Senaryoları

    /// <summary>
    ///     Senaryo 18: Stream Queue oluşturma (RabbitMQ 3.9+)
    ///     - x-queue-type: "stream" (Log-based, append-only kuyruk)
    ///     - Mesajlar silinmez, sadece eklenir
    ///     - Yüksek throughput için optimize edilmiş
    ///     Kullanım: Event sourcing, audit logs, yüksek throughput senaryoları
    ///     Not: RabbitMQ 3.9+ gerektirir
    /// </summary>
    public async Task CreateStreamQueue()
    {
        var queueName = "stream-queue";
        var arguments = new Dictionary<string, object>
        {
            { "x-queue-type", "stream" },
            { "x-max-length-bytes", 20000000000 }, // 20 GB
            { "x-stream-max-segment-size-bytes", 500000000 } // 500 MB segment boyutu
        };

        await _channel.QueueDeclareAsync(
            queueName,
            true,
            false,
            false,
            arguments
        );

        Console.WriteLine($"✓ Stream Queue oluşturuldu: {queueName}");
        Console.WriteLine("  → Mesajlar kalıcı olarak saklanır");
        Console.WriteLine("  → Yüksek throughput için optimize edilmiş");
        Console.WriteLine("  → Maksimum 20 GB veri tutabilir");
    }

    /// <summary>
    ///     Senaryo 19: Retention policy'li Stream Queue
    ///     - x-max-age: Mesajların ne kadar süre saklanacağı
    ///     Kullanım: Zaman bazlı veri saklama
    /// </summary>
    public async Task CreateStreamQueueWithRetention()
    {
        var queueName = "stream-queue-with-retention";
        var arguments = new Dictionary<string, object>
        {
            { "x-queue-type", "stream" },
            { "x-max-age", "7D" } // 7 gün (D=days, h=hours, m=minutes, s=seconds)
        };

        await _channel.QueueDeclareAsync(
            queueName,
            true,
            false,
            false,
            arguments
        );

        Console.WriteLine($"✓ Retention policy'li Stream Queue oluşturuldu: {queueName}");
        Console.WriteLine("  → Mesajlar 7 gün boyunca saklanır");
        Console.WriteLine("  → 7 günden eski mesajlar otomatik silinir");
    }

    #endregion

    #region Kombine Senaryolar

    /// <summary>
    ///     Senaryo 20: Üretim ortamı için tam donanımlı kuyruk
    ///     - Durable + TTL + DLX + Max Length + Priority kombinasyonu
    ///     Kullanım: Enterprise uygulamalar, kritik iş süreçleri
    /// </summary>
    public async Task CreateProductionReadyQueue()
    {
        var queueName = "production-ready-queue";
        var dlxName = "production-dlx";
        var dlqName = "production-dlq";

        // DLX ve DLQ oluştur
        await _channel.ExchangeDeclareAsync(dlxName, ExchangeType.Direct, true);
        await _channel.QueueDeclareAsync(dlqName, true, false, false);
        await _channel.QueueBindAsync(dlqName, dlxName, "failed");

        var arguments = new Dictionary<string, object>
        {
            { "x-message-ttl", 3600000 }, // 1 saat
            { "x-dead-letter-exchange", dlxName },
            { "x-dead-letter-routing-key", "failed" },
            { "x-max-length", 10000 },
            { "x-max-priority", 10 },
            { "x-overflow", "reject-publish-dlx" },
            { "x-queue-mode", "lazy" }
        };

        await _channel.QueueDeclareAsync(
            queueName,
            true,
            false,
            false,
            arguments
        );

        Console.WriteLine($"✓ Üretim ortamı kuyruk oluşturuldu: {queueName}");
        Console.WriteLine("  → Kalıcı (Durable)");
        Console.WriteLine("  → Mesaj TTL: 1 saat");
        Console.WriteLine("  → Dead Letter Exchange aktif");
        Console.WriteLine("  → Maksimum 10,000 mesaj");
        Console.WriteLine("  → Öncelik desteği (0-10)");
        Console.WriteLine("  → Lazy mode (RAM tasarrufu)");
    }

    /// <summary>
    ///     Senaryo 21: Retry mekanizmalı kuyruk sistemi
    ///     - Ana kuyruk + Retry kuyruk + DLQ kombinasyonu
    ///     Kullanım: Hatalı mesajları yeniden deneme mekanizması
    /// </summary>
    public async Task CreateRetryQueueSystem()
    {
        var mainQueue = "main-retry-queue";
        var retryQueue = "retry-queue";
        var dlqQueue = "final-dlq";
        var retryExchange = "retry-exchange";
        var dlxExchange = "final-dlx";

        // Retry Exchange ve DLX oluştur
        await _channel.ExchangeDeclareAsync(retryExchange, ExchangeType.Direct, true);
        await _channel.ExchangeDeclareAsync(dlxExchange, ExchangeType.Direct, true);

        // DLQ oluştur
        await _channel.QueueDeclareAsync(dlqQueue, true, false, false);
        await _channel.QueueBindAsync(dlqQueue, dlxExchange, "failed");

        // Retry Queue oluştur (mesajlar burada bekler ve tekrar ana kuyruğa gider)
        var retryArgs = new Dictionary<string, object>
        {
            { "x-message-ttl", 60000 }, // 1 dakika bekle
            { "x-dead-letter-exchange", "" }, // Default exchange
            { "x-dead-letter-routing-key", mainQueue } // Ana kuyruğa geri gönder
        };
        await _channel.QueueDeclareAsync(retryQueue, true, false, false, retryArgs);
        await _channel.QueueBindAsync(retryQueue, retryExchange, "retry");

        // Ana kuyruk oluştur
        var mainArgs = new Dictionary<string, object>
        {
            { "x-dead-letter-exchange", retryExchange },
            { "x-dead-letter-routing-key", "retry" }
        };
        await _channel.QueueDeclareAsync(mainQueue, true, false, false, mainArgs);

        Console.WriteLine("✓ Retry mekanizmalı kuyruk sistemi oluşturuldu:");
        Console.WriteLine($"  → Ana Kuyruk: {mainQueue}");
        Console.WriteLine($"  → Retry Kuyruk: {retryQueue} (1 dakika bekleme)");
        Console.WriteLine($"  → Final DLQ: {dlqQueue}");
        Console.WriteLine("  → Başarısız mesajlar 1 dakika sonra tekrar denenir");
    }

    #endregion

    #region Master Queue Types Özetleri

    /// <summary>
    ///     Senaryo 22: Classic Queue (Varsayılan)
    ///     - RabbitMQ'nun geleneksel kuyruk tipi
    ///     - Genel amaçlı kullanım için uygundur
    /// </summary>
    public async Task CreateClassicQueue()
    {
        var queueName = "classic-queue";

        await _channel.QueueDeclareAsync(
            queueName,
            true,
            false,
            false // x-queue-type belirtilmezse Classic Queue olur
        );

        Console.WriteLine($"✓ Classic Queue oluşturuldu: {queueName}");
        Console.WriteLine("  → Standart RabbitMQ kuyruk tipi");
    }

    /// <summary>
    ///     Tüm kuyruk tiplerini karşılaştırmalı oluşturma
    ///     Classic vs Quorum vs Stream
    /// </summary>
    public async Task CreateAllQueueTypesComparison()
    {
        // Classic Queue
        await _channel.QueueDeclareAsync("compare-classic", true, false, false);

        // Quorum Queue
        var quorumArgs = new Dictionary<string, object> { { "x-queue-type", "quorum" } };
        await _channel.QueueDeclareAsync("compare-quorum", true, false, false, quorumArgs);

        // Stream Queue
        var streamArgs = new Dictionary<string, object>
        {
            { "x-queue-type", "stream" },
            { "x-max-length-bytes", 10000000000 } // 10 GB
        };
        await _channel.QueueDeclareAsync("compare-stream", true, false, false, streamArgs);

        Console.WriteLine("✓ Tüm kuyruk tipleri oluşturuldu:");
        Console.WriteLine("  → Classic: Genel amaçlı, esnek");
        Console.WriteLine("  → Quorum: Yüksek güvenilirlik, cluster için ideal");
        Console.WriteLine("  → Stream: Yüksek throughput, event sourcing");
    }

    #endregion

    #region Utility Methods

    /// <summary>
    ///     Kuyruğun mevcut olup olmadığını kontrol eder (Passive declare)
    /// </summary>
    public async Task<bool> CheckQueueExists(string queueName)
    {
        try
        {
            await _channel.QueueDeclarePassiveAsync(queueName);
            Console.WriteLine($"✓ Kuyruk mevcut: {queueName}");
            return true;
        }
        catch (Exception)
        {
            Console.WriteLine($"✗ Kuyruk mevcut değil: {queueName}");
            return false;
        }
    }

    /// <summary>
    ///     Kuyruk silme
    /// </summary>
    public async Task DeleteQueue(string queueName, bool ifUnused = false, bool ifEmpty = false)
    {
        var messageCount = await _channel.QueueDeleteAsync(queueName, ifUnused, ifEmpty);
        Console.WriteLine($"✓ Kuyruk silindi: {queueName} ({messageCount} mesaj vardı)");
    }

    /// <summary>
    ///     Kuyruktaki tüm mesajları temizleme
    /// </summary>
    public async Task PurgeQueue(string queueName)
    {
        var messageCount = await _channel.QueuePurgeAsync(queueName);
        Console.WriteLine($"✓ Kuyruk temizlendi: {queueName} ({messageCount} mesaj silindi)");
    }

    #endregion
}
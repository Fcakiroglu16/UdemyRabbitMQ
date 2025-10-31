#region

using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

#endregion

namespace Producer;

/// <summary>
///     RabbitMQ BasicPublishAsync metodunun tüm senaryolarını içeren sınıf.
///     Her metod, BasicPublishAsync'in farklı parametre kombinasyonlarını ve kullanım senaryolarını gösterir.
/// </summary>
public class RabbitMQPublishScenarios
{
    private readonly IChannel _channel;

    public RabbitMQPublishScenarios(IChannel channel)
    {
        _channel = channel;
    }

    #region 1. En Basit Kullanım - Sadece Zorunlu Parametreler

    /// <summary>
    ///     Senaryo 1: En basit kullanım - Sadece exchange, routingKey ve body
    ///     - Exchange: string.Empty (default/direct exchange kullanır)
    ///     - RoutingKey: Kuyruk adı
    ///     - Body: Mesaj içeriği
    ///     Kullanım: Basit mesaj gönderme, test senaryoları
    /// </summary>
    public async Task Scenario01_BasicPublish()
    {
        var message = "Hello World - En Basit Kullanım";
        var body = Encoding.UTF8.GetBytes(message);

        await _channel.BasicPublishAsync(
            string.Empty, // Default exchange
            "test-queue", // Kuyruk adı
            body // Mesaj
        );

        Console.WriteLine("✅ Senaryo 1: En basit mesaj gönderildi");
    }

    #endregion

    #region 8. Demo - Tüm Senaryoları Çalıştır

    /// <summary>
    ///     Tüm senaryoları sırayla çalıştırır
    /// </summary>
    public async Task RunAllScenarios()
    {
        Console.WriteLine("\n╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║     BasicPublishAsync - Tüm Senaryolar Çalıştırılıyor     ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝\n");

        // 1. Temel kullanım
        await Scenario01_BasicPublish();
        await Task.Delay(100);

        // 2. Mandatory
        await Scenario02_MandatoryTrue();
        await Scenario02b_MandatoryFalse();
        await Task.Delay(100);

        // 3. Properties
        await Scenario03a_PersistentMessage();
        await Scenario03b_PriorityMessage(8);
        await Scenario03c_ExpirationMessage(30000);
        await Scenario03d_MessageIdMessage();
        await Scenario03e_CorrelationIdMessage("corr-123");
        await Scenario03g_TimestampMessage();
        await Scenario03h_ContentTypeMessage();
        await Task.Delay(100);

        // 4. Headers
        await Scenario04a_CustomHeadersMessage();
        await Scenario04c_CustomMetadataHeaders();
        await Task.Delay(100);

        // 5. Exchange types
        await Scenario05a_DirectExchange();
        await Scenario05b_FanoutExchange();
        await Scenario05c_TopicExchange("order.created.urgent");
        await Scenario05d_DefaultExchange();
        await Task.Delay(100);

        // 6. Kombine senaryolar
        await Scenario06a_ProductionReadyMessage();
        await Scenario06c_DelayedMessage(5000);
        await Scenario06d_RetryMessage(1, 1000);
        await Scenario06e_BatchMessage(1, 1, 10);

        Console.WriteLine("\n╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║           ✅ Tüm Senaryolar Tamamlandı!                    ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
    }

    #endregion

    #region 2. Mandatory Parametresi ile Kullanım

    /// <summary>
    ///     Senaryo 2: Mandatory = true ile kullanım
    ///     - Mandatory: true ise, mesaj route edilemezse publisher'a geri döner
    ///     - Mandatory: false ise, mesaj sessizce kaybolur
    ///     Kullanım: Mesajın mutlaka bir kuyruğa ulaşmasını garanti etmek için
    ///     Not: ReturnListener ile birlikte kullanılmalıdır
    /// </summary>
    public async Task Scenario02_MandatoryTrue()
    {
        var message = "Mandatory True - Bu mesaj mutlaka bir kuyruğa ulaşmalı";
        var body = Encoding.UTF8.GetBytes(message);

        await _channel.BasicPublishAsync(
            string.Empty,
            "test-queue",
            true, // Mesaj route edilemezse hata
            body
        );

        Console.WriteLine("✅ Senaryo 2: Mandatory=true ile mesaj gönderildi");
    }

    /// <summary>
    ///     Senaryo 2b: Mandatory = false ile kullanım (varsayılan)
    ///     - Mesaj route edilemezse sessizce kaybolur
    /// </summary>
    public async Task Scenario02b_MandatoryFalse()
    {
        var message = "Mandatory False - Mesaj kaybolabilir";
        var body = Encoding.UTF8.GetBytes(message);

        await _channel.BasicPublishAsync(
            string.Empty,
            "non-existing-queue", // Var olmayan kuyruk
            false, // Sessizce kaybolur
            body
        );

        Console.WriteLine("✅ Senaryo 2b: Mandatory=false ile mesaj gönderildi (kaybolabilir)");
    }

    #endregion

    #region 3. BasicProperties ile Mesaj Özellikleri

    /// <summary>
    ///     Senaryo 3a: Persistent mesaj gönderme
    ///     - Persistent = true: Mesaj disk'e yazılır, RabbitMQ restart'ta korunur
    ///     - Persistent = false: Mesaj sadece memory'de, restart'ta kaybolur
    ///     Kullanım: Kritik mesajlar için persistent kullanılmalı
    /// </summary>
    public async Task Scenario03a_PersistentMessage()
    {
        var message = "Persistent Message - RabbitMQ restart'ta korunur";
        var body = Encoding.UTF8.GetBytes(message);

        var properties = new BasicProperties
        {
            Persistent = true // Mesajı kalıcı yap
        };

        await _channel.BasicPublishAsync(
            string.Empty,
            "durable-queue",
            false,
            properties,
            body
        );

        Console.WriteLine("✅ Senaryo 3a: Persistent mesaj gönderildi");
    }

    /// <summary>
    ///     Senaryo 3b: Priority (Öncelik) ile mesaj gönderme
    ///     - Priority: 0-255 arası değer (genellikle 0-10 kullanılır)
    ///     - Kuyruğun x-max-priority argument'i ile oluşturulmuş olması gerekir
    ///     Kullanım: Kritik mesajların öncelikli işlenmesi için
    /// </summary>
    public async Task Scenario03b_PriorityMessage(byte priority = 5)
    {
        var message = $"Priority {priority} - Öncelikli mesaj";
        var body = Encoding.UTF8.GetBytes(message);

        var properties = new BasicProperties
        {
            Priority = priority // 0-255 arası öncelik
        };

        await _channel.BasicPublishAsync(
            string.Empty,
            "priority-queue",
            false,
            properties,
            body
        );

        Console.WriteLine($"✅ Senaryo 3b: Priority={priority} mesaj gönderildi");
    }

    /// <summary>
    ///     Senaryo 3c: Expiration (TTL) ile mesaj gönderme
    ///     - Expiration: Milisaniye cinsinden süre (string olarak)
    ///     - Bu süreden sonra mesaj otomatik silinir
    ///     Kullanım: Geçici öneme sahip mesajlar, cache senaryoları
    /// </summary>
    public async Task Scenario03c_ExpirationMessage(int ttlMilliseconds = 60000)
    {
        var message = $"TTL {ttlMilliseconds}ms - Bu mesaj {ttlMilliseconds}ms sonra silinecek";
        var body = Encoding.UTF8.GetBytes(message);

        var properties = new BasicProperties
        {
            Expiration = ttlMilliseconds.ToString() // Milisaniye cinsinden
        };

        await _channel.BasicPublishAsync(
            string.Empty,
            "test-queue",
            false,
            properties,
            body
        );

        Console.WriteLine($"✅ Senaryo 3c: {ttlMilliseconds}ms TTL ile mesaj gönderildi");
    }

    /// <summary>
    ///     Senaryo 3d: MessageId ile mesaj gönderme
    ///     - MessageId: Mesajın benzersiz tanımlayıcısı
    ///     - Kullanım: Mesaj takibi, idempotency kontrolü
    /// </summary>
    public async Task Scenario03d_MessageIdMessage()
    {
        var messageId = Guid.NewGuid().ToString();
        var message = "MessageId ile mesaj";
        var body = Encoding.UTF8.GetBytes(message);

        var properties = new BasicProperties
        {
            MessageId = messageId
        };

        await _channel.BasicPublishAsync(
            string.Empty,
            "test-queue",
            false,
            properties,
            body
        );

        Console.WriteLine($"✅ Senaryo 3d: MessageId={messageId} ile mesaj gönderildi");
    }

    /// <summary>
    ///     Senaryo 3e: CorrelationId ile mesaj gönderme
    ///     - CorrelationId: İlgili mesajları bağlamak için kullanılır
    ///     - Kullanım: Request-Response pattern, mesaj zincirleme
    /// </summary>
    public async Task Scenario03e_CorrelationIdMessage(string correlationId)
    {
        var message = "CorrelationId ile mesaj";
        var body = Encoding.UTF8.GetBytes(message);

        var properties = new BasicProperties
        {
            CorrelationId = correlationId
        };

        await _channel.BasicPublishAsync(
            string.Empty,
            "test-queue",
            false,
            properties,
            body
        );

        Console.WriteLine($"✅ Senaryo 3e: CorrelationId={correlationId} ile mesaj gönderildi");
    }

    /// <summary>
    ///     Senaryo 3f: ReplyTo ile mesaj gönderme (RPC Pattern)
    ///     - ReplyTo: Yanıt mesajının gönderileceği kuyruk adı
    ///     - Kullanım: RPC (Remote Procedure Call) pattern
    /// </summary>
    public async Task Scenario03f_ReplyToMessage(string replyQueue)
    {
        var message = "RPC Request - Yanıt bekliyor";
        var body = Encoding.UTF8.GetBytes(message);

        var properties = new BasicProperties
        {
            ReplyTo = replyQueue,
            CorrelationId = Guid.NewGuid().ToString()
        };

        await _channel.BasicPublishAsync(
            string.Empty,
            "rpc-queue",
            false,
            properties,
            body
        );

        Console.WriteLine($"✅ Senaryo 3f: ReplyTo={replyQueue} ile RPC mesajı gönderildi");
    }

    /// <summary>
    ///     Senaryo 3g: Timestamp ile mesaj gönderme
    ///     - Timestamp: Mesajın oluşturulma zamanı (Unix timestamp)
    ///     - Kullanım: Mesaj sıralama, audit logging
    /// </summary>
    public async Task Scenario03g_TimestampMessage()
    {
        var message = "Timestamp ile mesaj";
        var body = Encoding.UTF8.GetBytes(message);

        var properties = new BasicProperties
        {
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        };

        await _channel.BasicPublishAsync(
            string.Empty,
            "test-queue",
            false,
            properties,
            body
        );

        Console.WriteLine("✅ Senaryo 3g: Timestamp ile mesaj gönderildi");
    }

    /// <summary>
    ///     Senaryo 3h: ContentType ve ContentEncoding ile mesaj gönderme
    ///     - ContentType: Mesajın MIME tipi (application/json, text/plain, vb.)
    ///     - ContentEncoding: Karakter encoding (UTF-8, ASCII, vb.)
    ///     - Kullanım: Mesaj formatını belirtmek için
    /// </summary>
    public async Task Scenario03h_ContentTypeMessage()
    {
        var messageObject = new { Name = "Test", Value = 123 };
        var jsonMessage = JsonSerializer.Serialize(messageObject);
        var body = Encoding.UTF8.GetBytes(jsonMessage);

        var properties = new BasicProperties
        {
            ContentType = "application/json",
            ContentEncoding = "UTF-8"
        };

        await _channel.BasicPublishAsync(
            string.Empty,
            "test-queue",
            false,
            properties,
            body
        );

        Console.WriteLine("✅ Senaryo 3h: ContentType=application/json ile mesaj gönderildi");
    }

    /// <summary>
    ///     Senaryo 3i: DeliveryMode ile mesaj gönderme
    ///     - DeliveryMode: 1 = Non-persistent, 2 = Persistent
    ///     - Not: Persistent property kullanmak daha modern yaklaşımdır
    /// </summary>
    public async Task Scenario03i_DeliveryModeMessage()
    {
        var message = "DeliveryMode=2 (Persistent)";
        var body = Encoding.UTF8.GetBytes(message);

        var properties = new BasicProperties
        {
            DeliveryMode = DeliveryModes.Persistent // 2 = Persistent
        };

        await _channel.BasicPublishAsync(
            string.Empty,
            "test-queue",
            false,
            properties,
            body
        );

        Console.WriteLine("✅ Senaryo 3i: DeliveryMode=2 ile mesaj gönderildi");
    }

    /// <summary>
    ///     Senaryo 3j: Type ile mesaj gönderme
    ///     - Type: Mesaj türünü belirtir (user-defined)
    ///     - Kullanım: Mesaj routing, consumer filtreleme
    /// </summary>
    public async Task Scenario03j_TypeMessage(string messageType)
    {
        var message = $"Type={messageType} mesaj";
        var body = Encoding.UTF8.GetBytes(message);

        var properties = new BasicProperties
        {
            Type = messageType // Örn: "order.created", "user.updated"
        };

        await _channel.BasicPublishAsync(
            string.Empty,
            "test-queue",
            false,
            properties,
            body
        );

        Console.WriteLine($"✅ Senaryo 3j: Type={messageType} ile mesaj gönderildi");
    }

    /// <summary>
    ///     Senaryo 3k: UserId ile mesaj gönderme
    ///     - UserId: Mesajı gönderen kullanıcı
    ///     - Not: RabbitMQ tarafından doğrulama yapılır
    ///     - Connection kullanıcı adı ile eşleşmelidir
    /// </summary>
    public async Task Scenario03k_UserIdMessage(string userId)
    {
        var message = "UserId ile mesaj";
        var body = Encoding.UTF8.GetBytes(message);

        var properties = new BasicProperties
        {
            UserId = userId // Connection username ile aynı olmalı
        };

        await _channel.BasicPublishAsync(
            string.Empty,
            "test-queue",
            false,
            properties,
            body
        );

        Console.WriteLine($"✅ Senaryo 3k: UserId={userId} ile mesaj gönderildi");
    }

    /// <summary>
    ///     Senaryo 3l: AppId ile mesaj gönderme
    ///     - AppId: Mesajı üreten uygulamanın kimliği
    ///     - Kullanım: Mesaj kaynağını belirtmek için
    /// </summary>
    public async Task Scenario03l_AppIdMessage(string appId)
    {
        var message = "AppId ile mesaj";
        var body = Encoding.UTF8.GetBytes(message);

        var properties = new BasicProperties
        {
            AppId = appId // Örn: "OrderService", "PaymentAPI"
        };

        await _channel.BasicPublishAsync(
            string.Empty,
            "test-queue",
            false,
            properties,
            body
        );

        Console.WriteLine($"✅ Senaryo 3l: AppId={appId} ile mesaj gönderildi");
    }

    #endregion

    #region 4. Headers ile Mesaj Gönderme

    /// <summary>
    ///     Senaryo 4a: Custom Headers ile mesaj gönderme
    ///     - Headers: Key-Value çiftleri
    ///     - Kullanım: Metadata, routing, filtreleme
    /// </summary>
    public async Task Scenario04a_CustomHeadersMessage()
    {
        var message = "Custom Headers ile mesaj";
        var body = Encoding.UTF8.GetBytes(message);

        var headers = new Dictionary<string, object>
        {
            { "x-custom-header", "custom-value" },
            { "priority-level", "high" },
            { "source", "api" },
            { "version", 1 }
        };

        var properties = new BasicProperties
        {
            Headers = headers
        };

        await _channel.BasicPublishAsync(
            string.Empty,
            "test-queue",
            false,
            properties,
            body
        );

        Console.WriteLine("✅ Senaryo 4a: Custom Headers ile mesaj gönderildi");
    }

    /// <summary>
    ///     Senaryo 4b: Headers Exchange için mesaj gönderme
    ///     - Headers exchange, header değerlerine göre routing yapar
    ///     - x-match: "any" veya "all" (herhangi biri veya hepsi eşleşmeli)
    /// </summary>
    public async Task Scenario04b_HeadersExchangeMessage()
    {
        var message = "Headers Exchange mesajı";
        var body = Encoding.UTF8.GetBytes(message);

        var headers = new Dictionary<string, object>
        {
            { "format", "pdf" },
            { "type", "report" },
            { "priority", "high" }
        };

        var properties = new BasicProperties
        {
            Headers = headers
        };

        await _channel.BasicPublishAsync(
            "headers-exchange",
            string.Empty, // Headers exchange için routing key kullanılmaz
            false,
            properties,
            body
        );

        Console.WriteLine("✅ Senaryo 4b: Headers Exchange'e mesaj gönderildi");
    }

    /// <summary>
    ///     Senaryo 4c: Death Letter Headers ile mesaj gönderme
    ///     - x-death: RabbitMQ tarafından eklenen header
    ///     - Dead letter senaryolarında otomatik eklenir
    /// </summary>
    public async Task Scenario04c_CustomMetadataHeaders()
    {
        var message = "Metadata headers ile mesaj";
        var body = Encoding.UTF8.GetBytes(message);

        var headers = new Dictionary<string, object>
        {
            { "retry-count", 0 },
            { "original-queue", "main-queue" },
            { "created-at", DateTimeOffset.UtcNow.ToString("O") },
            { "tenant-id", "tenant-123" }
        };

        var properties = new BasicProperties
        {
            Headers = headers,
            Persistent = true
        };

        await _channel.BasicPublishAsync(
            string.Empty,
            "test-queue",
            false,
            properties,
            body
        );

        Console.WriteLine("✅ Senaryo 4c: Metadata Headers ile mesaj gönderildi");
    }

    #endregion

    #region 5. Farklı Exchange Tiplerine Mesaj Gönderme

    /// <summary>
    ///     Senaryo 5a: Direct Exchange'e mesaj gönderme
    ///     - Routing key tam eşleşme ile routing yapar
    ///     - En yaygın kullanılan exchange tipi
    /// </summary>
    public async Task Scenario05a_DirectExchange()
    {
        var message = "Direct Exchange mesajı";
        var body = Encoding.UTF8.GetBytes(message);

        await _channel.BasicPublishAsync(
            "direct-exchange",
            "order.created", // Tam eşleşme gerekli
            body
        );

        Console.WriteLine("✅ Senaryo 5a: Direct Exchange'e mesaj gönderildi");
    }

    /// <summary>
    ///     Senaryo 5b: Fanout Exchange'e mesaj gönderme
    ///     - Routing key göz ardı edilir
    ///     - Tüm bağlı kuyruklara mesaj gönderilir
    ///     - Kullanım: Broadcasting, pub/sub pattern
    /// </summary>
    public async Task Scenario05b_FanoutExchange()
    {
        var message = "Fanout Exchange mesajı - Tüm kuyruklara gönderilecek";
        var body = Encoding.UTF8.GetBytes(message);

        await _channel.BasicPublishAsync(
            "fanout-exchange",
            string.Empty, // Fanout'ta routing key kullanılmaz
            body
        );

        Console.WriteLine("✅ Senaryo 5b: Fanout Exchange'e mesaj gönderildi");
    }

    /// <summary>
    ///     Senaryo 5c: Topic Exchange'e mesaj gönderme
    ///     - Routing key pattern matching ile çalışır
    ///     - Wildcard: * (bir kelime), # (sıfır veya daha fazla kelime)
    ///     - Kullanım: Kategorilere göre routing
    /// </summary>
    public async Task Scenario05c_TopicExchange(string routingKey)
    {
        var message = $"Topic Exchange mesajı - Routing: {routingKey}";
        var body = Encoding.UTF8.GetBytes(message);

        await _channel.BasicPublishAsync(
            "topic-exchange",
            routingKey, // Örn: "order.created.urgent", "user.*.updated"
            body
        );

        Console.WriteLine($"✅ Senaryo 5c: Topic Exchange'e mesaj gönderildi (key: {routingKey})");
    }

    /// <summary>
    ///     Senaryo 5d: Default Exchange'e mesaj gönderme
    ///     - Exchange: string.Empty veya ""
    ///     - Routing key = kuyruk adı
    ///     - RabbitMQ'nun built-in direct exchange'i
    /// </summary>
    public async Task Scenario05d_DefaultExchange()
    {
        var message = "Default Exchange mesajı";
        var body = Encoding.UTF8.GetBytes(message);

        await _channel.BasicPublishAsync(
            string.Empty, // veya ""
            "my-queue", // Doğrudan kuyruk adı
            body
        );

        Console.WriteLine("✅ Senaryo 5d: Default Exchange'e mesaj gönderildi");
    }

    #endregion

    #region 6. Kombine Senaryolar - Tüm Özellikler Birlikte

    /// <summary>
    ///     Senaryo 6a: Üretim ortamı için tam donanımlı mesaj
    ///     - Persistent, Priority, MessageId, Timestamp, Headers
    ///     - Kullanım: Enterprise uygulamalar
    /// </summary>
    public async Task Scenario06a_ProductionReadyMessage()
    {
        var message = "Production Ready Message - Tüm özellikler aktif";
        var body = Encoding.UTF8.GetBytes(message);

        var headers = new Dictionary<string, object>
        {
            { "source", "order-service" },
            { "version", "1.0" },
            { "environment", "production" },
            { "tenant-id", "tenant-abc" }
        };

        var properties = new BasicProperties
        {
            Persistent = true,
            Priority = 8,
            MessageId = Guid.NewGuid().ToString(),
            CorrelationId = Guid.NewGuid().ToString(),
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            ContentType = "application/json",
            ContentEncoding = "UTF-8",
            Type = "order.created",
            AppId = "OrderService",
            Headers = headers
        };

        await _channel.BasicPublishAsync(
            "production-exchange",
            "order.created",
            true,
            properties,
            body
        );

        Console.WriteLine("✅ Senaryo 6a: Production Ready mesaj gönderildi");
    }

    /// <summary>
    ///     Senaryo 6b: RPC Request mesajı
    ///     - ReplyTo, CorrelationId, Expiration
    ///     - Kullanım: Request-Response pattern
    /// </summary>
    public async Task Scenario06b_RpcRequestMessage(string replyQueue)
    {
        var correlationId = Guid.NewGuid().ToString();
        var request = new { Method = "GetUserInfo", UserId = 123 };
        var jsonRequest = JsonSerializer.Serialize(request);
        var body = Encoding.UTF8.GetBytes(jsonRequest);

        var properties = new BasicProperties
        {
            ReplyTo = replyQueue,
            CorrelationId = correlationId,
            Expiration = "30000", // 30 saniye timeout
            ContentType = "application/json",
            Persistent = false // RPC mesajları genellikle geçicidir
        };

        await _channel.BasicPublishAsync(
            string.Empty,
            "rpc-queue",
            true,
            properties,
            body
        );

        Console.WriteLine($"✅ Senaryo 6b: RPC Request gönderildi (CorrelationId: {correlationId})");
    }

    /// <summary>
    ///     Senaryo 6c: Delayed Message (TTL + Dead Letter kullanarak)
    ///     - Mesaj belirli süre sonra işlenmesi için
    ///     - Kullanım: Zamanlanmış işlemler, retry mekanizmaları
    /// </summary>
    public async Task Scenario06c_DelayedMessage(int delayMilliseconds)
    {
        var message = $"Delayed Message - {delayMilliseconds}ms sonra işlenecek";
        var body = Encoding.UTF8.GetBytes(message);

        var properties = new BasicProperties
        {
            Expiration = delayMilliseconds.ToString(),
            Persistent = true
        };

        await _channel.BasicPublishAsync(
            string.Empty,
            "delay-queue", // DLX ile yapılandırılmış kuyruk
            false,
            properties,
            body
        );

        Console.WriteLine($"✅ Senaryo 6c: {delayMilliseconds}ms delayed mesaj gönderildi");
    }

    /// <summary>
    ///     Senaryo 6d: Retry mekanizması için mesaj
    ///     - Headers'da retry count
    ///     - Exponential backoff için TTL
    /// </summary>
    public async Task Scenario06d_RetryMessage(int retryCount, int backoffMs)
    {
        var message = $"Retry Message - Attempt #{retryCount}";
        var body = Encoding.UTF8.GetBytes(message);

        var headers = new Dictionary<string, object>
        {
            { "x-retry-count", retryCount },
            { "x-original-queue", "main-queue" },
            { "x-first-attempt", DateTimeOffset.UtcNow.ToString("O") }
        };

        var properties = new BasicProperties
        {
            Headers = headers,
            Expiration = backoffMs.ToString(),
            Persistent = true,
            MessageId = Guid.NewGuid().ToString()
        };

        await _channel.BasicPublishAsync(
            string.Empty,
            "retry-queue",
            false,
            properties,
            body
        );

        Console.WriteLine($"✅ Senaryo 6d: Retry mesajı gönderildi (Attempt: {retryCount}, Backoff: {backoffMs}ms)");
    }

    /// <summary>
    ///     Senaryo 6e: Batch Processing mesajı
    ///     - Toplu işleme için metadata
    /// </summary>
    public async Task Scenario06e_BatchMessage(int batchId, int messageIndex, int totalMessages)
    {
        var message = $"Batch Message - {messageIndex}/{totalMessages}";
        var body = Encoding.UTF8.GetBytes(message);

        var headers = new Dictionary<string, object>
        {
            { "x-batch-id", batchId },
            { "x-batch-index", messageIndex },
            { "x-batch-total", totalMessages }
        };

        var properties = new BasicProperties
        {
            Headers = headers,
            Persistent = true,
            MessageId = $"batch-{batchId}-msg-{messageIndex}"
        };

        await _channel.BasicPublishAsync(
            string.Empty,
            "batch-queue",
            false,
            properties,
            body
        );

        Console.WriteLine($"✅ Senaryo 6e: Batch mesajı gönderildi ({messageIndex}/{totalMessages})");
    }

    #endregion

    #region 7. Özel Senaryolar

    /// <summary>
    ///     Senaryo 7a: Large Message gönderme
    ///     - Büyük boyutlu mesajlar için
    ///     - Compression veya chunking kullanılabilir
    /// </summary>
    public async Task Scenario07a_LargeMessage()
    {
        var largeContent = new string('X', 1024 * 100); // 100KB
        var body = Encoding.UTF8.GetBytes(largeContent);

        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "text/plain",
            ContentEncoding = "UTF-8"
        };

        await _channel.BasicPublishAsync(
            string.Empty,
            "large-messages-queue",
            false,
            properties,
            body
        );

        Console.WriteLine($"✅ Senaryo 7a: Large message gönderildi ({body.Length} bytes)");
    }

    /// <summary>
    ///     Senaryo 7b: Binary mesaj gönderme
    ///     - Resim, PDF vb. binary dosyalar için
    /// </summary>
    public async Task Scenario07b_BinaryMessage(byte[] binaryData)
    {
        var properties = new BasicProperties
        {
            ContentType = "application/octet-stream",
            Persistent = true
        };

        await _channel.BasicPublishAsync(
            string.Empty,
            "binary-queue",
            false,
            properties,
            binaryData
        );

        Console.WriteLine($"✅ Senaryo 7b: Binary mesaj gönderildi ({binaryData.Length} bytes)");
    }

    /// <summary>
    ///     Senaryo 7c: Multi-tenant mesaj
    ///     - Tenant bilgisi ile routing
    /// </summary>
    public async Task Scenario07c_MultiTenantMessage(string tenantId, string messageContent)
    {
        var body = Encoding.UTF8.GetBytes(messageContent);

        var headers = new Dictionary<string, object>
        {
            { "tenant-id", tenantId }
        };

        var properties = new BasicProperties
        {
            Headers = headers,
            Type = $"tenant.{tenantId}.message"
        };

        await _channel.BasicPublishAsync(
            "multi-tenant-exchange",
            $"tenant.{tenantId}",
            false,
            properties,
            body
        );

        Console.WriteLine($"✅ Senaryo 7c: Multi-tenant mesaj gönderildi (Tenant: {tenantId})");
    }

    /// <summary>
    ///     Senaryo 7d: Event Sourcing mesajı
    ///     - Domain events için
    /// </summary>
    public async Task Scenario07d_EventSourcingMessage(string eventType, object eventData)
    {
        var jsonEvent = JsonSerializer.Serialize(eventData);
        var body = Encoding.UTF8.GetBytes(jsonEvent);

        var headers = new Dictionary<string, object>
        {
            { "event-type", eventType },
            { "event-version", 1 },
            { "aggregate-id", Guid.NewGuid().ToString() }
        };

        var properties = new BasicProperties
        {
            Headers = headers,
            Type = eventType,
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            ContentType = "application/json",
            Persistent = true,
            MessageId = Guid.NewGuid().ToString()
        };

        await _channel.BasicPublishAsync(
            "event-store",
            eventType,
            false,
            properties,
            body
        );

        Console.WriteLine($"✅ Senaryo 7d: Event Sourcing mesajı gönderildi (Type: {eventType})");
    }

    #endregion
}
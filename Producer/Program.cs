#region

using Producer;
using RabbitMQ.Client;

#endregion

Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
Console.WriteLine("║       RabbitMQ Queue Scenarios - Tüm Senaryolar Demo       ║");
Console.WriteLine("╚════════════════════════════════════════════════════════════╝\n");

// RabbitMQ bağlantı ayarları
//var factory = new ConnectionFactory
//{
//    HostName = "localhost", // RabbitMQ sunucu adresi
//    Port = 5672,
//    UserName = "guest",
//    Password = "guest",
//    VirtualHost = "/"

//    // CloudAMQP gibi cloud servis kullanıyorsanız:
//    // Uri = new Uri("amqps://username:password@host/vhost")
//};

var factory = new ConnectionFactory
{
    Uri = new Uri("amqps://ffqycmjd:cYOzydQzAx6gSvtbPdF4zU5T-2r4c-UR@gorilla.lmq.cloudamqp.com/ffqycmjd")
};


try
{
    await using var connection = await factory.CreateConnectionAsync();
    await using var channel = await connection.CreateChannelAsync();

    //var scenarios = new RabbitMQQueueScenarios(channel);

    //Console.WriteLine("📋 TEMEL KUYRUK SENARYOLARI");
    //Console.WriteLine("═══════════════════════════════════════════════════════════\n");

    //await scenarios.CreateBasicTemporaryQueue();
    //Console.WriteLine();

    //await scenarios.CreateDurableQueue();
    //Console.WriteLine();

    //await scenarios.CreateExclusiveQueue();
    //Console.WriteLine();

    //await scenarios.CreateAutoDeleteQueue();
    //Console.WriteLine();

    //var serverNamedQueue = await scenarios.CreateServerNamedQueue();
    //Console.WriteLine();

    //Console.WriteLine("\n⏰ TTL (TIME TO LIVE) SENARYOLARI");
    //Console.WriteLine("═══════════════════════════════════════════════════════════\n");

    //await scenarios.CreateQueueWithMessageTTL();
    //Console.WriteLine();

    //await scenarios.CreateQueueWithQueueTTL();
    //Console.WriteLine();

    //Console.WriteLine("\n💀 DEAD LETTER EXCHANGE SENARYOLARI");
    //Console.WriteLine("═══════════════════════════════════════════════════════════\n");

    //await scenarios.CreateQueueWithDeadLetterExchange();
    //Console.WriteLine();

    //await scenarios.CreateDeadLetterQueueSystem();
    //Console.WriteLine();

    //Console.WriteLine("\n📊 MAX LENGTH SENARYOLARI");
    //Console.WriteLine("═══════════════════════════════════════════════════════════\n");

    //await scenarios.CreateQueueWithMaxLength();
    //Console.WriteLine();

    //await scenarios.CreateQueueWithMaxLengthBytes();
    //Console.WriteLine();

    //await scenarios.CreateQueueWithOverflowBehavior();
    //Console.WriteLine();

    //Console.WriteLine("\n⭐ PRIORITY QUEUE SENARYOSU");
    //Console.WriteLine("═══════════════════════════════════════════════════════════\n");

    //await scenarios.CreatePriorityQueue();
    //Console.WriteLine();

    //Console.WriteLine("\n🐌 LAZY QUEUE SENARYOSU");
    //Console.WriteLine("═══════════════════════════════════════════════════════════\n");

    //await scenarios.CreateLazyQueue();
    //Console.WriteLine();

    //Console.WriteLine("\n👤 SINGLE ACTIVE CONSUMER SENARYOSU");
    //Console.WriteLine("═══════════════════════════════════════════════════════════\n");

    //await scenarios.CreateSingleActiveConsumerQueue();
    //Console.WriteLine();

    //Console.WriteLine("\n🔒 QUORUM QUEUE SENARYOLARI");
    //Console.WriteLine("═══════════════════════════════════════════════════════════\n");

    //await scenarios.CreateQuorumQueue();
    //Console.WriteLine();

    //await scenarios.CreateQuorumQueueWithDeliveryLimit();
    //Console.WriteLine();

    //Console.WriteLine("\n🌊 STREAM QUEUE SENARYOLARI");
    //Console.WriteLine("═══════════════════════════════════════════════════════════\n");

    //await scenarios.CreateStreamQueue();
    //Console.WriteLine();

    //await scenarios.CreateStreamQueueWithRetention();
    //Console.WriteLine();

    //Console.WriteLine("\n🏭 KOMBİNE VE ÖZEL SENARYOLAR");
    //Console.WriteLine("═══════════════════════════════════════════════════════════\n");

    //await scenarios.CreateProductionReadyQueue();
    //Console.WriteLine();

    //await scenarios.CreateRetryQueueSystem();
    //Console.WriteLine();

    //await scenarios.CreateClassicQueue();
    //Console.WriteLine();

    //await scenarios.CreateAllQueueTypesComparison();
    //Console.WriteLine();

    //Console.WriteLine("\n🔧 UTILITY METHODLAR");
    //Console.WriteLine("═══════════════════════════════════════════════════════════\n");

    //// Kuyruk var mı kontrol et
    //await scenarios.CheckQueueExists("durable-queue");
    //await scenarios.CheckQueueExists("non-existing-queue");
    //Console.WriteLine();

    //// Kuyruk temizleme örneği
    ////await scenarios.PurgeQueue("basic-temp-queue");
    ////Console.WriteLine();

    ////Console.WriteLine("\n╔════════════════════════════════════════════════════════════╗");
    ////Console.WriteLine("║                    İŞLEM TAMAMLANDI                        ║");
    ////Console.WriteLine("╚════════════════════════════════════════════════════════════╝");

    ////Console.WriteLine("\n📊 ÖZET:");
    ////Console.WriteLine("─────────────────────────────────────────────────────────────");
    ////Console.WriteLine("✓ 23 farklı kuyruk oluşturma senaryosu çalıştırıldı");
    ////Console.WriteLine("✓ Temel kuyruklar (5 senaryo)");
    ////Console.WriteLine("✓ TTL senaryoları (2 senaryo)");
    ////Console.WriteLine("✓ Dead Letter senaryoları (2 senaryo)");
    ////Console.WriteLine("✓ Max Length senaryoları (3 senaryo)");
    ////Console.WriteLine("✓ Özel özellikler (7 senaryo)");
    ////Console.WriteLine("✓ Kombine senaryolar (4 senaryo)");

    ////Console.WriteLine("\n🌐 RabbitMQ Management UI:");
    ////Console.WriteLine("   http://localhost:15672");
    ////Console.WriteLine("   Kullanıcı: guest");
    ////Console.WriteLine("   Şifre: guest");

    ////Console.WriteLine("\n💡 İPUÇLARI:");
    ////Console.WriteLine("─────────────────────────────────────────────────────────────");
    ////Console.WriteLine("• Management UI'dan tüm oluşturulan kuyrukları görebilirsiniz");
    ////Console.WriteLine("• Her kuyruğun 'Arguments' sekmesinden özelliklerini inceleyebilirsiniz");
    ////Console.WriteLine("• Quorum ve Stream queue'lar RabbitMQ 3.8+ gerektirir");
    ////Console.WriteLine("• Exclusive queue'lar bağlantı kapandığında otomatik silinir");

    ////Console.WriteLine("\n⚠️  DİKKAT:");
    ////Console.WriteLine("─────────────────────────────────────────────────────────────");
    ////Console.WriteLine("• Bazı kuyruklar (exclusive) bağlantı kapandığında silinecektir");
    ////Console.WriteLine("• Test amacıyla oluşturulan kuyrukları Management UI'dan");
    ////Console.WriteLine("  manuel olarak silebilirsiniz");

    //// ═══════════════════════════════════════════════════════════════════════
    //// MESAJ GÖNDERME İŞLEMLERİ
    //// ═══════════════════════════════════════════════════════════════════════

    var publisher = new RabbitMQMessagePublisher(channel);

    Console.WriteLine("\n\n📨 MESAJ GÖNDERME İŞLEMLERİ");
    Console.WriteLine("═══════════════════════════════════════════════════════════\n");

    // Tüm kuyruklara mesaj gönder
    await publisher.SendToAllQueues();

    // Bonus: Priority queue'ya farklı önceliklerle mesajlar gönder
    Console.WriteLine("\n\n⭐ ÖNCELIK QUEUE DEMO");
    Console.WriteLine("═══════════════════════════════════════════════════════════\n");
    await publisher.SendMultiplePriorityMessages();

    // Bonus: Bir kuyruğa çoklu mesaj gönderme örneği
    Console.WriteLine("\n\n📦 ÇOKLU MESAJ GÖNDERME DEMO");
    Console.WriteLine("═══════════════════════════════════════════════════════════\n");
    await publisher.SendMultipleMessages("durable-queue", 50);

    // Bonus: Custom headers ile mesaj gönderme
    Console.WriteLine("\n\n📋 CUSTOM HEADERS DEMO");
    Console.WriteLine("═══════════════════════════════════════════════════════════\n");
    var headers = new Dictionary<string, object>
    {
        { "app-id", "producer-demo" },
        { "user-id", "admin" },
        { "timestamp", DateTimeOffset.UtcNow.ToUnixTimeSeconds() }
    };
    await publisher.SendMessageWithHeaders("durable-queue", "Hello with Custom Headers!", headers);

    // Bonus: JSON mesaj gönderme
    Console.WriteLine("\n\n📄 JSON MESAJ DEMO");
    Console.WriteLine("═══════════════════════════════════════════════════════════\n");
    var messageObject = new
    {
        Id = Guid.NewGuid(),
        Title = "Test Message",
        Content = "Hello World from JSON!",
        Timestamp = DateTime.UtcNow,
        Tags = new[] { "test", "demo", "rabbitmq" }
    };
    await publisher.SendJsonMessage("durable-queue", messageObject);

    Console.WriteLine("\n\n╔════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║                    İŞLEM TAMAMLANDI                        ║");
    Console.WriteLine("╚════════════════════════════════════════════════════════════╝");

    Console.WriteLine("\n📊 ÖZET:");
    Console.WriteLine("─────────────────────────────────────────────────────────────");
    Console.WriteLine("✓ 23 farklı kuyruk oluşturma senaryosu çalıştırıldı");
    Console.WriteLine("✓ Tüm kuyruklara test mesajları gönderildi");
    Console.WriteLine("✓ Temel kuyruklar (5 senaryo)");
    Console.WriteLine("✓ TTL senaryoları (2 senaryo)");
    Console.WriteLine("✓ Dead Letter senaryoları (2 senaryo)");
    Console.WriteLine("✓ Max Length senaryoları (3 senaryo)");
    Console.WriteLine("✓ Özel özellikler (7 senaryo)");
    Console.WriteLine("✓ Kombine senaryolar (4 senaryo)");

    Console.WriteLine("\n🌐 RabbitMQ Management UI:");
    Console.WriteLine("   https://customer.cloudamqp.com/instance");

    Console.WriteLine("\n💡 İPUÇLARI:");
    Console.WriteLine("─────────────────────────────────────────────────────────────");
    Console.WriteLine("• Management UI'dan tüm oluşturulan kuyrukları görebilirsiniz");
    Console.WriteLine("• Her kuyruktaki mesajları 'Get messages' ile görüntüleyebilirsiniz");
    Console.WriteLine("• Quorum ve Stream queue'lar RabbitMQ 3.8+ gerektirir");
    Console.WriteLine("• Exclusive queue'lar bağlantı kapandığında otomatik silinir");

    Console.WriteLine("\n⚠️  DİKKAT:");
    Console.WriteLine("─────────────────────────────────────────────────────────────");
    Console.WriteLine("• Bazı kuyruklar (exclusive) bağlantı kapandığında silinecektir");
    Console.WriteLine("• Test amacıyla oluşturulan kuyrukları Management UI'dan");
    Console.WriteLine("  manuel olarak silebilirsiniz");
}
catch (Exception ex)
{
    Console.WriteLine("\n❌ HATA OLUŞTU!");
    Console.WriteLine("═══════════════════════════════════════════════════════════");
    Console.WriteLine($"Hata Mesajı: {ex.Message}");
    Console.WriteLine($"Hata Tipi: {ex.GetType().Name}");

    if (ex.InnerException != null) Console.WriteLine($"İç Hata: {ex.InnerException.Message}");

    Console.WriteLine("\n📋 KONTROL LİSTESİ:");
    Console.WriteLine("─────────────────────────────────────────────────────────────");
    Console.WriteLine("□ RabbitMQ sunucusu çalışıyor mu? (http://localhost:15672)");
    Console.WriteLine("□ Bağlantı bilgileri doğru mu? (HostName, Port, User, Password)");
    Console.WriteLine("□ RabbitMQ versiyonu uygun mu? (Quorum/Stream için 3.8+)");
    Console.WriteLine("□ Firewall veya port engellemesi var mı?");
}

Console.WriteLine("\n─────────────────────────────────────────────────────────────");
Console.WriteLine("Çıkmak için bir tuşa basın...");
Console.ReadKey();
# Dead Letter Exchange (DLX) - RabbitMQ

## ?çindekiler
1. [Dead Letter Exchange Nedir?](#dead-letter-exchange-nedir)
2. [Nas?l Çal???r?](#nas?l-çal???r)
3. [Kullan?m Senaryolar?](#kullan?m-senaryolar?)
4. [Kod Yap?s?](#kod-yap?s?)
5. [Çal??t?rma](#çal??t?rma)
6. [Test Senaryolar?](#test-senaryolar?)
7. [Management UI ile ?zleme](#management-ui-ile-izleme)
8. [Best Practices](#best-practices)

---

## Dead Letter Exchange Nedir?

**Dead Letter Exchange (DLX)**, RabbitMQ'da i?lenemeyen, reddedilen veya süresi dolan mesajlar?n yönlendirildi?i özel bir exchange'dir. 

### Mesajlar Ne Zaman DLX'e Gönderilir?

Bir mesaj a?a??daki durumlarda **Dead Letter Queue (DLQ)**'ya dü?er:

1. **Consumer taraf?ndan REJECT edilirse** (`basic.reject` veya `basic.nack` ile `requeue=false`)
2. **Mesaj?n TTL (Time To Live) süresi dolarsa**
3. **Queue'nun maksimum uzunlu?u a??l?rsa** (overflow durumunda)
4. **Consumer taraf?ndan tekrar tekrar redeliver edilirse** (Quorum queue'larda `x-delivery-limit`)

---

## Nas?l Çal???r?

### Temel Ak??

```
[Producer] --> [Main Queue (x-dead-letter-exchange)] --> [Consumer]
                                     | (NACK/TTL)
                                     v
                          [Dead Letter Exchange (DLX)]
                                     |
                          [Dead Letter Queue (DLQ)]
                                     |
                          [DLQ Consumer (Monitoring/Retry)]
```

### Yap?land?rma Ad?mlar?

#### 1. Dead Letter Exchange Olu?tur

```csharp
await channel.ExchangeDeclareAsync(
    exchange: "dead-letter-exchange",
    type: ExchangeType.Direct,
    durable: true
);
```

#### 2. Dead Letter Queue Olu?tur

```csharp
await channel.QueueDeclareAsync(
    queue: "dead-letter-queue",
    durable: true,
    exclusive: false,
    autoDelete: false
);
```

#### 3. DLQ'yu DLX'e Ba?la

```csharp
await channel.QueueBindAsync(
    queue: "dead-letter-queue",
    exchange: "dead-letter-exchange",
    routingKey: "failed"
);
```

#### 4. Ana Queue'yu DLX ile Yap?land?r

```csharp
var arguments = new Dictionary<string, object>
{
    { "x-dead-letter-exchange", "dead-letter-exchange" },
    { "x-dead-letter-routing-key", "failed" },
    { "x-message-ttl", 30000 } // Opsiyonel: 30 saniye TTL
};

await channel.QueueDeclareAsync(
    queue: "main-queue",
    durable: true,
    exclusive: false,
    autoDelete: false,
    arguments: arguments
);
```

---

## Kullan?m Senaryolar?

### 1. Hata Yönetimi
??lenemez mesajlar? yakalay?p analiz etmek için:
- Parse hatas? veren mesajlar
- ?? mant??? hatalar?
- Geçersiz veri formatlar?

### 2. Mesaj Yeniden Deneme (Retry)
Ba?ar?s?z mesajlar? belli bir süre sonra tekrar denemek için:
- Geçici a? hatalar?
- D?? servis eri?im sorunlar?
- Rate limit a??m?

### 3. TTL Tabanl? ??lemler
Belirli sürede i?lenmeyen mesajlar? yakalamak için:
- Zaman a??m? kontrolleri
- SLA ihlali tespiti
- Öncelik s?ras?n?n yönetimi

### 4. Monitoring ve Alerting
Sistem sa?l???n? izlemek için:
- Ba?ar?s?z mesaj oranlar?n? izleme
- Hata trendlerini analiz etme
- Alarm sistemleri

---

## Kod Yap?s?

### Dosya Organizasyonu

```
DeadLetterExample/
??? DeadLetterProducer.cs    # Producer s?n?f?
??? DeadLetterConsumer.cs    # Consumer s?n?f?
??? ProducerProgram.cs       # Producer ana program?
??? ConsumerProgram.cs       # Consumer ana program?
??? README.md                # Bu dokümantasyon
```

### Producer Özellikleri

**`DeadLetterProducer.cs`**
- DLX altyap?s?n? otomatik kurar
- Main Exchange ve Queue olu?turur
- DLX ve DLQ olu?turur
- Gerekli binding'leri yapar
- TTL'li mesajlar gönderebilir
- Persistent mesaj deste?i

**Temel Metodlar:**
```csharp
await producer.SetupInfrastructureAsync();           // Altyap?y? kur
await producer.PublishMessageAsync("mesaj");         // Tek mesaj gönder
await producer.PublishMultipleMessagesAsync(10);     // Çoklu mesaj
await producer.PublishExpirableMessagesAsync(5);     // TTL'li mesajlar
```

### Consumer Özellikleri

**`DeadLetterConsumer.cs`**
- Normal i?leme modu (ACK)
- Hata modu (NACK - DLQ'ya gönder)
- Karma mod (rastgele ba?ar?/hata)
- DLQ'dan mesaj tüketme
- Mesaj analizi ve header inceleme

**Temel Metodlar:**
```csharp
await consumer.ConsumeMainQueueAsync();              // Normal tüketim
await consumer.ConsumeAndRejectMessagesAsync();      // Tümünü reject et
await consumer.ConsumeWithMixedResultsAsync(50);     // %50 ba?ar? oran?
await consumer.ConsumeDeadLetterQueueAsync();        // DLQ'dan tüket
```

---

## Çal??t?rma

### Ön Gereksinimler

1. **RabbitMQ Kurulumu**
   ```bash
   # Docker ile (önerilen)
   docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:3-management
   
   # Management UI: http://localhost:15672
   # Kullan?c?: guest
   # ?ifre: guest
   ```

2. **.NET 9 SDK**
   ```bash
   dotnet --version
   ```

### Ad?m Ad?m Kullan?m

#### 1. Producer'? Çal??t?r

```bash
cd DeadLetterExample
dotnet run --project Producer
```

Producer menüsünden seçim yap?n:
- **1**: Tek mesaj gönder
- **2**: 10 test mesaj? gönder
- **3**: TTL'li mesajlar gönder (DLQ'ya dü?ecek)
- **4**: Özel miktar mesaj gönder

#### 2. Consumer'? Çal??t?r (Ayr? Terminal)

```bash
cd DeadLetterExample
dotnet run --project Consumer
```

Consumer modunu seçin:
- **1**: Normal mod (tüm mesajlar ba?ar?yla i?lenir)
- **2**: Hata modu (tüm mesajlar DLQ'ya gönderilir)
- **3**: Karma mod (%50 ba?ar? oran?)
- **4**: Özel ba?ar? oran?
- **5**: DLQ'dan mesaj tüket

---

## Test Senaryolar?

### Senaryo 1: TTL ile DLQ'ya Dü?me

**Amaç:** Tüketilmeyen mesajlar?n TTL sonras? DLQ'ya dü?mesini test etmek

1. Producer'? çal??t?r
2. "3 - Süreli mesajlar gönder" seçene?ini seç
3. Consumer'? **ÇALI?TIRMA** (mesajlar tüketilmeyecek)
4. 30 saniye bekle
5. Management UI'da `dead-letter-queue`'yu kontrol et
6. Mesajlar?n otomatik olarak DLQ'ya dü?tü?ünü gör

**Beklenen Sonuç:** ? 5 mesaj DLQ'da görünmeli

---

### Senaryo 2: Reject ile DLQ'ya Dü?me

**Amaç:** Consumer taraf?ndan reject edilen mesajlar?n DLQ'ya dü?mesini test etmek

1. Producer'? çal??t?r
2. "2 - 10 test mesaj? gönder" seçene?ini seç
3. Consumer'? çal??t?r
4. "2 - Hata Modu" seçene?ini seç
5. Consumer'?n tüm mesajlar? reject etti?ini gözlemle
6. DLQ Consumer'? çal??t?r (5. seçenek)
7. Reject edilen mesajlar? DLQ'dan tüket

**Beklenen Sonuç:** 
- ? 10 mesaj main queue'dan reject edilmeli
- ? 10 mesaj DLQ'ya dü?meli
- ? DLQ Consumer ile mesajlar okunabilmeli

---

### Senaryo 3: Karma Mod Testi

**Amaç:** Baz? mesajlar?n ba?ar?l? i?lendi?i, baz?lar?n?n DLQ'ya dü?tü?ü senaryoyu test etmek

1. Producer'? çal??t?r
2. "4 - Özel mesaj miktar? gönder" - 20 mesaj
3. Consumer'? çal??t?r
4. "4 - Özel Karma Mod" seçene?ini seç
5. Ba?ar? oran?: 30 gir
6. ?statistikleri gözlemle
7. DLQ'da yakla??k 14 mesaj olmal? (%70 reject)

**Beklenen Sonuç:** 
- ? ~6 mesaj ba?ar?yla i?lenmeli (ACK)
- ? ~14 mesaj DLQ'ya dü?meli (NACK)

---

### Senaryo 4: DLQ Monitoring

**Amaç:** DLQ'daki mesajlar? analiz etmek

1. Yukar?daki senaryolarla DLQ'ya mesaj gönder
2. Consumer'? çal??t?r
3. "5 - Dead Letter Queue'dan mesaj tüket" seçene?ini seç
4. DLQ mesajlar?n?n özelliklerini incele:
   - Message ID
   - Original Timestamp
   - DLX Headers (x-death, x-first-death-exchange, vb.)

**Beklenen Sonuç:** 
- ? Mesaj içerikleri görüntülenmeli
- ? DLX meta bilgileri görünmeli
- ? Mesajlar DLQ'dan temizlenmeli (ACK)

---

## Management UI ile ?zleme

### RabbitMQ Management UI'a Eri?im

**URL:** http://localhost:15672  
**Kullan?c?:** guest  
**?ifre:** guest

### Yap?lar

#### Exchanges

1. **main-exchange** (Direct)
   - Type: Direct
   - Durable: Yes
   - Binding: ? main-queue

2. **dead-letter-exchange** (Direct)
   - Type: Direct
   - Durable: Yes
   - Binding: ? dead-letter-queue

#### Queues

1. **main-queue**
   - Features:
     - `x-dead-letter-exchange: dead-letter-exchange`
     - `x-dead-letter-routing-key: failed`
     - `x-message-ttl: 30000` (30 saniye)
   - Consumers: 0 veya 1 (çal??an consumer varsa)

2. **dead-letter-queue**
   - Features: Standart queue
   - Messages: Reject edilen veya TTL a?an mesajlar

### Mesaj ?zleme

#### Main Queue Özellikleri
```
Queues ? main-queue ? Arguments:
  x-dead-letter-exchange: dead-letter-exchange
  x-dead-letter-routing-key: failed
  x-message-ttl: 30000
```

#### DLQ'da Mesaj ?nceleme
```
Queues ? dead-letter-queue ? Get messages
```

Her mesajda ?u bilgiler görüntülenir:
- **Payload:** Mesaj içeri?i
- **Properties:** Message ID, Timestamp, Content-Type
- **Headers:** DLX meta bilgileri
  - `x-death`: Array of death history
  - `x-first-death-exchange`: ?lk reject edildi?i exchange
  - `x-first-death-queue`: ?lk reject edildi?i queue
  - `x-first-death-reason`: Reject nedeni (rejected, expired, maxlen)

---

## Best Practices

### 1. DLQ Yap?land?rmas?

- **DLX ve DLQ'yu her zaman durable yap?n**
```csharp
await channel.ExchangeDeclareAsync("dlx", ExchangeType.Direct, durable: true);
await channel.QueueDeclareAsync("dlq", durable: true, exclusive: false, autoDelete: false);
```

- **Routing key kullan?n** (farkl? hata türlerini ay?rmak için)
```csharp
{ "x-dead-letter-routing-key", "error.validation" }  // Validation hatalar?
{ "x-dead-letter-routing-key", "error.timeout" }     // Timeout hatalar?
```

- **DLQ'ya da DLX eklemekten kaç?n?n** (sonsuz döngü riski)

---

### 2. TTL Ayarlar?

- **?? sürecinize göre TTL belirleyin**
```csharp
// H?zl? i?lemler için k?sa TTL
{ "x-message-ttl", 10000 }  // 10 saniye

// Uzun süren i?lemler için uzun TTL
{ "x-message-ttl", 3600000 }  // 1 saat
```

- **Per-message TTL kullanabilirsiniz**
```csharp
properties.Expiration = "5000";  // 5 saniye
```

---

### 3. Consumer Davran???

- **Retry mekanizmas? ekleyin**
```csharp
const int MaxRetries = 3;
var retryCount = GetRetryCount(eventArgs.BasicProperties.Headers);

if (retryCount < MaxRetries)
{
    // Retry queue'ya gönder
    await channel.BasicNackAsync(eventArgs.DeliveryTag, false, false);
}
else
{
    // Final DLQ'ya gönder
    await channel.BasicRejectAsync(eventArgs.DeliveryTag, false);
}
```

- **?dempotent consumer yaz?n** (ayn? mesaj? birden fazla i?leyebilecek)

- **Structured logging kullan?n**
```csharp
_logger.LogWarning("Message rejected. MessageId: {MessageId}, Reason: {Reason}",
    messageId, reason);
```

---

### 4. Monitoring ve Alerting

- **DLQ boyutunu izleyin**
```bash
# RabbitMQ CLI
rabbitmqctl list_queues name messages
```

- **Alarm kurun** (DLQ threshold a??ld???nda)
```csharp
if (dlqMessageCount > 100)
{
    await NotifyAdministrators("DLQ threshold exceeded!");
}
```

- **DLQ'yu periyodik olarak temizleyin veya ar?ivleyin**

---

### 5. Üretim Ortam? için

- **Quorum queue kullan?n** (yüksek güvenilirlik)
```csharp
var arguments = new Dictionary<string, object>
{
    { "x-queue-type", "quorum" },
    { "x-dead-letter-exchange", "dlx" },
    { "x-delivery-limit", 5 }  // 5 denemeden sonra DLQ
};
```

- **Publisher confirms aktif edin**
```csharp
await channel.ConfirmSelectAsync();
await channel.BasicPublishAsync(...);
await channel.WaitForConfirmsOrDieAsync();
```

- **Connection recovery ayarlay?n**
```csharp
factory.AutomaticRecoveryEnabled = true;
factory.NetworkRecoveryInterval = TimeSpan.FromSeconds(10);
```

---

## Troubleshooting

### Problem 1: Mesajlar DLQ'ya Dü?müyor

**Olas? Nedenler:**
- DLX yap?land?rmas? yanl??
- Binding eksik veya hatal? routing key
- Consumer `requeue=true` ile reject ediyor

**Çözüm:**
```csharp
// Binding'i kontrol et
await channel.QueueBindAsync("dlq", "dlx", "failed");

// Reject yaparken requeue=false olmal?
await channel.BasicNackAsync(deliveryTag, false, requeue: false);
```

---

### Problem 2: TTL Çal??m?yor

**Olas? Nedenler:**
- TTL milisaniye cinsinden de?il
- Consumer h?zl? tüketiyor (mesaj TTL'e ula?am?yor)

**Çözüm:**
```csharp
// Do?ru format
{ "x-message-ttl", 30000 }  // 30 saniye (milisaniye)

// Test için consumer'? durdurun
```

---

### Problem 3: DLX Sonsuz Döngü

**Olas? Neden:**
- DLQ'nun kendisine DLX tan?mlanm??

**Çözüm:**
```csharp
// DLQ olu?tururken DLX argüman? eklemeyin
await channel.QueueDeclareAsync("dlq", true, false, false, arguments: null);
```

---

## Ek Kaynaklar

- [RabbitMQ Dead Letter Exchanges Dokümantasyonu](https://www.rabbitmq.com/dlx.html)
- [RabbitMQ TTL (Time-To-Live) Dokümantasyonu](https://www.rabbitmq.com/ttl.html)
- [RabbitMQ Quorum Queues](https://www.rabbitmq.com/quorum-queues.html)
- [RabbitMQ .NET Client Guide](https://www.rabbitmq.com/dotnet-api-guide.html)

---

## Özet

Dead Letter Exchange (DLX), RabbitMQ'da hata yönetimi ve mesaj yeniden deneme mekanizmalar? için kritik bir özelliktir. 

**Temel Faydalar:**
- Hatal? mesajlar? izole eder
- Sistem güvenilirli?ini art?r?r
- Debugging ve analiz kolayl??? sa?lar
- Retry mekanizmalar? kurulabilir
- SLA ve monitoring metrikleri sa?lar

**Kullan?m Alanlar?:**
- Mesaj yeniden deneme (retry)
- Hata yönetimi ve monitoring
- TTL tabanl? i? ak??lar?
- Mesaj analizi ve raporlama

---

**Udemy RabbitMQ E?itimi - Dead Letter Exchange Örne?i**

© 2024 - Tüm haklar? sakl?d?r.

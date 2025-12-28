# Dead Letter Exchange Örne?i - H?zl? Ba?lang?ç

## Proje Yap?s?

```
DeadLetterExample/
??? README.md                                    # Detayl? dokümantasyon
??? QUICKSTART.md                                # Bu dosya
??? DeadLetterExample.Producer/                  # Producer projesi
?   ??? DeadLetterExample.Producer.csproj
?   ??? DeadLetterProducer.cs                    # Producer s?n?f?
?   ??? Program.cs                               # Producer ana program?
??? DeadLetterExample.Consumer/                  # Consumer projesi
    ??? DeadLetterExample.Consumer.csproj
    ??? DeadLetterConsumer.cs                    # Consumer s?n?f?
    ??? Program.cs                               # Consumer ana program?
```

## H?zl? Ba?latma

### 1. RabbitMQ'yu Ba?lat?n

```bash
# Docker ile (önerilen)
docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:3-management

# Management UI: http://localhost:15672
# Kullan?c?: guest / ?ifre: guest
```

### 2. Producer'? Çal??t?r?n

```bash
cd DeadLetterExample/DeadLetterExample.Producer
dotnet run
```

Producer ilk çal??t?r?ld???nda:
- ? Main Exchange olu?turur
- ? Main Queue olu?turur (DLX yap?land?rmal?)
- ? Dead Letter Exchange olu?turur
- ? Dead Letter Queue olu?turur
- ? Gerekli binding'leri yapar

Menüden seçim yap?n:
- **1**: Tek mesaj gönder
- **2**: 10 test mesaj? gönder
- **3**: TTL'li mesajlar gönder (DLQ test için)

### 3. Consumer'? Çal??t?r?n (Yeni Terminal)

```bash
cd DeadLetterExample/DeadLetterExample.Consumer
dotnet run
```

Consumer modunu seçin:
- **1**: Normal Mod - Tüm mesajlar? ba?ar?yla i?le
- **2**: Hata Modu - Tüm mesajlar? reject et (DLQ'ya gönder)
- **3**: Karma Mod - %50 ba?ar? oran?
- **5**: DLQ'dan mesaj tüket

## Temel Test Senaryolar?

### ? Senaryo 1: Normal Mesaj ??leme

1. Producer'da: "2 - 10 test mesaj? gönder"
2. Consumer'da: "1 - Normal Mod"
3. **Sonuç**: Tüm mesajlar ba?ar?yla i?lenir

### ? Senaryo 2: Mesajlar? Reject Etme

1. Producer'da: "2 - 10 test mesaj? gönder"
2. Consumer'da: "2 - Hata Modu"
3. **Sonuç**: Tüm mesajlar DLQ'ya dü?er
4. Consumer'da: "5 - DLQ'dan mesaj tüket"
5. **Sonuç**: DLQ'daki mesajlar görüntülenir

### ? Senaryo 3: TTL ile DLQ'ya Dü?me

1. Producer'da: "3 - Süreli mesajlar gönder"
2. Consumer'? **ÇALI?TIRMAYIN**
3. 30 saniye bekleyin
4. Management UI'da `dead-letter-queue`'yu kontrol edin
5. **Sonuç**: Mesajlar otomatik olarak DLQ'ya dü?mü? olacak

## RabbitMQ Management UI

**URL**: http://localhost:15672

### Kontrol Edilecek Noktalar

#### Exchanges
- `main-exchange` (Direct) ?
- `dead-letter-exchange` (Direct) ?

#### Queues
- `main-queue` ?
  - Arguments:
    - `x-dead-letter-exchange`: dead-letter-exchange
    - `x-dead-letter-routing-key`: failed
    - `x-message-ttl`: 30000
- `dead-letter-queue` ?

#### Bindings
- `main-queue` ? `main-exchange` (order.created)
- `dead-letter-queue` ? `dead-letter-exchange` (failed)

## Önemli Noktalar

### Dead Letter Exchange Özellikleri

1. **TTL (Time To Live)**
   - Ana kuyrukta 30 saniye TTL ayarlanm??
   - Tüketilmeyen mesajlar 30 saniye sonra DLQ'ya dü?er

2. **NACK/Reject**
   - `requeue=false` ile reject edilen mesajlar DLQ'ya gider
   - `requeue=true` ile reject edilen mesajlar tekrar ana kuyru?a döner

3. **DLQ Headers**
   - Mesajlar DLQ'ya dü?tü?ünde ekstra header bilgileri eklenir:
     - `x-death`: Death history
     - `x-first-death-reason`: Reject nedeni (rejected, expired, maxlen)
     - `x-first-death-queue`: ?lk reject edildi?i queue
     - `x-first-death-exchange`: ?lk reject edildi?i exchange

## Yap?land?rma

### Ba?lant? Ayarlar?

Producer ve Consumer'daki `Program.cs` dosyalar?nda:

```csharp
var factory = new ConnectionFactory
{
    HostName = "localhost",  // RabbitMQ sunucu adresi
    Port = 5672,
    UserName = "guest",
    Password = "guest",
    VirtualHost = "/"
};
```

### Cloud AMQP için

```csharp
var factory = new ConnectionFactory
{
    Uri = new Uri("amqps://username:password@host/vhost")
};
```

## Detayl? Dokümantasyon

Daha fazla bilgi için `README.md` dosyas?na bak?n:
- Dead Letter Exchange nas?l çal???r?
- Best practices
- Troubleshooting
- ?leri seviye senaryolar
- Retry mekanizmalar?

## Sorun Giderme

### Problem: Mesajlar DLQ'ya Dü?müyor

**Çözüm**:
1. Producer'? önce çal??t?r?n (altyap?y? kurar)
2. Binding'leri Management UI'dan kontrol edin
3. Consumer'da `requeue=false` kullan?ld???ndan emin olun

### Problem: RabbitMQ Ba?lant?s? Ba?ar?s?z

**Çözüm**:
1. RabbitMQ çal???yor mu? ? `docker ps`
2. Port 5672 aç?k m??
3. Management UI'a eri?ebiliyor musunuz? ? http://localhost:15672

### Problem: TTL Çal??m?yor

**Çözüm**:
1. Consumer'? kapat?n (mesajlar tüketilmemeli)
2. 30 saniye bekleyin
3. Management UI'da DLQ'yu kontrol edin

## Destek

- [RabbitMQ Dokümantasyon](https://www.rabbitmq.com/documentation.html)
- [Dead Letter Exchanges](https://www.rabbitmq.com/dlx.html)
- [RabbitMQ .NET Client](https://www.rabbitmq.com/dotnet-api-guide.html)

---

**Udemy RabbitMQ E?itimi - Dead Letter Exchange Örne?i**

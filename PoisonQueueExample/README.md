# Poison Queue Örne?i (Quorum + Delivery Limit)

Bu örnekte kuyruk **quorum** tipinde ve `x-delivery-limit=3` ile yap?land?r?lm??t?r. Mesaj her redelivery'de **broker** taraf?ndan say?l?r; limit a??l?nca mesaj otomatik olarak DLQ'ya ta??n?r.

## Mimari
- `poison-main-exchange` (direct)
- `poison-main-queue` (quorum)
  - `x-queue-type = quorum`
  - `x-dead-letter-exchange = poison-dlx`
  - `x-dead-letter-routing-key = poison.failed`
  - `x-delivery-limit = 3`
- `poison-dlx` (direct)
- `poison-dead-letter-queue`

Ak?? (delivery-limit + requeue=true):
1. Producer, mesaj? `poison-main-exchange` ? `poison-main-queue`'ya yazar.
2. Consumer hata simüle eder ve `BasicNack(requeue:true)` gönderir.
3. Broker delivery sayac?n? art?r?r; `<3` ise mesaj yeniden teslim edilir.
4. 3. teslim sonras? hala NACK al?n?rsa broker mesaj? `poison-dlx` ? `poison-dead-letter-queue`'ya ta??r.

## Çal??t?rma
1. RabbitMQ'yu ba?lat?n (örn. Docker `rabbitmq:3-management`).
2. Producer'? çal??t?r?n ve birkaç mesaj gönderin:
   ```bash
   dotnet run --project PoisonQueueExample/PoisonQueueExample.Producer
   ```
3. Consumer'? çal??t?r?n (ayr? terminal):
   ```bash
   dotnet run --project PoisonQueueExample/PoisonQueueExample.Consumer
   ```
4. Consumer log'lar?nda tekrar teslimleri ve DLQ'ya dü?ü?ü gözlemleyin.

## H?zl? Test
1. Producer'da bir mesaj gönderin (örn. "poison-test").
2. Consumer log'lar?:
   - 1. ve 2. teslim: `BasicNack(requeue:true)`
   - 3. teslimde yine NACK: broker `delivery-limit` nedeniyle mesaj? DLQ'ya ta??r.
3. RabbitMQ UI'da `poison-dead-letter-queue` içeri?ini ve `x-death` bilgilerini kontrol edin.

## Dosya Yap?s?
```
PoisonQueueExample/
??? README.md
??? PoisonQueueExample.Producer/
?   ??? PoisonQueueExample.Producer.csproj
?   ??? Program.cs
??? PoisonQueueExample.Consumer/
    ??? PoisonQueueExample.Consumer.csproj
    ??? Program.cs
```

## Beklenen Sonuç
- Broker, redelivery say?s?n? kendi takip eder; 3. ba?ar?s?z teslim sonras? mesaj DLQ'ya gider.
- DLQ'da `x-death` header'lar? görünür.

### Notlar
- Ba?ar?y? simüle etmek için consumer’daki `processedSuccessfully` de?erini `true` yapabilirsiniz.
- Limit için `x-delivery-limit` de?erini Producer/Consumer içindeki `DeliveryLimit` sabitiyle de?i?tirin.

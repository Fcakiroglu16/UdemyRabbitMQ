Replika Sayısı	Quorum	Tolere Edilen Hata	Açıklama
1	1	0 node	⚠️ HA yok - Production için önerilmez
3 ✅	2	1 node	🎯 Önerilen - 1 node çökse bile çalışır
5	3	2 node	🔒 Yüksek HA - 2 node çökse bile çalışır
7	4	3 node	⚡ Performans düşer - Nadiren kullanılır



Quorum = (Replika Sayısı / 2) + 1
   
   3 replika → Quorum = 2 (en az 2 node çalışmalı)
   5 replika → Quorum = 3 (en az 3 node çalışmalı)



   Özellik	Quorum Queue (Yeni Nesil)	Classic Mirrored Queue (Eski Nesil)
Temel Prensip	Raft Mutabakatı (CP - Tutarlılık öncelikli)	Guaranteed Multicast (AP - Erişilebilirlik öncelikli)
Veri Güvenliği	Çok Yüksek. Mesaj, çoğunluğa yazılmadan onaylanmaz.	Daha Düşük. Veri kaybı riski (split-brain) vardı.
Dayanıklılık	Her Zaman Dayanıklı (Durable). Mesajlar hep diske yazılır.	Transient (geçici) olabilirdi.
Performans	Veri güvenliği için performanstan az da olsa ödün verir.	Belirli senaryolarda daha hızlı olabiliyordu.
Zehirli Mesaj	Dahili "Poison Message" (İşlenemeyen mesaj) yönetimi vardır.

# Ofis Yönetimi ve BBSoft Veritabanı Entegrasyonu

Bu belge, ERSoftSMSBox içindeki **Ofis Yönetimi** sekmesinin ve **Veritabanı Ayarları** ekranının BBSoft ile nasıl eşleştiğini anlatır. Uygulama BBSoft veritabanına **yalnızca okuma** amaçlı bağlanır; şemada ve verilerde değişiklik yapmaz.

## Bağlantı ayarları (BBSoft ile birebir)

| BBSoft `Ayarlar.INI` | ERSoftSMSBox alanı |
|---|---|
| `[SQL] ServerName` | SQL sunucusu |
| `[SQL] GenelDatabaseName` | Veritabanı (varsayılan `BBSoft`) |
| `[SQL] SQLUserName` | SQL kullanıcı |
| `[SQL] SQLPassword` | SQL parola (DPAPI ile bu Windows hesabına şifrelenir) |
| `[Dizin] OfficeCode` | Ofis kodu (sekmelerde varsayılan ofis filtresi) |

**Ayarlar.INI'den Al** düğmesi BBSoft'un `Tdm` veri modülüyle aynı yolu izler ([BBSoftIniImporter.cs](../Data/BBSoftIniImporter.cs)): seçilen (program klasörü) INI'den `[Dizin] SabitlerDizini` okunur, `[SQL]` değerleri `<SabitlerDizini>\Ayarlar.INI` dosyasından alınır (yoksa seçilen dosyadan). `SQLUserName` yoksa BBSoft varsayılanı `sa` kullanılır.

Bağlantı BBSoft'un BDE parametrelerini yansıtır (`Tdm::DatabaseAc`): `MAX QUERY TIME=300` → komut zaman aşımı 300 sn, `TDS PACKET SIZE=4096` → paket boyutu, `NATIONAL LANG NAME=Turkish` → `Current Language=Turkish`, şifreleme zorunlu değil (`Encrypt=Optional`). Sunucu şifreleme isteyip sertifikası geçersizse "Sunucu sertifikasına güven" açılır. Bağlanınca sunucu saati BBSoft gibi `SELECT CURRENT_TIMESTAMP` ile okunur ve gösterilir.

Bağlanınca `BBSoft.dbo.EkranTanimlama` okunur ve BBSoft'un mesaj aşamaları (`Mesaj1..5`, `Mesaj1..5VadeGunu`), Nebim veritabanı adı (`VeriAlinacakDatabases`) ve kefil ayarı (`KefilGonder`) ekranda gösterilir.

## Kullanılan BBSoft tabloları

| Tablo | Kullanım |
|---|---|
| `MusteriAdSoyad` | Müşteri kodu, ad, ofis kodu/açıklaması, BBSoft aşaması (`SSMMesaj`) |
| `MusteriTelefonNo` | Telefon rehberi; müşteri başına telefon adedi |
| `MusteriKalanBorc` | Kalan borç (`Debit`) |
| `MusteriSonOdemeTarihi` | Referans tarihi (`QuitPaymentDate`) → geçen gün hesabı |
| `AvukatlikKarti` | Avukatlık takibindeki müşteriler (vade kategorilerinden hariç) |
| `GonderilenSSMler` | Gönderim günlüğü: kime, hangi ofisten, ne zaman |
| `EkranTanimlama` | Aşama/vade ayarları ve Nebim veritabanı adı |
| `[Nebim].dbo.prCurrAccPersonalInfo` | Doğum tarihi (yalnızca doğum günü kategorilerinde, `VeriAlinacakDatabases` üzerinden) |

Sorgular [BBSoftRepository.cs](../Data/BBSoftRepository.cs) içindedir; birim testleri sözdizimini ve salt okunur olduklarını doğrular.

## Sekmeler

- **Ofisler** — ofis başına müşteri, telefonu olan, borçlu, toplam bakiye, avukatlık, gönderilen SMS ve son gönderim. Çift tık → o ofisin müşterileri.
- **Müşteriler** — ofis + kategori + arama filtresi. Kategoriler: tüm müşteriler, vadesi geçmiş borç, vadeli 1–5 / 6–10 / 11–15 / 16–20 / 21–30 / 31–45 / 46–60 / 60+ gün, doğum günü (bugün / 7 gün / 30 gün), telefonu olmayanlar. Satırlar 20+ gün sarı, 60+ gün kırmızı tonlanır. Seçilen kayıtlardan kategoriye özel şablonla **taslak** üretilir (SMS gönderilmez).
- **Telefon Rehberi** — telefonlar ofise göre gruplanır, CSV'ye aktarılır.
- **Gönderilen SMS'ler** — BBSoft'un ekranındaki gibi üstte ofis/tarih özeti, altta detay; özet satırı seçilince detay süzülür. Günlükte mesaj metni saklanmadığı için kategori borç ve geçen günden türetilir.
- **Mesaj Şablonları** — kategori başına şablon; alanlar `{AdSoyad} {CariKodu} {Ofis} {Borc} {Gun} {DogumGunu}`. `%LocalAppData%\ERSoftSMSBox\templates.json` içinde saklanır. Varsayılan vade metni BBSoft'un özgün mesajıdır.

## Canlıya geçmeden önce

1. BBSoft sunucusunda SELECT yetkili bir hesapla bağlanıp Ofisler sekmesindeki sayıları BBSoft ekranlarıyla karşılaştırın.
2. `GonderilenSSMler.SentDate/SentTime` alan türlerini kontrol edin; uygulama hem tarih hem metin türünü okur.
3. Doğum günü kategorisi için Nebim veritabanında `prCurrAccPersonalInfo.BirthDate` erişimini doğrulayın.
4. Gerçek gönderim eklenirken `GonderilenSSMler` ile aynı kolonlara yazan bir günlük katmanı ve İYS/izin kontrolü planlayın.

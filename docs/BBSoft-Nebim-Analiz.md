# BBSoft / Nebim inceleme ve uyarlama raporu

## 1. Kapsam ve kanýt sýnýrý

Ýncelenen klasör: `C:\Users\Samet\Desktop\BBSoft`.

Klasör, C++Builder/VCL kaynaklarý (`GsmSMS`, `SmSGonder`), EXE/BPL dosyalarý, INI yapýlandýrmalarý, geçici Paradox tablolarý ve üç `.bak` dosyasý içeriyor. Kaynaklar eski sürümlere ait olabilir; EXE'lerin bu kaynaklarla birebir ayný olduðu doðrulanmadý. EXE çalýþtýrýlmadý, SQL sunucusuna baðlanýlmadý, yedek geri yüklenmedi, gerçek SMS gönderilmedi.

Aþaðýdaki nesne adlarý **kaynak kodda görülen referanslardýr**. Canlý veritabanýndaki nesne türleri, veri tipleri, anahtarlar, izinler, indeksler ve güncellik henüz doðrulanmýþ deðildir. `RetailCustomerTransactions`, `RetailInstallments` gibi adlarý doðrudan fiziksel tablo kabul etmeyin; görünüm olabilirler. `CurrAccCommunication('TR')` ve `ContactType('TR')` fonksiyon çaðrýlarýdýr.

## 2. Bileþenler

| Kaynak | Görev / kanýt |
|---|---|
| `GsmSMS/Data.cpp` | SQL/INI ayarlarý; Nebim verilerinin BBSoft ara tablolarýna aktarýlmasý |
| `GsmSMS/GunlukSMSUnit.cpp` | Günlük aday listesi ve harici gönderici çalýþtýrma |
| `GsmSMS/GonderilenSMSUnit.cpp` | `GonderilenSSMler` üzerinden geçmiþ |
| `GsmSMS/Servis_Tur_NumaraFiyatUnit.cpp` | Ekran/servis ayarlarý |
| `SmSGonder/MenuUnit.cpp` | Aday hesaplama, mesaj oluþturma, HTTP gönderim, aþama/geçmiþ güncelleme |
| `SmSGonder/Data.cpp` | Göndericinin ayrý SQL baðlantýsý ve INI okumasý |
| `NebimSmsTimer*.exe`, `NebimSmsKampanya*.exe`, `SmsGonder*.exe` | Daðýtýlmýþ sürümler; tüm sürümler için kaynak eþleþmesi doðrulanmadý |

Genel muhasebe altyapýsýndan gelen `FirmaKarti`, `CHareket`, `Siparis`, `SiparisKalem`, `StokKarti`, `KasaKarti`, `CekKarti`, `SenetKarti`, `Fatura`, `SHareket` gibi referanslar da var. Bunlar SMS uygulamasýna topluca taþýnmadý.

## 3. Baðlantý ve yapýlandýrma haritasý

| Yer | Alan | Ýþlev |
|---|---|---|
| `Ayarlar.INI`, `[SQL]` | `ServerName` | SQL sunucusu/örneði |
| `[SQL]` | `GenelDatabaseName` | Genel uygulama veritabaný |
| `[SQL]` | `SQLUserName`, `SQLPassword` | SQL kimlik bilgileri; deðerler rapora alýnmadý |
| `[SQL]` | `SirketKodu`, `KullaniciAdi` | Eski þirket/kullanýcý seçimi; SQL hesabý ile ayný kavram deðil |
| `[Dizin]` | `SabitlerDizini` | Ýkinci yapýlandýrma kaynaðýna yönlendirebilir |
| `[Dizin]` | `OfficeCode` | Kök INI'de mevcut; her sorguda gerçekten uygulandýðý varsayýlmamalý |
| `[Dizin]` | `xmltirnak`, `xmlsatir2` | Eski XML yanýtýný metin deðiþtirerek ayýklama yardýmcýlarý |
| `BBSoft.dbo.EkranTanimlama` | `VeriAlinacakDatabases` | Nebim kaynak veritabaný seçimi |
| `EkranTanimlama` | `Mesaj1VadeGunu` … `Mesaj4VadeGunu`, `KefilGonder` | SMS aþama aralýklarý / kefil davranýþý |
| Nebim ana katalog | `NebimV3Master.dbo.cdUser`, `NEbimV3Master..bsField` | Kullanýcý/metadata referanslarý; yeni uygulamada kimlik doðrulama kaynaðý yapýlmadý |

Sunucu adresi, kullanýcý adý, parola, saðlayýcý parolasý ve gerçek telefonlar kopyalanmadý. Yeni ekran baðlantýyý kullanýcýdan alýr. SQL parolasý Windows kullanýcý hesabýna baðlý DPAPI ile yerelde saklanýr. Windows kimlik doðrulamasý varsayýlandýr; TLS zorunludur ve sunucu sertifikasýna kör güven varsayýlaný yoktur. SQL hesabýna yalnýz ilgili nesnelerde SELECT izni verilmelidir. `ApplicationIntent=ReadOnly` tek baþýna yetki kýsýtý deðildir.

## 4. Nebim ? BBSoft veri haritasý

| Nebim nesnesi / alaný | BBSoft nesnesi / alaný | Kaynak |
|---|---|---|
| `cdCurrAcc`: `CurrAccTypeCode=4`, `CurrAccCode`, `FirstLastName`, `OfficeCode` | `MusteriAdSoyad`: `CurrAccCode`, `FirstLastName`, `OfficeCode` | `GsmSMS/Data.cpp:1980–1993` |
| `cdOfficeDesc`: `OfficeCode`, `OfficeDescription`, `LangCode='TR'` | `MusteriAdSoyad.OfficeDescription` | Ayný aktarým |
| `CurrAccCommunication('TR')`: `CurrAccTypeCode`, `CurrAccCode`, `ContactID`, `CommunicationTypeDescription`, `CommAddress` | `MusteriTelefonNo`: `CurrAccCode`, `Description`, `TelefonNo` | `1967–1977` |
| `RetailCustomerTransactions`: `RetailCustomerCode`, `Debit`, `Payment` | `MusteriKalanBorc`: `CurrAccCode`, `Debit` | `1869–1889` |
| `RetailInstallments`: `CurrAccCode`, `RemainingAmount`, `PaymentAmount`, `DueDate` | Borç ve hiç ödeme yapmamýþ müþteri tarih desteði | `1875–1911` |
| `RetailCustomerTransactions.OperationDate` | `MusteriSonOdeme`: `RetailCustomerCode`, `QuitPaymentDate` | `1892–1911` |
| `trBadDebtTransLine.CurrAccCode` | `AvukatlikKarti.CurrAccCode` | `1858–1866` |
| `MusteriSonOdeme` eksi avukatlýk müþterileri | `MusteriSonOdemeTarihi` | `1954–1963` |
| `prCurrAccContact`: `CurrAccCode`, `ContactID`, `ContactTypeCode`, `FirstLastName`; `prCurrAccCommunication.CommAddress`; `ContactType('TR')` | `MusteriKefilKarti`: `ContackCurrAccCode`, `TypeContact`, `KefilFirsLastName`, `KefilTelefonNo` | `1996–2013` |

`ContackCurrAccCode`, `KefilFirsLastName`, `GonderilenSSMler`, `SSMMesaj` yazýmlarý eski þemadaki yazýmdýr; SQL'de kendiliðinden düzeltilmemelidir.

### Borç ve tarih semantiði

Eski borç aktarýmý iki kümeyi `UNION ALL` ile birleþtirir: hareketlerinde `SUM(Debit-Payment)>0 AND SUM(Payment)>0` olanlar ile taksitlerinde `SUM(PaymentAmount)=0` olanlar. Bunlarýn her canlý veri setinde ayrýk olduðu kanýtlanmadý. Ayrýca `QuitPaymentDate` bazý müþterilerde son hareket/ödeme tarihi, hiç ödeme yapmamýþlarda ilk vade tarihidir; etiketi kesin biçimde "son ödeme tarihi" saymak yanýltýcýdýr.

Yeni Nebim okumasý hareketi bulunan müþteride hareket bakiyesini, hareketi bulunmayanda taksit bakiyesini kullanýr; bu nedenle finansal rakamlarýn Nebim raporuyla karþýlaþtýrýlmasý zorunludur. BBSoft okumasý mevcut ara tablo bakiyesini gösterir; verinin güncelliði bu uygulama tarafýndan garanti edilmez. Yeni uygulama ara tablolarý yenilemez ve finansal muhasebe kaydý oluþturmaz.

## 5. Eski SMS süreci

`SmSGonder/MenuUnit.cpp:21–99`:

1. Sunucu tarihi alýnýr; `BrrDataAlinanTarih.Durum` iþleniyor durumuna çekilir.
2. `EkranTanimlama` okunur.
3. Müþteri/tarih/borç/telefon tablolarý birleþtirilir. Sonraki aþamalarda `GonderilenMusteriler.MaxSentDate` ve gün aralýklarý kullanýlýr.
4. Liste üzerinde otomatik dolaþýlýp gönderim düðmesi çaðrýlýr.
5. `GonderilenMusteriler` silinip gün içindeki `GonderilenSSMler` kayýtlarýndan yeniden doldurulur.
6. Gün durumu deðiþtirilir; uygulama sonlandýrýlýr.

`SmSGonder/MenuUnit.cpp:103–219`:

- Aþama `SSMMesaj` üzerinden 1?2?3?4?5 ilerletilir. Liste sorgusu ile aþama sayýlarý tam bir durum makinesi olarak doðrulanmadý.
- Ad/soyad, maðaza açýklamasý, geçen gün ve borç tutarý mesaja yerleþtirilir. Kaynakta beþ aþamanýn metinleri ayný/sabit görünüyor; otomatik güncel þablon sistemi varsayýlmamalý.
- Türkçe karakterler tek tek URL kodlanýr.
- AjansWebSMS adresine **HTTP GET** çaðrýlýr:
  `http://g.ajanswebsms.com/sms_soap/sms.asmx/send_sms`
- Parametre adlarý: `username`, `password`, `vendor`, `header`, `message`, `gsm`, `charset`, `startdate`, `exdate`.
- XML yanýtýndaki `<string>` metni, XML parser yerine metin deðiþimleriyle ayrýþtýrýlýr. Namespace referansý `http://g.iletimx.com/sms_soap/`.
- Dönen metin `SentID` olarak saklanýr. HTTP yanýtý/ID, telefona teslim edildiðinin kanýtý deðildir.
- `GonderilenSSMler` alanlarý: `CurrAccCode`, `Description`, `TelefonNo`, `Debit`, `FirstLastName`, `OfficeCode`, `OfficeDescription`, `QuitPaymentDate`, `SentDate`, `SentTime`, `SentDateTime`, `SentID`.
- `MusteriAdSoyad.SSMMesaj` ve `PaymentDate` deðiþtirilir. Kefil akýþý da kaynakta vardýr.

### Taþýnmamasý gereken davranýþlar

Þifresiz HTTP, URL içinde parola/mesaj, sabit saðlayýcý kimlik bilgileri, metin birleþtirmeli SQL, liste açýlýr açýlmaz toplu gönderim, geçmiþ tablolarýný silerek yeniden üretme, belirsiz yanýtta aþama ilerletme ve ayný numaraya mükerrer borç mesajý yeni uygulamaya taþýnmadý. Kefile borç bilgisi gönderimi ayrýca yetki/mahremiyet incelemesi gerektirir.

## 6. ERSoftSMSBox uyarlamasý

- Mevcut ÝletiMerkezi key/hash baðlantýsý ve salt okunur hesap testi korunur. AjansWebSMS hesabý bununla ayný hesap/protokol deðildir.
- Ýnceleme sýrasýnda eklenen NetGsm ve dinamik saðlayýcý menüsü korunmuþtur; yeni veri ekraný saðlayýcýlardan baðýmsýzdýr.
- Ayrý "Nebim / BBSoft" ekranýnda kaynak türü, sunucu, veritabaný ve kimlik doðrulama seçilir.
- Baðlantý bilgileri kendiliðinden eski INI'den ithal edilmez; kullanýcý açýkça girer ve kaydeder.
- Ofisler veritabanýndan Türkçe açýklamalarýyla okunur. Ofis, müþteri kodu/adý, minimum/maksimum borç ve referans tarih aralýðý sorguya parametre olarak geçer.
- Salt okunur, zaman aþýmý ve iptal destekli SQL kullanýlýr. Avukatlýk kayýtlarý adaylardan dýþlanýr. `NOLOCK` kullanýlmaz.
- En fazla 500 aday gösterilir; daha fazlasý varsa filtreyi daraltma uyarýsý verilir. Bu bir tam veritabaný dýþa aktarýmý deðildir.
- Seçilen kayýtlardan Türkçe þablonlarla SMS önizlemesi hazýrlanýr. Türkiye cep numaralarý normalize edilir; hatalý numaralar atlanýr. Ayný numara birden fazla seçili kayda baðlýysa güvenlik için tüm çakýþan kayýtlar dýþlanýr.
- Taslaklar bellektedir; uygulama açýlýþýnda SQL sorgusu veya SMS isteði yapýlmaz.
- Gerçek gönderim, teslimat sorgusu ve otomatik zamanlayýcý bu aþamada etkin deðildir. Güncel ÝletiMerkezi gönderim dokümanýna denenen iki resmi adresten ulaþýlamadý; belgesiz payload tahminiyle ücretli gönderim eklenmedi.

## 7. Canlýya geçiþ kontrol listesi

1. Test sunucusunda, en az yetkili SELECT hesabýyla baðlantýyý doðrulayýn; geçerli SQL TLS sertifikasý kullanýn.
2. Nesne adlarýný, sütunlarý, þirket/ofis ayrýmýný, müþteri tipi 4 kullanýmýný ve veri tiplerini doðrulayýn.
3. Farklý ofislerden örnek müþterilerin borç/tarih/telefonlarýný Nebim ekranlarýyla karþýlaþtýrýn. Özellikle hiç ödeme yapmayanlarý kontrol edin.
4. BBSoft ara tablolarýnýn son aktarým zamanýný iþ sahibiyle doðrulayýn.
5. SMS saðlayýcýsýný seçin; güncel HTTPS gönderim, raporlama, hata kodlarý, limitler, Türkçe karakter/segment maliyeti ve IYS kurallarýný belgeleyin.
6. Ýzinli alýcý listesini ve kefil iletiþimi politikasýný doðrulayýn. Telefonun rehberde bulunmasý ileti izni deðildir.
7. Gönderimden önce kalýcý outbox, idempotency/tekrar gönderim korumasý, audit kaydý, yetkilendirme ve belirsiz sonuç mutabakatý ekleyin. Að zaman aþýmýndan sonra otomatik tekrar göndermeyin.
8. Kullanýcýya alýcý adedi, mesaj içeriði ve tahmini maliyet gösteren açýk bir son onay ekleyin.
9. Teslimat raporunu ayrý statü olarak izleyin; kabul edildi ile teslim edildi ayrýlmalýdýr.
10. Zamanlayýcý, mesaj aþamalarý, þablon sürümleri, kara liste, reddetme talepleri, tarihli kampanyalar ve dýþa aktarým izinlerini bu temelden sonra geliþtirin.

## 8. Doðrulama sonucu

- Çözüm derlemesi baþarýlý.
- Mevcut saðlayýcý testleri dahil 99 NUnit testi baþarýlý.
- Geçici arayüz testi 13 kontrolü geçti: menü ikonu, ekran geçiþi, açýk baðlantý öncesi sorgunun kapalý olmasý, otomatik sorgu yapýlmamasý, yedi kolon, seçili alýcý önizlemesi, eski sonuçlarýn temizlenmesi, kaydýrma ve kapanýþ.
- Kapsam çýktýsý: `obj/coverage.cobertura.xml`. Bu rapor canlý SQL sorgularýnýn çalýþtýðýný kanýtlamaz.
- `docs/Veritabani-Sema-Kontrol.sql` yalnýz metadata ve SELECT yetkisini kontrol eden yardýmcý betiktir. Uygulama bunu kendiliðinden çalýþtýrmaz. Betik canlý sunucuda henüz çalýþtýrýlmadý.
- BBSoft EXE dosyalarý ve veritabaný yedekleri deðiþtirilmedi; eski kimlik bilgileri aktarýlmadý.

**Sonuç:** Kaynak akýþý ve tablo eþleþmeleri çýkarýldý; güvenli okuma/filtreleme/taslak temeli eklendi. Canlý þema, finansal mutabakat ve ücretli SMS gönderimi onaylanmýþ veya tamamlanmýþ olarak deðerlendirilmemelidir.

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Data.SqlClient;
using per10SatisWPF.Models;

namespace per10SatisWPF
{
    public partial class MainWindow : Window
    {
        private readonly string _connStr = ConfigurationManager.ConnectionStrings["Per10DB"].ConnectionString;

        private List<SepetItem> _sepet   = new();
        private List<Urun>      _urunler = new();
        private int    _aktifTurID = 0;
        private readonly DispatcherTimer _idleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        private readonly DispatcherTimer _barkodTimer = new DispatcherTimer
            { Interval = TimeSpan.FromMilliseconds(200) };


        private List<(int TurID, string Ikon, string Ad)> _kategoriler = new List<(int TurID, string Ikon, string Ad)>();

        public MainWindow()
        {
            InitializeComponent();

            _idleTimer.Tick += (s, e) => txtBarkod.Focus(); // 10 sn dolunca barkoda zıpla
            this.PreviewKeyDown += (s, e) => { _idleTimer.Stop(); _idleTimer.Start(); }; // Tuşa basılınca süreyi sıfırla
            this.PreviewMouseDown += (s, e) => { _idleTimer.Stop(); _idleTimer.Start(); }; // Fareye tıklanınca süreyi sıfırla
            _idleTimer.Start();

            _barkodTimer.Tick += (s, e) => { _barkodTimer.Stop(); BarkodIleAra(); };
            Loaded += (s, e) =>
            {
                SaatBaslat();
                KategorileriYukle();
                UrunleriYukle(0);
                PlaceholderSet(txtArama);
               
            };
        }
       
        // ─── SAAT ─────────────────────────────────────────────────────
        private void SaatBaslat()
        {
            SaatGuncelle();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += (s, e) => SaatGuncelle();
            timer.Start();
        }
        private void SaatGuncelle() =>
            lblSaat.Text = DateTime.Now.ToString("dd MMMM yyyy  HH:mm:ss", new System.Globalization.CultureInfo("tr-TR"));

        // ─── KATEGORİLER ──────────────────────────────────────────────
        private void KategorileriYukle()
        {
            _kategoriler.Clear();
            _kategoriler.Add((0, "🏪", "Tümü"));

            try
            {
                using (var conn = new SqlConnection(_connStr))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand("SELECT TurID, TurAdi FROM Turler ORDER BY TurID", conn))
                    {
                        using (var r = cmd.ExecuteReader())
                        {
                            while (r.Read())
                            {
                                int id = Convert.ToInt32(r["TurID"]);
                                string ad = r["TurAdi"].ToString();
                                string ikon = "🏷️"; // Yeni eklenenler için varsayılan

                                // Hata vermemesi için klasik switch kullandım
                                switch (id)
                                {
                                    case 1: ikon = "🔧"; break;
                                    case 2: ikon = "🪟"; break;
                                    case 3: ikon = "🌸"; break;
                                    case 4: ikon = "🛞"; break;
                                    case 5: ikon = "✨"; break;
                                    case 6: ikon = "🧽"; break;
                                    case 7: ikon = "🥤"; break;
                                    case 8: ikon = "🚗"; break;
                                    case 9: ikon = "📦"; break;
                                }

                                _kategoriler.Add((id, ikon, ad));
                            }
                        }
                    }
                }
            }
            catch { } // Veritabanı hatası alırsan çökmesin

            // Arayüz butonlarını oluşturma kısmı (Senin kodunla birebir aynı)
            pnlKategoriler.Children.Clear();
            foreach (var item in _kategoriler)
            {
                bool aktif = item.TurID == _aktifTurID;

                var sp = new StackPanel { Orientation = Orientation.Horizontal };
                sp.Children.Add(new TextBlock
                {
                    Text = item.Ikon,
                    FontSize = 14,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 10, 0)
                });
                sp.Children.Add(new TextBlock
                {
                    Text = item.Ad,
                    FontSize = 13,
                    Foreground = Brushes.White,
                    VerticalAlignment = VerticalAlignment.Center
                });

                var btn = new Button
                {
                    Content = sp,
                    Tag = item.TurID,
                    Height = 42,
                    Margin = new Thickness(0, 2, 0, 2),
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand,
                    Padding = new Thickness(10, 0, 0, 0),
                    Background = aktif
                        ? (Brush)FindResource("AccentBlue")
                        : (Brush)FindResource("BgCard"),
                    Template = (ControlTemplate)FindResource("KategoriBtn")
                };
                btn.Click += KategoriBtn_Click;
                pnlKategoriler.Children.Add(btn);
            }
        }

        private void KategoriBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int turID)
            {
                _aktifTurID = turID;
                var kat = _kategoriler.FirstOrDefault(k => k.TurID == turID);
                lblKategoriBaslik.Text = turID == 0 ? "Tüm Ürünler" : kat.Ad;
                KategorileriYukle();
                UrunleriYukle(turID);
            }
        }

        // ─── ÜRÜNLER ──────────────────────────────────────────────────
        private void UrunleriYukle(int turID, string arama = null)
        {
            _urunler.Clear();
            pnlUrunler.Children.Clear();

            int sabitUrunID = -1; // Paspas kağıdını iki defa göstermemek için ID'sini tutacağız

            try
            {
                using (var conn = new SqlConnection(_connStr))
                {
                    conn.Open();

                    // 1. AŞAMA: ÖNCE VIP ÜRÜNÜ (PASPAS KAĞIDI) BUL VE EN BAŞA EKLE
                    string sabitSorgu = @"SELECT u.UrunID, u.UrunAdi, m.MarkaAdi, u.AlisFiyati, u.SatisFiyati, u.MevcutStok, u.TurID
                                  FROM Urunler u
                                  JOIN Markalar m ON u.MarkaID = m.MarkaID
                                  WHERE u.UrunAdi = 'PASPAS KAĞIDI' AND m.MarkaAdi = 'PER10' AND u.MevcutStok > 0";

                    using (var cmdSabit = new SqlCommand(sabitSorgu, conn))
                    using (var rSabit = cmdSabit.ExecuteReader())
                    {
                        if (rSabit.Read())
                        {
                            var sabitUrun = new Urun
                            {
                                UrunID = Convert.ToInt32(rSabit["UrunID"]),
                                UrunAdi = rSabit["UrunAdi"].ToString(),
                                MarkaAdi = rSabit["MarkaAdi"].ToString(),
                                AlisFiyati = Convert.ToDecimal(rSabit["AlisFiyati"]),
                                SatisFiyati = Convert.ToDecimal(rSabit["SatisFiyati"]),
                                MevcutStok = Convert.ToInt32(rSabit["MevcutStok"]),
                                TurID = Convert.ToInt32(rSabit["TurID"])
                            };
                            sabitUrunID = sabitUrun.UrunID;
                            _urunler.Add(sabitUrun);

                            var sabitKart = UrunKartiOlustur(sabitUrun);
                            // Sabitlendiği belli olsun diye çerçevesini turuncu ve kalın yapıyoruz:
                            sabitKart.BorderBrush = (Brush)FindResource("AccentOrange");
                            sabitKart.BorderThickness = new Thickness(2);

                            pnlUrunler.Children.Add(sabitKart);
                        }
                    }

                    // 2. AŞAMA: DİĞER ÜRÜNLERİ LİSTELE (Sabit ürün hariç)
                    string query = @"SELECT u.UrunID, u.UrunAdi, m.MarkaAdi, u.AlisFiyati, u.SatisFiyati, u.MevcutStok, u.TurID
                             FROM Urunler u
                             JOIN Markalar m ON u.MarkaID = m.MarkaID
                             WHERE u.MevcutStok > 0";

                    // Eğer Paspas Kağıdı bulunduysa, onu diğer aramaların dışında tut ki 2 defa ekranda çıkmasın
                    if (sabitUrunID > 0) query += " AND u.UrunID != @sabitID";
                    if (turID > 0) query += " AND u.TurID = @turID";
                    if (!string.IsNullOrWhiteSpace(arama)) query += " AND (u.UrunAdi LIKE @arama OR m.MarkaAdi LIKE @arama)";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        if (sabitUrunID > 0) cmd.Parameters.AddWithValue("@sabitID", sabitUrunID);
                        if (turID > 0) cmd.Parameters.AddWithValue("@turID", turID);
                        if (!string.IsNullOrWhiteSpace(arama)) cmd.Parameters.AddWithValue("@arama", $"%{arama}%");

                        using (var r = cmd.ExecuteReader())
                        {
                            while (r.Read())
                            {
                                var urun = new Urun
                                {
                                    UrunID = Convert.ToInt32(r["UrunID"]),
                                    UrunAdi = r["UrunAdi"].ToString(),
                                    MarkaAdi = r["MarkaAdi"].ToString(),
                                    AlisFiyati = Convert.ToDecimal(r["AlisFiyati"]),
                                    SatisFiyati = Convert.ToDecimal(r["SatisFiyati"]),
                                    MevcutStok = Convert.ToInt32(r["MevcutStok"]),
                                    TurID = Convert.ToInt32(r["TurID"])
                                };
                                _urunler.Add(urun);
                                pnlUrunler.Children.Add(UrunKartiOlustur(urun));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ürün yükleme hatası: {ex.Message}", "Hata");
            }

            lblUrunSayisi.Text = $"{_urunler.Count} ürün";
        }
        private Border UrunKartiOlustur(Urun urun)
        {
            var border = new Border
            {
                Width = 165, Height = 138,
                Margin = new Thickness(6),
                CornerRadius = new CornerRadius(12),
                Background = (Brush)FindResource("BgCard"),
                BorderThickness = new Thickness(1),
                BorderBrush = (Brush)FindResource("BorderColor"),
                Cursor = Cursors.Hand,
                Tag = urun,
                SnapsToDevicePixels = true
            };

            // Stok rengine göre üst çizgi
            var stokRenk = urun.MevcutStok == 0
                ? (Brush)FindResource("AccentRed")
                : urun.DusukStok
                    ? (Brush)FindResource("AccentOrange")
                    : (Brush)FindResource("AccentGreen");

            var icerik = new Grid { Margin = new Thickness(0) };
            icerik.RowDefinitions.Add(new RowDefinition { Height = new GridLength(4) });
            icerik.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            icerik.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            icerik.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Üst renkli çizgi
            var ustCizgi = new Border
            {
                Background = stokRenk,
                CornerRadius = new CornerRadius(12, 12, 0, 0),
                Opacity = 0.7
            };
            Grid.SetRow(ustCizgi, 0);

            var pad = new Grid { Margin = new Thickness(12, 8, 12, 0) };
            var txtAd = new TextBlock
            {
                Text = urun.TamAdi,
                Foreground = (Brush)FindResource("TextPrimary"),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 18
            };
            Grid.SetRow(pad, 1);
            pad.Children.Add(txtAd);

            var txtFiyat = new TextBlock
            {
                Text = urun.FiyatText,
                Foreground = (Brush)FindResource("AccentBlue"),
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(12, 0, 12, 4)
            };
            Grid.SetRow(txtFiyat, 2);

            var stokBadge = new Border
            {
                Margin = new Thickness(12, 0, 12, 10),
                Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 3, 8, 3),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            stokBadge.Child = new TextBlock
            {
                Text = urun.StokText,
                Foreground = stokRenk,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold
            };
            Grid.SetRow(stokBadge, 3);

            icerik.Children.Add(ustCizgi);
            icerik.Children.Add(pad);
            icerik.Children.Add(txtFiyat);
            icerik.Children.Add(stokBadge);
            border.Child = icerik;

            if (urun.DusukStok || urun.MevcutStok == 0)
            {
                border.BorderBrush = (Brush)FindResource("AccentRed");
                border.BorderThickness = new Thickness(1.5);
                border.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color       = Color.FromRgb(0xE7, 0x4C, 0x3C),
                    BlurRadius  = 18,
                    ShadowDepth = 0,
                    Opacity     = 0.9
                };
            }

            border.MouseDown  += (s, e) => SepeteEkle(urun);
            border.MouseEnter += (s, e) =>
            {
                border.Background = (Brush)FindResource("BgCardHover");
                border.BorderBrush = (Brush)FindResource("AccentBlue");
            };
            border.MouseLeave += (s, e) =>
            {
                border.Background = (Brush)FindResource("BgCard");
                border.BorderBrush = (Brush)FindResource("BorderColor");
            };

            return border;
        }

        // ─── SEPET ────────────────────────────────────────────────────
        private void SepeteEkle(Urun urun)
        {
            var mevcut = _sepet.FirstOrDefault(x => x.UrunID == urun.UrunID);
            if (mevcut != null)
            {
                int stok = StokGetir(urun.UrunID);
                if (mevcut.Adet < stok) { mevcut.Adet++; SepetYenile(); }
                else MessageBox.Show($"Stok yetersiz! Maksimum: {stok}", "Uyarı");
                return;
            }
            _sepet.Add(new SepetItem
            {
                UrunID     = urun.UrunID,
                UrunAdi    = urun.TamAdi,
                AlisFiyati = urun.AlisFiyati,
                BirimFiyat = urun.SatisFiyati,
                Adet       = 1
            });
            SepetYenile();
            txtBarkod.Focus();
        }

        private int StokGetir(int urunID)
        {
            try
            {
                using var conn = new SqlConnection(_connStr);
                conn.Open();
                using var cmd = new SqlCommand("SELECT MevcutStok FROM Urunler WHERE UrunID=@id", conn);
                cmd.Parameters.AddWithValue("@id", urunID);
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
            catch { return 0; }
        }

        private decimal IndirimAl()
        {
            if (decimal.TryParse(txtIndirim.Text, out decimal indirim) && indirim > 0)
                return indirim;
            return 0;
        }

        private void SepetYenile()
        {
            pnlSepet.Children.Clear();

            bool dolu = _sepet.Count > 0;
            bdSepetBos.Visibility = dolu ? Visibility.Collapsed : Visibility.Visible;
            svSepet.Visibility    = dolu ? Visibility.Visible   : Visibility.Collapsed;
            bdSepetSayisi.Visibility = dolu ? Visibility.Visible : Visibility.Collapsed;

            if (dolu)
            {
                lblSepetSayisi.Text = _sepet.Sum(x => x.Adet).ToString();
                foreach (var item in _sepet)
                    pnlSepet.Children.Add(SepetSatiriOlustur(item));
            }

            decimal net = Math.Max(0, _sepet.Sum(x => x.ToplamFiyat) - IndirimAl());
            lblToplam.Text = $"{net:N2} ₺";
        }

        private Border SepetSatiriOlustur(SepetItem item)
        {
            var border = new Border
            {
                Background = (Brush)FindResource("BgCard"),
                CornerRadius = new CornerRadius(10),
                BorderBrush = (Brush)FindResource("BorderColor"),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 6),
                Padding = new Thickness(12, 10, 12, 10)
            };

            var root = new StackPanel();

            // Üst: ad + sil
            var ust = new Grid();
            var ad = new TextBlock
            {
                Text = item.UrunAdi,
                Foreground = (Brush)FindResource("TextPrimary"),
                FontSize = 12, FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 240, Margin = new Thickness(0, 0, 28, 0)
            };
            var sil = new Button
            {
                Content = "✕",
                Background = Brushes.Transparent,
                Foreground = (Brush)FindResource("AccentRed"),
                BorderThickness = new Thickness(0),
                FontSize = 13, Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top
            };
            sil.Click += (s, e) => { _sepet.RemoveAll(x => x.UrunID == item.UrunID); SepetYenile(); };
            ust.Children.Add(ad);
            ust.Children.Add(sil);

            // Barkod (küçük metin)
            var altGrid = new Grid { Margin = new Thickness(0, 8, 0, 0) };

            var adetPanel = new StackPanel { Orientation = Orientation.Horizontal };

            var btnAzalt = new Button
            {
                Content = "−", Width = 28, Height = 28, FontSize = 15,
                Background = (Brush)FindResource("NumpadBg"),
                Foreground = (Brush)FindResource("TextPrimary"),
                BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
                Template = (ControlTemplate)FindResource("SmallRoundBtnTemplate")
            };
            var txtAdet = new TextBlock
            {
                Text = item.Adet.ToString(),
                Foreground = (Brush)FindResource("TextPrimary"),
                FontSize = 15, FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 12, 0),
                MinWidth = 22, TextAlignment = TextAlignment.Center
            };
            var btnArtir = new Button
            {
                Content = "+", Width = 28, Height = 28, FontSize = 15,
                Background = (Brush)FindResource("AccentBlue"),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
                Template = (ControlTemplate)FindResource("SmallRoundBtnTemplate")
            };

            btnAzalt.Click += (s, e) =>
            {
                if (item.Adet > 1) { item.Adet--; SepetYenile(); }
                else MessageBox.Show("Silmek için ✕ kullanın.", "Uyarı");
            };
            btnArtir.Click += (s, e) =>
            {
                int stok = StokGetir(item.UrunID);
                if (item.Adet < stok) { item.Adet++; SepetYenile(); }
                else MessageBox.Show($"Stok yetersiz! Maks: {stok}", "Uyarı");
            };

            adetPanel.Children.Add(btnAzalt);
            adetPanel.Children.Add(txtAdet);
            adetPanel.Children.Add(btnArtir);

            var fiyat = new TextBlock
            {
                Text = item.ToplamFiyatText,
                Foreground = (Brush)FindResource("AccentBlue"),
                FontSize = 14, FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };

            altGrid.Children.Add(adetPanel);
            altGrid.Children.Add(fiyat);

            root.Children.Add(ust);
            root.Children.Add(altGrid);
            border.Child = root;
            return border;
        }

        private void btnSepetTemizle_Click(object sender, RoutedEventArgs e)
        {
            if (_sepet.Count == 0) return;
            if (MessageBox.Show("Sepeti temizlemek istediğinize emin misiniz?", "Onay",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            { _sepet.Clear(); SepetYenile(); }
        }

        // ─── BARKOD ───────────────────────────────────────────────────
        private void txtBarkod_TextChanged(object sender, TextChangedEventArgs e)
        {
            bool bos = string.IsNullOrEmpty(txtBarkod.Text);
            txtBarkodHint.Visibility = bos ? Visibility.Visible : Visibility.Collapsed;
            _barkodTimer.Stop();
            if (!bos) _barkodTimer.Start();
        }

        private void txtBarkod_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) { _barkodTimer.Stop(); BarkodIleAra(); }
        }

        private void btnOkut_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _barkodTimer.Stop();
            BarkodIleAra();
        }

        private void BarkodIleAra()
        {
            string barkod = txtBarkod.Text.Trim();
            if (barkod == (string)txtBarkod.Tag || string.IsNullOrEmpty(barkod)) return;

            try
            {
                using var conn = new SqlConnection(_connStr);
                conn.Open();
                using var cmd = new SqlCommand(
                    @"SELECT u.UrunID, u.UrunAdi, m.MarkaAdi, u.AlisFiyati, u.SatisFiyati, u.MevcutStok, u.TurID
                      FROM Urunler u JOIN Markalar m ON u.MarkaID = m.MarkaID
                      WHERE u.Barkod = @b AND u.MevcutStok > 0", conn);
                cmd.Parameters.AddWithValue("@b", barkod);
                using var r = cmd.ExecuteReader();
                if (r.Read())
                    SepeteEkle(new Urun
                    {
                        UrunID      = Convert.ToInt32(r["UrunID"]),
                        UrunAdi     = r["UrunAdi"].ToString(),
                        MarkaAdi    = r["MarkaAdi"].ToString(),
                        AlisFiyati  = Convert.ToDecimal(r["AlisFiyati"]),
                        SatisFiyati = Convert.ToDecimal(r["SatisFiyati"]),
                        MevcutStok  = Convert.ToInt32(r["MevcutStok"])
                    });
                else
                    MessageBox.Show($"'{barkod}' barkoduna ait ürün bulunamadı!", "Uyarı");
            }
            catch (Exception ex) { MessageBox.Show($"Barkod hatası: {ex.Message}"); }

            txtBarkod.Clear();
            txtBarkod.Focus();
        }

        private void txtYikamaTutar_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) btnYikamaEkle_Click(sender, e);
        }

        // ─── YIKAMA ───────────────────────────────────────────────────
        private void btnYikamaEkle_Click(object sender, RoutedEventArgs e)
        {
            string girdi = txtYikamaTutar.Text.Trim();
            if (string.IsNullOrEmpty(girdi) || !decimal.TryParse(girdi, out decimal tutar) || tutar <= 0)
            {
                MessageBox.Show("Geçerli bir yıkama tutarı girin.", "Uyarı");
                return;
            }

            // 1. Ödeme seçim ekranını çağır
            OdemeSecimWindow odemeEkrani = new OdemeSecimWindow(tutar);
            odemeEkrani.Owner=this;
            bool? sonuc = odemeEkrani.ShowDialog();

            // 2. Kullanıcı seçim yapmadan çarpıdan çıkarsa işlemi durdur
            if (sonuc != true) return;

            // Seçilen yöntemi al (Nakit veya Kredi Kartı)
            string yontem = odemeEkrani.SecilenYontem;

            try
            {
                using var conn = new SqlConnection(_connStr);
                conn.Open();

                // 3. SQL Sorgusuna OdemeYontemi kolonunu ve @odeme parametresini eklendi
                using var cmd = new SqlCommand(
                    "INSERT INTO Yikamalar (Tutar, Tarih, OdemeYontemi) VALUES (@tutar, GETDATE(), @odeme)", conn);

                cmd.Parameters.AddWithValue("@tutar", tutar);
                cmd.Parameters.AddWithValue("@odeme", yontem); // Yeni parametre
                cmd.ExecuteNonQuery();

                // Mesajda hangi yöntemle ödendiğini de gösterelim
                MessageBox.Show($"✅  {tutar:N2} ₺ yıkama geliri ({yontem}) kaydedildi!", "Başarılı",
                    MessageBoxButton.OK, MessageBoxImage.None);

                txtYikamaTutar.Clear();
                txtBarkod.Focus();
            }
            catch (Exception ex) { MessageBox.Show($"Kayıt hatası: {ex.Message}", "Hata"); }
        }
        // ─── SON YIKAMA İPTAL ─────────────────────────────────────────
        private void btnYikamaIptal_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var conn = new SqlConnection(_connStr);
                conn.Open();

                // En son eklenen yıkamayı bul (En büyük YikamaID)
                using var cmdBul = new SqlCommand("SELECT TOP 1 YikamaID, Tutar FROM Yikamalar ORDER BY YikamaID DESC", conn);
                using var reader = cmdBul.ExecuteReader();

                if (reader.Read())
                {
                    int sonId = Convert.ToInt32(reader["YikamaID"]);
                    decimal sonTutar = Convert.ToDecimal(reader["Tutar"]);
                    reader.Close(); // Okuyucuyu kapat ki silme işlemine izin versin

                    // Kazayla silmeyi önlemek için teyit alıyoruz
                    var onay = MessageBox.Show(
                        $"Son eklenen {sonTutar:N2} ₺ tutarındaki yıkamayı iptal etmek istediğine emin misin?",
                        "Yıkama İptali", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                    if (onay == MessageBoxResult.Yes)
                    {
                        // Onay verilirse sil
                        using var cmdSil = new SqlCommand("DELETE FROM Yikamalar WHERE YikamaID = @id", conn);
                        cmdSil.Parameters.AddWithValue("@id", sonId);
                        cmdSil.ExecuteNonQuery();

                        MessageBox.Show("✅ Son yıkama kaydı başarıyla silindi!", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                        txtBarkod.Focus(); // İmleci tekrar barkoda al
                    }
                }
                else
                {
                    MessageBox.Show("Sistemde iptal edilecek bir yıkama kaydı bulunamadı.", "Bilgi");
                }
            }
            catch (Exception ex) { MessageBox.Show($"Hata oluştu: {ex.Message}", "Hata"); }
        }

        // ─── İNDİRİM ─────────────────────────────────────────────────
        private void txtIndirim_TextChanged(object sender, TextChangedEventArgs e) => SepetYenile();

        // ─── ÖDEME ────────────────────────────────────────────────────
        private void btnNakit_Click(object sender, RoutedEventArgs e)
        {
            decimal sepetToplam = _sepet.Sum(x => x.ToplamFiyat);
            decimal girilenIndirim = IndirimAl();

            decimal gercekIndirim = Math.Min(sepetToplam, girilenIndirim);
            decimal netOdeme = sepetToplam - gercekIndirim;

            OdemeSecimWindow odemeEkrani = new OdemeSecimWindow(netOdeme);
            odemeEkrani.Owner= this;
            bool? sonuc = odemeEkrani.ShowDialog();

            if (sonuc == true)
            {
                string secim = odemeEkrani.SecilenYontem;
                OdemeYap(secim);
                // Buradaki ikinci MessageBox'ı sildik ki çift mesaj çıkmasın.
            }
        }        
        private void OdemeYap(string yontem)
        {
            if (_sepet.Count == 0) { MessageBox.Show("Sepet boş!", "Uyarı"); return; }

            decimal sepetToplam = _sepet.Sum(x => x.ToplamFiyat);
            decimal girilenIndirim = IndirimAl();

            decimal gercekIndirim = Math.Min(sepetToplam, girilenIndirim);
            decimal netOdeme = sepetToplam - gercekIndirim;

            try
            {
                using var conn = new SqlConnection(_connStr);
                conn.Open();
                using var transaction = conn.BeginTransaction();

                try
                {
                    // 1. Sepeti kaydet
                    var cmdSepet = new SqlCommand(
                        "INSERT INTO Sepetler (Tarih, Indirim) VALUES (GETDATE(), @indirim); SELECT SCOPE_IDENTITY();", conn, transaction);
                    cmdSepet.Parameters.AddWithValue("@indirim", gercekIndirim);

                    int sepetID = Convert.ToInt32(cmdSepet.ExecuteScalar());

                    // 2. Satışları (Ürünleri) kaydet
                    foreach (var item in _sepet)
                    {
                        using var cmd = new SqlCommand(
                            "INSERT INTO Satislar (UrunID, Miktar, birimalisfiyati, birimsatisfiyati, SatisTarihi, SepetID, OdemeYontemi) " +
                            "VALUES (@id,@miktar,@alis,@satis,GETDATE(),@sid, @odeme)", conn, transaction);

                        cmd.Parameters.AddWithValue("@id", item.UrunID);
                        cmd.Parameters.AddWithValue("@miktar", item.Adet);
                        cmd.Parameters.AddWithValue("@alis", item.AlisFiyati);
                        cmd.Parameters.AddWithValue("@satis", item.BirimFiyat);
                        cmd.Parameters.AddWithValue("@sid", sepetID);

                        // Buraya "Nakit", "Kredi Kartı" veya "200 N 300 K" gelir.
                        cmd.Parameters.AddWithValue("@odeme", yontem);

                        cmd.ExecuteNonQuery();
                    }

                    transaction.Commit();

                    // Bilgilendirme
                    string indirimStr = gercekIndirim > 0 ? $"\nİndirim : -{gercekIndirim:N2} ₺" : "";
                    MessageBox.Show(
                        $"✅ Ödeme Başarılı!\n\nÖdeme Detayı: {yontem}\nAra Toplam: {sepetToplam:N2} ₺{indirimStr}\nNet Tahsilat: {netOdeme:N2} ₺",
                        "Başarılı", MessageBoxButton.OK, MessageBoxImage.None);

                    // Temizlik
                    _sepet.Clear();
                    txtIndirim.Text = "";
                    SepetYenile();
                    UrunleriYukle(_aktifTurID);
                    txtBarkod.Focus();
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    MessageBox.Show($"Ödeme hatası: {ex.Message}", "Güvenlik İptali");
                }
            }
            catch (Exception ex) { MessageBox.Show($"Bağlantı hatası: {ex.Message}", "Hata"); }
        }
        // ─── ARAMA ────────────────────────────────────────────────────
        private void txtArama_TextChanged(object sender, TextChangedEventArgs e)
        {
            string metin = txtArama.Text;
            if (metin == (string)txtArama.Tag) return;
            UrunleriYukle(_aktifTurID, metin);
        }

        // ─── SON SATIŞ İPTAL ──────────────────────────────────────────
        private void btnSatisgeri_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Son satışı iptal etmek istediğinize emin misiniz?",
                "Onay", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            try
            {
                using var conn = new SqlConnection(_connStr);
                conn.Open();
                object res = new SqlCommand("SELECT MAX(SepetID) FROM Sepetler", conn).ExecuteScalar();
                if (res != DBNull.Value)
                {
                    var sp = new SqlCommand("sp_SepetIptal", conn) { CommandType = CommandType.StoredProcedure };
                    sp.Parameters.AddWithValue("@SepetID", Convert.ToInt32(res));
                    sp.ExecuteNonQuery();
                    MessageBox.Show("Son satış iptal edildi, stoklar geri yüklendi!", "Başarılı");
                    UrunleriYukle(_aktifTurID);
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        // ─── AYARLAR ──────────────────────────────────────────────────
        private void btnAyarlar_Click(object sender, RoutedEventArgs e)
        {
            new AyarlarWindow().ShowDialog();
            KategorileriYukle(); // Ayarlar kapanınca listeyi tazele
        }

        // ─── F TUŞ KISAYOLLARI ─────────────────────────────────────────
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.F1: OdemeYap("Nakit");       break;
                case Key.F4: OdemeYap("KrediKarti");  break;
                case Key.F5: UrunleriYukle(_aktifTurID); break;
            }
        }

        // ─── PLACEHOLDER ──────────────────────────────────────────────
        private void PlaceholderSet(TextBox tb)
        {
            tb.Text = tb.Tag?.ToString() ?? "";
            tb.Foreground = (Brush)FindResource("TextSecondary");
        }
        private void Placeholder_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb && tb.Text == tb.Tag?.ToString())
            { tb.Text = ""; tb.Foreground = (Brush)FindResource("TextPrimary"); }
        }
        private void Placeholder_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb && string.IsNullOrEmpty(tb.Text))
                PlaceholderSet(tb);
        }

        // ─── KAPAT ────────────────────────────────────────────────────
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (MessageBox.Show("Programı kapatmak istiyor musunuz?", "Çıkış",
                MessageBoxButton.YesNo) == MessageBoxResult.No)
                e.Cancel = true;
        }
    }
}

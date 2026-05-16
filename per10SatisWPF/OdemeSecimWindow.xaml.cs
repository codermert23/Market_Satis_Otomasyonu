using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace per10SatisWPF
{
    public partial class OdemeSecimWindow : Window
    {
        public string SecilenYontem { get; private set; }

        // Sepetin net tutarını hafızada tutacağımız değişken
        private decimal _beklenenTutar;

        // Parantez içine (decimal beklenenTutar) ekledik!
        public OdemeSecimWindow(decimal beklenenTutar)
        {
            InitializeComponent();
            _beklenenTutar = beklenenTutar;
        }

        private void BtnKrediKarti_Click(object sender, RoutedEventArgs e)
        {
            SecilenYontem = "Kredi Kartı";
            this.DialogResult = true;
        }

        private void BtnNakit_Click(object sender, RoutedEventArgs e)
        {
            SecilenYontem = "Nakit";
            this.DialogResult = true;
        }

        private void Kalem_Click(object sender, RoutedEventArgs e)
        {
            PnlManuel.Visibility = Visibility.Visible;
        }

        private void BtnParcaliOnay_Click(object sender, RoutedEventArgs e)
        {
            decimal.TryParse(TxtNakitTutar.Text, out decimal n);
            decimal.TryParse(TxtKartTutar.Text, out decimal k);

            if (n == 0 && k == 0)
            {
                MessageBox.Show("Lütfen en az bir tutar girin.");
                return;
            }

            // ─── YENİ EKLENEN GÜVENLİK KONTROLÜ ───
            decimal girilenToplam = n + k;

            if (girilenToplam > _beklenenTutar)
            {
                MessageBox.Show(
                    $"HATA: Girdiğiniz tutarların toplamı ({girilenToplam:N2} ₺),\nödenecek net tutardan ({_beklenenTutar:N2} ₺) fazla olamaz!",
                    "Fazla Tutar Girildi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return; // Return diyerek işlemi durduruyoruz, pencere kapanmıyor.
            }
            // ──────────────────────────────────────

            // Format: "200 N 300 K"
            SecilenYontem = $"{(n > 0 ? n.ToString("0.#") + " N " : "")}{(k > 0 ? k.ToString("0.#") + " K" : "")}".Trim();
            this.DialogResult = true;
        }
    }
}
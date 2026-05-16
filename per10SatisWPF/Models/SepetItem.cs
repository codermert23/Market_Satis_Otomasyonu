using System.ComponentModel;

namespace per10SatisWPF.Models
{
    public class SepetItem : INotifyPropertyChanged
    {
        private int _adet;

        public int UrunID { get; set; }
        public string UrunAdi { get; set; } = "";
        public string Barkod { get; set; }
        public decimal BirimFiyat { get; set; }
        public decimal AlisFiyati { get; set; }

        public int Adet
        {
            get => _adet;
            set
            {
                _adet = value;
                OnPropertyChanged(nameof(Adet));
                OnPropertyChanged(nameof(ToplamFiyat));
                OnPropertyChanged(nameof(ToplamFiyatText));
            }
        }

        public decimal ToplamFiyat => BirimFiyat * Adet;
        public string ToplamFiyatText => $"{ToplamFiyat:N2} ₺";
        public string BirimFiyatText => $"{BirimFiyat:N2} ₺";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

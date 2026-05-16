namespace per10SatisWPF.Models
{
    public class Urun
    {
        public int UrunID { get; set; }
        public string UrunAdi { get; set; } = "";
        public string MarkaAdi { get; set; } = "";
        public decimal SatisFiyati { get; set; }
        public int MevcutStok { get; set; }
        public int TurID { get; set; }
        public string Barkod { get; set; }

        public decimal AlisFiyati { get; set; }

        public string TamAdi          => $"{MarkaAdi} {UrunAdi}";
        public string FiyatText       => $"{SatisFiyati:N2} ₺";
        public string AlisFiyatiText  => $"{AlisFiyati:N2} ₺";
        public string StokText        => $"Stok: {MevcutStok}";
        public bool   DusukStok       => MevcutStok <= 5 && MevcutStok > 0;
    }
}

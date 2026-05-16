using System.Windows;
using System.Windows.Input;

namespace per10SatisWPF
{
    public partial class LoginWindow : Window
    {
        private string _username = "admin";
        private string _password = "per10kurtar23";
        public LoginWindow()
        {
            InitializeComponent();
            txtKullanici.Focus();
        }

        private void btnGiris_Click(object sender, RoutedEventArgs e) => TryLogin();

        private void txtSifre_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) TryLogin();
        }

        private void TryLogin()
        {
            if (txtKullanici.Text == Properties.Settings.Default.KullaniciAdi && txtSifre.Password == Properties.Settings.Default.Sifre ||
                txtKullanici.Text == _username && txtSifre.Password == _password)
            {   
                Hide();
                var main = new MainWindow();
                main.Show();
                Close();
            }
            else
            {               
                lblHata.Text = "Hatalı kullanıcı adı veya şifre!";
                lblHata.Visibility = Visibility.Visible;
                txtSifre.Clear();
                txtSifre.Focus();
            }
        }
    }
}

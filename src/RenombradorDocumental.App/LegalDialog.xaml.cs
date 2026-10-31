using System.Windows;

namespace RenombradorDocumental.App;

public partial class LegalDialog : Window
{
    public LegalDialog()
    {
        InitializeComponent();
    }

    private void Entendido_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}

using System.Windows.Controls;
using System.Windows.Controls.Primitives;
namespace WindowsDoctor.UI.Views;
public partial class ReportsView : UserControl
{
    public ReportsView() => InitializeComponent();
    private void FormatRadio_Checked(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is ViewModels.ReportsViewModel vm && sender is RadioButton rb)
            vm.SelectedFormat = rb.Tag?.ToString() ?? "Html";
    }
}

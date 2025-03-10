
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace WpfApp1
{
    public partial class CustomMessageBox : Window
    {
        public CustomMessageBox()
        {
            InitializeComponent();
            SetupWindowBehavior();
            SetupKeyboardShortcuts();
        }

        private void SetupWindowBehavior()
        {
            // Плавное появление окна
            this.Loaded += (s, e) =>
            {
                this.Opacity = 0;
                this.BeginAnimation(UIElement.OpacityProperty,
                    new DoubleAnimation(1, TimeSpan.FromMilliseconds(300)));
            };
        }

        private void SetupKeyboardShortcuts()
        {
            // Закрытие по ESC
            this.PreviewKeyDown += (sender, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    CloseWithAnimation();
                    e.Handled = true;
                }
            };

            // Закрытие по Enter
            this.PreviewKeyDown += (sender, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    CloseWithAnimation();
                    e.Handled = true;
                }
            };
        }

        private void CloseWithAnimation()
        {
            var animation = new DoubleAnimation(0, TimeSpan.FromMilliseconds(200));
            animation.Completed += (s, _) => this.Close();
            this.BeginAnimation(UIElement.OpacityProperty, animation);
        }

        // Метод для ручного закрытия с проверкой DataContext
        public new void Close()
        {
            if (DataContext is MessageViewModel vm)
            {
                if (vm.CloseCommand.CanExecute(this))
                {
                    vm.CloseCommand.Execute(this);
                    return;
                }
            }
            base.Close();
        }
    }
}

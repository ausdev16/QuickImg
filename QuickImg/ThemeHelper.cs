using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace QuickImg
{
    public static class ThemeHelper
    {
        /// <summary>
        /// Returns the "next" Theme as an Enum in the following order: Light, Dark, Default/System
        /// </summary>
        /// <param name="currentTheme"></param>
        /// <returns></returns>
        public static Theme GetNextTheme(Theme currentTheme)
        {
            int nextTheme = (int)currentTheme + 1;
            if (nextTheme > 2)
            {
                nextTheme = 0;
            }

            return (Theme)nextTheme;
        }

        /// <summary>
        /// Updates the Theme App Bar Button Icon/Label.
        /// </summary>
        /// <param name="button"></param>
        /// <param name="theme"></param>
        public static void UpdateThemeButtons(AppBarButton button, Theme theme)
        {
            FontIcon themeFontIcon = (FontIcon)button.Content;

            switch (theme)
            {
                case Theme.Dark:
                    button.Label = "Dark";                    
                    themeFontIcon.Glyph = "\uE708";                    
                    break;

                case Theme.Default:
                    button.Label = "Default";
                    themeFontIcon.Glyph = "\uF08C";
                    break;

                case Theme.Light:
                    button.Label = "Light";
                    themeFontIcon.Glyph = "\uE706";
                    break;
            }
        }

        /// <summary>
        /// Updates the Title Bar and Page theme.
        /// </summary>
        /// <param name="titleBar"></param>
        /// <param name="page"></param>
        /// <param name="theme"></param>
        public static void SetTheme(ApplicationViewTitleBar titleBar, Page page, Theme theme)
        {   
            if (theme == Theme.Dark)
            {
                // Title Bar
                titleBar.BackgroundColor = Colors.Black;
                titleBar.ForegroundColor = Colors.White;
                titleBar.ButtonBackgroundColor = Colors.Black;
                titleBar.ButtonForegroundColor = Colors.White;
                titleBar.ButtonHoverForegroundColor = Colors.White;
                titleBar.ButtonHoverBackgroundColor = Colors.Gray;
                titleBar.ButtonPressedForegroundColor = Colors.White;
                titleBar.ButtonPressedBackgroundColor = Colors.Gray;

                titleBar.InactiveBackgroundColor = Colors.Black;
                titleBar.InactiveForegroundColor = Colors.Gray;
                titleBar.ButtonInactiveBackgroundColor = Colors.Black;
                titleBar.ButtonInactiveForegroundColor = Colors.Gray;

                // Everything Else
                page.RequestedTheme = ElementTheme.Dark;
            }
            else if (theme == Theme.Light)
            {
                //Title Bar
                titleBar.BackgroundColor = Colors.White;
                titleBar.ForegroundColor = Colors.Black;
                titleBar.ButtonBackgroundColor = Colors.White;
                titleBar.ButtonForegroundColor = Colors.Black;
                titleBar.ButtonHoverForegroundColor = Colors.Black;
                titleBar.ButtonHoverBackgroundColor = Colors.Gainsboro;
                titleBar.ButtonPressedForegroundColor = Colors.Black;
                titleBar.ButtonPressedBackgroundColor = Colors.Gainsboro;

                titleBar.InactiveBackgroundColor = Colors.White;
                titleBar.InactiveForegroundColor = Colors.Gainsboro;
                titleBar.ButtonInactiveBackgroundColor = Colors.White;
                titleBar.ButtonInactiveForegroundColor = Colors.Gainsboro;

                // Everything Else
                page.RequestedTheme = ElementTheme.Light;
            }
            else // Default/System Theme
            {
                // Set active window colors
                titleBar.ForegroundColor = Windows.UI.Colors.White;
                titleBar.BackgroundColor = (Color)page.Resources["SystemAccentColor"];
                titleBar.ButtonForegroundColor = Windows.UI.Colors.White;
                titleBar.ButtonBackgroundColor = (Color)page.Resources["SystemAccentColor"];
                titleBar.ButtonHoverForegroundColor = Windows.UI.Colors.White;
                titleBar.ButtonHoverBackgroundColor = (Color)page.Resources["SystemAccentColorLight1"];
                titleBar.ButtonPressedForegroundColor = Windows.UI.Colors.White;
                titleBar.ButtonPressedBackgroundColor = (Color)page.Resources["SystemAccentColorLight2"];

                // Set inactive window colors
                titleBar.InactiveForegroundColor = Windows.UI.Colors.Gray;
                titleBar.InactiveBackgroundColor = (Color)page.Resources["SystemAccentColor"];
                titleBar.ButtonInactiveForegroundColor = Windows.UI.Colors.Gray;
                titleBar.ButtonInactiveBackgroundColor = (Color)page.Resources["SystemAccentColor"];

                // Everything Else
                page.RequestedTheme = ElementTheme.Default;
            }
        }
    }
}

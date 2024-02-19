using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Windows.Storage;
using Windows.UI.Xaml;

namespace QuickImg
{
    public static class SettingsHelper
    {
        public static ViewMode GetSavedViewMode(ApplicationDataContainer localSettings)
        {
            int? viewModeAsInt;

            viewModeAsInt = (int)localSettings.Values["SavedViewMode"];
            if (viewModeAsInt == null)
            {
                localSettings.Values["SavedViewMode"] = (int)ViewMode.Fit;

                return ViewMode.Fit;
            }
            else
            {
                return (ViewMode)viewModeAsInt;
            }
        }

        public static void SetSavedViewMode(ApplicationDataContainer localSettings, ViewMode viewMode)
        {
            localSettings.Values["SavedViewMode"] = (int)viewMode;
        }

        public static bool GetDisableAnimation(ApplicationDataContainer localSettings)
        {
            bool? disableAnimation;

            disableAnimation = (bool?)localSettings.Values["DisableAnimation"];
            if (disableAnimation == null)
            {
                localSettings.Values["DisableAnimation"] = false;
            }

            return disableAnimation ?? false;
        }

        public static void SetDisableAnimation(ApplicationDataContainer localSettings, bool disableAnimation)
        {
            localSettings.Values["DisableAnimation"] = disableAnimation;
        }

        public static Visibility GetStatusBarVisibility(ApplicationDataContainer localSettings)
        {
            object visibilitySetting;
            Visibility visibility;

            visibilitySetting = localSettings.Values["StatusBarVisiblity"];
            if (visibilitySetting == null)
            {
                visibility = Visibility.Visible;
                localSettings.Values["StatusBarVisiblity"] = (int)visibility;
            }
            else
            {
                visibility = (Visibility)visibilitySetting;
            }

            return visibility;
        }

        public static void SetStatusBarVisibility(ApplicationDataContainer localSettings, Visibility visibility)
        {
            localSettings.Values["StatusBarVisiblity"] = (int)visibility;
        }

        public static double GetDPIOverrideFraction(ApplicationDataContainer localSettings)
        {
            double? dpiOverrideFraction;

            dpiOverrideFraction = (double?)localSettings.Values["DPIOverrideFraction"];
            if (dpiOverrideFraction == null)
            {
                dpiOverrideFraction = 1.0;
                localSettings.Values["DPIOverrideFraction"] = dpiOverrideFraction;
            }

            return dpiOverrideFraction ?? 1.0;
        }

        public static void SetDPIOverrideFraction(ApplicationDataContainer localSettings, double fraction)
        {
            localSettings.Values["DPIOverrideFraction"] = fraction;
        }

        public static Theme GetTheme(ApplicationDataContainer localSettings)
        {
            int? theme;

            theme = (int?)localSettings.Values["Theme"];

            if (theme == null)
            {
                localSettings.Values["Theme"] = (int)Theme.Default;
                return Theme.Default;
            }
            else
            {
                return (Theme)theme;
            }
        }

        public static void SetTheme(ApplicationDataContainer localSettings, Theme theme)
        {
            localSettings.Values["Theme"] = (int)theme;
        }

        public static InitialViewMode GetInitialViewMode(ApplicationDataContainer localSettings)
        {
            int? initialViewMode;

            initialViewMode = (int?)localSettings.Values["InitialViewMode"];

            if (initialViewMode == null)
            {
                localSettings.Values["InitialViewMode"] = (int)InitialViewMode.Fit;
                return InitialViewMode.Fit;
            }
            else
            {
                return (InitialViewMode)initialViewMode;
            }
        }

        public static void SetInitialViewMode(ApplicationDataContainer localSettings, InitialViewMode initialViewMode)
        {
            localSettings.Values["InitialViewMode"] = (int)initialViewMode;
        }
    }
}

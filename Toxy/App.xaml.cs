using System.Linq;
using System.Windows;
using System.Collections.Generic;
using System.Threading;
using System.Globalization;

namespace Toxy
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static void SetCulture(string culture)
        {
            var dicList = Application.Current.Resources.MergedDictionaries.ToList();
   
            string culturePath = string.Format("pack://application:,,,/Toxy;component/Resources/Translations/{0}.xaml", culture);
            var resource = dicList.FirstOrDefault(dic => dic.Source.OriginalString == culturePath);

            if (resource == null)
            {
                culturePath = "en-US.xaml";
                resource = dicList.FirstOrDefault(dic => dic.Source.OriginalString == culturePath);
            }

            if (resource != null)
            {
                Application.Current.Resources.MergedDictionaries.Remove(resource);
                Application.Current.Resources.MergedDictionaries.Add(resource);
            }
    
            Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture(culture);
            Thread.CurrentThread.CurrentUICulture = new CultureInfo(culture);
        }
    }
}

using System;
using IronSoftware.Drawing;

namespace OpenMetaverse.Imaging;

public class ColorTranslator
{
    public static Color FromHtml(string htmlColor)
    {
        var color = Color.Empty;
        if (htmlColor == null || htmlColor.Length == 0)
            return color;
        if (htmlColor[0] == '#' && (htmlColor.Length == 7 || htmlColor.Length == 4))
        {
            if (htmlColor.Length == 7)
            {
                color = Color.FromArgb(Convert.ToInt32(htmlColor.Substring(1, 2), 16),
                    Convert.ToInt32(htmlColor.Substring(3, 2), 16), Convert.ToInt32(htmlColor.Substring(5, 2), 16));
            }
            else
            {
                var str1 = char.ToString(htmlColor[1]);
                var str2 = char.ToString(htmlColor[2]);
                var str3 = char.ToString(htmlColor[3]);
                color = Color.FromArgb(Convert.ToInt32(str1 + str1, 16), Convert.ToInt32(str2 + str2, 16),
                    Convert.ToInt32(str3 + str3, 16));
            }
        }

        return color;
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactionTimeMeasurementApp.Classes
{
    internal static class Functions
    {
        internal static void Debug(string text)
        {
            System.Diagnostics.Debug.WriteLine(text);
        }

        internal static bool IsInside(int radius, int x1, int y1, int x2, int y2)
        {
            return GetDistance(x1, y1, x2, y2) < radius;
        }

        internal static double GetDistance(int x1, int y1, int x2, int y2)
        {
            return Math.Sqrt(Math.Pow(x2 - x1, 2) + Math.Pow(y2 - y1, 2));
        }
    }
}

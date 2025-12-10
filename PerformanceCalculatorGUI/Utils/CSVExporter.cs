using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace PerformanceCalculatorGUI.Utils
{
    public static class CSVExporter
    {
        public static void ExportToCSV<TInfo>(IEnumerable<TInfo> info, string filepath) where TInfo : ICSVInfo
        {
            // Ensure Dot as a separator
            System.Globalization.CultureInfo customCulture = (System.Globalization.CultureInfo)Thread.CurrentThread.CurrentCulture.Clone();
            customCulture.NumberFormat.NumberDecimalSeparator = ".";

            var currentCulture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = customCulture;

            var csvContent = new StringBuilder();
            csvContent.AppendLine(info.First().GetCSVHeader());

            foreach (var line in info)
            {
                csvContent.AppendLine(line.GetCSV());
            }

            File.WriteAllText(filepath, csvContent.ToString());

            Thread.CurrentThread.CurrentCulture = currentCulture;
            Console.WriteLine($"CSV file created at: {filepath}");
        }

        public static void ExportToCSV(IEnumerable<string> info, string header, string filepath)
        {
            // Ensure Dot as a separator
            System.Globalization.CultureInfo customCulture = (System.Globalization.CultureInfo)Thread.CurrentThread.CurrentCulture.Clone();
            customCulture.NumberFormat.NumberDecimalSeparator = ".";

            var currentCulture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = customCulture;

            var csvContent = new StringBuilder();
            if (header != "") csvContent.AppendLine(header);

            foreach (string line in info)
            {
                csvContent.AppendLine(line);
            }

            File.WriteAllText(filepath, csvContent.ToString());

            Thread.CurrentThread.CurrentCulture = currentCulture;
            Console.WriteLine($"CSV file created at: {filepath}");
        }
    }

    public interface ICSVInfo
    {
        string GetCSV();
        string GetCSVHeader();
    }
}

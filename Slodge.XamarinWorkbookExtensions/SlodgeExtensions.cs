using System.Collections.Generic;
using Slodge.XamarinWorkbookExtensions;
using Xamarin.Interactive.Representations;

// Dear Xam, please forgive me for invading your namespace ...
// ... but using your namespace helps the extension "just work"
// ... and I would love to get `AsTable()` inside the workbooks
namespace Xamarin.Interactive.CodeAnalysis.Workbooks
{
    public static class SlodgeExtensions
    {
        public static Representations.VerbatimHtml AsTable(
            this System.Data.DataTable table,
            int maxColumns = 10,
            int maxRows = 10,
            bool includeProperties = true,
            bool includeFields = false,
            IStringifier stringifier = null,
            string customTableClassName = null,
            string customCss = null)
        {
            var generator = new DataTableTableGenerator(
                table,
                maxColumns,
                maxRows,
                stringifier,
                customTableClassName,
                customCss);
            return generator.GenerateHtml();
        }

        public static VerbatimHtml AsTable<TKey, TValue>(
            this IDictionary<TKey, TValue> items,
            int maxColumns = 10,
            int maxRows = 10,
            bool includeProperties = true,
            bool includeFields = false,
            IStringifier stringifier = null,
            string customTableClassName = null,
            string customCss = null)
        {
            var generator = new DictionaryTableGenerator<TKey, TValue>(
                items,
                includeProperties,
                includeFields,
                maxColumns,
                maxRows,
                stringifier,
                customTableClassName,
                customCss);
            return generator.GenerateHtml();
        }

        public static VerbatimHtml AsTable<T>(
            this IEnumerable<T> items,
            int maxColumns = 10,
            int maxRows = 10,
            bool includeProperties = true,
            bool includeFields = false,
            IStringifier stringifier = null,
            string customTableClassName = null,
            string customCss = null)
        {
            var generator = new EnumerableTableGenerator<T>(
                items,
                includeProperties,
                includeFields,
                maxColumns,
                maxRows,
                stringifier,
                customTableClassName,
                customCss);
            return generator.GenerateHtml();
        }
    }
}
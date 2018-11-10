using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Xamarin.Interactive;
using Xamarin.Interactive.CodeAnalysis.Workbooks;

// forgive me for invading your namespace ... but it helps the workbook work
namespace Xamarin.Interactive.CodeAnalysis.Workbooks
{
    public interface IFormatter
    {
        string Format(object o);
    }

    public class DefaultFormatter : IFormatter
    {
        int _maxCellLength = 50;

        public int MaxCellLength
        {
            get => _maxCellLength;
            set
            {
                if (value < 4)
                    throw new ArgumentOutOfRangeException(nameof(MaxCellLength), "4 is the smallest cell length supported");
                _maxCellLength = value;
            }
        }

        public virtual string Format(object o)
        {
            var toReturn = InnerFormat(o);
            if (toReturn != null)
            {
                if (toReturn.Length > MaxCellLength)
                {
                    toReturn = toReturn.Substring(0, MaxCellLength - 3) + "...";
                }
            }
            return toReturn;
        }

        static string InnerFormat(object o)
        {
            switch (o)
            {
                case null:
                    return string.Empty;

                case DateTime dateTime:
                    if (dateTime.Hour == 0
                        && dateTime.Minute == 0
                        && dateTime.Second == 0
                        && dateTime.Millisecond == 0)
                        return dateTime.ToString("yyyy-MM-dd");
                    else
                        return dateTime.ToString("u");

                case TimeSpan timeSpan:
                    return timeSpan.ToString("g");

                default:
                    return o.ToString();
            }
        }
    }

    interface IValueGetter<in TItem>
    {
        string Title { get; }
        object GetValue(TItem item);
    }

    class PropertyValueGetter<TItem>
            : IValueGetter<TItem>
    {
        readonly PropertyInfo _propertyInfo;

        public PropertyValueGetter(PropertyInfo propertyInfo)
        {
            _propertyInfo = propertyInfo;
        }

        public string Title => _propertyInfo.Name;

        public object GetValue(TItem item)
        {
            if (item == null) return null;
            return _propertyInfo.GetValue(item);
        }
    }

    class FieldValueGetter<TItem>
        : IValueGetter<TItem>
    {
        readonly FieldInfo _fieldInfo;

        public FieldValueGetter(FieldInfo fieldInfo)
        {
            _fieldInfo = fieldInfo;
        }

        public string Title => _fieldInfo.Name;

        public object GetValue(TItem item)
        {
            if (item == null) return null;
            return _fieldInfo.GetValue(item);
        }
    }

    public static class SlodgeExtensions
    {
        static IEnumerable<IValueGetter<TItem>> ValuesGetters<TItem>(bool includeProperties, bool includeFields)
        {
            var type = typeof(TItem);
            if (includeProperties)
            {
                var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                foreach (var p in properties)
                {
                    yield return new PropertyValueGetter<TItem>(p);
                }
            }
            if (includeFields)
            {
                var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                foreach (var f in fields)
                {
                    yield return new FieldValueGetter<TItem>(f);
                }
            }
        }

        public static Xamarin.Interactive.Representations.VerbatimHtml AsTable<T>(
                            this IEnumerable<T> items,
                            int maxColumns = 10,
                            int maxRows = 10,
                            bool includeProperties = true,
                            bool includeFields = false,
                            IFormatter formatter = null,
                            string customTableClassName = null,
                            string customCss = null)
        {
            if (items == null)
            {
                return "-- null --".AsHtml();
            }

            formatter = formatter ?? new DefaultFormatter();

            var getters = ValuesGetters<T>(includeProperties, includeFields).ToList();
            var mainProperties = getters.Take(maxColumns).ToList();
            var extraProperties = getters.Skip(maxColumns).ToList();
            var tableText = new System.Text.StringBuilder();
            tableText.Append($@"<style>  
   .ourTable {{ border-collapse: collapse; }} 
   .ourTable th {{ border: 0px solid #ddd; padding-left: 4px; padding-right: 4px; text-align:left; }} 
   .ourTable td {{ border: 0px solid #ddd; padding-left: 4px; padding-right: 4px; text-align:left; }}
    { customCss }
   </style>");
            tableText.Append($"<table class='ourTable {customTableClassName}'>");
            tableText.Append("<thead><tr>");
            tableText.Append($"<th>#</th>");
            foreach (var p in mainProperties)
            {
                tableText.Append($"<th>{p.Title}</th>");
            }
            tableText.Append("</tr></thead>");
            tableText.Append("<tbody>");
            var rowCount = 0;
            foreach (var i in items.Take(maxRows))
            {
                tableText.Append("<tr>");
                tableText.Append($"<td>{rowCount}</td>");
                foreach (var p in mainProperties)
                {
                    var t = formatter.Format(p.GetValue(i));
                    tableText.Append($"<td>{t}</td>");
                }
                tableText.Append("</tr>");
                rowCount++;
            }

            if (rowCount >= maxRows)
            {
                var colCount = mainProperties.Count();
                var totalRowCount = SafeGetCount(items);
                if (totalRowCount != null)
                {
                    if (totalRowCount.Value > rowCount)
                    {
                        var remaining = totalRowCount.Value - rowCount;
                        var overflowText = $"{remaining} more row{(remaining > 1 ? "s" : "")}";
                        GenerateOverflowRow<T>(tableText, colCount, overflowText);
                    }
                }
                else
                {
                    var overflowText = $"Enumeration limited to {rowCount} rows";
                    GenerateOverflowRow<T>(tableText, colCount, overflowText);
                }
            }

            tableText.Append("</tbody>");
            tableText.Append("</table>");
            if (extraProperties.Any())
            {
                tableText.Append("<div>Other properties: ");
                tableText.Append(string.Join(", ", extraProperties.Select(p => p.Title)));
                tableText.Append("</div>");
            }
            return tableText.ToString().AsHtml();
        }

        static void GenerateOverflowRow<T>(StringBuilder tableText, int colCount, string overflowText)
        {
            tableText.Append("<tr>");
            tableText.Append($"<td></td>");
            tableText.Append($"<td colspan='{colCount}'>{overflowText}</td>");
            tableText.Append("</tr>");
        }

        static int? SafeGetCount<T>(this IEnumerable<T> items)
        {
            if (items == null)
                return null;

            var type = items.GetType();
            var count = type.GetProperty("Count");
            if (count != null && count.PropertyType == typeof(int))
                return (int)count.GetValue(items);

            var length = type.GetProperty("Length");
            if (length != null && length.PropertyType == typeof(int))
                return (int)length.GetValue(items);

            return null;
        }
    }
}

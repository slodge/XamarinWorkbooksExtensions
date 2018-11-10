using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Xamarin.Interactive;
using Xamarin.Interactive.CodeAnalysis.Workbooks;

// Dear Xam, please forgive me for invading your namespace ...
// ... but using your namespace helps the extension "just work"
// ... and I would love to get `AsTable()` inside the workbooks
namespace Xamarin.Interactive.CodeAnalysis.Workbooks
{
    public interface IStringifier
    {
        string Stringify(MemberInfo column, object o);
    }

    public class Stringifier : IStringifier
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

        public virtual string Stringify(MemberInfo column /* ignored */, object o)
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

        protected virtual string InnerFormat(object o)
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
        MemberInfo MemberInfo { get; }
    }

    class PropertyValueGetter<TItem>
            : IValueGetter<TItem>
    {
        readonly PropertyInfo _propertyInfo;

        public PropertyValueGetter(PropertyInfo propertyInfo)
        {
            _propertyInfo = propertyInfo;
        }

        public MemberInfo MemberInfo => _propertyInfo;

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

        public MemberInfo MemberInfo => (_fieldInfo);

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
            var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy;
            if (includeProperties)
                flags |= BindingFlags.GetProperty;
            if (includeFields)
                flags |= BindingFlags.GetField;

            var members = type.GetMembers(flags);
            foreach (var m in members)
            {
                switch (m)
                {
                    case PropertyInfo p:
                        yield return new PropertyValueGetter<TItem>(p);
                        break;
                    case FieldInfo f:
                        yield return new FieldValueGetter<TItem>(f);
                        break;
                    default:
                        // ignored...
                        break;
                }
            }
        }

        public static Representations.VerbatimHtml AsTable<T>(
                            this IEnumerable<T> items,
                            int maxColumns = 10,
                            int maxRows = 10,
                            bool includeProperties = true,
                            bool includeFields = false,
                            IStringifier stringifier = null,
                            string customTableClassName = null,
                            string customCss = null)
        {
            if (items == null)
            {
                return "-- null --".AsHtml();
            }

            stringifier = stringifier ?? new Stringifier();
            customTableClassName = customTableClassName ?? "slodgeTable";
            customCss = customCss ?? @"
   .slodgeTable {{ border-collapse: collapse; }} 
   .slodgeTable th {{ border: 0px solid #ddd; padding-left: 6px; padding-right: 6px; text-align:left; }} 
   .slodgeTable td {{ border: 0px solid #ddd; padding-left: 6px; padding-right: 6px; text-align:left; }}
";

            var getters = ValuesGetters<T>(includeProperties, includeFields).ToList();
            var shownColumns = getters.Take(maxColumns).ToList();
            var overflowColumns = getters.Skip(maxColumns).ToList();

            var tableText = new System.Text.StringBuilder();
            tableText.Append($@"<style>  
    { customCss }
   </style>");

            tableText.Append($"<table class='{customTableClassName}'>");
            tableText.Append("<thead><tr>");
            tableText.Append($"<th>#</th>");
            foreach (var p in shownColumns)
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
                foreach (var p in shownColumns)
                {
                    var t = stringifier.Stringify(p.MemberInfo, p.GetValue(i));
                    tableText.Append($"<td>{t}</td>");
                }
                tableText.Append("</tr>");
                rowCount++;
            }

            if (rowCount >= maxRows)
            {
                var colCount = shownColumns.Count();
                var totalRowCount = ReadCountOrLengthPropertyIfAvailable(items);
                if (totalRowCount != null)
                {
                    if (totalRowCount.Value > rowCount)
                    {
                        var overflowRowCount = totalRowCount.Value - rowCount;
                        var overflowRowText = $"{overflowRowCount} more row{(overflowRowCount > 1 ? "s" : "")}";
                        GenerateOverflowRow<T>(tableText, colCount, overflowRowText);
                    }
                }
                else
                {
                    var overflowRowText = $"Enumeration limited to {rowCount} rows";
                    GenerateOverflowRow<T>(tableText, colCount, overflowRowText);
                }
            }

            tableText.Append("</tbody>");
            tableText.Append("</table>");

            if (overflowColumns.Any())
            {
                tableText.Append("<div>Columns not shown: ");
                tableText.Append(string.Join(", ", overflowColumns.Select(p => p.Title)));
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

        static int? ReadCountOrLengthPropertyIfAvailable<T>(this IEnumerable<T> items)
        {
            if (items == null)
                return null;

            var type = items.GetType();
            var count = type.GetProperty("Count", BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetProperty);
            if (count != null && count.PropertyType == typeof(int))
                return (int)count.GetValue(items);

            var length = type.GetProperty("Length", BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetProperty);
            if (length != null && length.PropertyType == typeof(int))
                return (int)length.GetValue(items);

            return null;
        }
    }
}

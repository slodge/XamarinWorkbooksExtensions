using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using Xamarin.Interactive;
using Xamarin.Interactive.CodeAnalysis.Workbooks;
using Xamarin.Interactive.Representations;

namespace Slodge.XamarinWorkbookExtensions
{
    public interface IStringifier
    {
        string Stringify(string title, Type type, object o);
    }

    public class Stringifier : IStringifier
    {
        int _maxCellLength;

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

        public static IStringifier Default => new Stringifier();

        public Stringifier(int maxCellLength = 50)
        {
            MaxCellLength = maxCellLength;
        }

        public virtual string Stringify(string title /* ignored */, Type type /* ignored */, object o)
        {
            var toReturn = InnerStringify(o);
            if (toReturn != null)
            {
                if (toReturn.Length > MaxCellLength)
                {
                    toReturn = toReturn.Substring(0, MaxCellLength - 3) + "...";
                }
            }
            return toReturn;
        }

        protected virtual string InnerStringify(object o)
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


    public interface IValueGetter<in TItem>
    {
        string Title { get; }
        Type ValueType { get; }
        object GetValue(TItem item);
    }

    public class PropertyValueGetter<TItem>
            : IValueGetter<TItem>
    {
        readonly PropertyInfo _propertyInfo;

        public PropertyValueGetter(PropertyInfo propertyInfo)
        {
            _propertyInfo = propertyInfo;
        }

        public string Title => _propertyInfo.Name;

        public Type ValueType => _propertyInfo.PropertyType;

        public object GetValue(TItem item)
        {
            if (item == null) return null;
            return _propertyInfo.GetValue(item);
        }
    }

    public class FieldValueGetter<TItem>
        : IValueGetter<TItem>
    {
        readonly FieldInfo _fieldInfo;

        public FieldValueGetter(FieldInfo fieldInfo)
        {
            _fieldInfo = fieldInfo;
        }

        public string Title => _fieldInfo.Name;

        public Type ValueType => _fieldInfo.FieldType;

        public object GetValue(TItem item)
        {
            if (item == null) return null;
            return _fieldInfo.GetValue(item);
        }
    }

    public class DataTableTableGenerator
        : BaseTableGenerator<DataRow>
    {
        readonly DataTable _table;

        public DataTableTableGenerator(
            DataTable table,
            int maxColumns = 10, 
            int maxRows = 10, 
            IStringifier stringifier = null, 
            string customTableClassName = null, 
            string customCss = null) 
            : base(maxColumns, maxRows, stringifier, customTableClassName, customCss)
        {
            _table = table;
        }

        protected override IEnumerable<DataRow> GetRows(int maxRows)
        {
            return _table.Rows.Cast<DataRow>().Take(maxRows);
        }

        protected override bool IsNull => _table == null;

        protected override int? TotalRowCountIfAvailable => _table.Rows.Count;

        protected override IEnumerable<IValueGetter<DataRow>> ValueGetters()
        {
            if (_table == null)
                yield break;

            foreach (DataColumn c in _table.Columns)
            {
                yield return new DataColumnValueGetter(c);
            }
        }
    }

    public class DataColumnValueGetter 
        : IValueGetter<DataRow>
    {
        readonly DataColumn _dataColumn;

        public DataColumnValueGetter(DataColumn dataColumn)
        {
            _dataColumn = dataColumn;
        }

        public string Title => _dataColumn.Caption ?? _dataColumn.ColumnName;

        public Type ValueType
        {
            get
            {
                var type = _dataColumn.DataType;
                if (_dataColumn.AllowDBNull && type.IsValueType)
                    return typeof(Nullable<>).MakeGenericType(type);
                return type;
            }
        }

        public object GetValue(DataRow item)
        {
            var result = item[_dataColumn];
            if (_dataColumn.AllowDBNull && result == DBNull.Value)
                return null;
            return result;
        }
    }

    public class EnumerableTableGenerator<TItem>
        : BaseTableGenerator<TItem>
    {
        readonly IEnumerable<TItem> _items;
        readonly bool _includeProperties;
        readonly bool _includeFields;

        public EnumerableTableGenerator(
            IEnumerable<TItem> items,
            bool includeProperties = true,
            bool includeFields = false,
            int maxColumns = 10, 
            int maxRows = 10, 
            IStringifier stringifier = null, 
            string customTableClassName = null, 
            string customCss = null) 
            : base(maxColumns, maxRows, stringifier, customTableClassName, customCss)
        {
            _items = items;
            _includeProperties = includeProperties;
            _includeFields = includeFields;
        }

        protected override IEnumerable<TItem> GetRows(int maxRows)
        {
            return _items?.Take(maxRows);
        }

        protected override bool IsNull => _items == null;

        protected override int? TotalRowCountIfAvailable
        {
            get
            {
                if (_items == null)
                    return null;

                if (TryGetCollectionValue("Count", out var totalCountIfAvailable))
                    return totalCountIfAvailable;

                if (TryGetCollectionValue("Length", out var totalLengthIfAvailable))
                    return totalLengthIfAvailable;

                return null;
            }
        }

        bool TryGetCollectionValue(string propertyName, out int? totalRowCountIfAvailable)
        {
            if (_items == null)
            {
                totalRowCountIfAvailable = null;
                return false;
            }

            var count = _items.GetType().GetProperty(propertyName,
                BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetProperty);
            if (count != null && count.PropertyType == typeof(int))
            {
                totalRowCountIfAvailable = (int) count.GetValue(_items);
                return true;
            }

            totalRowCountIfAvailable = null;
            return false;
        }

        protected override IEnumerable<IValueGetter<TItem>> ValueGetters()
        {
            var type = typeof(TItem);
            var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy;
            if (_includeProperties)
                flags |= BindingFlags.GetProperty;
            if (_includeFields)
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
    }

    public abstract class BaseTableGenerator<TRow>
    {
        readonly int _maxColumns;
        readonly int _maxRows;
        readonly IStringifier _stringifier;
        readonly string _customTableClassName;
        readonly string _customCss;

        protected BaseTableGenerator(
            int maxColumns = 10,
            int maxRows = 10,
            IStringifier stringifier = null,
            string customTableClassName = null,
            string customCss = null)
        {
            _maxColumns = maxColumns;
            _maxRows = maxRows;
            _stringifier = stringifier ?? new Stringifier();
            _customTableClassName = customTableClassName ?? "slodgeTable";
            _customCss = customCss ?? @".slodgeTable { border-collapse: collapse; } 
.slodgeTable th { border: 0px; padding-left: 4px; padding-right: 4px; text-align:left; }
.slodgeTable td { border: 0px; padding-left: 4px; padding-right: 4px; text-align:left; }
";
        }

        public virtual VerbatimHtml GenerateHtml()
        {
            return GeneratHtmlText().AsHtml();
        }

        public virtual string GeneratHtmlText()
        {
            if (IsNull)
            {
                return "-- null --";
            }

            var columns = ValueGetters().ToList();
            var shownColumns = columns.Take(_maxColumns).ToList();
            var overflowColumns = columns.Skip(_maxColumns).ToList();

            var tableText = new System.Text.StringBuilder();
            tableText.Append($@"<style>{ _customCss }</style>");
            tableText.Append($"<table class='{_customTableClassName}'>");
            RenderHeaderRow(tableText, shownColumns);
            tableText.Append("<tbody>");
            var rowCount = RenderDetailRows(tableText, shownColumns);
            RenderOverflowRows(tableText, shownColumns, rowCount);
            tableText.Append("</tbody>");
            tableText.Append("</table>");
            RenderOverflowColumns(tableText, overflowColumns);

            return tableText.ToString();
        }

        static void RenderOverflowColumns(StringBuilder tableText, IList<IValueGetter<TRow>> overflowColumns)
        {
            if (overflowColumns.Any())
            {
                tableText.Append("<div>Columns not shown: ");
                tableText.Append(string.Join(", ", overflowColumns.Select(p => p.Title)));
                tableText.Append("</div>");
            }
        }

        protected virtual void RenderOverflowRows(StringBuilder tableText, List<IValueGetter<TRow>> shownColumns, int rowCount)
        {
            if (rowCount >= _maxRows)
            {
                var colCount = shownColumns.Count();
                var totalRowCount = TotalRowCountIfAvailable;
                if (totalRowCount.HasValue)
                {
                    if (totalRowCount.Value > rowCount)
                    {
                        var overflowRowCount = totalRowCount.Value - rowCount;
                        var overflowRowText = $"{overflowRowCount} more row{(overflowRowCount > 1 ? "s" : "")}";
                        RenderOverflowRow(tableText, colCount, overflowRowText);
                    }
                }
                else
                {
                    var overflowRowText = $"Enumeration limited to {rowCount} rows";
                    RenderOverflowRow(tableText, colCount, overflowRowText);
                }
            }
        }

        protected virtual int RenderDetailRows(StringBuilder tableText, List<IValueGetter<TRow>> shownColumns)
        {
            var rowCount = 0;
            foreach (var row in GetRows(_maxRows))
            {
                tableText.Append("<tr>");
                tableText.Append($"<td>{rowCount}</td>");
                foreach (var p in shownColumns)
                {
                    var t = _stringifier.Stringify(p.Title, p.ValueType, p.GetValue(row));
                    tableText.Append($"<td>{t}</td>");
                }

                tableText.Append("</tr>");
                rowCount++;
            }

            return rowCount;
        }

        protected abstract IEnumerable<TRow> GetRows(int maxRows);

        protected virtual void RenderOverflowRow(StringBuilder tableText, int colCount, string overflowText)
        {
            tableText.Append("<tr>");
            tableText.Append($"<td></td>");
            tableText.Append($"<td colspan='{colCount}'>{overflowText}</td>");
            tableText.Append("</tr>");
        }

        protected virtual void RenderHeaderRow(StringBuilder tableText, List<IValueGetter<TRow>> shownColumns)
        {
            tableText.Append("<thead><tr>");
            tableText.Append($"<th>#</th>");
            foreach (var p in shownColumns)
            {
                tableText.Append($"<th>{p.Title}</th>");
            }

            tableText.Append("</tr></thead>");
        }

        protected abstract bool IsNull { get; }
        protected abstract int? TotalRowCountIfAvailable { get; }
        protected abstract IEnumerable<IValueGetter<TRow>> ValueGetters();
    }
}

﻿using System;
using System.Collections;
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
    public class RowFrame
    {
        public List<List<string>> Rows = new List<List<string>>();

        public void AddRowWithHeader(string header, params string[] items)
        {
            var list = new List<string>();
            list.Add(header);
            list.AddRange(items);
            Rows.Add(list);
        }

        public void AddRow(params string[] items)
        {
            Rows.Add(items.ToList());
        }
    }

    public class ColumnFrame
    {
        public List<List<string>> Columns = new List<List<string>>();

        public void AddColumnWithHeader(string header, params string[] items)
        {
            var list = new List<string>();
            list.Add(header);
            list.AddRange(items);
            Columns.Add(list);
        }

        public void AddColumn(params string[] items)
        {
            Columns.Add(items.ToList());
        }

        public IEnumerable<IList<string>> ToRows()
        {
            var enumerators = Columns
                .Select(c => new SafeEnumerator(c.GetEnumerator())).ToList();
            while (enumerators.Any(e => e.MoveNext()))
            {
                yield return enumerators.Select(e => e.Current).ToList();
            }
        }

        private class SafeEnumerator : IEnumerator<string>
        {
            readonly IEnumerator<string> _inner;
            bool _isBeyondEnd = false;
            public SafeEnumerator(IEnumerator<string> inner)
            {
                _inner = inner;
            }

            public bool MoveNext()
            {
                if (_isBeyondEnd) return false;
                var result = _inner.MoveNext();
                _isBeyondEnd = !result;
                return result;
            }

            public void Reset()
            {
                _inner.Reset();
                _isBeyondEnd = false;
            }

            public string Current
            {
                get
                {
                    if (_isBeyondEnd) return null;
                    return _inner.Current;
                }
            }

            object IEnumerator.Current => Current;

            public void Dispose()
            {
                _inner.Dispose();
            }
        }
    }

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

    public class ToStringValueGetter<T> : IValueGetter<T>
    {
        public string Title { get; }
        public Type ValueType => typeof(String);
        public object GetValue(T item) => item?.ToString();

        public ToStringValueGetter(string title = "ToString")
        {
            Title = title;
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

    public class RowFrameTableGenerator
        : BaseTableGenerator<IList<string>>
    {
        readonly RowFrame _rowFrame;

        public RowFrameTableGenerator(RowFrame rowFrame,
            int maxColumns = 10,
            int maxRows = 10,
            IStringifier stringifier = null,
            string customTableClassName = null,
            string customCss = null)
            : base(maxColumns, maxRows, stringifier, customTableClassName, customCss)
        {
            _rowFrame = rowFrame;
        }

        protected override IEnumerable<IList<string>> GetRows(int maxRows)
        {
            return _rowFrame.Rows;
        }

        protected override bool IsNull => _rowFrame == null;
        protected override int? TotalRowCountIfAvailable => _rowFrame?.Rows?.Count;
        protected override IEnumerable<IValueGetter<IList<string>>> ValueGetters()
        {
            var maxCount = _rowFrame.Rows.Max(r => r.Count);
            for (var i = 0; i<maxCount; i++)
            {
                yield return new OptionalStringGetter(i);
            }
        }
    }

    public class ColumnFrameTableGenerator
        : BaseTableGenerator<IList<string>>
    {
        readonly ColumnFrame _columnFrame;

        public ColumnFrameTableGenerator(
            ColumnFrame columnFrame,
            int maxColumns = 10,
            int maxRows = 10,
            IStringifier stringifier = null,
            string customTableClassName = null,
            string customCss = null)
            : base(maxColumns, maxRows, stringifier, customTableClassName, customCss)
        {
            _columnFrame = columnFrame;
        }

        protected override IEnumerable<IList<string>> GetRows(int maxRows)
        {
            return _columnFrame.ToRows();
        }

        protected override bool IsNull => _columnFrame == null;
        protected override int? TotalRowCountIfAvailable => _columnFrame?.Columns.Max(c => c.Count);
        protected override IEnumerable<IValueGetter<IList<string>>> ValueGetters()
        {
            var maxCount = _columnFrame.Columns.Count;
            for (var i = 0; i < maxCount; i++)
            {
                yield return new OptionalStringGetter(i);
            }
        }
    }
    public class OptionalStringGetter : IValueGetter<IList<string>>
    {
        readonly int _index;

        public OptionalStringGetter(int index)
        {
            _index = index;
        }

        public string Title => _index.ToString();
        public Type ValueType => typeof(string);
        public object GetValue(IList<string> item)
        {
            if (_index < item.Count)
                return item[_index];
            return null;
        }
    }

    public class DictionaryTableGenerator<TKey, TValue>
        : BaseTableGenerator<KeyValuePair<TKey, TValue>>
    {
        readonly IDictionary<TKey, TValue> _items;
        readonly bool _includeProperties;
        readonly bool _includeFields;

        public DictionaryTableGenerator(
            IDictionary<TKey, TValue> items,
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

        protected override IEnumerable<KeyValuePair<TKey, TValue>> GetRows(int maxRows)
        {
            return _items?.Take(maxRows);
        }

        protected override bool IsNull => _items == null;

        protected override int? TotalRowCountIfAvailable => _items?.Count;
        
        protected override IEnumerable<IValueGetter<KeyValuePair<TKey, TValue>>> ValueGetters()
        {
            foreach (var valueGetter in EnumerateValueGetters<TKey>(_includeProperties, _includeFields))
                yield return new KeyWrappedValueGetter<TKey, TValue>(valueGetter);
            foreach (var valueGetter in EnumerateValueGetters<TValue>(_includeProperties, _includeFields))
                yield return new ValueWrappedValueGetter<TKey, TValue>(valueGetter);
        }
    }

    public class ValueWrappedValueGetter<TKey, TValue>
        : IValueGetter<KeyValuePair<TKey, TValue>>
    {
        readonly IValueGetter<TValue> _underlying;

        public ValueWrappedValueGetter(IValueGetter<TValue> underlying)
        {
            _underlying = underlying;
        }

        public string Title => $"Value.{_underlying.Title}";
        public Type ValueType => _underlying.ValueType;
        public object GetValue(KeyValuePair<TKey, TValue> item)
            => _underlying.GetValue(item.Value);
    }

    public class KeyWrappedValueGetter<TKey, TValue> 
        : IValueGetter<KeyValuePair<TKey, TValue>>
    {
        readonly IValueGetter<TKey> _underlying;

        public KeyWrappedValueGetter(IValueGetter<TKey> underlying)
        {
            _underlying = underlying;
        }

        public string Title => $"Key.{_underlying.Title}";
        public Type ValueType => _underlying.ValueType;
        public object GetValue(KeyValuePair<TKey, TValue> item)
            => _underlying.GetValue(item.Key);
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
            foreach (var valueGetter in EnumerateValueGetters<TItem>(_includeProperties, _includeFields))
                yield return valueGetter;
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

        static HashSet<Type> SimpleNumericTypes = new HashSet<Type>()
        {
            typeof(bool),
            typeof(char),
            typeof(short),
            typeof(int),
            typeof(double),
            typeof(float),
            typeof(decimal),
            typeof(bool?),
            typeof(char?),
            typeof(short?),
            typeof(int?),
            typeof(double?),
            typeof(float?),
            typeof(decimal?),
        };

        protected static IEnumerable<IValueGetter<T>> EnumerateValueGetters<T>(bool includeProperties, bool includeFields)
        {
            var type = typeof(T);
            if (type == typeof(string))
            {
                yield return new ToStringValueGetter<T>("String");
                yield break;
            }

            if (SimpleNumericTypes.Contains(type))
            {
                yield return new ToStringValueGetter<T>("Value");
                yield break;
            }

            var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy;
            if (includeProperties)
                flags |= BindingFlags.GetProperty;
            if (includeFields)
                flags |= BindingFlags.GetField;

            var members = type.GetMembers(flags);
            var numMembersReturned = 0;
            foreach (var m in members)
            {
                switch (m)
                {
                    case PropertyInfo p:
                        // exclude properties which need index access (especially `Item[]`)
                        if (!p.GetIndexParameters().Any())
                        {
                            numMembersReturned++;
                            yield return new PropertyValueGetter<T>(p);
                        }

                        break;
                    case FieldInfo f:
                        numMembersReturned++;
                        yield return new FieldValueGetter<T>(f);
                        break;
                    default:
                        // ignored...
                        break;
                }
            }

            if (numMembersReturned == 0)
            {
                yield return new ToStringValueGetter<T>();
            }
        }
    }
}

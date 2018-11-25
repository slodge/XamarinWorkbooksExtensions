# Slodge Xamarin Workbooks Extensions

This project provides some extensions to help you visualise your outputs.

Everything here is MIT licensed - but I'm happy to adjust if an alternative would be helpful.

## AsTable()

We've added two `AsTable()` extension method which render `IEnumerable<T>`, `IDictionary<TKey, TValue>`,  and `DataTable` collections to HTML Tables.

## Basic use `IEnumerable<T>`

```
class Thing
{
    public string Name {get;set;}
    public DateTime When {get;set;}
}

var list = new List<Thing>() {
    new Thing() { When = DateTime.UtcNow.Date.AddDays(-1), Name = "Yesterday" },
    new Thing() { When = DateTime.UtcNow.Date, Name = "Today" },
    new Thing() { When = DateTime.UtcNow.Date.AddDays(1), Name = "Tomorrow" }
}

list.AsTable()
```

To run this yourself, see [IEnumerable Table Workbook](IEnumerable_AsTable.workbook)

![TableIntro](/docs/TableIntro.png)

### Advanced options:

These options are available
- maxColumns - default `10` - the maximum number of properties/fields to show
- maxRows - default `10` - the maximum number of rows to show
- includeProperties - default `true` - should `public` Properties be shown
- includeFields - default `false` - should `public` Fields be shown
- stringifier - default `null` - provide an `IStringifier` implementation for custom cell text
- customTableClassName - default `null` - provide a css class name for the table. If null, then "slodgeTable" is used
- customCss - default `null` - provide css style for the table. If null, then the following CSS is inserted

```
   .slodgeTable { border-collapse: collapse; } 
   .slodgeTable th { border: 0px solid #ddd; padding-left: 4px; padding-right: 4px; text-align:left; } 
   .slodgeTable td { border: 0px solid #ddd; padding-left: 4px; padding-right: 4px; text-align:left; }

```

## Basic use `IDictionary<TKey, TValue>`

```
var d = new Dictionary<string, int>();
d.Add("one", 1);
d.Add("two", 2);
d.Add("three", 3);
d.AsTable()
```

To run this yourself, see [Dictionary Table Workbook](Dictionary_AsTable.workbook)

![TableIntro](/docs/TableIntro.png)

### Advanced options:

These options are available
- maxColumns - default `10` - the maximum number of properties/fields to show
- maxRows - default `10` - the maximum number of rows to show
- includeProperties - default `true` - should `public` Properties be shown
- includeFields - default `false` - should `public` Fields be shown
- stringifier - default `null` - provide an `IStringifier` implementation for custom cell text
- customTableClassName - default `null` - provide a css class name for the table. If null, then "slodgeTable" is used
- customCss - default `null` - provide css style for the table. If null, then the following CSS is inserted

```
   .slodgeTable { border-collapse: collapse; } 
   .slodgeTable th { border: 0px solid #ddd; padding-left: 4px; padding-right: 4px; text-align:left; } 
   .slodgeTable td { border: 0px solid #ddd; padding-left: 4px; padding-right: 4px; text-align:left; }

```


## Basic use `DataTable`

```
using System.Data;

var table = new DataTable("A table");
table.Columns.Add(new DataColumn("Name", typeof(string)));
table.Columns.Add(new DataColumn("When", typeof(DateTime)) { AllowDBNull = true });

table.Rows.Add("Yesterday", DateTime.UtcNow.Date.AddDays(-1));
table.Rows.Add("Today", DateTime.UtcNow.Date);
table.Rows.Add("Tomorrow", DateTime.UtcNow.Date.AddDays(1));

table.AsTable()
```

To run this yourself, see [DataTable Table Workbook](DataTable_AsTable.workbook)

![TableIntro](/docs/DataTableIntro.png)

### Advanced options:

These options are available
- maxColumns - default `10` - the maximum number of properties/fields to show
- maxRows - default `10` - the maximum number of rows to show
- stringifier - default `null` - provide an `IStringifier` implementation for custom cell text
- customTableClassName - default `null` - provide a css class name for the table. If null, then "slodgeTable" is used
- customCss - default `null` - provide css style for the table. If null, then the following CSS is inserted

```
   .slodgeTable { border-collapse: collapse; } 
   .slodgeTable th { border: 0px solid #ddd; padding-left: 4px; padding-right: 4px; text-align:left; } 
   .slodgeTable td { border: 0px solid #ddd; padding-left: 4px; padding-right: 4px; text-align:left; }

```


## Nuget Package

This extension is available in Nuget in https://www.nuget.org/packages/Slodge.XamarinWorkbookExtensions/1.0.0 - so nuget install with `Install-Package Slodge.XamarinWorkbookExtensions` 


## Building and Problems...

I've had some problems using the prerelease .NetStandard 2.0 Xamarin Workbooks Integrations package.

If you want to build this yourself, you may need to do some hacking to get the project to build...

If you can figure out what's happening with .Net Standard 2.0 or with the prerelease package from Xamarin, then Pull Requests are accepted :)


## Roadmap

I hope this project only has a short life... I'm hopeful we can get these extensions (or better!) pushed into "core" Xamarin Workbooks instead - see work in progress in https://github.com/Microsoft/workbooks/issues/499

There are many possible additional extensions - including ideas for Dictionaries, for more presentation options, for incremental enumerations, ...

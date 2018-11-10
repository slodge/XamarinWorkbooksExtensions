# Slodge Xamarin Workbooks Extensions

This project provides some extensions to help you visualise your outputs.

Everything here is MIT licensed - but I'm happy to adjust if an alternative would be helpful.

## AsTable()

We've added an `AsTable()` extension method which renders `IEnumerable<T>` collections to an HTML Table.

## Basic use:

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
To run this yourself, see [Table Workbook](table.workbook)

![TableIntro](/docs/TableIntro.png)

## Advanced options:

These options are available
- maxColumns - default `10` - the maximum number of properties/fields to show
- maxRows - default `10` - the maximum number of rows to show
- includeProperties - default `true` - should `public` Properties be shown
- includeFields - default `false` - should `public` Fields be shown
- stringifier - default `null` - provide an `IStringifier` implementation for custom cell text
- customTableClassName - default `null` - provide a css class name for the table. If null, then "slodgeTable" is used
- customCss - default `null` - provide css style for the table. If null, then the following CSS is inserted

```
   .slodgeTable {{ border-collapse: collapse; }} 
   .slodgeTable th {{ border: 0px solid #ddd; padding-left: 4px; padding-right: 4px; text-align:left; }} 
   .slodgeTable td {{ border: 0px solid #ddd; padding-left: 4px; padding-right: 4px; text-align:left; }}

```

# Nuget Package

This extension is available in Nuget in TODO - coming real soon..


# Building and Problems...

I've had some problems using the prerelease .NetStandard 2.0 Xamarin Workbooks Integrations package.

If you want to build this yourself, you may need to do some hacking to get the project to build...

If you can figure out what's happening with .Net Standard 2.0 or with the prerelease package from Xamarin, then Pull Requests are accepted :)


# Roadmap

I hope this project only has a short life... I'm hopeful we can get these extensions (or better!) pushed into "core" Xamarin Workbooks instead.

There are many possible additional extensions - including ideas for Dictionaries, for more presentation options, for incremental enumerations, ...